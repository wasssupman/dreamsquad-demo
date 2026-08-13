using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Wassup.Bridge;
using Wassup.Core;
using Wassup.Data;

namespace Wassup.UI
{
    public class DefenderDragSlot : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
    {
        private DefenderUnitData _unitData;
        private DefenderDragPlacementController _controller;
        private CostDisplay _costDisplay;
        // defender-board-limit 1·2 — 소진 판정(브리지 카운트)과 소진 셀의 목적지(판 위 그 유닛).
        // 슬롯은 런타임 생성이라 인스펙터 배선이 불가능해 DefenderSelector 가 Bind 로 넘긴다.
        private BattleBridge _bridge;
        private DcInspectController _inspect;
        // defender-tap-to-place unit 1 — arm 하이라이트 오버레이(lazy).
        private GameObject _armOverlay;
        // action-tray unit 4 — 비용 부족으로 차단된 드래그 제스처. 이후 OnDrag/OnEndDrag
        // 를 무시해 controller 세션이 시작되지 않는다(다음 제스처에서 리셋).
        private bool _suppressedDrag;

        public void Bind(DefenderUnitData unitData, DefenderDragPlacementController controller,
            CostDisplay costDisplay = null, BattleBridge bridge = null, DcInspectController inspect = null)
        {
            _unitData = unitData;
            _controller = controller;
            _costDisplay = costDisplay;
            _bridge = bridge;
            _inspect = inspect;
        }

        // defender-board-limit 1 — 이 유닛이 이미 상한만큼 판에 나가 있나. 최종 권한은 여전히
        // BattleBridge.CanPlaceDefenderAt(LimitReached) 이고 여기는 사전 차단이다(코스트 선례).
        private bool IsExhausted()
        {
            return _bridge != null && _unitData != null
                && _bridge.DeployedCountOf(_unitData) >= _unitData.EffectiveMaxOnBoard;
        }

        // defender-board-limit 2 — 소진 셀의 **탭** 응답: 판 위 그 유닛으로 데려간다.
        // 흔들림·링 핑 같은 별도 거부 연출을 만들지 않는다: 카메라가 그 유닛으로 가고
        // 리티클이 뜨는 것 자체가 응답이다.
        private void GoToDeployedUnit()
        {
            if (_inspect == null || _bridge == null || _unitData == null) return;
            if (_bridge.TryGetDeployedEntity(_unitData, out var entity))
                _inspect.SelectDeployed(entity);
        }

        // defender-relocation unit 10 rev — 소진 슬롯의 **드래그** 응답: 판 위 그 유닛을
        // 집어 든다(이동모드 진입). 손가락은 이미 눌려 있으므로 그대로 목적지 제스처가 된다.
        //
        // ⚠ board-limit 계약 5("소진 셀의 탭·드래그가 같은 결과")를 여기서 **가른다**.
        // 그 계약은 재배치에 대가가 없던 시절 것이다 — 그땐 두 제스처의 속마음이 "이 유닛
        // 쓰고 싶다" 하나였다. 이제 드래그에는 "저기로 옮긴다"라는 자기 의미가 생겼고,
        // 탭(데려가기)과 결과가 달라야 한다. 임계는 UGUI 가 이미 가르므로 새로 만들지 않는다.
        //
        // 진입에 실패하면(코스트 부족·쿨다운·페이즈) **종전 동작으로 폴백**한다 — 끌었는데
        // 아무 일도 안 일어나는 것보다 그 유닛을 보여주는 편이 낫다.
        private void TryBeginRelocationFromSlot(Vector2 pressScreen)
        {
            var relocation = _inspect != null ? _inspect.Relocation : null;
            if (relocation == null || _bridge == null || _unitData == null) { GoToDeployedUnit(); return; }
            if (!_bridge.TryGetDeployedEntity(_unitData, out var entity)
                || !_bridge.TryGetDefenderCell(entity, out var cell)) { GoToDeployedUnit(); return; }
            if (!relocation.BeginMoveModeFor(entity, cell, carriedPress: true, pressScreen: pressScreen))
                GoToDeployedUnit();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (_unitData == null || _controller == null) return;
            // defender-board-limit 1 — 소진은 기다려도 안 풀리는 사유라 쿨타임·코스트보다 먼저
            // 거른다(우선순위 소진 > 쿨타임 > 코스트).
            if (IsExhausted())
            {
                _suppressedDrag = true; // 배치 세션은 시작하지 않는다(이 유닛은 이미 판에 있다)
                TryBeginRelocationFromSlot(eventData.position); // 드래그 = 집어들기(탭은 데려가기)
                return;
            }
            // defender-placement-cooldown 1 — 쿨타임 중이면 세션 자체를 시작하지 않는다
            // (코스트와 독립 사유, 코스트 체크보다 먼저). 남은시간 표시는 unit 2 오버레이.
            var cdRuntime = GameManager.Instance != null ? GameManager.Instance.CooldownRuntime : null;
            if (cdRuntime != null && cdRuntime.RemainingFor(_unitData) > 0f)
            {
                _suppressedDrag = true;
                return;
            }
            // action-tray unit 4 — 비용 부족은 슬롯에서 즉시 차단: preview/slomo/drag
            // session 자체를 시작하지 않는다. 최종 권한은 여전히 BattleBridge
            // (TryBeginDefenderDeployment)에 있다 — 여기는 사전 피드백만.
            var costRuntime = GameManager.Instance != null ? GameManager.Instance.CostRuntime : null;
            if (costRuntime != null && !costRuntime.CanAfford(_unitData.cost))
            {
                _suppressedDrag = true;
                if (_costDisplay != null)
                    _costDisplay.PulseInsufficient(_unitData.cost - costRuntime.CurrentInt);
                return;
            }
            _suppressedDrag = false;
            _controller.BeginDrag(_unitData, eventData.position);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_controller == null || _suppressedDrag) return;
            _controller.UpdateDrag(eventData.position);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (_controller == null) return;
            if (_suppressedDrag) { _suppressedDrag = false; return; }
            _controller.EndDrag(eventData.position);
        }

