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
        // selection-hand-attach unit 15 — 유닛 주변 액션 플립북(defender-relocation unit 5)은
        // **폐기**됐다. unit 7 에서 표시만 껐다가(재배치가 도달 불가로 잠들었다) 이제 이동
        // 진입구를 상세 패널의 버튼으로 옮겼다 — 정보와 조작이 한 자리에 모인다.
        //
        // `DcActionFlipbookView` 클래스와 씬의 `DcActionFlipbook` 오브젝트는 아직 남아 있다.
        // 지금 씬을 저장할 수 없어(타 세션 WIP) 오브젝트를 지우지 못했고, 클래스를 먼저 지우면
        // 씬에 missing script 가 남기 때문이다. **아무도 구동하지 않으므로 inert** 하다 —
        // 씬을 안전하게 저장할 수 있을 때 오브젝트+클래스를 함께 제거한다.
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
            // selection-hand-attach unit 2 — 손패 오픈 중 보드 탭은 캐처가 소유하고 여기로 온다.
            if (handView != null)
            {
                handView.BoardTapped += OnBoardTapped;
                // unit 4 — 리티클 재주장 트리거 2개(슬롯 종료 깔때기 + 뷰의 하드 클리어).
                handView.InteractionEnded += OnFocusSessionReleased;
                handView.FocusCleared += OnFocusSessionReleased;
                // unit 8 — 부착 후 쓸 카드가 바닥나면 선택도 풀어 기본 진행으로 되돌린다.
                handView.UsableCardsExhausted += OnUsableCardsExhausted;
                // unit 9 — 선택 중 각성 버튼 = 그만하기. 손패와 함께 선택도 걷는다.
                handView.SelectionDismissed += OnSelectionDismissed;
                // active-ally-zone unit 3 — 선택 중 Active 조준 시작 = 선택만 놓는다(손패 유지).
                handView.SelectionReleasedForAim += ReleaseSelectionKeepHand;
            }
        }

        private void OnDisable()
        {
            if (hand != null) hand.AttachmentsChanged -= OnAttachmentsChanged;
            if (GameManager.Instance != null) GameManager.Instance.PhaseChanged -= OnPhaseChanged;
            if (handView != null)
            {
                handView.BoardTapped -= OnBoardTapped;
                handView.InteractionEnded -= OnFocusSessionReleased;
                handView.FocusCleared -= OnFocusSessionReleased;
                handView.UsableCardsExhausted -= OnUsableCardsExhausted;
                handView.SelectionDismissed -= OnSelectionDismissed;
                handView.SelectionReleasedForAim -= ReleaseSelectionKeepHand;
            }
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
            if (InspectZoomEnabled && cameraDirector != null && !AimingNow())
                cameraDirector.SetInspectFocus(anchor.position);

            // unit 11 — 실효 스탯은 매 프레임 갱신한다. 대상이 1 엔티티라 비용이 무시할 만하고
            // (이 메서드가 이미 매 프레임 앵커를 조회한다), HP 가 뚝뚝 끊기지 않는다.
            // 기본값은 readout 이 함께 실어 오므로 별도 캐시가 필요 없다.
            if (panel != null)
            {
                bool hasStats = bridge.TryGetUnitStatReadout(_selected, out var stats);
                panel.SetStats(stats, hasStats);

                // defender-relocation unit 10 — 액션 버튼 잠금도 같은 프레임 피드에 태운다.
                // Show 때 한 번으로는 "조건이 풀리면 활성화된다" 를 표현할 수 없다.
                // defender-clock-out unit 2 — 액션의 주인이 이동 → 퇴근으로 바뀌었다.
                // 비행 중(busy)이면 흐렸다가 착지하면 풀린다.
                if (bridge.TryGetDefenderCell(_selected, out var actionCell))
                {
                    if (RelocationEnabled && relocationController != null)
                    {
                        int cost = bridge.TryGetDefenderData(actionCell, out var moveData) && moveData != null
                            ? moveData.cost : 0;
                        panel.SetActionState(
                            relocationController.CanBeginMoveModeFor(_selected, actionCell),
                            cost > 0 ? $"이동  {cost}" : "이동");
                    }
                    else
                    {
                        panel.SetActionState(CanRetire(_selected, actionCell), RetireLabel);
                    }
                }
            }
        }

        // defender-clock-out unit 0 — 이동/재배치는 **퇴근으로 대체**됐다(팀 리뷰 2026-08-13).
        // 기능 코드(DefenderRelocationController · BattleBridge.Relocation.cs · RelocationSettings ·
        // 재배치 테스트)는 전부 남기고 **사람이 만지는 진입구만** 끈다. true 로 되돌리면 부활한다.
        //
        // ⚠ [SerializeField] 로 노출하지 않는다 — 인스펙터에서 켜고 씬을 저장하면 그 값이 조용히
        // 리포에 박힌다(이 프로젝트에서 반복된 사고). 진실원은 이 줄 하나다.
        // const 가 아니라 static readonly 인 이유: const 면 분기가 상수 폴딩돼 도달 불가 경고가 뜬다.
        private static readonly bool RelocationEnabled = false;

        // 2026-08-19 사용자 결정으로 **보드에 놓인 유닛을 직접 탭해 선택하는 경로를 껐다가**,
        // 2026-08-20 사용자 요청으로 **다시 켠다** — 판 위 유닛을 찍으면 상세가 떠야 한다.
        //
        // 켜진 지금 선택 진입구는 둘이다. 보드 유닛 탭(HandleTap · OnBoardTapped → TryPick →
        // Select)과 하단 트레이 소진 셀 탭(DefenderDragSlot.GoToDeployedUnit → SelectDeployed).
        // 후자는 이 게이트를 지나지 않는 **별도 입구**라 스위치 값과 무관하게 늘 산다 —
        // 입구가 갈라져 있다는 것이 한쪽만 끌 수 있었던 이유고, 되켜도 그대로다.
        //
        // 꺼져 있는 동안 보드 탭은 «해제 제스처» 전용이었다. 켜도 해제는 사라지지 않는다:
        // 빈 보드 탭(TryPick 실패)과 선택 유닛 재탭(entity == _selected)이 종전대로 걷는다.
        private static readonly bool BoardTapSelectEnabled = true;

        // 2026-08-19 사용자 결정 — **선택 줌(인스펙트 포커스)을 끈다.** 카메라는 홈 포즈를
        // 유지한다. 피드를 끊는 쪽으로 끈다(CameraDirectionConfig 의 inspectDolly/FovDelta/
        // LookWeight 를 0 으로 만드는 대신) — 그래야 프레이밍 바이어스까지 한 번에 멎고,
        // 같은 채널을 쓰는 DirectionAimController(방향 지정 조준 셀 포커스)는 영향이 없다.
        // 피드가 끊기면 CameraDirector 가 2프레임 staleness 로 자동 해제한다.
        //
        // ⚠ 이 파일의 스위치는 전부 [SerializeField] 금지 · const 아닌 static readonly
        // (const 면 분기가 상수 폴딩돼 «도달 불가» 경고가 뜨고 게이트 안 헬퍼가 미사용으로
        // 잡힌다). 인스펙터로 노출하면 켠 값이 씬 저장에 조용히 박힌다 — 진실원은 이 줄들이다.
        private static readonly bool InspectZoomEnabled = false;

        // defender-relocation unit 10 rev — 트레이 소진 슬롯이 이동모드를 열 때 쓰는 경로.
        // 슬롯(DefenderDragSlot)은 런타임 생성이라 인스펙터 배선이 없고, 이미 이 컨트롤러
        // 참조를 Bind 로 받고 있다. **여기 하나만 노출하면 씬 배선이 늘지 않는다.**
        //
        // defender-clock-out unit 0 — null 을 돌려주면 DefenderDragSlot.TryBeginRelocationFromSlot 이
        // **자기 폴백**으로 종전 동작(판 위 그 유닛 선택)에 돌아간다. 슬롯 파일은 건드리지 않는다 —
        // 거기에 두 번째 스위치를 심으면 진실원이 둘이 된다.
        public DefenderRelocationController Relocation => RelocationEnabled ? relocationController : null;

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
            // 조준 중 탭 오독 방지(Active 캐스트·포탈 대기).
            if (AimingNow()) return true;
            return false;
        }

        // 조준(Active 카드 캐스트 / 포탈 2탭 대기) 진행 중인가.
        // 탭 게이트와 줌 피드 중단이 같은 판정을 공유한다(계약 2).
        // unit 11 — 배치 방향 지정(drag.IsAiming)은 facing 과 함께 은퇴.
        private bool AimingNow()
            => GameManager.Instance != null && GameManager.Instance.IsAiming;

        // selection-hand-attach unit 2 — 손패 오픈 중 보드 탭 라우팅. 손패가 닫혀 있을 때의
        // raw 탭(HandleTap)과 결과는 같지만 "빈 보드/재탭" 이 **닫기 의도 탭**이라 선택 유무와
        // 무관하게 손패까지 걷는다(계약 7 — 항아리 단독 오픈의 바깥 탭 dismiss 보존).
        private void OnBoardTapped(Vector2 screenPos)
        {
            // 이동모드/배치 드래그가 입력 주인일 때는 캐처 클릭으로 선택이 오염되지 않게(critic M6).
            if (MustClose()) return;
            // BoardTapSelectEnabled — 켜져 있으면 보드 탭이 유닛을 고른다(전환 포함).
            // 꺼져 있으면 이 분기를 건너뛰어 모든 보드 탭이 해제가 되고, 선택 전환은
            // 트레이 셀 재탭만 남는다(SelectDeployed 는 전환도 겸한다).
            if (BoardTapSelectEnabled && TryPick(screenPos, out var entity) && entity != _selected)
            {
                Select(entity); // 전환(무선택 상태의 첫 선택도 이 경로) — 손패는 유지된다
                return;
            }
            CloseByIntent(); // 빈 보드 · 선택 유닛 재탭 · (선택 꺼짐) 모든 보드 탭 = 해제
        }

        // 닫기 의도 탭 전용 — 선택이 없어도 손패를 걷는다. Close() 는 "선택이 있었을 때만"
        // 손패를 닫으므로(무선택 no-op 계약) 그것만으로는 항아리 단독 오픈을 못 닫는다(critic H3).
        private void CloseByIntent()
        {
            Close();
            if (handView == null) return;
            handView.ClearSelectionTarget();
            handView.CloseFromSelection(); // 이미 닫혔으면 no-op
        }

        private void HandleTap(Vector2 screenPos)
        {
            // BoardTapSelectEnabled 가 꺼지면 보드 raw 탭은 «해제» 만 한다(선택은 트레이 셀 전용).
            if (!BoardTapSelectEnabled) { Close(); return; }
            if (!TryPick(screenPos, out var entity)) { Close(); return; } // 빈 보드 → 닫기
            if (entity == _selected) { Close(); return; }                 // 재탭 → 토글
            Select(entity);
        }

        // 계약 2 — DreamcatcherCardDragSlot.UpdateUnitHover 와 같은 2단 픽킹.
        // 1차는 스프라이트 스크린 렉트(몸체 포인팅), 2차는 보드 평면 셀(발밑 + quad 폴백 뷰).
        // 틸트 빌보드라 몸체가 발밑 셀보다 화면상 위로 솟으므로 평면 레이캐스트만으로는 놓친다.
        private bool TryPick(Vector2 screenPos, out Entity entity)
        {
            // defender-footprint unit 4 — 부착과 **같은 SO**(handView 의 FocusConfig)에서 패딩을
            // 읽는다(오탭 비용이 낮은 완충). rev 3(2026-08-28 재검토) — **자석은 탭에 걸지 않는다**:
            // 자석의 근거(락온→콜아웃 확인→조정→릴리즈의 자기 교정 루프)는 드래그의 성질이고,
            // 탭은 즉시 커밋이라 확인 루프가 없다. 걸면 «빈 보드 탭 = 닫기/해제» 제스처가 유닛
            // 60px 반경마다 재선택으로 오발돼 밀집 판에서 닫을 곳이 사라진다.
            var cfg = handView != null ? handView.FocusConfig : null;
            if (bridge.TryPickDefenderAtScreen(mainCamera, screenPos, out entity, out _,
                    cfg != null ? cfg.unitPickPaddingPx : 0f)) return true;
            if (bridge.TryScreenToCell(mainCamera, screenPos, out var cell) &&
                bridge.TryGetDefenderAt(cell, out entity)) return true;
            entity = Entity.Null;
            return false;
        }

        // unit 4 (사용자 결정 2026-07-15) — 부착 유무와 무관하게 선택된다. 줌 자체가 피드백이라
        // 부착 0장이어도 "이 유닛을 보고 있다"가 성립한다.
        // unit 11 — 패널도 **항상** 뜬다. 스탯이 생기면서 부착 0장이어도 보여줄 것이 있다
        // (구 "패널은 보여줄 게 있을 때만" 은 부착 목록만 있던 시절의 계약이다).
        // defender-board-limit 2 — 트레이 소진 셀에서 들어오는 선택. 보드를 직접 탭한 것과
        // **완전히 같은 사건**이어야 하므로 새 경로를 만들지 않고 Select 를 그대로 탄다
        // (그래서 각성 손패도 함께 열린다 — "선택 = 손패 등장" 기존 계약, 의도된 결과다).
        // 재탭 토글은 없다: 트레이 셀은 "가리키기" 전용이고, 닫기는 보드 탭이 소유한다.
        public void SelectDeployed(Entity entity)
        {
            if (entity == Entity.Null) return;
            // 보드 탭이 Update 에서 통과해야 하는 관문을 **여기서도** 지난다. 트레이 탭은
            // Update 의 입력 경로를 타지 않으므로 게이트를 물려받지 못한다.
            // (tutorial-content-teardown unit 0 — 첫 판 각성 봉인 관문은 함께 걷혔다.)
            if (MustClose()) return;         // 배치 드래그/arm/이동모드가 입력을 쥔 상태
            Select(entity);
        }

        private void Select(Entity entity)
        {
            // 앵커는 **유효성 확인용**이다 — 살아있는 뷰가 없으면 선택 자체가 성립하지 않는다.
            // 좌표는 더 이상 아무도 쓰지 않는다(패널은 고정 도킹 unit 11, 플립북은 폐기 unit 15).
            if (!bridge.TryGetUnitViewAnchor(entity, out _)) { Close(); return; }

            _selected = entity;
            _anchorMissFrames = 0; // unit 1 — 새 대상으로 수명 카운터 리셋
            ShowPanelFor(entity); // 내부에서 Resolve — _cards/_costs 도 여기서 채워진다
            ShowSelectionReticle(entity);
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

        // unit 6 — 조준 락온과 같은 리티클+콜아웃(portrait+이름)을 선택에도. 프레젠터는 손패 뷰가
        // 소유(Awake 생성)하므로 경유 도달하고, 미배선/focusConfig 미할당이면 조용히 생략한다.
        // 호출처 2 — Select(신규/전환) + 재주장(unit 4).
        private void ShowSelectionReticle(Entity entity)
        {
            var focus = handView != null ? handView.Focus : null;
            if (focus == null) return;
            if (bridge.TryGetDefenderCell(entity, out var cell))
            {
                focus.BeginSelection(entity, cell);
                _reticleShown = true;
            }
            else if (_reticleShown)
            {
                // 셀 해석 실패(전환 대상 소실 등) — 이전 유닛 리티클이 남지 않게 끈다.
                _reticleShown = false;
                focus.End();
            }
        }

        // selection-hand-attach unit 8 — 부착으로 게이지를 다 쓴 순간(손패 자동 닫힘 확정).
        // 선택을 유지하면 줌+슬로모 상태로 할 일 없이 남으므로 여기서 함께 풀어 기본 진행으로
        // 되돌린다(계약 7 의 유일한 예외 — 손패 단독 닫힘이 선택을 데려가는 경우).
        // 미선택(항아리 단독 오픈)에서 게이지가 바닥난 경우엔 Close() 가 no-op 이라 무해하다.
        private void OnUsableCardsExhausted() => Close();

        // selection-hand-attach unit 9 — 선택 중 각성 버튼(항아리)을 눌렀다 = 명시적 "그만하기".
        // 손패만 걷으면 줌+슬로모가 걸린 채 할 일 없는 선택이 남아 빈 보드를 따로 탭해야 풀린다.
        // 뷰가 InSelectionMode 일 때만 쏘므로 항아리 단독 오픈의 dismiss 는 영향받지 않는다.
        private void OnSelectionDismissed() => Close();

        // selection-hand-attach unit 4 — 프레젠터 세션이 남에 의해 끝났다. 선택이 살아 있으면
        // 리티클을 1회 재주장한다(폴링 금지 — BeginSelection 은 매 프레임 부르면 pop 이 계속
        // 재생돼 깨진다). 조건은 코드 그대로다: TapGated() 를 쓰면 손패 오픈이 포함돼 재주장이
        // 영영 돌지 않는다(그게 주 시나리오다).
        private void OnFocusSessionReleased()
        {
            _reticleShown = false; // 남이 끝낸 세션 — 우리 플래그가 stale 로 남으면 남의 세션에 End 를 쏜다
            if (_selected == Entity.Null) return;
            if (AimingNow()) return; // 포탈 2탭 대기 등 — 조준이 끝날 때 다시 신호가 온다
            ShowSelectionReticle(_selected);
        }

        // unit 5 — 이동모드 버튼: 선택 해제(패널/줌/플립북) 후 relocation 이 자기 슬로모/하이라이트/
        // 카메라를 새로 잡는다(순차 handoff, lease 겹침 없음).
        //
        // defender-clock-out unit 0 — RelocationEnabled=false 인 동안 액션 슬롯의 주인은 퇴근이라
        // 이 콜백은 배선되지 않는다. 되살릴 땐 아래 액션 배선 3줄(라벨·가드·콜백)을 되돌린다.
        private void OnMovePressed()
        {
            var e = _selected;
            if (e == Entity.Null || relocationController == null) return;
            bool hasCell = bridge.TryGetDefenderCell(e, out var cell);
            Close();
            if (hasCell) relocationController.BeginMoveModeFor(e, cell);
        }

        // ── 퇴근 (defender-clock-out unit 2) ──────────────────────────────────

        // 액션 슬롯 문안. 뷰는 기능을 모르므로 라벨의 주인은 여기다.
        private const string RetireLabel = "철수";

        // 액션 슬롯 1칸의 **현재 주인**을 고른다. 이동을 되살리려면 RelocationEnabled 하나만
        // true 로 바꾸면 되고, 라벨·가드·콜백이 이 세 곳에서 함께 갈린다.
        private System.Action ResolveActionCallback()
        {
            if (RelocationEnabled) return relocationController != null ? (System.Action)OnMovePressed : null;
            return bridge != null ? (System.Action)OnRetirePressed : null;
        }

        // 진입 조건 = 전투 페이즈 + 살아 있는 대상 + 비-busy. 이동 가드에서 코스트·이동모드·
        // 진입쿨다운을 뺀 형태다(퇴근은 무료이고 모드가 없다).
        //
        // 참고: TryGetDefenderAt(…, out busy) 는 BattleBridge.Relocation.cs 에 산다 — 퇴근이
        // 재배치 파일의 헬퍼를 쓴다는 뜻이라 "이동은 죽었다" 는 절반만 참이다(README 후속 후보).
        private bool CanRetire(Entity e, Vector2Int cell)
        {
            var gm = GameManager.Instance;
            if (bridge == null || gm == null || gm.CurrentPhase != GamePhase.Battle) return false;
            return bridge.TryGetDefenderAt(cell, out var found, out _, out bool busy) && !busy && found == e;
        }

        // 확인 절차 없음 — 누르면 즉시 퇴근한다(계약 8). 되돌릴 수 없다는 사실은 그 유닛의
        // 트레이 칸에서 쿨타임이 흐르기 시작하는 것으로 사후에 읽힌다.
        private void OnRetirePressed()
        {
            var e = _selected;
            if (e == Entity.Null || bridge == null) return;
            if (!bridge.TryGetDefenderCell(e, out var cell)) return;
            if (!CanRetire(e, cell)) return;
            Close(); // 대상이 사라지므로 패널·리티클·줌을 먼저 접는다
            bridge.RetireDefender(cell);
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
            if (panel != null) panel.Hide(); // 이동 버튼도 패널과 함께 사라진다(unit 15)
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

        // active-ally-zone unit 3 — 선택만 놓고 **손패는 유지**한다. 선택 중 Active 조준이 시작될 때
        // 쓰는 경로다: 카드를 이미 잡고 있으므로 손패가 닫히면 안 된다.
        //
        // Close() 를 쓰지 않는 이유: 그쪽은 손패 걷기(CloseFromSelection)까지 한 몸이고, 그 안의
        // CancelAllCardInteraction() 이 **방금 시작한 드래그를 취소**한다.
        //
        // 반대로 `_slomoLease` 는 **반드시** 놓는다. 이건 인스펙트 자기 lease(AcquireSlomo, 0.3×,
        // priority 50)이고 지금까지 Close() 에서만 해제됐다. 안 걷으면 손패가 열린 동안엔 조준
        // lease 와 값이 같아 보이지 않다가, 손패가 닫힌 뒤 Battle 도메인을 0.3× 로 **고착**시킨다
        // (TimeManager 가 lease 스택에서 (priority desc, scale asc) 로 승자를 뽑는다).
        // 조준 슬로모는 DreamcatcherHandView 가 소유하는 다른 lease 라 영향받지 않는다.
        private void ReleaseSelectionKeepHand()
        {
            if (_selected == Entity.Null) return;
            _selected = Entity.Null;
            _anchorMissFrames = 0;
            if (panel != null) panel.Hide();
            // 우리가 켠 리티클만 끈다 — 카드 조준 세션의 포커스는 건드리지 않는다.
            if (_reticleShown)
            {
                _reticleShown = false;
                var focus = handView != null ? handView.Focus : null;
                if (focus != null) focus.End();
            }
            _slomoLease.Dispose();
            // 탭 즉발 부착의 게이트가 이 값이다 — 안 비우면 해제된 선택을 계속 타겟으로 본다.
            if (handView != null) handView.ClearSelectionTarget();
            // 줌은 신규 API 없이 자동 복귀한다: 위에서 `_selected` 를 비웠으므로
            // TickSelectionAnchor 가 첫 줄에서 조기 return 해 SetInspectFocus 피드가 끊기고,
            // CameraDirector 가 staleness 로 페이드아웃한다.
            //
            // selection-hand-attach unit 17 rev 2 — 이 기전은 **조준 종류에 중립**이다. 예전
            // 주석은 "조준 시작이 IsAiming 을 세웠으므로 피드가 끊긴다"고 적었는데, 그건 Active
            // 캐스트만 세우는 플래그다(제물 표식은 안 세운다). 실제 근거는 `_selected` 이고,
            // 그래서 비-Defender 조준 전체가 이 경로를 안전하게 공유한다.
        }

        // 부착 변경(부착/사망 회수/Placement 리셋). 선택 유닛이 카드를 잃었거나 죽었으면 닫고,
        // 아니면 목록을 다시 그린다.
        private void OnAttachmentsChanged()
        {
            if (_selected == Entity.Null) return;
            // 앵커 소실 = 호스트 사망 → 선택 해제. unit 4 이후로는 **부착이 0장이 된 것만으로는
            // 닫지 않는다**(선택은 부착과 무관) — 카드를 다 잃어도 유닛은 살아있을 수 있다.
            if (!bridge.TryGetUnitViewAnchor(_selected, out _)) { Close(); return; }
            ShowPanelFor(_selected);
        }

        // unit 11 — 패널 구조 갱신(선택 / 부착 변경). 스탯 값은 TickSelectionAnchor 가 매 프레임
        // 따로 넣는다. 이름·포트레이트는 배치 셀로 SO 를 되짚어 얻는다(뷰는 Entity 를 모른다).
        private void ShowPanelFor(Entity entity)
        {
            // `?.` 가 아니라 `!= null` — 전자는 참조 null 검사로 낮아져 UnityEngine.Object 의
            // 수명 인지 == 연산자를 건너뛴다(파괴된 오브젝트가 통과한다).
            if (panel == null) return;
            Resolve(entity);
            string unitName = null;
            Sprite portrait = null;
            if (bridge.TryGetDefenderCell(entity, out var cell) &&
                bridge.TryGetDefenderData(cell, out var data) && data != null)
            {
                unitName = data.displayName;
                portrait = data.portrait;
            }
            // unit 15 — 진입구는 패널의 액션 버튼 1칸이다(유닛 주변 플립북 폐기).
            // defender-clock-out unit 2 — 그 1칸의 주인이 이동 → **퇴근**으로 바뀌었다.
            // 미배선이면 콜백을 넘기지 않아 버튼 자체가 뜨지 않는다(뷰의 기존 계약).
            panel.Show(unitName, portrait, _cards, _costs, ResolveActionCallback());
        }

        // 계약 9 — DreamcatcherHandView.OnPhaseChanged 선례. UGUI 패널은 월드 스프라이트와
        // 달리 앵커 파괴 후 잔류물이 없어 BattleBridge teardown 훅이 필요 없다.
        private void OnPhaseChanged(GamePhase phase)
        {
            if (phase != GamePhase.Placement && phase != GamePhase.Battle) Close();
        }
    }
}
