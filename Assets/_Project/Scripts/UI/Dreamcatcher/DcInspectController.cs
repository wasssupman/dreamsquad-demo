using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using Wassup.Bridge;
using Wassup.Core;
using Wassup.Core.TimeControl;
using Wassup.Data;

namespace Wassup.UI
{
    // unit-dreamcatcher-inspect unit 1 — 보드 유닛 탭 → 부착 드림캐쳐 상세 패널 + Battle 슬로우.
    //
    // 보드 raw 탭의 단일 소비자다(spec 계약 11). 후속(배치 유닛 범위 표시 등)은 두 번째 탭
    // 핸들러를 만들지 말고 이 컨트롤러의 선택을 구독해 확장할 것 — 두 소비자가 같은 press 를
    // 노리면 PlacementInput 이 기록한 aim-mode race 를 재생산한다.
    //
    // DefaultExecutionOrder = -50: PlacementInput 과 동렬. 포탈 2탭 핸들러
    // (DreamcatcherCardDragSlot.Update, order 0)가 같은 프레임에 EndInteraction 으로
    // GameManager.IsAiming 을 내리기 전에 읽어야 한다. 늦게 읽으면 포탈 출구를 확정한 바로
    // 그 탭이 인스펙트 패널을 연다.
    [DefaultExecutionOrder(-50)]
    public class DcInspectController : MonoBehaviour
    {
        private const int SlomoPriority = 50; // DreamcatcherHandView.Open 과 동일 등급

        [SerializeField] private BattleBridge bridge;
        [SerializeField] private Camera mainCamera;
        [SerializeField] private DreamcatcherHandController hand;
        [Tooltip("손패 오픈 중 인스펙트 양보")]
        [SerializeField] private DreamcatcherHandView handView;
        // 배치 드래그 중 인스펙트 양보. DefenderDragPlacementController 자체는 런타임
        // AddComponent 라 씬 배선이 불가능해, 수명 소유자인 DefenderSelector 를 경유한다.
        [SerializeField] private DefenderSelector defenderSelector;
        // hand.config 는 [SerializeField] private 에 접근자가 없어 자체 참조를 둔다
        // (DreamcatcherHandView 도 같은 이유로 config 를 따로 들고 있다).
        [SerializeField] private AwakeningConfig config;
        [SerializeField] private DcInspectPanelView panel;
        // unit 4 — 선택 유닛 줌. 카메라 포즈의 유일한 쓰기 주체가 CameraDirector 이므로
        // 여기선 타겟만 피드한다(카메라 직접 조작 금지 계약).
        [SerializeField] private Wassup.Presentation.CameraDirector cameraDirector;

        private readonly List<(Entity host, DreamcatcherCard card)> _scratch = new List<(Entity, DreamcatcherCard)>();
        private readonly List<DreamcatcherCard> _cards = new List<DreamcatcherCard>();
        private readonly List<int> _costs = new List<int>();
        private readonly List<RaycastResult> _uiHits = new List<RaycastResult>();
        private Entity _selected = Entity.Null;
        private TimeLease _slomoLease;

        private void Start()
        {
            if (mainCamera == null) mainCamera = Camera.main;
        }

        private void OnEnable()
        {
            if (hand != null) hand.AttachmentsChanged += OnAttachmentsChanged;
            if (GameManager.Instance != null) GameManager.Instance.PhaseChanged += OnPhaseChanged;
        }

        private void OnDisable()
        {
            if (hand != null) hand.AttachmentsChanged -= OnAttachmentsChanged;
            if (GameManager.Instance != null) GameManager.Instance.PhaseChanged -= OnPhaseChanged;
            Close(); // lease 해제 — 비활성화가 슬로우를 남기면 안 된다
        }