        // defender-tap-to-place unit 1 — 탭(드래그 임계 미만)=arm 토글. 끌기면 Unity 가 OnBeginDrag 를 부르고 이건 안 옴.
        public void OnPointerClick(PointerEventData eventData)
        {
            if (_unitData == null || _controller == null) return;
            // review fix — 드래그 경로(OnBeginDrag)와 동일한 비용 사전 피드백: 부족하면 arm 하지 않고 pulse.
            // 단 이미 armed 인 슬롯의 재탭(=해제)은 비용과 무관하게 허용.
            if (!_controller.IsArmed(this))
            {
                // defender-board-limit 1·2 — 소진 셀은 arm 하지 않고 판 위 그 유닛으로 데려간다
                // (드래그 경로와 같은 결과 — 계약 5).
                if (IsExhausted())
                {
                    GoToDeployedUnit();
                    return;
                }
                // defender-placement-cooldown 1 — 쿨타임 중이면 arm 하지 않는다. 단 이미 armed
                // 슬롯의 재탭(=해제)은 위 !IsArmed 가드 밖이라 쿨타임과 무관하게 허용된다.
                var cdRuntime = GameManager.Instance != null ? GameManager.Instance.CooldownRuntime : null;
                if (cdRuntime != null && cdRuntime.RemainingFor(_unitData) > 0f) return;
                var costRuntime = GameManager.Instance != null ? GameManager.Instance.CostRuntime : null;
                if (costRuntime != null && !costRuntime.CanAfford(_unitData.cost))
                {
                    if (_costDisplay != null)
                        _costDisplay.PulseInsufficient(_unitData.cost - costRuntime.CurrentInt);
                    return;
                }
            }
            _controller.ToggleArm(this, _unitData, eventData.position);
        }

        public void SetArmed(bool armed)
        {
            if (armed && _armOverlay == null)
            {
                _armOverlay = new GameObject("ArmHighlight", typeof(RectTransform), typeof(Image));
                var rt = (RectTransform)_armOverlay.transform;
                rt.SetParent(transform, false);
                rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
                rt.offsetMin = new Vector2(-4f, -4f); rt.offsetMax = new Vector2(4f, 4f);
                rt.SetAsLastSibling();
                _armOverlay.GetComponent<Image>().raycastTarget = false;
            }
            if (_armOverlay != null)
            {
                if (armed) // review fix — 색은 SO(DragSwaySettings.armHighlightColor), 켤 때마다 재적용(라이브 튜닝)
                    _armOverlay.GetComponent<Image>().color = _controller != null
                        ? _controller.ArmHighlightColor : new Color(0.35f, 1f, 0.9f, 0.28f);
                _armOverlay.SetActive(armed);
            }
        }
    }
}
