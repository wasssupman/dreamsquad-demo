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
        // selection-hand-attach unit 0 — 손패 오픈 중에는 보드 탭만 양보한다(선택은 유지).
        // unit 6 의 선택 리티클 프레젠터(handView 소유)에도 이 참조로 도달한다.
        [Tooltip("손패 오픈 중 보드 탭 양보 + 선택 리티클 프레젠터 도달 경로")]
        [SerializeField] private DreamcatcherHandView handView;
        // 배치 드래그 중 인스펙트 양보. DefenderDragPlacementController 자체는 런타임
        // AddComponent 라 씬 배선이 불가능해, 수명 소유자인 DefenderSelector 를 경유한다.
        [SerializeField] private DefenderSelector defenderSelector;
        // defender-relocation unit 4 — 이동모드 중 인스펙트 양보(모드 배타). 짧은 탭=인스펙트,
        // 1초 홀드=이동모드는 그대로 공존한다(인스펙트는 press 다운 즉시, 홀드는 1초 뒤 발화).
        [SerializeField] private DefenderRelocationController relocationController;
        // hand.config 는 [SerializeField] private 에 접근자가 없어 자체 참조를 둔다
        // (DreamcatcherHandView 도 같은 이유로 config 를 따로 들고 있다).
        [SerializeField] private AwakeningConfig config;
        [SerializeField] private DcInspectPanelView panel;
        // defender-relocation unit 5 — 선택 시 유닛 좌측 액션 플립북(이동모드+더미2).
        [SerializeField] private DcActionFlipbookView actionFlipbook;
        // unit 4 — 선택 유닛 줌. 카메라 포즈의 유일한 쓰기 주체가 CameraDirector 이므로
        // 여기선 타겟만 피드한다(카메라 직접 조작 금지 계약).
        [SerializeField] private Wassup.Presentation.CameraDirector cameraDirector;
        // defender-relocation UX — 탭 판정 이동 허용치(px). 이보다 크게 움직이면 탭이 아님(홀드/드래그).
        [SerializeField] private float tapMoveThreshold = 24f;
        // selection-hand-attach unit 1 — 선택 유닛 앵커가 이만큼 연속 프레임 사라지면 선택 해제.
        // 1프레임 판정은 배치 직후/회수 타이밍의 일시 실패에 흔들린다.
        [SerializeField] private int anchorMissFramesToClose = 3;

        private readonly List<(Entity host, DreamcatcherCard card)> _scratch = new List<(Entity, DreamcatcherCard)>();
        private readonly List<DreamcatcherCard> _cards = new List<DreamcatcherCard>();
        private readonly List<int> _costs = new List<int>();
        private readonly List<RaycastResult> _uiHits = new List<RaycastResult>();
        private Entity _selected = Entity.Null;
        private TimeLease _slomoLease;
        // unit 6 — 선택 리티클을 우리가 켰는지 추적. Close 는 MustClose() 동안 매 프레임
        // 불리는데, 무조건 Focus.End() 하면 손패 카드 드래그가 방금 시작한 조준 세션까지
        // 끊는다 — 우리가 시작한 세션만 끝낸다.
        private bool _reticleShown;
        // selection-hand-attach unit 1 — 앵커 소실 연속 프레임 카운터(사망 감지).
        private int _anchorMissFrames;
        // defender-relocation UX — 탭 릴리즈 판정용 후보 상태(터치다운에 선택 금지).
        private bool _pendingTap;
        private Vector2 _pendingScreen;

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

            // 배치 드래그/arm 또는 이동모드가 입력을 쥐면 선택 자체를 닫는다(계약 8).
            // 손패 오픈·조준은 여기가 아니라 TapGated — 선택과 공존해야 한다(selection-hand-attach 계약 2).
            if (MustClose()) { _pendingTap = false; Close(); return; }

            // unit 4 — 선택 중 매 프레임 줌 타겟 피드. 끊기면 CameraDirector 가 2프레임 후
            // 자동 해제한다(명시 Clear 불필요 — 붙박이 줌 방지). Update(-50) → Director
            // LateUpdate(-90) 순서라 같은 프레임에 반영된다(전 Update 가 전 LateUpdate 보다 앞).
            //
            // selection-hand-attach unit 0 — 조준 중에는 피드를 끊는다. 예전엔 IsAiming 이
            // close-trigger 라 선택이 닫히며 줌이 함께 풀렸는데, 이제 선택이 조준과 공존하므로
            // 끊지 않으면 inspectDolly 만큼 당겨진 채 Meteor 타일/포탈 출구를 고르게 된다.
            // staleness 자동 해제라 조준이 끝나면 다음 프레임 피드로 줌이 되돌아온다.
            // unit 1 — 같은 앵커 조회가 사망 감지도 겸한다(TickSelectionAnchor).
            TickSelectionAnchor();

            var pointer = Pointer.current;
            if (pointer == null) { _pendingTap = false; return; }
            var screenPos = pointer.position.ReadValue();

            // selection-hand-attach unit 0 — 탭 후보 게이트는 **무장 분기 앞**이다. 뒤에 두면
            // press 프레임에 무장이 이미 끝나 무효다. 여기서 후보를 비우므로 stale 도 없다.
            if (TapGated()) { _pendingTap = false; return; }

            // defender-relocation review MEDIUM / UX — 선택(패널+카메라 줌)은 터치다운이 아니라
            // **탭 릴리즈**에 발동한다. 터치다운 즉시 인스펙트가 열려 카메라가 움직이면 1초 홀드
            // (재배치) 조작을 방해한다. 홀드로 이어지면 relocation 이 move mode 에 진입하고
            // MustClose() 가 여기 탭 후보를 취소하므로 인스펙트는 아예 안 열린다.
            //
            // 과거 press 규약을 쓴 이유(카드 드래그 OnEndDrag touchup 이 release 로 패널을 여는 것)는
            // 여기서 재발하지 않는다: 탭 후보는 **UI 밖 프레스다운**에서만 무장되는데 카드 드래그의
            // 프레스다운은 손패 카드(UI) 위라 무장되지 않고, 손패가 열린 동안은 TapGated() 가 참이다.
            if (pointer.press.wasPressedThisFrame)
            {
                _pendingTap = !IsOverUi(screenPos);
                _pendingScreen = screenPos;
                return;
            }
            if (!_pendingTap) return;

            // 이동량이 크면 탭이 아니다(드래그/홀드 의도) — 후보 취소.
            if (Vector2.Distance(screenPos, _pendingScreen) > Mathf.Max(1f, tapMoveThreshold))
            {
                _pendingTap = false;
                return;
            }
            if (pointer.press.wasReleasedThisFrame)
            {
                _pendingTap = false;
                if (!IsOverUi(screenPos)) HandleTap(screenPos); // 릴리즈가 UI 위면 무시
                return;
            }
            if (!pointer.press.isPressed) _pendingTap = false; // 릴리즈 이벤트 유실 방어
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

        // 선택 유닛 앵커를 프레임당 1회 조회해 두 가지를 한다:
        //  (1) 줌 타겟 피드 (unit 4 — 조준 중이면 끊는다, unit 0)
        //  (2) 수명 감시 (selection-hand-attach unit 1, critic M3)
        //
        // (2)가 필요한 이유: 부착 0장 유닛이 죽으면 DreamcatcherHandController.OnDefenderDied 가
        // 회수할 카드가 없어 AttachmentsChanged 를 **아예 발화하지 않는다**. 사망 닫힘을 그
        // 이벤트에만 걸어두면 선택·슬로모 lease·패널·열린 손패·죽은 SelectionTarget 이 좀비로
        // 남는다(즉발은 매번 "부착 불가" 움찔만 낸다). 앵커 소실이 유일하게 항상 오는 신호다.
        private void TickSelectionAnchor()
        {
            if (_selected == Entity.Null) { _anchorMissFrames = 0; return; }
            if (!bridge.TryGetUnitViewAnchor(_selected, out var anchor) || anchor == null)
            {
                if (++_anchorMissFrames >= Mathf.Max(1, anchorMissFramesToClose)) Close();
                return;
            }
            _anchorMissFrames = 0;
            if (cameraDirector != null && !AimingNow()) cameraDirector.SetInspectFocus(anchor.position);
        }

        // selection-hand-attach unit 0 — 구 Blocked() 의 절반: **선택 자체를 닫아야 하는** 조건.
        // 손패 오픈/조준은 여기서 빠졌다(TapGated 로 이관) — 선택과 공존해야 하는 것이 그 spec 의
        // 전제이기 때문이다. 여기 남은 둘은 "보드 입력의 주인이 아예 바뀌는" 경우다.
        private bool MustClose()
        {
            // 컨트롤러가 아직 AddComponent 되기 전이면 null — 드래그 중일 수 없으므로 통과.
            // placement-armed-board-drag unit 0: HasArmedUnit — 유닛이 arm 되면 보드 press 는 배치
            // 제스처(프레스-드래그-릴리즈)가 소유한다. 막지 않으면 armed 상태의 보드 탭이 인스펙트로도
            // 읽혀 두 소비자가 같은 press 를 노리는 aim-mode race 를 재생산한다(계약 11). arm 은 직전
            // 프레임에 확정돼 이 -50 실행순서와 무관하게 안정적으로 읽힌다.
            var drag = defenderSelector != null ? defenderSelector.DragController : null;
            if (drag != null && (drag.IsDragging || drag.HasArmedUnit)) return true;
            // defender-relocation unit 4 — 이동모드가 보드 입력을 쥐면 인스펙트는 닫고 물러난다.
            if (relocationController != null && relocationController.InMoveMode) return true;
            return false;
        }

        // selection-hand-attach unit 0 — 구 Blocked() 의 나머지 절반: **새 탭 후보만** 막는다.
        // 선택·줌·리티클·패널은 그대로 살아 있다(예전엔 이 조건들이 Close() 까지 했다).
        private bool TapGated()
        {
            // 손패가 열린 동안 보드 탭은 dismiss 캐처가 소유한다(unit 2). 여기서 raw press 를
            // 같이 노리면 두 소비자가 같은 press 를 다투게 된다(계약 11).
            if (handView != null && handView.State == DreamcatcherHandView.HandState.Hand) return true;
            // 조준 중 탭 오독 방지. drag.IsAiming(defender-directional-volley unit 6): 드롭은
            // 끝났지만 방향 지정 스와이프가 진행 중 — 그 스와이프가 유닛 탭으로도 읽히면 안 된다.
            if (AimingNow()) return true;
            return false;
        }

        // 조준(Active 카드 캐스트 / 포탈 2탭 대기 / 배치 방향 지정) 진행 중인가.
        // 탭 게이트와 줌 피드 중단이 같은 판정을 공유한다(계약 2).
        private bool AimingNow()
        {
            if (GameManager.Instance != null && GameManager.Instance.IsAiming) return true;
            var drag = defenderSelector != null ? defenderSelector.DragController : null;
            return drag != null && drag.IsAiming;
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
            _anchorMissFrames = 0; // unit 1 — 새 대상으로 수명 카운터 리셋
            Resolve(entity);
            // `?.` 가 아니라 `!= null` — 전자는 참조 null 검사로 낮아져 UnityEngine.Object 의
            // 수명 인지 == 연산자를 건너뛴다(파괴된 오브젝트가 통과한다).
            if (panel != null)
            {
                if (_cards.Count > 0) panel.Show(anchor, mainCamera, _cards, _costs);
                else panel.Hide(); // 빈 상태 UI 는 만들지 않는다 — 없는 게 정직하다
            }
            // unit 5 — 부착 유무와 무관하게 좌측 액션 플립북 등장(이동모드 진입 입구).
            if (actionFlipbook != null)
                actionFlipbook.Show(anchor, mainCamera, relocationController != null, OnMovePressed);
            // unit 6 — 조준 락온과 같은 리티클+콜아웃(portrait+이름)을 선택에도. 프레젠터는
            // 손패 뷰가 소유(Awake 생성)하므로 경유 도달, 미배선/focusConfig 미할당이면 생략.
            var focus = handView != null ? handView.Focus : null;
            if (focus != null)
            {
                if (bridge.TryGetDefenderCell(entity, out var cell))
                {
                    focus.BeginSelection(entity, cell);
                    _reticleShown = true;
                }
                else if (_reticleShown)
                {
                    // 전환 대상의 셀 해석 실패 — 이전 유닛 리티클이 남지 않게 끈다.
                    _reticleShown = false;
                    focus.End();
                }
            }
            AcquireSlomo();
            // selection-hand-attach unit 1 — 선택 = 손패 등장(사용자 결정 1: 항상). 대상 전달이
            // 먼저다(뷰가 InSelectionMode 를 그 값에서 파생하므로 오픈 연출 분기가 첫 프레임부터
            // 맞는다). 선택 전환(A→B)은 대상만 갱신되고 손패는 이미 열려 있어 재딜이 없다.
            if (handView != null)
            {
                handView.SetSelectionTarget(entity);
                handView.OpenForSelection();
            }
        }

        // unit 5 — 이동모드 버튼: 선택 해제(패널/줌/플립북) 후 relocation 이 자기 슬로모/하이라이트/
        // 카메라를 새로 잡는다(순차 handoff, lease 겹침 없음).
        private void OnMovePressed()
        {
            var e = _selected;
            if (e == Entity.Null || relocationController == null) return;
            bool hasCell = bridge.TryGetDefenderCell(e, out var cell);
            Close();
            if (hasCell) relocationController.BeginMoveModeFor(e, cell);
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
            // selection-hand-attach unit 1 — 손패 걷기는 "선택이 실제로 있었던" 닫힘만(계약 7
            // 비대칭). 지우기 전에 읽어야 한다. 무선택 상태에서 불린 Close 가 항아리 단독
            // 오픈을 걷어버리면 orb-dock 의 토글 계약이 깨진다(닫기 의도 탭은 unit 2 소관).
            bool hadSelection = _selected != Entity.Null;
            _selected = Entity.Null;
            _anchorMissFrames = 0;
            if (panel != null) panel.Hide();
            if (actionFlipbook != null) actionFlipbook.Hide();
            // unit 6 — 우리가 켠 리티클만 끈다(_reticleShown 가드 — 카드 드래그 조준 세션 보호).
            if (_reticleShown)
            {
                _reticleShown = false;
                var focus = handView != null ? handView.Focus : null;
                if (focus != null) focus.End();
            }
            _slomoLease.Dispose();
            if (hadSelection && handView != null)
            {
                handView.ClearSelectionTarget();
                handView.CloseFromSelection();
            }
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