        private void Update()
        {
            if (bridge == null || mainCamera == null) return;

            // 배타 파트너가 입력을 쥐면 열려 있던 패널은 닫는다(계약 8). 손패를 열거나
            // 배치 드래그를 시작하면 인스펙트는 물러난다.
            if (Blocked()) { Close(); return; }

            // unit 4 — 선택 중 매 프레임 줌 타겟 피드. 끊기면 CameraDirector 가 2프레임 후
            // 자동 해제한다(명시 Clear 불필요 — 붙박이 줌 방지). Update(-50) → Director
            // LateUpdate(-90) 순서라 같은 프레임에 반영된다(전 Update 가 전 LateUpdate 보다 앞).
            FeedZoomTarget();

            var pointer = Pointer.current;
            if (pointer == null) return;
            // 계약 3 — press 규약. release 로 하면 DreamcatcherCardDragSlot.OnEndDrag 가
            // touchup 으로 부착을 커밋하는 바로 그 제스처가 패널까지 열어버린다.
            if (!pointer.press.wasPressedThisFrame) return;

            var screenPos = pointer.position.ReadValue();
            // UI 위에서 시작한 press 는 보드 탭이 아니다.
            if (IsOverUi(screenPos)) return;

            HandleTap(screenPos);
        }

        // EventSystem.IsPointerOverGameObject() 를 쓰지 않는다(중요).
        // 그 API 는 EventSystem.Update 가 세운 **지난 프레임** pointer 상태를 읽는다
        // (InputSystemUIInputModule: 음수 id → m_PointerStates[..].eventData.pointerEnter,
        //  그리고 "calling this method earlier than that in the frame will make it poll
        //  state from last frame" 라고 명시). EventSystem 은 실행 순서 0 인데 이 컨트롤러는
        // -50(계약 4, 포탈 레이스) 이라 항상 먼저 돈다. 터치는 hover 가 없어 press 프레임에
        // pointer 상태 자체가 없으므로 → 손가락이 UI 위에 있어도 false 를 답한다.
        // 마우스에선 hover 잔상이 이 결함을 가린다(에디터에서 안 잡히는 실기기 전용 버그).
        // PlacementInput 이 같은 패턴을 쓰지만 클릭 배치가 은퇴해 아무도 밟지 않았다.
        // → 실행 순서와 무관한 즉석 UI 레이캐스트로 대체한다. press 때만 도는 경로다.
        private bool IsOverUi(Vector2 screenPos)
        {
            var es = EventSystem.current;
            if (es == null) return false;
            _uiHits.Clear();
            es.RaycastAll(new PointerEventData(es) { position = screenPos }, _uiHits);
            return _uiHits.Count > 0;
        }

        // 앵커가 사라졌으면(유닛 사망) 피드를 멈춘다 — staleness 가 줌을 되돌린다.
        private void FeedZoomTarget()
        {
            if (cameraDirector == null || _selected == Entity.Null) return;
            if (!bridge.TryGetUnitViewAnchor(_selected, out var anchor) || anchor == null) return;
            cameraDirector.SetInspectFocus(anchor.position);
        }

        private bool Blocked()
        {
            if (GameManager.Instance != null && GameManager.Instance.IsAiming) return true;
            if (handView != null && handView.State == DreamcatcherHandView.HandState.Hand) return true;
            // 컨트롤러가 아직 AddComponent 되기 전이면 null — 드래그 중일 수 없으므로 통과.
            // IsAiming(defender-directional-volley unit 6): 드롭은 끝났지만 방향 지정
            // 스와이프가 진행 중이다. 막지 않으면 그 스와이프가 유닛 탭으로도 읽혀 인스펙트가
            // 열리고, 방향 확정 뒤에도 이쪽 slomo/줌이 남아 닫는 클릭이 한 번 더 필요해진다.
            var drag = defenderSelector != null ? defenderSelector.DragController : null;
            if (drag != null && (drag.IsDragging || drag.IsAiming)) return true;
            return false;
        }

        private void HandleTap(Vector2 screenPos)
        {
            if (!TryPick(screenPos, out var entity)) { Close(); return; } // 빈 보드 → 닫기
            if (entity == _selected) { Close(); return; }                 // 재탭 → 토글
            Select(entity);
        }

