using Unity.Mathematics;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using Wassup.BattleCore;
using Wassup.BattleCoreUnity.Hud;
using Wassup.BattleCoreUnity.View;
using Wassup.Core;
using Wassup.Data;
using Wassup.Presentation;
using Wassup.UI;

namespace Wassup.BattleCoreUnity.Input
{
    // battle-core-rebuild unit 5b — **드래그 배치.** 옛 `DefenderDragPlacementController`(2,144줄)
    // 에서 가져온 것은 **드래그·스냅·프리뷰뿐**이다.
    //
    // 옛 컨트롤러는 판정을 복제하고 있었다: 「코스트가 모자라면 거부」·「소진이면 거부」·
    // 「여기 못 놓으면 거부」가 컨트롤러·트레이·브리지 **세 곳**에 살았고, 그래서 셋 중 하나만
    // 고치면 화면과 판정이 갈렸다. 여기서는 그 셋이 **receipt 하나**로 접힌다.
    //
    // 이 파일이 하는 일 정확히 넷:
    //   ⓪ 트레이 칸을 **탭**하면 그 유닛을 집어 든다(armed) — 그 뒤 판 탭/드래그로 놓는다
    //   ① 손가락을 칸으로 옮긴다(`PlacementCellSnap` · `PlacementSnapDebounce` — 순수 함수 재사용)
    //   ② 그 칸이 놓을 수 있는지 **코어에 묻는다**(`Judge`) — 재판정 아님. **보정은 없다**:
    //      손끝이 가리킨 칸이 곧 결과이고, 못 놓는 칸이면 고스트가 빨강으로 머문다
    //      (자석 스냅은 사용자 결정 2026-09-23 으로 은퇴 — `TryResolveAnchor` 헤더)
    //   ③ 놓는다(`Command.PlaceDefender`) 그리고 receipt 를 **표시한다**
    //   ④ 비행을 **띄워 보낸다**(`CoreDeployFlightPresenter.Launch`). 비행이 뜨면 「착지했다」
    //      (`Command.LandDefender`)는 **끝을 아는 그쪽**이 낸다 — 비행은 프레젠테이션 시간이라
    //      코어가 길이를 모른다. 프리젠터가 없으면 그 자리에서 착지한다(헤드리스와 같다).
    //
    // ⚠ 이름이 `*Controller` 가 아닌 이유는 계약 12 다. 이 컴포넌트는 아무것도 «통제»하지
    // 않는다 — 포인터를 커맨드로 옮길 뿐이다.
    //
    // ⚠ **방향 지정 배치(facing)는 안 옮겼다.** 옛 컨트롤러의 조준 화살표는 별도 축(저작된
    // 방향 유닛)이고, 이 unit 의 질문(「배치가 도나」)에 답하는 데 필요하지 않다 — 5b
    // 「이식 제외」 표 참조. 커맨드에는 자리가 이미 있으므로 그 축이 열릴 때 여기 한 줄이다.
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-50)]
    public sealed class DragPlacementInput : MonoBehaviour
    {
        [SerializeField] private BattleDriver _driver;
        [SerializeField] private CoreDefenderTray _tray;
        [SerializeField] private CoreMapOverlay _overlay;
        [SerializeField] private Camera _boardCamera;

        [Header("손끝 → 칸")]
        [Tooltip("드래그로 승격되는 이동량(px). 이보다 작게 움직이면 탭이다.")]
        [SerializeField, Min(0f)] private float _dragThresholdPx = 16f;
        [Tooltip("판정 포인터를 손가락 위로 얼마나 띄우나(px). 엄지에 가려지는 칸을 보이게 한다.")]
        [SerializeField, Min(0f)] private float _pointerOffsetPx = 64f;
        [Tooltip("승격 이후 이만큼 더 끌면 오프셋이 최대가 된다(px). 0 = 즉시 최대.")]
        [SerializeField, Min(0f)] private float _pointerRampPx = 90f;
        [Tooltip("셀 경계 히스테리시스(칸). 0.2~0.3 권장.")]
        [SerializeField, Range(0f, 0.95f)] private float _stickMargin = 0.28f;
        [Tooltip("칸 확정 주기(초). 0 = 매 프레임 실시간.")]
        [SerializeField, Min(0f)] private float _snapIntervalSec = 0.08f;
        [Tooltip("격자 밖 이 칸 수까지는 테두리 칸에 붙인다. 더 나가면 「아무 칸도 아니다」 = 취소.")]
        [SerializeField, Min(0)] private int _outsideToleranceCells = 1;

        [Header("착지")]
        // ⚠ **자기 타이머를 갖지 않는다.** 5b 는 「드롭 → n초 뒤 착지」를 여기 한 칸으로 뒀는데,
        // 그러면 비행 길이의 주인이 둘(이 칸 · 드롭 하마 저작)이 되고 실제로 갈렸다 — 씬 값이
        // 0 이라 **유닛이 툭 생겼다**(사용자 플레이 1차). 길이도 궤적도 프리젠터의 것이다.
        [Tooltip("배치 비행 프리젠터. 비어 있으면 착지는 즉시다(헤드리스와 같다).")]
        [SerializeField] private CoreDeployFlightPresenter _deployFlight;

        [Header("드래그 실루엣")]
        // 고스트 칸과 **같은 수명**이다(옛 계약 「실루엣 수명 = hover 수명」) — 그래서 고스트를
        // 밀 때 같이 민다. 비어 있으면 고스트만 그린다.
        [Tooltip("판 위에 끄는 유닛의 그림을 세우는 프리젠터. 비어 있으면 고스트 칸만 보인다.")]
        [SerializeField] private CoreDragPreviewPresenter _dragPreview;

        private int _defIndex = -1;
        private bool _pressing;
        private bool _promoted;
        private Vector2 _pressScreen;
        private float _travelPx;
        private Vector2Int? _cell;
        private PlacementSnapDebounce.State _snap;
        private int2 _anchor;
        private bool _anchorValid;
        private bool _awaitingPlaced;
        // 비행의 **출발점**. 유닛이 실제로 있던 자리(트레이 칸)의 화면 좌표이고, `TryPlace` 가
        // 커맨드를 걸기 직전에 얼린다 — 사건이 그 값을 나르지 않기 때문이다.
        private Vector2 _launchScreen;

        public bool IsDragging => _pressing && _promoted;

        // ── 집어 든 상태(armed) ───────────────────────────────────────────────
        //
        // 트레이 칸을 **탭**하면(이동 없이 뗌) 그 유닛을 «집어 든다». 그 뒤 판을 탭하면 그 칸에
        // 놓이고, 판에서 누르고 끌면 드래그 배치로 이어진다. 옛 `defender-tap-to-place` +
        // `placement-armed-board-drag` 의 상태 전이를 그대로 옮겼다.
        //
        // ⚠ **릴리즈는 언제나 제스처를 끝낸다** — 손을 뗐는데 선택이 남아 있는 상태가 없다.
        // 그 규칙이 없으면 「놓으려다 만 유닛」이 판 밖 탭에도 계속 붙어 다닌다.
        private int _armedDefIndex = -1;
        private Vector2 _armedFromScreen;
        private bool _boardGesture;
        private bool _boardDragging;
        private Vector2 _boardDownScreen;

        /// <summary>지금 집어 든 유닛. -1 = 없음.</summary>
        public int ArmedDefIndex => _armedDefIndex;

        public bool IsArmed => _armedDefIndex >= 0;

        /// <summary>
        /// 트레이 칸 탭. **같은 칸 재탭 = 해제, 다른 칸 탭 = 갈아타기**(옛 `ToggleArm` 그대로).
        /// `fromScreen` 은 비행의 출발점이 될 그 칸의 화면 자리다.
        /// </summary>
        public void ToggleArm(int defIndex, Vector2 fromScreen)
        {
            if (_armedDefIndex == defIndex) { Disarm(); return; }
            Disarm();
            if (defIndex < 0) return;
            _armedDefIndex = defIndex;
            _armedFromScreen = fromScreen;
            // 「이 칸이 내 손에 있다」는 드래그와 **같은 사실**이라 같은 강조를 쓴다.
            if (_tray != null) _tray.DraggingDefIndex = defIndex;
        }

        public void Disarm()
        {
            if (_armedDefIndex < 0) return;
            _armedDefIndex = -1;
            _boardGesture = false;
            _boardDragging = false;
            _cell = null;
            _snap = default;
            if (_tray != null) _tray.DraggingDefIndex = -1;
            HideGhost();
            if (_dragPreview != null) _dragPreview.End();
        }

        /// <summary>
        /// 집어 든 채로 판에서 손을 뗐다. 탭이면 누른 칸 그대로(`sticky` false), 드래그면
        /// 밴드를 태운 칸이다.
        ///
        /// 판정을 여기서 다시 하지 않는다 — 칸이 나오면 **커맨드를 보내고** 거절 사유는 코어가
        /// 말한다. 칸이 아예 없으면(판 밖) 그건 거부가 아니라 **취소**라 커맨드를 안 보낸다
        /// (드래그 배치의 보드 밖 드롭과 같은 규칙).
        /// </summary>
        public bool ReleaseArmedAt(Vector2 screen, bool sticky)
        {
            int defIndex = _armedDefIndex;
            if (defIndex < 0) return false;
            var from = _armedFromScreen;

            bool resolved = TryResolveAnchor(screen, defIndex, sticky, out var anchor, out _);
            Disarm();
            return resolved && TryPlace(defIndex, anchor, from);
        }

        // ── 판 위 제스처(집어 든 뒤) ──────────────────────────────────────────

        private void BeginBoardGesture(Vector2 screen)
        {
            // 버튼 위의 누름은 버튼의 것이다 — UI 탭이 선택을 풀지 않는다.
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
            _boardGesture = true;
            _boardDragging = false;
            _boardDownScreen = screen;
            _travelPx = 0f;
            _cell = null;
            _snap = default;
        }

        private void StepBoardGesture(Vector2 screen)
        {
            _travelPx = Mathf.Max(_travelPx, Vector2.Distance(screen, _boardDownScreen));
            if (!_boardDragging && _travelPx >= _dragThresholdPx) _boardDragging = true;
            ShowArmedGhost(BoardJudgedPoint(screen), _boardDragging);
        }

        private void ReleaseBoardGesture(Vector2 screen)
        {
            bool dragged = _boardDragging;
            // ⚠ 판정 좌표를 **상태를 내리기 전에** 뽑는다 — `BoardJudgedPoint` 가 승격 여부를
            // 보기 때문이다. 먼저 내리면 드래그 릴리즈가 오프셋 없이 확정돼 스카우트가
            // 비춘 칸과 결과가 갈린다.
            var judged = BoardJudgedPoint(screen);
            _boardGesture = false;
            _boardDragging = false;
            ReleaseArmedAt(judged, dragged);
        }

        // ⚠ **오프셋은 드래그로 승격된 뒤에만** 태운다. 탭은 피드백 루프를 볼 시간 없이
        // 커밋되므로 판정 포인터가 손가락보다 위에 있으면 그 자체가 오배치로 읽힌다
        // (옛 `placement-armed-board-drag` 의 확정 규칙).
        private Vector2 BoardJudgedPoint(Vector2 screen)
        {
            if (!_boardDragging) return screen;
            float ramp = PlacementPointerOffset.Ramp(_travelPx, _dragThresholdPx, _pointerRampPx);
            return PlacementPointerOffset.Apply(screen, _pointerOffsetPx, ramp);
        }

        private void ShowArmedGhost(Vector2 screen, bool sticky)
        {
            if (_overlay == null || _armedDefIndex < 0) return;
            if (!TryResolveAnchor(screen, _armedDefIndex, sticky, out var anchor, out bool valid))
            {
                HideGhost();
                return;
            }
            _overlay.ShowPlacement(_armedDefIndex, anchor, valid);
            // 실루엣은 **드래그로 승격된 뒤에만**(옛 `:1130` — 탭·호버 경로 무변 계약).
            // `sticky` 가 곧 승격 여부다(`StepBoardGesture` 가 `_boardDragging` 을 넘긴다).
            if (_dragPreview == null) return;
            if (sticky) _dragPreview.Show(_armedDefIndex, anchor, armed: true);
            else _dragPreview.Hide();
        }

        private void OnEnable()
        {
            if (_driver != null) _driver.Subscribe(ViewOrder.Unit, OnCoreEvent);
        }

        private void OnDisable()
        {
            if (_driver != null) _driver.Unsubscribe(OnCoreEvent);
            EndDrag();
            Disarm();
        }

        private void OnCoreEvent(CoreEvent e)
        {
            // 배치가 성사된 개체의 id 는 **사건만이 안다**(receipt 는 「받아들여졌다」만 말한다).
            // 비행 뒤 「착지했다」를 알리려면 그 id 가 필요하므로 여기서 집는다.
            if (e.Kind != CoreEventKind.Placed || !_awaitingPlaced) return;
            _awaitingPlaced = false;
            // 비행이 떴으면 **착지 신호는 비행의 것**이다 — 끝을 아는 쪽이 낸다. 프리젠터가
            // 없거나(헤드리스·테스트) 날 수 없는 상황이면 지금까지처럼 그 자리에서 착지한다.
            if (_deployFlight != null && _deployFlight.Launch(e.A, _launchScreen)) return;
            _driver.Apply(Command.LandDefender(e.A));
        }

        private void Update()
        {
            if (_driver == null || !_driver.Running) return;

            var pointer = Pointer.current;
            if (pointer == null) { EndDrag(); Disarm(); return; }

            Vector2 screen = pointer.position.ReadValue();

            if (pointer.press.wasPressedThisFrame)
            {
                // 트레이 칸이 먼저다. 그 위가 아니고 집어 든 유닛이 있으면 **판 제스처**가 열린다.
                if (!TryBeginPress(screen) && IsArmed) BeginBoardGesture(screen);
            }
            else if (pointer.press.isPressed)
            {
                if (_pressing) StepDrag(screen);
                else if (_boardGesture) StepBoardGesture(screen);
            }

            if (pointer.press.wasReleasedThisFrame)
            {
                if (_pressing) Release(screen);
                else if (_boardGesture) ReleaseBoardGesture(screen);
            }

            // 집어 든 채로 손가락이 놀고 있으면 고스트가 포인터를 따라간다(마우스 호버).
            // 터치에는 호버가 없어 누르는 동안에만 갱신되지만, 그때는 위 제스처가 그린다.
            if (IsArmed && !_boardGesture) ShowArmedGhost(screen, sticky: false);
        }

        // ── 집기 ─────────────────────────────────────────────────────────────

        /// <summary>트레이 칸을 눌렀으면 true. 그 밖이면 false — 호출자가 판 제스처로 넘긴다.</summary>
        private bool TryBeginPress(Vector2 screen)
        {
            if (_tray == null) return false;
            // 버튼 위의 누름은 버튼의 것이다. 트레이 칸은 레이캐스트 대상이 아니라 여기 안 걸린다.
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return false;
            if (!_tray.TryPickSlot(screen, null, out int defIndex)) return false;

            _defIndex = defIndex;
            _pressing = true;
            _promoted = false;
            _pressScreen = screen;
            _travelPx = 0f;
            _cell = null;
            _snap = default;
            _anchorValid = false;
            return true;
        }

        // ── 끌기 ─────────────────────────────────────────────────────────────
        private void StepDrag(Vector2 screen)
        {
            _travelPx = Mathf.Max(_travelPx, Vector2.Distance(screen, _pressScreen));
            if (!_promoted)
            {
                if (_travelPx < _dragThresholdPx) return;
                _promoted = true;
                if (_tray != null) _tray.DraggingDefIndex = _defIndex;
            }

            // 판정 포인터는 손가락의 **파생값**이지 치환이 아니다 — UI 판정·임계 비교는
            // 계속 실제 좌표로 한다(`PlacementPointerOffset` 헤더의 계약).
            float ramp = PlacementPointerOffset.Ramp(_travelPx, _dragThresholdPx, _pointerRampPx);
            Vector2 judged = PlacementPointerOffset.Apply(screen, _pointerOffsetPx, ramp);

            // 카메라를 손가락 쪽으로 살짝 당긴다(옛 드래그 포커스 채널 그대로).
            var director = EnsureDirector();
            if (director != null) director.SetDragFocus(screen);

            if (!TryResolveAnchor(judged, _defIndex, sticky: true, out var anchor, out bool valid))
            {
                _anchorValid = false;
                HideGhost();
                return;
            }

            _anchor = anchor;
            _anchorValid = true;
            if (_overlay != null) _overlay.ShowPlacement(_defIndex, anchor, valid);
            if (_dragPreview != null) _dragPreview.Show(_defIndex, anchor, armed: false);
        }

        /// <summary>
        /// 화면 한 점 → **그 유닛을 세울 앵커**와 그 자리의 유효성. 드래그와 armed 보드 제스처가
        /// **같은 함수**를 쓴다 — 둘이 각자 손끝 규약을 가지면 같은 손동작이 한쪽은 배치,
        /// 한쪽은 취소가 된다.
        ///
        /// ⚠ **보정(자석)이 없다**(사용자 결정 2026-09-23). 예전에는 못 놓는 칸이면 둘레에서
        /// 합격 앵커를 찾아 **거기로 옮겼는데**, 불가 칸이 겹친 자리나 포인터 미세 이동에서
        /// 손끝과 결과가 크게 갈렸다 — 화면이 「여기 놓인다」고 말한 적이 없는 칸에 유닛이 섰다.
        /// 지금은 손끝이 가리킨 칸이 곧 결과이고, 못 놓는 칸이면 **고스트가 빨강으로 그 자리에
        /// 머물고** 드롭은 거절된다.
        ///
        /// `sticky` = 칸 경계 히스테리시스·시간 스로틀을 태운다(드래그는 켜고, 탭은 끈다 —
        /// 탭은 피드백 루프를 볼 시간 없이 커밋되므로 밴드가 오히려 누른 칸을 배신한다).
        /// </summary>
        public bool TryResolveAnchor(Vector2 screen, int defIndex, bool sticky,
                                     out int2 anchor, out bool valid)
        {
            anchor = default;
            valid = false;
            if (_driver == null || !_driver.Running) return false;

            var def = _driver.Definition;
            if (defIndex < 0 || defIndex >= def.Units.Length) return false;
            if (!TryResolveCell(screen, sticky, out var cell)) return false;
            if (sticky) _cell = cell;

            var size = new Vector2Int(math.max(1, def.Units[defIndex].FootprintWidth),
                                      math.max(1, def.Units[defIndex].FootprintHeight));
            // 손끝 규약: 손가락 칸 = footprint **하단 행의 가로 중앙**. 유닛은 손가락 위로 자란다.
            var anchorV = FootprintMath.AnchorFromBottomCenter(cell, size);
            anchor = new int2(anchorV.x, anchorV.y);
            var placement = _driver.Match.Placement;
            valid = placement.Judge(defIndex, anchor) == RejectReason.None;
            return true;
        }

        // 화면 좌표 → 칸. 「아무 칸도 아니다」(격자 밖 관용 초과)는 **null** 이고 그것이
        // 「보드 밖 = 취소」를 성립시킨다 — 무조건 격자로 clamp 하면 그 경로가 도달 불가해진다.
        private bool TryResolveCell(Vector2 screen, bool sticky, out Vector2Int cell)
        {
            cell = default;
            if (!BoardSpace.IsConfigured) return false;
            var cam = EnsureCamera();
            if (cam == null) return false;

            var ray = cam.ScreenPointToRay(screen);
            var plane = BoardSpace.RaycastPlane();
            if (!plane.Raycast(ray, out float enter)) return false;

            float3 sim = BoardSpace.ToSim(ray.GetPoint(enter));
            float ts = _driver.TileSize;
            // sim 원점은 무조건 zero 다(맵 계약). 셀 중심이 정수이므로 그대로 칸 좌표가 된다.
            var frac = new Vector2(sim.x / ts, sim.z / ts);

            var grid = _driver.GridSize;
            // 격자 밖 관용은 **두 경로가 같이 쓴다** — armed 탭만 관용 0 이면 같은 손동작이
            // 한쪽은 배치, 한쪽은 취소가 된다(옛 `TryResolveArmedCell` 헤더의 계약).
            var resolved = PlacementCellSnap.Resolve(sticky ? _cell : null, frac,
                                                     sticky ? _stickMargin : 0f,
                                                     new Vector2Int(grid.x, grid.y),
                                                     _outsideToleranceCells);
            if (!resolved.HasValue) return false;

            if (!sticky) { cell = resolved.Value; return true; }

            // 공간 히스테리시스 **위에** 시간 스로틀. 둘을 겹치는 이유: 공간만으로는 경계를
            // 넘는 순간 즉시 따라가 이동 중에 칸이 휙휙 바뀌고, 시간만으로는 정지 중에도
            // 경계 지터가 그대로 들어온다.
            var committed = _cell ?? resolved.Value;
            cell = PlacementSnapDebounce.Step(ref _snap, committed, resolved.Value,
                                              Time.unscaledDeltaTime, _snapIntervalSec);
            return true;
        }

        // ── 놓기 ─────────────────────────────────────────────────────────────
        private void Release(Vector2 screen)
        {
            bool promoted = _promoted;
            int defIndex = _defIndex;
            bool hasAnchor = _anchorValid;
            var anchor = _anchor;
            // 비행의 출발점은 **집었던 자리**다(옛 컨트롤러의 `fromScreen` 그대로) — 실루엣
            // 모드에는 손끝에 유닛이 없어서, 유닛이 실제로 있던 곳은 트레이다.
            var from = _pressScreen;
            EndDrag();

            if (defIndex < 0) return;

            // 이동 없이 뗐다 = **탭**. 놓는 것이 아니라 «집어 드는» 것이다 — 그 뒤 판을 탭하면
            // 거기 놓이고, 판에서 끌면 드래그 배치로 이어진다.
            if (!promoted) { ToggleArm(defIndex, from); return; }

            // 보드 밖 드롭 = 취소. **커맨드를 안 보낸다**(그래서 코스트도 안 나간다).
            if (!hasAnchor) return;
            TryPlace(defIndex, anchor, from);
        }

        /// <summary>
        /// **배치를 거는 단 하나의 자리.** 드래그 릴리즈와 armed 탭이 여기로 합류한다 —
        /// 둘이 각자 커맨드를 보내면 비행·거절 표시 중 하나가 한쪽에만 붙는다.
        ///
        /// **판정은 여기 없다.** 코스트도 소진도 쿨타임도 묻지 않고 그냥 보낸다 — 미리
        /// 거르면 그 거름이 두 번째 자가 되고, 옛 컨트롤러가 그래서 갈렸다.
        ///
        /// `fromScreen` = 비행의 출발점(유닛이 실제로 있던 트레이 칸의 화면 자리).
        /// </summary>
        public bool TryPlace(int defIndex, int2 anchor, Vector2 fromScreen)
        {
            if (_driver == null || !_driver.Running) return false;

            _launchScreen = fromScreen;

            // ⚠ **표식을 `Apply` 앞에서 켠다.** `BattleDriver.Apply` 는 커맨드를 건 **그 호출
            // 안에서** 사건을 배달한다(계약 7 — 커맨드는 동기 + receipt). 뒤에서 켜면 이번
            // 배치의 `Placed` 는 표식이 꺼진 채 지나가고, 다음 배치가 직전에 남은 표식을
            // 주워 쓴다 — **한 판 늦은 걸쇠**다. 증상은 「첫 유닛만 안 난다」였다.
            //
            // 창은 이 호출 하나다: 배달이 끝난 뒤 반드시 내린다. 안 내리면 디버그·테스트가
            // 건 배치까지 이 입력이 자기 것으로 착각해 남의 출발점에서 날려 보낸다.
            _awaitingPlaced = true;
            var receipt = _driver.Apply(Command.PlaceDefender(defIndex, anchor));
            _awaitingPlaced = false;

            if (receipt.Accepted) return true;
            if (_tray != null) _tray.ShowReject(receipt.Reason);
            return false;
        }

        private void EndDrag()
        {
            _pressing = false;
            _promoted = false;
            _defIndex = -1;
            _cell = null;
            _anchorValid = false;
            if (_tray != null) _tray.DraggingDefIndex = -1;
            HideGhost();
            if (_dragPreview != null) _dragPreview.End();
        }

        private void HideGhost()
        {
            if (_overlay != null) _overlay.HidePlacement();
            if (_dragPreview != null) _dragPreview.Hide();
        }

        private Camera EnsureCamera()
        {
            if (_boardCamera != null && _boardCamera.isActiveAndEnabled) return _boardCamera;
            _boardCamera = Camera.main;
            return _boardCamera;
        }

        private CameraDirector _director;
        private bool _directorMissed;

        private CameraDirector EnsureDirector()
        {
            if (_director != null) return _director;
            if (_directorMissed) return null;
            var cam = EnsureCamera();
            _director = cam != null ? cam.GetComponent<CameraDirector>() : null;
            if (_director == null) _directorMissed = true;   // 한 번만 찾는다 — 매 프레임 조회 금지
            return _director;
        }
    }
}
