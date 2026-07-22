using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Wassup.Core;
using Wassup.Data;

namespace Wassup.UI
{
    public class DefenderDragSlot : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
    {
        private DefenderUnitData _unitData;
        private DefenderDragPlacementController _controller;
        private CostDisplay _costDisplay;
        // defender-tap-to-place unit 1 — arm 하이라이트 오버레이(lazy).
        private GameObject _armOverlay;
        // action-tray unit 4 — 비용 부족으로 차단된 드래그 제스처. 이후 OnDrag/OnEndDrag
        // 를 무시해 controller 세션이 시작되지 않는다(다음 제스처에서 리셋).
        private bool _suppressedDrag;

        public void Bind(DefenderUnitData unitData, DefenderDragPlacementController controller,
            CostDisplay costDisplay = null)
        {
            _unitData = unitData;
            _controller = controller;
            _costDisplay = costDisplay;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (_unitData == null || _controller == null) return;
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