        // 계약 2 — DreamcatcherCardDragSlot.UpdateUnitHover 와 같은 2단 픽킹.
        // 1차는 스프라이트 스크린 렉트(몸체 포인팅), 2차는 보드 평면 셀(발밑 + quad 폴백 뷰).
        // 틸트 빌보드라 몸체가 발밑 셀보다 화면상 위로 솟으므로 평면 레이캐스트만으로는 놓친다.
        private bool TryPick(Vector2 screenPos, out Entity entity)
        {
            if (bridge.TryPickDefenderAtScreen(mainCamera, screenPos, out entity, out _)) return true;
            if (bridge.TryScreenToCell(mainCamera, screenPos, out var cell) &&
                bridge.TryGetDefenderAt(cell, out entity)) return true;
            entity = Entity.Null;
            return false;
        }

        // unit 4 (사용자 결정 2026-07-15) — 부착 유무와 무관하게 선택된다. 줌 자체가 피드백이라
        // 부착 0장이어도 "이 유닛을 보고 있다"가 성립한다. 패널만 보여줄 게 있을 때 뜬다.
        private void Select(Entity entity)
        {
            if (!bridge.TryGetUnitViewAnchor(entity, out var anchor)) { Close(); return; }

            _selected = entity;
            Resolve(entity);
            // `?.` 가 아니라 `!= null` — 전자는 참조 null 검사로 낮아져 UnityEngine.Object 의
            // 수명 인지 == 연산자를 건너뛴다(파괴된 오브젝트가 통과한다).
            if (panel != null)
            {
                if (_cards.Count > 0) panel.Show(anchor, mainCamera, _cards, _costs);
                else panel.Hide(); // 빈 상태 UI 는 만들지 않는다 — 없는 게 정직하다
            }
            AcquireSlomo();
        }

        // 부착 목록에서 host 에 걸린 카드만 추린다. 반환 false = 보여줄 게 없다(선택 자체는 유효).
        private bool Resolve(Entity host)
        {
            _cards.Clear();
            _costs.Clear();
            if (hand == null) return false;
            hand.GetAttachments(_scratch);
            foreach (var (h, card) in _scratch)
            {
                if (h != host || card == null) continue;
                _cards.Add(card);
                _costs.Add(hand.CostOf(card)); // 코스트는 카드 SO 에 없다 — 컨트롤러가 해석해 뷰에 넘긴다
            }
            return _cards.Count > 0;
        }

        private void AcquireSlomo()
        {
            _slomoLease.Dispose(); // 교체 시 누수 방지(멱등 — id 재사용 없음)
            float scale = config != null ? Mathf.Max(0.01f, config.slomoTimeScale) : 0.3f;
            // 절대 0 아님 — 게임이 멈추면 안 된다(AwakeningConfig.slomoTimeScale 계약).
            _slomoLease = TimeManager.Instance.Request(TimeDomain.Battle, scale, SlomoPriority);
        }

        // 멱등 — 미선택 상태에서 불려도 no-op.
        private void Close()
        {
            _selected = Entity.Null;
            if (panel != null) panel.Hide();
            _slomoLease.Dispose();
        }

        // 부착 변경(부착/사망 회수/Placement 리셋). 선택 유닛이 카드를 잃었거나 죽었으면 닫고,
        // 아니면 목록을 다시 그린다.
        private void OnAttachmentsChanged()
        {
            if (_selected == Entity.Null) return;
            // 앵커 소실 = 호스트 사망 → 선택 해제. unit 4 이후로는 **부착이 0장이 된 것만으로는
            // 닫지 않는다**(선택은 부착과 무관) — 카드를 다 잃어도 유닛은 살아있을 수 있다.
            if (!bridge.TryGetUnitViewAnchor(_selected, out var anchor)) { Close(); return; }
            Resolve(_selected);
            if (panel != null)
            {
                if (_cards.Count > 0) panel.Show(anchor, mainCamera, _cards, _costs);
                else panel.Hide();
            }
        }

        // 계약 9 — DreamcatcherHandView.OnPhaseChanged 선례. UGUI 패널은 월드 스프라이트와
        // 달리 앵커 파괴 후 잔류물이 없어 BattleBridge teardown 훅이 필요 없다.
        private void OnPhaseChanged(GamePhase phase)
        {
            if (phase != GamePhase.Placement && phase != GamePhase.Battle) Close();
        }
    }
}
