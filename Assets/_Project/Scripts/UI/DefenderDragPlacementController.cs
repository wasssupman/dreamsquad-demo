using System.Collections;
using Spine.Unity;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using Wassup.Bridge;
using Wassup.Core;
using Wassup.Core.TimeControl;
using Wassup.Data;
using Wassup.Presentation;
using Wassup.Rendering;

namespace Wassup.UI
{
    public class DefenderDragPlacementController : MonoBehaviour
    {
        public event System.Action<DefenderUnitData> Armed;
        public event System.Action Disarmed;
        public event System.Action<DefenderUnitData> PlacementCommitted;
        // first-session-tutorial — physical slot D&D only. Tap-to-place's
        // simulated flight does not fire this, so guidance can use distinct copy.
        public event System.Action UserDragStarted;
        // gimmick-match-integration unit 5 — 배치 드래그 세션이 실제로 시작될 때(가드 통과 후) 발화.
        // 기믹 안내 카드가 "첫 배치 상호작용" 접힘 트리거로 구독한다. arm 경로는 Armed 가 담당.
        public event System.Action DragBegan;

        [SerializeField] private BattleBridge bridge;
        [SerializeField] private Camera mainCamera;
        [SerializeField] private PlacementInput placementInput;
        [SerializeField] private float previewHeight = 0.35f;
        [SerializeField] private float previewScale = 0.65f;
        // time-manager Unit 5 — 드래그 배치 중 전투만 이 배율로 느려진다. 드래그 프리뷰/입력은
        // Interaction 도메인(unscaledDeltaTime)이라 실시간 유지된다. 0=정지, 1=영향 없음.
        [SerializeField, Range(0f, 1f)] private float dragSlowmoScale = 0.2f;

        // 드래그 프리뷰 키링 튜닝값은 DragSwaySettings SO 에서 온다. 컨트롤러가 런타임 AddComponent 라
        // 인스펙터 튜닝이 안 되므로 SO 로 분리 — DefenderSelector 에 할당하면 Configure 로 주입. 미주입 시 기본값.
        private DragSwaySettings _cfg;
        private DragSwaySettings Cfg => _cfg != null ? _cfg : (_cfg = ScriptableObject.CreateInstance<DragSwaySettings>());

        private const int RingSegments = 14;
        // 거부 라벨 레이아웃 구조 상수(게임플레이 수치 아님 — SO 로 빼지 않는다).
        // Rise = 포인터 위로 띄우는 양, TopMargin = 화면 상단 클램프 여유(라벨 rect 높이 40 의 절반 + 패딩).
        private const float RejectLabelRise = 96f;
        private const float RejectLabelTopMargin = 32f;

        // defender-deploy-cutscene unit 3 — 드래그 시작 시 좌하단 컷신 재생기(옵셔널 주입).
        // null 이면 컷신 없이 기존 흐름. 컷신은 자동 종료하되 배치 성공 시 즉시 강제 초기화한다.
        private DeployCutscenePlayer _cutscenePlayer;

        // defender-directional-volley unit 6 — 방향 지정 유닛의 두 번째 배치 페이즈.
        // 이 컨트롤러 자체가 런타임 AddComponent 라 씬 배선이 없으므로 같은 방식으로 붙인다.
        private DirectionAimController _aimController;

        private DragSession _session;
        private TimeLease _slowmoLease; // time-manager Unit 5 — 드래그 중 Battle 슬로우모 lease
        private Material _previewMaterial; // 폴백 capsule 용
        private Material _cordMaterial;    // 줄/고리 공유(세션마다 생성 금지)
        // action-tray unit 4 — 드래그 중 거부 사유 라벨(포인터 추종 오버레이).
        // 색만으로 알리지 않는다: 사유별 글리프+한글 label+색 3중 표기.
        private TMP_FontAsset _uiFont;
        private GameObject _rejectCanvasGO;
        private TextMeshProUGUI _rejectLabel;
        // drag-cancel-affordance unit 0 — 취소 존(= 트레이 패널 rect). DefenderSelector 가 패널을
        // 만든 직후 주입한다(컨트롤러는 런타임 AddComponent 라 씬 배선이 없다). null 이면 취소 존
        // 비활성 = 기존 동작 그대로(컨트롤러만 띄우는 테스트 하네스 경로).
        private RectTransform _cancelZone;
        private bool _cancelHover;      // 이번 프레임 가상 포인터가 취소 존 안인가
        private bool _cancelZoneLeft;   // 이 세션이 취소 존을 한 번 벗어난 적 있는가(예고 룩 게이트)
        private float _cancelDwell;     // 존을 벗어나기 전 머문 시간(초, unscaled) — 게이트의 두 번째 문
        private bool _cancelVisualOn;   // 예고 룩(고스트 알파) 적용 상태
        // unit 3 — 격자 밖 관용 초과로 "아무 칸도 아님". 취소 예고(고스트 + 포인터 라벨)의 두 번째 사유.
        private bool _noCell;

        // 이번 프레임 "이대로 놓으면 취소" 인가 — **사유 무관**(트레이 존 복귀 / 격자 밖 관용 초과).
        // 릴리즈 취소는 이 술어를 보지 않는다: 트레이 존은 `_cancelHover` 를 직접 보고, 칸 없음은
        // hoverTile 이 비어 EndDrag 꼬리로 떨어진다. 게이트는 **예고에만** 걸린다.
        private bool CancelStateNow => _cancelHover || _noCell;

        // 예고(고스트 알파 + 보드 침묵 + 라벨)를 켤 것인가. 두 문 중 하나를 통과해야 한다:
        //
        //  (a) 취소 존을 한 번 벗어난 뒤의 **존 재진입** = 의도적 복귀 → 즉시 켠다.
        //  (b) 그 외 → dwell(cancelHintDwellSeconds)을 넘겨야 켠다.
        //
        // (b) 가 두 종류의 깜빡임을 함께 막는다. 하나는 **드래그 시작 구간** — 트레이 드래그는
        // 취소 존 안에서 시작하므로(슬롯 위 + 오프셋이 아직 트레이 대역) 게이트가 없으면 모든
        // 드래그가 취소 예고로 시작한다. 다른 하나는 **격자 밖 오버슛** — 가장자리 열을 노리며
        // 좌우로 흔들면 관용 링을 순간 넘었다 돌아오는데, 그때마다 Spine 실루엣 알파가 1↔0.4 로
        // 튀고 라벨이 껌뻑인다(맵 무관). `_noCell` 에 시간 히스테리시스가 없던 것이 원인이었다.
        //
        // 반대로 (a) 가 없으면 "트레이로 되돌리기" 라는 **의도적** 제스처가 매번 dwell 을 기다린다.
        private bool CancelArmed =>
            CancelStateNow &&
            ((_cancelHover && _cancelZoneLeft) || _cancelDwell >= Cfg.cancelHintDwellSeconds);

        // 취소 예고 문구 — 빠른 템포에서 유일한 불안이 "코스트 날아갔나" 라 무차감을 문자로 못박는다.
        // 거부 라벨(`X 코스트 부족`)과 같은 글리프+문구 문법(색 단독 표기 금지).
        private const string CancelLabelText = "✕  놓으면 취소 · 코스트 유지";
        // placement-thumb-occlusion unit 0 — 두 축을 이름으로 가른다. _lastAimScreenPos 는 **가상
        // 포인터**(셀 판정·고리·줄·고스트·거부 라벨 앵커), _lastRawScreenPos 는 실제 포인터다.
        // 가르는 이유: 카메라 포커스는 스크린좌표를 NDC 로 **절대 변환**하므로
        // (CameraDirector.SetDragFocus) 가상을 먹이면 상수 바이어스가 실려 카메라가 프레임을 당기고,
        // 보드가 화면상 내려가 손가락↔칸 간격을 되돌린다. 포커스는 "어디를 보나" 채널이라 raw 가 맞다.
        private Vector2 _lastAimScreenPos;
        private Vector2 _lastRawScreenPos;

        // depth-parallax unit 7 — 스와이프→틸트 피드 상태. _prevScreenPos = 직전 프레임 포인터(속도 델타용),
        // _swipeVelSmoothed = exp-lerp 스무딩 스와이프 속도, _tiltGain = 유닛별 게인(BeginDrag 에서 주입, 컨트롤러 단독 소유).
        private Vector2 _prevScreenPos, _swipeVelSmoothed;
        private float _tiltGain = 1f;

        // 키링 배치 상태: 고리 = 손가락(공중). 유닛 = 보드에 서서 무게추처럼 지연 추종.
        private Vector3 _ringWorld;        // 고리(손가락, 공중)
        private Vector3 _unitTargetWorld;  // 유닛 발 추종 목표(프리뷰 전용). 실제 drag=포인터 발점, tap=곡선 발점.
        // 셀 판정 기준점 = 보드 평면 위 **손가락 직접 히트**. _unitTargetWorld 와 분리돼 있다:
        // 발점은 totalDrop(유닛 키 + 줄×visualScale)만큼 화면 아래라, 그걸로 칸을 정하면 손가락이 화면
        // 최상단에 닿아도 보드 상단 N행에 도달할 수 없다(실측 15×11 맵에서 3행 영구 배치 불가 + 화면
        // 하단 절반이 전부 row 0 에 뭉침). 프리뷰는 계속 매달린 발점을 쓰고, 판정만 손가락을 따른다 —
        // 같은 파일의 armed 보드 드래그(UpdateBoardScout/CommitBoardDrag)와 재배치 컨트롤러가 이미
        // 쓰는 bridge.TryScreenToCell 과 같은 기준이다(프로젝트 단일 계약).
        private Vector3 _fingerBoardWorld;
        private PlacementSnapDebounce.State _debounce; // unit 3: throttle 경과 상태(hoverTile 과 수명 동일)
        // defender-tap-to-place — arm(탭 선택) 상태(단일). 보드 탭 시 이 유닛을 슬롯에서 시뮬 배치.
        private DefenderDragSlot _armedSlot;
        private DefenderUnitData _armedUnit;
        private Vector2 _armedFromScreen;
        // defender-tap-to-place unit 4 — 탭 비행 중 고정할 선택 타일(발밑 추종 대신). unit 5 — 곡선 좌우 변주 인덱스(결정론).
        // placement-armed-board-drag unit 0 — armed 유닛의 보드 프레스-드래그-릴리즈 제스처 상태.
        // press 가 보드에서 시작(가드 통과)되면 active, 이동 임계 초과 시 dragging 승격. release 에서
        // dragging=커밋(시뮬 비행) / 아니면 탭(범위 피크는 unit 2). 시간 delta 로 판정하지 않는다(이동량만).
        private bool _boardGestureActive;
        private bool _boardDragging;
        private Vector2 _boardDownScreen;
        // placement-armed-board-drag unit 1 — 스카우트가 현재 표시 중인 셀(변경 감지·소거용). 세션 없는 range-only 경로.
        private Vector2Int? _boardScoutCell;
        private Vector2Int? _boardScoutAnchor; // defender-footprint unit 2 — 스카우트 앵커(범위 재페인트 판정)

        // ── defender-footprint unit 2 — footprint 고스트(4색) + 주변 배치불가 컨텍스트 ──
        // 한 번의 GetPlacementCellReasons(anchor−r, size+2r) 스캔에서: footprint 칸 = 하늘/빨강,
        // 컨텍스트 칸 = 점유 노랑 / 지형 무채색, None·맵밖 = 무표시(배치 불가 위주 — 결정 3).
        // 비공간 사유(코스트·상한)로 전체 무효면 footprint 전 칸 빨강 — «성공으로 보였는데 실패» 금지.
        private readonly System.Collections.Generic.List<FootprintCellReason> _ghostAreaReasons = new();
        private readonly System.Collections.Generic.List<Vector2Int> _ghostPaintCells = new();
        private readonly System.Collections.Generic.List<Color> _ghostPaintColors = new();
        private readonly System.Collections.Generic.List<Vector2Int> _ghostLastCells = new();
        private readonly System.Collections.Generic.List<Color> _ghostLastColors = new();
        private bool _ghostShown;

        // 자석은 위치를 바꾸면 풀리는 사유에만 발동한다(비용·상한은 어디로 가도 같다).
        private static bool IsSpatialReason(PlacementRejectReason reason)
            => reason == PlacementRejectReason.Occupied
               || reason == PlacementRejectReason.NotBuildable
               || reason == PlacementRejectReason.OutOfBounds;

        private void UpdateGhost(Vector2Int anchor, bool valid, DefenderUnitData unit)
        {
            if (bridge == null || unit == null) return;
            var size = unit.Footprint;
            int r = Mathf.Max(0, Cfg.ghostContextRadiusCells);
            bridge.GetPlacementCellReasons(anchor - new Vector2Int(r, r),
                size + new Vector2Int(r * 2, r * 2), unit, _ghostAreaReasons);
            var footRect = FootprintMath.Cells(anchor, size);

            bool anyCellFail = false;
            for (int i = 0; i < _ghostAreaReasons.Count; i++)
            {
                var e = _ghostAreaReasons[i];
                if (footRect.Contains(e.cell) && e.reason != PlacementRejectReason.None) { anyCellFail = true; break; }
            }
            bool nonSpatialInvalid = !valid && !anyCellFail;

            _ghostPaintCells.Clear();
            _ghostPaintColors.Clear();
            for (int i = 0; i < _ghostAreaReasons.Count; i++)
            {
                var e = _ghostAreaReasons[i];
                if (footRect.Contains(e.cell))
                {
                    _ghostPaintCells.Add(e.cell);
                    _ghostPaintColors.Add(e.reason != PlacementRejectReason.None || nonSpatialInvalid
                        ? Cfg.ghostInvalidColor : Cfg.ghostValidColor);
                }
                else if (e.reason == PlacementRejectReason.Occupied)
                {
                    _ghostPaintCells.Add(e.cell);
                    _ghostPaintColors.Add(Cfg.ghostOccupiedColor);
                }
                else if (e.reason == PlacementRejectReason.NotBuildable)
                {
                    _ghostPaintCells.Add(e.cell);
                    _ghostPaintColors.Add(Cfg.ghostTerrainColor);
                }
            }

            // 페인트는 변경시에만(SetTile 스팸 방지) — 매 프레임 재계산은 값싸고, 페인트는 diff.
            if (_ghostShown && ListsEqual(_ghostPaintCells, _ghostLastCells, _ghostPaintColors, _ghostLastColors))
                return;
            bridge.SetPlacementGhostCells(_ghostPaintCells, _ghostPaintColors);
            _ghostShown = true;
            _ghostLastCells.Clear(); _ghostLastCells.AddRange(_ghostPaintCells);
            _ghostLastColors.Clear(); _ghostLastColors.AddRange(_ghostPaintColors);
        }

        private static bool ListsEqual(
            System.Collections.Generic.List<Vector2Int> a, System.Collections.Generic.List<Vector2Int> b,
            System.Collections.Generic.List<Color> ca, System.Collections.Generic.List<Color> cb)
        {
            if (a.Count != b.Count || ca.Count != cb.Count) return false;
            for (int i = 0; i < a.Count; i++) if (a[i] != b[i]) return false;
            for (int i = 0; i < ca.Count; i++) if (ca[i] != cb[i]) return false;
            return true;
        }

        private void ClearGhost()
        {
            if (!_ghostShown) return;
            _ghostShown = false;
            _ghostLastCells.Clear();
            _ghostLastColors.Clear();
            bridge?.ClearPlacementGhostCells();
        }

        // placement-armed-board-drag unit 2 — 유효셀 탭: 배치 비행과 병렬로 착지 셀에 범위를 유지(비행 clear 를
        // 덮어씀). 배치(착지)되면 소거. 자기 flight 의 Disarm/ResetBoardGesture 에 취소되면 안 돼 별도 소유.
        private Coroutine _tapPlaceRangeRoutine;
        // review fix — 세션 세대 토큰. CleanupSession 마다 증가. 시뮬 코루틴이 자기 세대를 캡처해
        // 비행 중 새 드래그(BeginDrag→CleanupSession→새 세션)가 시작되면 즉시 물러난다(세션 하이재킹 방지).
        private int _sessionGen;
        private Vector3 _unitPosWorld;     // 유닛 발 실제(지연 추종)
        private Vector3 _unitVelWorld;
        private bool _posInit;
        private bool _onBoard;

        private struct DragSession
        {
            public bool active;
            public DefenderUnitData unit;
            public GameObject preview;      // root(scale 1). 자식이 고리/줄/실루엣.
            public LineRenderer cordLine;
            public Transform ring;
            public Transform endNode;       // 빌보드. 유닛 머리 위치.
            public Transform swingPivot;    // 머리 중심 기울임.
            public Transform spineChild;
            // drag-cancel-affordance unit 0 — 취소 예고 고스트 알파용 핸들. 폴백 capsule 프리뷰면 null.
            public SkeletonAnimation skeleton;
            public float visualScale;
            public float unitHeight;        // 실루엣 월드 높이(발→머리). 머리 오프셋용.
            public Vector2Int? hoverTile;
            // defender-footprint unit 2 — 확정될 footprint 앵커(하단 중앙 산식 + 자석 결과).
            // hoverTile(손가락 셀)이 히스테리시스의 진실원이고 이 값은 그 순수 파생이다.
            // 커밋은 이 값을 쓴다 — 고스트가 보여준 곳과 확정 위치가 같아야 한다(표시=확정).
            public Vector2Int? anchorTile;
            public bool isValidTile;
            // action-tray unit 4 — 마지막 hover 판정의 거부 사유(유효 칸이면 None).
            public PlacementRejectReason rejectReason;
        }

        // unit-dreamcatcher-inspect unit 0 — 배치 드래그 중임을 프레젠테이션이 관측하기 위한
        // 읽기 seam. DcInspectController 가 드래그 중 유닛 탭 인스펙트를 양보하는 데 쓴다.
        // 이 컨트롤러는 GameManager.IsAiming 을 쓰지 않고 Battle 페이즈에서도 활성이라
        // (DefenderSelector 가 Battle 에서 슬림 리사이즈만 함) 다른 관측 수단이 없다.
        // 새 상태를 만들지 않고 기존 _session.active 를 그대로 읽는다 — 진실 소스는 하나.
        public bool IsDragging => _session.active;

        // defender-directional-volley unit 6 — 방향 지정 페이즈 진행 중. 드래그 세션은 이미
        // 끝났지만(드롭 완료) 화면은 여전히 배치 조작 중이다 — 보드 탭을 소비하는 다른
        // 컨트롤러(DcInspect 등)가 이 스와이프를 자기 제스처로 오해하지 않게 알린다.
        public bool IsAiming => _aimController != null && _aimController.IsActive;

        // placement-armed-board-drag unit 0 — arm 된 동안 보드 press 는 배치 제스처가 단독 소유한다.
        // DcInspectController 가 같은 press 를 인스펙트로 이중 소비하지 않게 양보하는 읽기 seam
        // (계약 11 aim-mode race 재생산 방지). arm 은 직전 프레임에 확정돼 press 프레임 실행순서와 무관.
        public bool HasArmedUnit => _armedUnit != null;

        // defender-relocation unit 1 — 재배치 컨트롤러가 같은 튜닝 소스를 공유하는 읽기 seam.
        // 슬로모 스케일(계약 7: 기존 드래그와 동일 소스)과 탭/드래그 판정 임계(제스처 일관).
        public float DragSlowmoScale => dragSlowmoScale;
        public float BoardDragThreshold => Mathf.Max(1f, Cfg.boardDragThreshold);

        // placement-thumb-occlusion unit 0 — 같은 튜닝 소스를 공유하는 읽기 seam(위 둘과 동형).
        // 소비처: 재배치 컨트롤러(unit 1) · PlayMode 테스트(구동 좌표 보정).
        public float PlacementPointerOffsetPx => Cfg.PlacementPointerOffsetPx;
        public float PlacementPointerOffsetRampDistance => Cfg.placementPointerOffsetRampDistance;

        // 배치 판정 포인터. 계약은 PlacementPointerOffset.Apply 가 소유한다(중복 서술 금지).
        // **ramp 는 인자다 — 필드로 들지 않는다.** 필드로 두면 보드 제스처가 굴린 mid-flight 램프(예 0.7)가
        // 릴리즈→CommitBoardDrag→SimulateDragTo→BeginDrag 로 새어, 무관한 트레이 경로가 그 값을 물려받는다.
        // 그걸 막으려고 BeginDrag 에서 1f 로 덮는 순서 의존이 생겼던 것 — 인자화가 함정 자체를 없앤다.
        private Vector2 ToPlacementPointer(Vector2 rawScreen, float ramp01)
            => PlacementPointerOffset.Apply(rawScreen, Cfg.PlacementPointerOffsetPx, ramp01);

        public void Configure(BattleBridge battleBridge, Camera camera, PlacementInput input,
            DragSwaySettings swaySettings = null, TMP_FontAsset uiFont = null,
            DeployCutscenePlayer cutscenePlayer = null)
        {
            bridge = battleBridge;
            mainCamera = camera != null ? camera : Camera.main;
            placementInput = input;
            if (swaySettings != null) _cfg = swaySettings;
            if (uiFont != null) _uiFont = uiFont;
            if (cutscenePlayer != null) _cutscenePlayer = cutscenePlayer;

            // unit 6 — 방향 페이즈 컨트롤러. 드롭 성공 시 핸드오프(CommitPlacementAt).
            // 튜닝값(slowmoScale)은 드래그와 공유하는 DragSwaySettings 로 주입 — 전용 SO 폐기.
            if (_aimController == null)
                _aimController = gameObject.GetComponent<DirectionAimController>()
                                 ?? gameObject.AddComponent<DirectionAimController>();
            _aimController.Configure(bridge, mainCamera, swaySettings);
        }

        // drag-cancel-affordance unit 0 — 취소 존 주입(트레이 패널 rect). DefenderSelector 가 패널을
        // 만든 뒤 호출한다. 판정에 상수 오프셋을 더하지 않고 이 rect 하나만 읽는다(계약 2) — 트레이가
        // 페이즈별로 크기를 바꿔도(placement/battle) 취소 영역이 보이는 트레이를 그대로 따라간다.
        public void SetCancelZone(RectTransform zone) => _cancelZone = zone;

        public void BeginDrag(DefenderUnitData unitData, Vector2 screenPosition, bool simulated = false)
        {
            if (unitData == null || bridge == null) return;
            // defender-directional-volley unit 6 — 방향 지정 중엔 트레이가 잠긴다. 허용하면
            // 그 드래그 제스처가 조준 스와이프로도 해석되고(두 곳에서 소비), 앞 유닛이
            // 기본 방향으로 강제 활성화된다. 방향을 정해야 다음 배치로 넘어간다(계약 9).
            if (_aimController != null && _aimController.IsActive) return;
            DragBegan?.Invoke(); // unit 5 — 가드 통과 = 세션 확정. 이 아래론 early-return 없음.
            // placement-armed-board-drag unit 2 — 새 트레이 드래그(실드래그)는 직전 탭 배치의 range flourish 를 정지.
            // 시뮬 경로(탭/드래그 릴리즈의 자기 flight)는 자기 flourish 를 죽이면 안 되므로 제외.
            if (!simulated) CancelTapPlaceRangePeek();
            // Tap-to-place의 보드 탭은 내부적으로 simulated drag를 쓰지만 사용자
            // 선택 취소가 아니다. Disarmed를 내보내면 튜토리얼 문구가 Pick으로 되감긴다.
            Disarm(notify: !simulated);
            CleanupSession();
            // defender-deploy-cutscene unit 8 review — 직전 실패/취소 컷씬이 자동 퇴장 중이어도
            // 새 배치 세션에는 이전 유닛 연출을 넘기지 않는다. 프레임 없는 유닛도 동일하게 원복.
            if (_cutscenePlayer != null) _cutscenePlayer.ForceStopAndReset();
            if (!simulated) UserDragStarted?.Invoke();
            // time-manager Unit 5 — 드래그 시작 시 전투만 슬로우모. 드롭/취소 시 CleanupSession 에서 해제.
            _slowmoLease = TimeManager.Instance.Request(TimeDomain.Battle, dragSlowmoScale);
            if (mainCamera == null) mainCamera = Camera.main;

            _session = BuildSession(unitData);
            // defender-deploy-cutscene unit 3/8 — 프레임이 있으면 좌하단 컷신 1회 재생(자동 종료).
            // 기능 온/오프는 DragSwaySettings.enableDeployCutscene 로 게이트.
            //
            // drop-dismount unit 7 — **시뮬 경로는 제외한다.** 탭 배치는 세션이 한 프레임이라
            // CommitPlacementAt 의 ForceStopAndReset 이 같은 프레임에 컷신을 죽인다 — 켜 두면
            // 좌하단이 1프레임 번쩍일 뿐이다. 컷신은 «들고 있는 동안» 의 연출이고 탭에는 그 구간이 없다.
            if (!simulated && Cfg.enableDeployCutscene && _cutscenePlayer != null &&
                unitData.deployCutsceneFrames != null && unitData.deployCutsceneFrames.Length > 0)
            {
                // depth-parallax unit 7 — 색+뎁스 lockstep 재생(5-arg). 뎁스 미할당이면 null 전달=색만(패럴랙스 없음).
                _cutscenePlayer.Play(unitData.deployCutsceneFrames, unitData.deployCutsceneDepth,
                    unitData.deployCutsceneFps, unitData.deployCutsceneScale, unitData.deployCutsceneOffset);
                // 유닛별 틸트 게인은 Play 로 넘기지 않고 컨트롤러가 보관 → 스와이프 블록에서 곱함(게인 단독 소유).
                _tiltGain = unitData.deployCutsceneTiltGain;
            }
            // review fix — 스와이프→틸트 블록은 컷신 여부와 무관하게 매 프레임 돌므로, seed 를 컷신 분기 밖에서
            // 무조건 수행(비컷신/시뮬 드래그의 첫 프레임 stale-prev 속도 스파이크 방지).
            // placement-thumb-occlusion unit 0 — seed 도 **가상** 좌표여야 한다. 아래 rawVel 은
            // (_lastAimScreenPos - _prevScreenPos) 인데 한쪽만 가상이면 첫 프레임에 offset/dt 스파이크가
            // 실려 컷신 틸트가 튄다(위 주석이 경고하는 그 버그와 동종).
            // 트레이 D&D 는 램프 없이 첫 프레임부터 풀 오프셋 — 직전 하이라이트가 없어 부드럽게 할 대상이 없다.
            _prevScreenPos = ToPlacementPointer(screenPosition, 1f);
            _swipeVelSmoothed = Vector2.zero;
            bridge?.SetEnemiesDimmed(true); // placement-enemy-see-through — 적 반투명 on
            bridge?.SetPlacementHighlightAboveUnits(true); // unit 6 — 배치 하이라이트를 적 위로
            if (placementInput != null) placementInput.SetClickPlacementEnabled(false);
            UpdateDrag(screenPosition);
        }

        // placement-eligible-tile-highlight unit 2 — 배치 판단 상태(실드래그 또는 탭 arm) 파생 → 하이라이트 토글.
        // 지점마다 show/hide 산탄 대신 원하는 상태를 파생해 idempotent 호출. 이 파생이 BeginDrag 의
        // Disarm→재Show 순서의존·_sessionGen 하이재킹을 무관하게 만든다.
        private bool _placeableHlDesired;
        private DefenderUnitData _placeableHlUnit;   // placement-mask unit 4 — 마지막으로 게시한 하이라이트 유닛(층 축)

        // defender-footprint unit 2 — 배치가능 **전체** 하이라이트 은퇴(2026-08-28 사용자 결정 —
        // 요구 문서 4절 «평상시 전체 하이라이트 없음»을 문자대로). 표시 축은 고스트 4색(불가 위주)이
        // 대신한다. 스위치로만 껐으므로 되켜면 그대로 복원된다(selection-entry-narrowing 관용).
        // 재배치 이동모드(DefenderRelocationController)도 이 스위치를 공유한다.
        internal const bool PlaceableAreaHighlightEnabled = false;

        private void UpdatePlacementHighlightState()
        {
            if (bridge == null) return;
            // drag-cancel-affordance unit 0 — 취소 예고 중이면 배치 하이라이트도 끈다. "보드에서
            // 아무 일도 일어나지 않는다" 를 한 덩어리로 보여야 한다(계약 4). CancelArmed 가 두 사유
            // (트레이 존 / 칸 없음)를 함께 덮으므로 사유별로 화면이 갈리지 않는다(계약 6).
            bool desired = PlaceableAreaHighlightEnabled
                           && ((_session.active && !CancelArmed) || _armedUnit != null);
            // placement-mask unit 4 — 하이라이트는 드는 유닛의 배치 층 기준(드래그 세션 우선, 없으면 탭 arm).
            var unit = desired ? (_session.active ? _session.unit : _armedUnit) : null;
            // 래치에 **유닛과 실제 표시 상태**를 포함한다. bool 하나만 래치하면 desired 가 true 로 유지된 채
            // 유닛만 바뀌는 전이(탭 arm 갈아타기 ToggleArm=Disarm→재arm, arm 중 다른 유닛 BeginDrag)에서
            // 재호출이 스킵돼 **이전 유닛의 층**이 계속 그려진다 — 판정은 새 유닛 층을 쓰므로
            // "빛나는데 거부 / 어두운데 성공" 이 된다(판정↔하이라이트 술어 공유 계약 파손).
            // 표시 상태까지 보는 이유: 재배치 컨트롤러가 Hide 를 쏜 뒤에도 여기서 자기치유 재게시된다.
            //
            // ⚠ 자기치유는 **켜는 방향만**이다. 하이라이트는 이 컨트롤러와 재배치 컨트롤러가
            // 공유하는 전역 상태인데 **매 프레임 도는 건 이쪽뿐**이다. 그래서 `shown == desired`
            // 로 양방향 자기치유를 걸면, 재배치가 이동모드 진입에 켜 놓은 하이라이트를 여기서
            // 매 프레임 도로 꺼버린다(desired=false, shown=true → early-return 이 뚫려 Hide 로 감).
            // 실제로 그 증상이었다 — 이동모드에 들어가도 배치 가능 타일이 안 보였다.
            // 끄는 것은 **내가 켰던 것만** 끈다(내 래치가 true 였을 때의 전이).
            bool needsRepost = desired && !bridge.IsPlacementHighlightShown;
            if (!needsRepost && desired == _placeableHlDesired && ReferenceEquals(unit, _placeableHlUnit)) return;
            _placeableHlDesired = desired;
            _placeableHlUnit = unit;
            if (desired) bridge.ShowPlacementHighlight(unit);
            else bridge.HidePlacementHighlight();
        }

        private void Update()
        {
            UpdatePlacementHighlightState(); // placement-eligible-tile-highlight unit 2 — early-return 위에서 매 프레임 파생 토글

            // placement-armed-board-drag unit 0 — arm 된 상태 + 드래그 아님일 때 보드 프레스-드래그-릴리즈 제스처.
            if (_armedUnit != null && !_session.active) UpdateBoardGesture();

            // depth-parallax unit 7 — 스와이프 속도 → 정규화 틸트를 매 프레임 컷신 플레이어에 피드.
            // 컷신은 보드 독립(오프보드에서도 재생) 이라 보드 early-return 위에서 실행. 블록 로컬 dt
            // (아래 dt 는 early-return 밑이라 스코프 밖 + CS0136 회피 위해 이름 분리). 게인은 컨트롤러가 단독 소유.
            if (_session.active)
            {
                float swipeDt = Mathf.Max(Time.unscaledDeltaTime, 1e-4f);
                Vector2 rawVel = (_lastAimScreenPos - _prevScreenPos) / swipeDt;
                _swipeVelSmoothed = Vector2.Lerp(_swipeVelSmoothed, rawVel, Cfg.deployCutsceneSwipeSmoothing);
                Vector2 tilt = _swipeVelSmoothed / Mathf.Max(Cfg.deployCutsceneSwipeRefSpeed, 1f);
                tilt = Vector2.ClampMagnitude(tilt, 1f) * _tiltGain; // 게인 곱은 컨트롤러가 단독 소유(플레이어는 게인 모름)
                _cutscenePlayer?.SetTilt(tilt);
                _prevScreenPos = _lastAimScreenPos;
            }

            // drag-cancel-affordance unit 0 — 예고 룩은 아래 early-return 위에서 판단한다.
            // 오프보드/프리뷰 없음 프레임에도 예고는 켜지고 꺼져야 한다(dwell 누적도 여기서 돈다).
            UpdateCancelVisual();

            if (!_session.active || _session.preview == null || _session.endNode == null || mainCamera == null) return;
            if (!_onBoard || !_posInit) return;
            var s = Cfg;
            float dt = Mathf.Max(Time.unscaledDeltaTime, 1e-4f);
            var camT = mainCamera.transform;

            // unit 2·3 — 추종 스텝 전에 포커스 셀을 확정(+디바운스)한다.
            // dt = unscaled(슬로우모 무관 실시간) — 디바운스 타이밍이 배치 슬로우모에 안 끌리게.
            // unit 4 — 탭 비행 중엔 선택 타일에 포커스 고정(실시간 발밑 추종 제거). 스와이프는 발밑 추종 유지.
            // drag-cancel-affordance unit 0 — 취소 존 안에서는 판정 자체를 멈춘다. 추종 스텝(아래)은
            // 계속 돌아 고스트가 손가락을 따라 트레이로 내려온다 — 멈추는 건 "어느 칸이냐" 뿐이다.
            // 소거는 진입 프레임 1회면 충분하다(hoverTile 이 비면 그 뒤로 페인트가 없다) —
            // 매 프레임 Clear* 를 부르면 타일맵 오버레이를 계속 다시 칠한다.
            if (CancelArmed)
            {
                if (_session.hoverTile.HasValue) ClearHover();
                _noCell = false;      // 사유는 트레이 존 하나로 귀속(라벨 문구는 어차피 같다)
                UpdateRejectLabel();  // ClearHover 가 감춘 라벨을 취소 문구로 다시 켠다
            }
            else ResolveFocusAndTarget(dt);

            // 무게추 스프링(탄성)+속도 상한으로 지연·스윙을 유지한다. 이 블록은 **손가락이 끄는 세션
            // 전용**이 됐다 — drop-dismount unit 7 이후 탭 경로엔 추종할 세션이 한 프레임도 없다.
            KeyringSim.SpringStep(ref _unitPosWorld, ref _unitVelWorld, _unitTargetWorld,
                s.spring, s.damping, s.maxSpeed, dt);

            // camera-direction unit 5 rev 3 — 드래그 포커스 피드 = **터치/포인터 스크린 좌표 그대로**
            // (고리/유닛 월드 좌표 아님 — 카메라 되먹임·스프링 출렁임 원천 차단, 스무딩은 Director
            // 쪽 스프링-댐핑). 매 프레임 피드가 계약 — 끊기면(오프보드/세션 종료/파괴) staleness 해제.
            // placement-thumb-occlusion unit 0 — **raw 를 먹인다.** Director 는 스크린좌표를 NDC 로
            // 절대 변환하므로 가상 포인터를 주면 포커스 y 에 상수 바이어스가 실려 카메라가 프레임을
            // 당기고, 보드가 화면상 내려가 오프셋이 벌린 손가락↔칸 간격을 일부 되돌린다.
            EnsureCameraDirector()?.SetDragFocus(_lastRawScreenPos);

            // 배치: 고리(공중) · 유닛 머리(발+높이) · 줄(고리→머리).
            if (_session.ring != null) _session.ring.position = _ringWorld;
            Vector3 headPos = _unitPosWorld + camT.up * _session.unitHeight;
            _session.endNode.position = headPos;

            // 유닛 기울임: 줄(고리→머리) 방향으로 기움(뒤로 처질수록 기욺). clamp maxAngle.
            if (_session.swingPivot != null)
            {
                Vector3 toRing = (_ringWorld - headPos).normalized; // 머리→고리 = 유닛 up 방향
                float lean = KeyringSim.LeanAngle(
                    Vector3.Dot(toRing, camT.right), Vector3.Dot(toRing, camT.up), s.maxAngle);
                _session.swingPivot.localRotation = Quaternion.Euler(0f, 0f, lean);
            }

            if (_session.cordLine != null)
            {
                if (_session.cordLine.positionCount != 2) _session.cordLine.positionCount = 2;
                _session.cordLine.SetPosition(0, _ringWorld);
                _session.cordLine.SetPosition(1, headPos);
            }

        }

        // camera-direction unit 5 — Director 캐시 (miss 캐시 + 1회 경고, 기존 패턴).
        private Wassup.Presentation.CameraDirector _cameraDirector;
        private bool _cameraDirectorMissWarned;

        private Wassup.Presentation.CameraDirector EnsureCameraDirector()
        {
            if (_cameraDirector != null) return _cameraDirector;
            if (_cameraDirectorMissWarned) return null;
            if (mainCamera == null) return null;
            _cameraDirector = mainCamera.GetComponent<Wassup.Presentation.CameraDirector>();
            if (_cameraDirector == null)
            {
                Debug.LogWarning("[DefenderDragPlacementController] CameraDirector 미배선 — 드래그 포커스 생략.", this);
                _cameraDirectorMissWarned = true;
            }
            return _cameraDirector;
        }

        public void UpdateDrag(Vector2 screenPosition)
        {
            if (!_session.active) return;
            // placement-thumb-occlusion unit 0 — **변환은 여기 한 곳뿐이다.** 이 아래는 전부 가상 포인터.
            // EndDrag 는 이 메서드에 위임하고 screenPosition 을 재사용하지 않으므로 무변경이어야 한다 —
            // 거기서 또 변환하면 오프셋이 두 번 더해져 릴리즈 칸이 하이라이트보다 한 칸 더 위로 튄다.
            _lastRawScreenPos = screenPosition;
            screenPosition = ToPlacementPointer(screenPosition, 1f); // 트레이 세션 전용 경로 — 램프 없음
            _lastAimScreenPos = screenPosition; // unit 4 — 거부 라벨 포인터 추종
            // drag-cancel-affordance unit 0 — 취소 존 판정. 가상 포인터로 재는 이유는 spec README
            // ("도달성 무손실") 이 소유한다 — raw 로 바꾸면 큰 맵 최하단 행이 배치 불가가 된다.
            // 갱신은 여기 한 곳(가상 포인터가 확정되는 유일한 지점)이고 EndDrag 는 이 값을 읽기만 한다.
            // activeInHierarchy — 트레이는 페이즈에 따라 숨는다(None/Draft/Gimmick/Result). 드래그 세션이
            // 페이즈 전환을 넘겨 살면 "보이지 않는 취소 영역" 이 되므로 보이는 동안만 판정한다.
            // rect 를 하나만 읽는다는 계약 2 는 유지된다(오프셋을 더하지 않는다).
            _cancelHover = _cancelZone != null && _cancelZone.gameObject.activeInHierarchy
                           && RectTransformUtility.RectangleContainsScreenPoint(_cancelZone, screenPosition, null);
            // 존을 벗어난 적이 있는가 — CancelArmed 의 (a) 문(의도적 복귀는 dwell 면제). dwell 누적/
            // 리셋은 통합 상태 기준이라 UpdateCancelVisual 이 소유한다(여기서 리셋하면 칸 없음 쪽이 새다).
            if (!_cancelHover) _cancelZoneLeft = true;
            // 발↔고리 화면 세로 거리 = 유닛 키 + 줄 길이. 고리는 손가락에, 유닛은 그만큼 화면 아래 보드에.
            float totalDrop = _session.unitHeight + Cfg.ropeLength * _session.visualScale;

            if (TryComputeRingUnit(screenPosition, totalDrop, out Vector3 ringW, out Vector3 unitTargetW,
                    out Vector3 fingerBoardW))
            {
                _ringWorld = ringW;
                _unitTargetWorld = unitTargetW; // 추종 목표 = 손가락 바로 아래 발점(프리뷰 전용)
                _fingerBoardWorld = fingerBoardW; // 셀 판정 기준 = 손가락 보드 히트
                if (!_posInit) { _unitPosWorld = unitTargetW; _unitVelWorld = Vector3.zero; _posInit = true; }
                _onBoard = true;
                if (_session.preview != null && !_session.preview.activeSelf) _session.preview.SetActive(true);
            }
            else
            {
                _onBoard = false;
                _noCell = false; // 세션 플래그를 이 분기에서도 관리(프리뷰가 숨으므로 고스트 상태를 남기지 않는다)
                ClearHover();
                if (_session.preview != null) _session.preview.SetActive(false);
            }
        }

        // 손가락 ray → 고리(손가락 위치) + 유닛 발 목표. 수직 분리는 카메라-up(화면 세로) 기준:
        // 고리는 손가락 ray 위, 발은 고리보다 화면상 totalDrop 아래이면서 보드 평면 위에 놓이도록 s 를 푼다.
        // (월드-up 으로 올리면 기울어진 카메라에서 화면상 거의 안 올라가 고리·유닛이 겹친다.)
        //
        // fingerBoardW = **셀 판정 기준점**(보드 평면 위 손가락 직접 히트). 프리뷰용 발점과 분리하는 게
        // load-bearing: 발점은 totalDrop(유닛 키 + 줄)만큼 화면 아래라, 그걸로 칸을 정하면 손가락이
        // 화면 상단에 닿아도 발점이 상단 행에 못 미쳐 **보드 최상단 N행이 영구 배치 불가**가 된다
        // (실측 15×11 맵에서 상단 3행). 아래 skew 보정이 이미 같은 점을 구해 수평축만 정렬하고 버렸다 —
        // 세로도 같은 기준으로 통일해 spec 계약("배치 칸 = 마우스", keyring-cord-preview README)을 회복한다.
        private bool TryComputeRingUnit(Vector2 screenPos, float totalDrop,
            out Vector3 ringW, out Vector3 unitTargetW, out Vector3 fingerBoardW)
        {
            ringW = default; unitTargetW = default; fingerBoardW = default;
            if (mainCamera == null) return false;
            var ray = mainCamera.ScreenPointToRay(screenPos);
            var boardPlane = BoardSpace.RaycastPlane();
            var camT = mainCamera.transform;
            Vector3 N = boardPlane.normal;
            float nd = Vector3.Dot(N, ray.direction);
            if (Mathf.Abs(nd) < 1e-6f) return false;
            // ring = camPos + s*rayDir(손가락 위), feet = ring - camUp*totalDrop 가 boardPlane 위가 되는 s.
            float s = -(Vector3.Dot(N, camT.position - camT.up * totalDrop) + boardPlane.distance) / nd;
            if (s <= 0f) return false;
            ringW = camT.position + ray.direction * s;
            Vector3 feet = ringW - camT.up * totalDrop;
            // placement-cell-snap unit 5 — 퍼스펙티브 수평 스큐 제거: camUp 오프셋을 보드에 투영하면
            // 수평(카메라 right 투영) 성분이 화면 x 에 따라 달라져 좌우 판정이 카메라 위치에 의존했다(좌 0.89↔우 0.50셀).
            // feet 의 보드-수평 성분을 손가락 직접 히트와 정렬 → 손가락이 가리키는 열에 정확히 판정(깊이 오프셋은 유지).
            // (시뮬 탭 경로는 이 함수를 소비하지 않는다 — RunSimulatedDrag 가 월드 좌표를 직접 구동. review fix 로 시뮬 분기 제거.)
            float sf = -(Vector3.Dot(N, camT.position) + boardPlane.distance) / nd;
            if (sf > 0f)
            {
                Vector3 pFinger = camT.position + ray.direction * sf;
                fingerBoardW = pFinger; // 셀 판정 기준(위 주석)
                Vector3 boardRight = Vector3.ProjectOnPlane(camT.right, N);
                if (boardRight.sqrMagnitude > 1e-8f)
                {
                    boardRight.Normalize();
                    feet -= boardRight * Vector3.Dot(feet - pFinger, boardRight);
                }
            }
            else
            {
                // 손가락 ray 가 카메라 앞에서 보드를 안 만나는 퇴화 프레임(화면상 발생 안 함).
                // 판정 기준을 비워두지 않고 발점으로 폴백 — 이전 동작과 동일.
                fingerBoardW = feet;
            }
            Vector3 nUp = N.normalized;
            if (Vector3.Dot(nUp, camT.position - feet) < 0f) nUp = -nUp;
            unitTargetW = feet + nUp * previewHeight; // 발 = 보드 표면 + 살짝 띄움
            return true;
        }

        // placement-cell-snap — 손가락 바로 아래 발점(_unitTargetWorld)에서 포커스 셀을 확정한다. unit 1 히스테리시스 + unit 3
        // settle-to-commit 으로 판정을 안정화하되, 고스트 자체는 이 발점을 스프링 추종(키링 스윙 유지) —
        // **스냅하지 않는다**(스냅하면 유닛이 셀 중심에 얼어붙어 줄/스윙이 사라짐 — unit 2 회귀). "어느 칸"은 하이라이트가 보여준다.
        private void ResolveFocusAndTarget(float dt, bool forceCommit = false)
        {
            Vector2Int cell;
            Vector2 frac = default; // unit 7 — SetHover 뒤 액체 하이라이트 신호 산출에 재사용
            // 손가락 보드 히트로 칸을 정한다 — 스윙하는 _unitPosWorld 도, 화면 아래로 매달린
            // 발점(_unitTargetWorld)도 아니다. 전자는 흔들려서, 후자는 totalDrop 만큼 밀려서
            // 상단 행에 도달하지 못한다(_fingerBoardWorld 선언부 주석 참조).
            var sim = BoardSpace.ToSim(_fingerBoardWorld);
            if (bridge != null)
            {
                // unit 1 — 매 프레임 반올림 대신 히스테리시스. 이전 포커스 셀(_session.hoverTile — 이미 sticky
                // 상태, 진실 소스 하나)을 밴드 안에서 유지해 경계 지터를 흡수. frac/gridSize 는 DebugWorldToCell 과 동일 공간.
                frac = bridge.DebugWorldToCellFractional((Vector3)sim);
                var resolved = PlacementCellSnap.Resolve(_session.hoverTile, frac,
                    Cfg.placementStickMargin, bridge.DebugGridSize, Cfg.placementOutsideToleranceCells);
                // drag-cancel-affordance unit 3 — 관용 밖 = **칸 없음**. 하이라이트/사거리를 걷고
                // 취소 예고(고스트 + 포인터 라벨)로 넘긴다. 릴리즈는 EndDrag 의 "칸 없음" 경로가
                // 받는다(그 분기는 원래부터 있었고, clamp 때문에 도달 불가였을 뿐이다).
                if (!resolved.HasValue)
                {
                    _noCell = true;
                    // 소거는 진입 프레임 1회 — 아래 CancelArmed 분기와 같은 규칙(hoverTile 이
                    // 비면 그 뒤로 페인트가 없다). 라벨은 포인터를 따라야 하므로 매 프레임.
                    if (_session.hoverTile.HasValue) ClearHover();
                    UpdateRejectLabel();
                    return;
                }
                _noCell = false;
                Vector2Int target = resolved.Value;
                // unit 3 — throttle(주기적 커밋): 이동 중에도 interval 마다 현재 칸으로 스텝 갱신, 사이엔 유지.
                // 첫 프레임(hoverTile 없음)과 forceCommit(릴리즈 최종 해석)은 즉시 확정 + 상태 리셋.
                if (forceCommit || !_session.hoverTile.HasValue) { cell = target; _debounce = default; }
                else
                    cell = PlacementSnapDebounce.Step(ref _debounce, _session.hoverTile.Value, target,
                        dt, Cfg.placementCommitInterval);
            }
            else
            {
                cell = new Vector2Int(Mathf.FloorToInt(sim.x + 0.5f), Mathf.FloorToInt(sim.z + 0.5f));
            }
            // defender-footprint unit 2 — 손가락 셀 → 앵커(하단 중앙) → 공간 무효 시 자석.
            // 히스테리시스·throttle 은 위 손가락 셀 축이 소유하고 앵커는 그 순수 파생이라
            // 안정성이 승계된다. 릴리즈 forceCommit 도 같은 산식을 다시 지나 표시=확정.
            var fpSize = _session.unit != null ? _session.unit.Footprint : Vector2Int.one;
            var anchor = FootprintMath.AnchorFromBottomCenter(cell, fpSize);
            // action-tray unit 4 — reason 을 버리지 않고 세션에 보관, 라벨로 구분 표기.
            var reason = PlacementRejectReason.None;
            bool valid = bridge != null && bridge.CanPlaceDefenderAt(anchor.x, anchor.y, _session.unit, out reason);
            if (!valid && bridge != null && IsSpatialReason(reason)
                && bridge.TryFindNearestPlaceableAnchor(_session.unit, anchor,
                    Cfg.placementMagnetRadiusCells, out var snapped))
            {
                anchor = snapped;
                valid = bridge.CanPlaceDefenderAt(anchor.x, anchor.y, _session.unit, out reason);
            }
            _session.rejectReason = valid ? PlacementRejectReason.None : reason;
            SetHover(cell, anchor, valid);
            // placement-thumb-occlusion unit 3 — 사거리 적색화. **SetHover 뒤**여야 한다: SetHover 가 셀
            // 변경 시 SetPlacementRange 를 부르고 그 페인트는 유효성을 모른다. 뷰가 전이만 스탬프하므로
            // 매 프레임 호출이 스팸이 아니다.
            bridge?.SetPlacementRangeValidity(valid);
            // unit 7 rev — 끈적 액체 하이라이트: 확정 칸 테두리는 고정, 내부 액체가 손가락 쪽으로 번진다.
            // 신호(dir,t)는 Resolve 와 같은 밴드로 산출 → t=1 이 실제 파열점과 일치.
            if (bridge != null && Cfg.stickyLiquidEnabled)
            {
                PlacementCellSnap.EvaluateStretch(cell, frac, Cfg.placementStickMargin, out var bDir, out var bT);
                bridge.SetPlacementStretch(cell, bDir, bT, valid);
            }
            UpdateRejectLabel();
        }

        // action-tray unit 4 — 사유 매핑: coral X(비용) / amber ■(점유) / neutral —(불가).
        private void UpdateRejectLabel()
        {
            // 취소 예고의 **유일한 문자 채널**. 사유가 둘(트레이 존 복귀 / 격자 밖 관용 초과)이지만
            // 표면은 하나다 — rev3 에서 트레이 배너를 지우고 이 라벨로 합쳤다. 거부(코스트 부족·점유)와
            // 달리 취소는 "진행되지 않는다" 가 아니라 "되돌린다" 라서 문구·색이 따로다.
            // 게이트(CancelArmed)를 지나므로 오버슛·드래그 시작 구간에 껌뻑이지 않는다.
            if (CancelArmed && _session.active)
            {
                EnsureRejectLabel();
                if (!_rejectLabel.gameObject.activeSelf) _rejectLabel.gameObject.SetActive(true);
                _rejectLabel.text = CancelLabelText;
                _rejectLabel.color = Cfg.cancelTint;
                PositionRejectLabel();
                return;
            }
            bool show = _session.active && _onBoard && _session.hoverTile.HasValue
                        && !_session.isValidTile && _session.rejectReason != PlacementRejectReason.None;
            if (!show)
            {
                HideRejectLabel();
                return;
            }

            EnsureRejectLabel();
            string text;
            Color color;
            switch (_session.rejectReason)
            {
                case PlacementRejectReason.InsufficientCost:
                    text = "X 코스트 부족"; color = new Color(1f, 0.42f, 0.36f, 1f); break;
                case PlacementRejectReason.Occupied:
                    text = "■ 점유됨"; color = new Color(1f, 0.76f, 0.30f, 1f); break;
                default:
                    text = "— 배치 불가"; color = new Color(0.82f, 0.83f, 0.88f, 1f); break;
            }
            if (!_rejectLabel.gameObject.activeSelf) _rejectLabel.gameObject.SetActive(true);
            _rejectLabel.text = text;
            _rejectLabel.color = color;
            PositionRejectLabel();
        }

        // placement-thumb-occlusion unit 0 — 앵커는 **가상** 포인터(하이라이트 위에 떠야 한다).
        // 대신 화면 위로 이탈하지 않게 클램프한다: 오프셋이 붙으면 실제 손가락 기준 96+offset px 라
        // 상단 드래그에서 잘려 나간다. 이 라벨은 코스트 부족의 **유일한 문자 채널**이라 이탈 비용이 크다.
        private void PositionRejectLabel()
        {
            float labelY = Mathf.Min(_lastAimScreenPos.y + RejectLabelRise, Screen.height - RejectLabelTopMargin);
            _rejectLabel.transform.position = new Vector3(_lastAimScreenPos.x, labelY, 0f);
        }

        private void HideRejectLabel()
        {
            if (_rejectLabel != null && _rejectLabel.gameObject.activeSelf)
                _rejectLabel.gameObject.SetActive(false);
        }

        private void EnsureRejectLabel()
        {
            if (_rejectLabel != null) return;
            _rejectCanvasGO = new GameObject("DragRejectCanvas", typeof(Canvas));
            _rejectCanvasGO.transform.SetParent(transform, false);
            var canvas = _rejectCanvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20001; // 드래그 프리뷰(20000) 위
            var go = new GameObject("RejectLabel", typeof(RectTransform));
            go.transform.SetParent(_rejectCanvasGO.transform, false);
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(300f, 40f);
            _rejectLabel = go.AddComponent<TextMeshProUGUI>();
            if (_uiFont != null) _rejectLabel.font = _uiFont;
            _rejectLabel.fontSize = 26f;
            _rejectLabel.fontStyle = FontStyles.Bold;
            _rejectLabel.alignment = TextAlignmentOptions.Center;
            _rejectLabel.textWrappingMode = TextWrappingModes.NoWrap;
            _rejectLabel.raycastTarget = false;
            var mat = _rejectLabel.fontMaterial;
            mat.EnableKeyword(ShaderUtilities.Keyword_Outline);
            mat.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0f, 0f, 0f, 0.9f));
            mat.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.16f);
            go.SetActive(false);
        }

        // ── drag-cancel-affordance unit 0 — 취소 예고 ────────────────────────────
        // 신호는 **두 개뿐**이다: (a) 프리뷰 실루엣이 고스트 알파, (b) 보드 하이라이트·사거리 소거.
        // 문자는 포인터 추종 라벨 하나가 담당한다(UpdateRejectLabel).
        //
        // rev3 (사용자 결정 2026-07-30) — 트레이를 덮던 취소 배너를 **삭제**했다. 위 두 신호가 이미
        // 플레이어의 시선 위치(손에 든 유닛 · 보드)에 있어서 배너는 그 위에 얹은 스크림이었고,
        // 코스트 물통과 출발 슬롯을 가려 "어디로 되돌아가는지" 를 오히려 지웠다. 1초짜리 인터랙션에
        // 전면 오버레이는 과하다. 표면을 하나로 줄이면 "칸 없음" 취소와도 같은 문법이 된다.
        private void UpdateCancelVisual()
        {
            // dwell 은 **취소 상태에 머무는 동안**만 자란다(사유 무관). OnDrag 는 포인터가 움직일
            // 때만 오지만 _cancelHover/_noCell 은 마지막 값을 유지하므로, 손가락을 멈춘 채 머무는
            // 경우가 정확히 잡힌다. 상태가 끊기면 0 으로 되돌려 오버슛이 dwell 을 적립하지 못하게 한다.
            if (_session.active && CancelStateNow) _cancelDwell += Time.unscaledDeltaTime;
            else _cancelDwell = 0f;

            // 고스트 알파는 **두 사유 공용** — "이대로 놓으면 안 꽂힌다" 는 한 가지 신호로 읽혀야 한다.
            bool ghost = CancelArmed && _session.active;
            if (ghost == _cancelVisualOn) return;
            _cancelVisualOn = ghost;
            // 폴백 capsule 프리뷰는 skeleton 이 없다 — 알파 변화 없음(미지원, 계약 아님).
            if (_session.skeleton != null)
                SetPreviewAlpha(_session.skeleton, ghost ? Mathf.Clamp01(Cfg.cancelPreviewAlpha) : 1f);
        }

        // 취소 룩 하드 해제 — 세션 정리 경유(커밋/취소/비활성). 알파 원복은 프리뷰가 곧 파괴되므로
        // 생략해도 무해하지만, 세대가 바뀐 세션에 상태가 새지 않게 플래그는 반드시 내린다.
        private void ResetCancelVisual()
        {
            _cancelHover = false;
            _cancelZoneLeft = false;
            _cancelDwell = 0f;
            _cancelVisualOn = false;
            _noCell = false;
        }

        public void EndDrag(Vector2 screenPosition)
        {
            if (!_session.active) return;
            UpdateDrag(screenPosition);
            // drag-cancel-affordance unit 0 — 취소 존 릴리즈 = 사용자가 의도한 정상 종료.
            // FlashPlacementReject 를 부르지 않는다(취소는 거부가 아니다). 커밋 이전에 갈라지므로
            // 코스트·쿨타임·엔티티 어느 것도 발생하지 않는다(계약 1).
            if (_cancelHover)
            {
                CleanupSession();
                // 전용 클립이 없고 의미("집었던 걸 되돌림")가 같아 카드 복귀음을 재사용한다.
                SoundManager.Instance?.PlayCardReturn();
                return;
            }
            // review fix — 릴리즈 확정은 throttle tick 을 기다리지 않는다. 손가락 최종 위치를 히스테리시스로만
            // 거른 칸으로 즉시 재해석(하이라이트·팝도 같은 호출에서 갱신 → 표시 칸 == 배치 칸 유지).
            // 없으면 빠른 드롭이 최대 interval(0.5s) 전 stale 칸에 배치되는 회귀.
            if (_onBoard) ResolveFocusAndTarget(0f, forceCommit: true);

            if (_session.hoverTile.HasValue && _session.isValidTile)
            {
                // defender-footprint unit 2 — 커밋 = 고스트가 보여준 앵커(표시=확정). 1×1 은 손가락 셀과 동일.
                CommitPlacementAt(_session.anchorTile ?? _session.hoverTile.Value);
                return;
            }
            if (_session.hoverTile.HasValue)
                bridge?.FlashPlacementReject(_session.hoverTile.Value);
            else
                // drag-cancel-affordance unit 3 — 칸 없음 = 취소(거부 아님). 트레이 존 릴리즈와 같은
                // 소리를 쓴다 — 사용자에겐 "되돌렸다" 라는 같은 사건이다.
                SoundManager.Instance?.PlayCardReturn();
            CleanupSession();
        }

        // defender-tap-to-place unit 1 — 트레이 슬롯 탭으로 arm 토글(단일 armed). 보드 탭 시 이 유닛을 배치.
        public void ToggleArm(DefenderDragSlot slot, DefenderUnitData unit, Vector2 fromScreen)
        {
            if (_armedSlot == slot) { Disarm(); return; } // 같은 슬롯 재탭 = 해제
            Disarm();
            _armedSlot = slot; _armedUnit = unit; _armedFromScreen = fromScreen;
            slot?.SetArmed(true);
            Armed?.Invoke(unit);
        }

        public void Disarm(bool notify = true)
        {
            bool hadArmedUnit = _armedUnit != null;
            // review fix — `?.` 는 Unity destroyed fake-null 을 못 거르므로 Unity `!=` 로 가드(트레이 리빌드 후 MissingReference 방지).
            if (_armedSlot != null) _armedSlot.SetArmed(false);
            _armedSlot = null; _armedUnit = null;
            ResetBoardGesture(); // placement-armed-board-drag unit 0 — arm 해제 시 진행 중 보드 제스처도 정리
            if (hadArmedUnit && notify) Disarmed?.Invoke();
        }

        // defender-tap-to-place unit 1 — 이 슬롯이 현재 armed 인가(슬롯의 disarm-토글 판정용).
        public bool IsArmed(DefenderDragSlot slot) => _armedSlot == slot;

        // review fix — arm 하이라이트 색은 SO(하드코딩 금지, TilemapMapView 확정 팝 색과 함께 튜닝).
        public Color ArmHighlightColor => Cfg.armHighlightColor;

        // placement-armed-board-drag unit 0 — arm 상태에서 보드 프레스 → 이동량 기반 tap/drag 판정 → release 분기.
        // press 프레임에 이어서 같은 프레임의 이동/릴리즈도 평가한다(early-return 금지 — 순간 탭이 stuck 되는 회귀 방지).
        private void UpdateBoardGesture()
        {
            if (_armedSlot == null) { Disarm(); return; } // 슬롯 파괴(트레이 리빌드) 시 자가 해제(Unity ==)
            var pointer = Pointer.current;
            if (pointer == null) return;
            if (mainCamera == null) mainCamera = Camera.main;
            if (mainCamera == null || bridge == null) return;

            // press 다운: 가드 3종 통과 시 제스처 개시.
            if (!_boardGestureActive && pointer.press.wasPressedThisFrame)
            {
                // 스킬 조준 탭과 이중 소비 방지(PlacementInput aim-mode race 가드와 동일 이유).
                if (GameManager.Instance != null && GameManager.Instance.IsAiming) return;
                if (PointerOverUi()) return; // UI 탭(슬롯 arm 등) 제외 — 터치는 touchId 로 판정
                _boardDownScreen = pointer.position.ReadValue();
                // unit 4 — **맵 밖 프레스도 제스처를 연다.** 예전엔 여기서 셀이 안 나오면 제스처를
                // 시작하지 않았는데, 그러면 배경을 눌렀다 떼는 동작이 아무 데도 도달하지 못해
                // 선택이 그대로 남았다. 릴리즈가 «배치 아니면 해제» 를 단독으로 결정하려면
                // 그 릴리즈가 반드시 이 상태기계 안에서 일어나야 한다.
                _boardGestureActive = true;
                _boardDragging = false;
                CancelTapPlaceRangePeek();   // unit 2 — 직전 탭 배치 range flourish 정지(새 제스처 우선)
            }

            if (!_boardGestureActive) return;

            var cur = pointer.position.ReadValue();
            // 이동량 승격 — 시간이 아니라 거리로 탭/드래그를 가른다(사용자 결정 2026-07-20).
            float travel = Vector2.Distance(cur, _boardDownScreen);
            if (!_boardDragging && travel >= BoardDragThreshold)
                _boardDragging = true;

            // placement-thumb-occlusion unit 1 — 오프셋은 **드래그로 승격된 뒤에만**. 이 경로엔 트레이
            // 세션의 히스테리시스·throttle 이 없어(UpdateBoardScout 은 매 프레임 생 판정) 램프가 유일한
            // 완충이다. 이동량 비례라 손가락이 움직인 만큼만 하이라이트가 앞서간다.
            // 승격 전 램프 0 → ToPlacementPointer 가 항등 = 스카우트가 누른 칸을 그대로 비춘다.
            float ramp = _boardDragging
                ? PlacementPointerOffset.Ramp(travel, BoardDragThreshold, Cfg.placementPointerOffsetRampDistance)
                : 0f;

            if (pointer.press.wasReleasedThisFrame)
            {
                // 드래그 릴리즈 = 스카우트가 보여준 칸(가상 포인터)에 배치. 탭은 누른 칸 그대로(raw) —
                // 탭은 피드백 루프를 볼 시간 없이 커밋되므로 오프셋이 오배치로 읽힌다.
                if (_boardDragging) { CommitBoardDrag(ToPlacementPointer(cur, ramp)); ResetBoardGesture(); }
                // 탭(무이동): 기존 클릭 배치와 동일 액션 — 즉시 배치하되(HandleBoardTap) 공격범위를 착지 셀에 잠깐 노출.
                else { _boardGestureActive = false; _boardDragging = false; HandleBoardTap(cur); }
                return;
            }

            // placement-armed-board-drag unit 1 — 프레스부터 릴리즈 직전까지 range-only 스카우트(손가락 셀 추종).
            // 릴리즈(탭) 결과와 일치한다("하이라이트는 어느 순간에도 거짓말하지 않는다").
            UpdateBoardScout(ToPlacementPointer(cur, ramp));
        }

        // placement-armed-board-drag unit 4 — armed 보드 경로(스카우트·릴리즈 공용)의 셀 판정.
        //
        // **트레이 D&D 와 같은 관용 노브를 태운다.** 배치 판정 포인터는 손가락보다 화면 위로
        // `placementPointerOffsetHeightRatio`(65px@1080) 올라가 있어서, 최상단 행을 노리고 끌면
        // 조준점이 격자 위로 한 칸 넘어가기 쉽다. D&D 는 `placementOutsideToleranceCells`(=1) 로
        // 그 오버슛을 용서해 테두리 칸에 붙여주고 2칸 이상 나가야 취소한다 — armed 경로만 관용 0 이면
        // **같은 손동작이 한쪽은 배치, 한쪽은 선택 해제**가 된다(최상단 행 도달성은
        // `DragPlacementReachTest` 가 지키는 계약이다).
        //
        // 관용 규칙 자체는 여기서 재구현하지 않고 `PlacementCellSnap.Resolve` 를 그대로 부른다 —
        // 정책이 두 곳에 살면 한쪽만 튜닝된다. 히스테리시스는 **0**: armed 스카우트는 세션의
        // throttle/밴드 없이 매 프레임 생 판정하는 게 unit 1 계약이라, 관용만 공유하고 밴드는 안 쓴다.
        private bool TryResolveArmedCell(Vector2 screen, out Vector2Int cell)
        {
            cell = default;
            if (bridge == null || mainCamera == null) return false;
            if (!bridge.TryScreenToBoardFrac(mainCamera, screen, out var frac)) return false;
            var resolved = PlacementCellSnap.Resolve(null, frac, 0f,
                bridge.DebugGridSize, Cfg.placementOutsideToleranceCells);
            if (!resolved.HasValue) return false;
            cell = resolved.Value;
            return true;
        }

        // placement-armed-board-drag unit 1 — 세션 없는 range-only 스카우트. 드래그 세션 SetHover 의 표시 계약을
        // 미러하되(범위·팝은 셀 변경 시만, hover 는 매 프레임), 키링 유닛은 띄우지 않는다. 유닛은 트레이에 남는다.
        private void UpdateBoardScout(Vector2 screen)
        {
            if (bridge == null || _armedUnit == null) return;
            // unit 4 — 릴리즈와 **같은 판정**을 써야 한다. 관대한 clamp 변환은 배경 위에서도 가장자리
            // 칸을 비추는데 릴리즈가 그걸 취소로 버리면 하이라이트가 거짓말한다
            // ("하이라이트는 어느 순간에도 거짓말하지 않는다").
            if (!TryResolveArmedCell(screen, out var cell)) { ClearBoardScout(); return; }

            // defender-footprint unit 2 — 트레이 D&D 와 같은 앵커 산식(하단 중앙 + 공간 무효 시 자석).
            // 릴리즈(ResolveArmedRelease)와 순수 함수로 일치해야 표시=확정이 성립한다.
            var fpSize = _armedUnit.Footprint;
            var anchor = FootprintMath.AnchorFromBottomCenter(cell, fpSize);
            bool valid = bridge.CanPlaceDefenderAt(anchor.x, anchor.y, _armedUnit, out var scoutReason);
            if (!valid && IsSpatialReason(scoutReason)
                && bridge.TryFindNearestPlaceableAnchor(_armedUnit, anchor,
                    Cfg.placementMagnetRadiusCells, out var scoutSnapped))
            {
                anchor = scoutSnapped;
                valid = bridge.CanPlaceDefenderAt(anchor.x, anchor.y, _armedUnit, out _);
            }
            var fpPrimary = FootprintMath.PrimaryCell(anchor, fpSize);
            bridge.SetPlacementRangeValidity(valid); // unit 3 — 스카우트도 같은 적색 채널(매 프레임, 전이만 반응)
            bool changed = !_boardScoutCell.HasValue || _boardScoutCell.Value != cell
                           || !_boardScoutAnchor.HasValue || _boardScoutAnchor.Value != anchor;
            if (changed && _boardScoutCell.HasValue)
                bridge.ClearPlacementHover(_boardScoutCell.Value); // 이전 셀 hover 정리(액체 비활성 경로)
            _boardScoutCell = cell;
            _boardScoutAnchor = anchor;

            if (changed)
            {
                bridge.SetPlacementRange(fpPrimary, _armedUnit);  // 범위 격자 — 셀/앵커 변경 시만, 중심 = 대표 셀
                bridge.PulsePlacementHover(cell, valid);          // 확정 팝
            }
            UpdateGhost(anchor, valid, _armedUnit);
            if (Cfg.stickyLiquidEnabled)
                bridge.SetPlacementStretch(cell, Vector2.zero, 0f, valid); // 정적(손가락 방향 번짐 없음 — 탭 비행 unit 4 와 동일)
            else
                bridge.SetPlacementHover(cell, valid);
        }

        private void ClearBoardScout()
        {
            if (bridge != null)
            {
                if (_boardScoutCell.HasValue) bridge.ClearPlacementHover(_boardScoutCell.Value);
                bridge.ClearPlacementRange();
                bridge.SetPlacementRangeValidity(true); // unit 3 — 스카우트 경계 리셋(ClearHover 와 대칭)
                bridge.ClearPlacementStretch();
            }
            _boardScoutCell = null;
            _boardScoutAnchor = null;
            ClearGhost(); // defender-footprint unit 2
        }

        // placement-armed-board-drag unit 0 — 드래그 릴리즈 커밋: 유효셀이면 기존 tray→cell 시뮬 비행 재사용.
        // unit 4 — 못 놓는 자리에서 손을 떼면 **선택까지 풀린다**(아래 ResolveArmedRelease 단일 꼬리).
        private void CommitBoardDrag(Vector2 screen)
        {
            if (!ResolveArmedRelease(screen, out var cell, out var unit)) return;
            SimulateDragTo(unit, _armedFromScreen, cell); // 내부 BeginDrag 가 Disarm(=arm 해제=배치 확정)
        }

        // unit 4 — armed 보드 제스처의 **공용 릴리즈 꼬리**. 탭·드래그가 같은 규칙을 쓴다:
        //   유효셀 = 배치(true 반환) · 무효셀 = 거부 표시 후 해제 · 맵 밖 = 취소음 후 해제.
        // 즉 릴리즈는 언제나 제스처를 끝낸다 — 손을 뗐는데 선택이 남아 있는 상태가 없다.
        // 셀 판정을 `TryResolveArmedCell` 에 맡기는 이유: 관대한 clamp 변환은 맵 밖을 가장자리 셀로
        // 접어 true 를 주므로 배경에 놓아도 그 칸이 배치 가능하면 배치된다(= "배치 불가한 곳에 놓으면
        // 취소" 가 성립하지 않는다). 반대로 관용 0 의 하드 판정은 가장자리 오버슛을 전부 취소로
        // 돌려 D&D 와 조작감이 갈린다 — 그래서 D&D 와 같은 관용을 태운 그 함수를 쓴다.
        private bool ResolveArmedRelease(Vector2 screen, out Vector2Int cell, out DefenderUnitData unit)
        {
            unit = _armedUnit; // SimulateDragTo 내부 BeginDrag→Disarm 이 비우기 전에 캡처
            if (!TryResolveArmedCell(screen, out cell))
            {
                // 칸 없음 = 거부가 아니라 취소. 트레이 D&D 의 칸 없음 릴리즈와 같은 소리를 쓴다
                // (drag-cancel-affordance unit 3 — 사용자에겐 "되돌렸다" 라는 같은 사건).
                SoundManager.Instance?.PlayCardReturn();
                Disarm();
                return false;
            }
            // defender-footprint unit 2 — 릴리즈도 스카우트와 **같은 순수 산식**(하단 중앙 + 자석).
            // 스카우트가 비춘 앵커와 릴리즈 확정 앵커가 갈리면 하이라이트가 거짓말한다.
            var relSize = unit != null ? unit.Footprint : Vector2Int.one;
            cell = FootprintMath.AnchorFromBottomCenter(cell, relSize);
            if (!bridge.CanPlaceDefenderAt(cell.x, cell.y, unit, out var relReason))
            {
                if (IsSpatialReason(relReason)
                    && bridge.TryFindNearestPlaceableAnchor(unit, cell,
                        Cfg.placementMagnetRadiusCells, out var relSnapped)
                    && bridge.CanPlaceDefenderAt(relSnapped.x, relSnapped.y, unit, out _))
                {
                    cell = relSnapped;
                    return true;
                }
                bridge.FlashPlacementReject(cell); // 왜 안 놓였는지는 칸이 말한다
                Disarm();                          // 그리고 원래 플레이 상태로 — 내부에서 ResetBoardGesture
                return false;
            }
            return true;
        }

        // placement-armed-board-drag unit 0 — 제스처 상태 리셋(드래그 커밋·무효셀 탭·arm 해제 경유).
        private void ResetBoardGesture()
        {
            _boardGestureActive = false;
            _boardDragging = false;
            ClearBoardScout();  // unit 1 — 스카우트 범위/hover 소거
        }

        // placement-armed-board-drag unit 2 — 탭(무이동 릴리즈) = 기존 클릭 배치와 동일 액션 + 범위 노출.
        // 유효셀: 즉시 비행 배치 + 착지 셀에 범위 flourish. 무효셀/맵 밖: unit 4 공용 꼬리가 해제한다.
        private void HandleBoardTap(Vector2 screen)
        {
            if (!ResolveArmedRelease(screen, out var cell, out var unit)) return;
            SimulateDragTo(unit, _armedFromScreen, cell); // 즉시 비행 배치(내부 BeginDrag 가 스카우트/arm 정리)
            StartTapPlaceRangePeek(cell, unit);           // 비행 시작 후 범위 재노출(재확인 flourish)
        }

        // placement-armed-board-drag unit 2 — 유효셀 탭 배치의 범위 flourish. 배치 세션이 CleanupSession 으로
        // 범위를 지우는 것과 안 싸우게 매 프레임 재확인한다. drop-dismount unit 7 이후 유지 조건은 **하마 비행**
        // 이다(아래 루프) — 세션은 한 프레임이라 그것만 보면 즉시 꺼진다. 자기 flight 의 Disarm 에는 안 죽고
        // (별도 코루틴), 새 press·트레이 드래그에서만 취소된다.
        private void StartTapPlaceRangePeek(Vector2Int cell, DefenderUnitData unit)
        {
            CancelTapPlaceRangePeek();
            if (bridge == null || unit == null) return;
            _tapPlaceRangeRoutine = StartCoroutine(RunTapPlaceRangePeek(cell, unit));
        }

        private IEnumerator RunTapPlaceRangePeek(Vector2Int cell, DefenderUnitData unit)
        {
            // 비행 중에만 범위 표시(sim 경로라 비행이 스스로 안 그리고 CleanupSession clear 만 하므로 매 프레임 재확인).
            // 배치(착지=커밋)로 비행 세션이 끝나면 곧바로 소거 — linger 없음(다른 배치 동작과 동일).
            // unit 7 — 시뮬 세션은 이제 **한 프레임짜리**라(커밋이 탭 프레임) 여기에 걸어두면 flourish 가
            // 즉시 꺼진다. 범위는 **하마 비행이 사는 동안** 유지하고 착지에 걷는다 — 탭에는 스카우트
            // 구간이 없어(D&D 의 드래그 · 탭투프레스의 프레스-드래그와 다른 점) 이 flourish 가 유일한
            // 범위 피드백이기 때문이다. 하마가 안 떴으면(폴백) 둘 다 거짓이라 즉시 소거된다.
            // defender-footprint unit 2 — cell = 앵커(하마 등록부 키). 범위 중심은 대표 셀.
            var peekRangeCell = FootprintMath.PrimaryCell(cell, unit != null ? unit.Footprint : Vector2Int.one);
            while (_session.active || _activeDismounts.ContainsValue(cell))
            {
                bridge.SetPlacementRange(peekRangeCell, unit);
                yield return null;
            }
            _tapPlaceRangeRoutine = null;
            bridge.ClearPlacementRange();
        }

        private void CancelTapPlaceRangePeek()
        {
            if (_tapPlaceRangeRoutine != null)
            {
                StopCoroutine(_tapPlaceRangeRoutine);
                _tapPlaceRangeRoutine = null;
                bridge?.ClearPlacementRange();
            }
        }

        // review fix — no-arg IsPointerOverGameObject 는 마우스 pointerId 만 조회해 터치에서 UI 를 못 거른다
        // (Android 실기기에서 UI 위 탭이 보드로 관통). 터치 중이면 primaryTouch.touchId 로 판정.
        private static bool PointerOverUi()
        {
            var es = UnityEngine.EventSystems.EventSystem.current;
            if (es == null) return false;
            var ts = Touchscreen.current;
            if (ts != null && ts.primaryTouch.press.isPressed)
                return es.IsPointerOverGameObject(ts.primaryTouch.touchId.ReadValue());
            return es.IsPointerOverGameObject();
        }

        // defender-tap-to-place unit 0 — 탭/보드드래그 릴리스 배치.
        //
        // drop-dismount unit 7 (2026-08-19) — **구조가 바뀌었다.** 예전엔 키링에 매달린 고스트가
        // 트레이에서 타일까지 던져지는 아치를 1.5초 날고, 타일 바로 위에 정착한 **뒤에** 커밋했다.
        // 그래서 실유닛은 그 자리에 팝했고, 하마(下馬)를 붙여도 낙차가 없어 제자리 홉이 됐다.
        //
        // 이제 트레이 D&D 와 **같은 구조**다: 집은 곳에서 놓는 순간 커밋하고, 실유닛이 거기서
        // 타일까지 날아가 착지한다. D&D 의 «집은 곳» 이 키링에 매달린 위치라면, 탭의 «집은 곳» 은
        // **트레이 유닛 셀**이다. 비행·착지·잔류는 전부 `StartDropDismount` 가 가져간다.
        //
        // 그래서 코루틴이 아니다 — 세션은 이 한 프레임만 살아서 고리·줄 하드웨어와 커밋 꼬리를
        // 빌려주고 곧바로 정리된다. 딸려오는 것 둘(수용됨):
        //   · 비행 시간이 하마의 0.45s 상한을 따른다(계약 3 — 공중 유닛이 활성이 되면 안 된다).
        //   · 배치 컷신은 재생 창이 없다 — 아래 BeginDrag 에서 시뮬 경로를 아예 제외한다.
        public void SimulateDragTo(DefenderUnitData unit, Vector2 fromScreen, Vector2Int targetCell)
        {
            if (unit == null || bridge == null || _session.active) return;
            BeginDrag(unit, fromScreen, simulated: true);
            if (!_session.active) return;
            if (mainCamera == null) mainCamera = Camera.main;
            if (mainCamera == null) { CleanupSession(); return; }

            var camT = mainCamera.transform;
            var cfg = Cfg;
            // defender-footprint unit 2 — 비행 종점 = footprint 기하 중심(짝수 변 +0.5칸, 1×1 동일).
            Vector3 endFeet = bridge.GridAnchorToViewCenter(targetCell, unit);
            Vector3 boardN = BoardSpace.RaycastPlane().normal.normalized;
            if (Vector3.Dot(boardN, camT.position - endFeet) < 0f) boardN = -boardN; // 카메라 쪽
            Vector3 startFeet = ScreenToBoardFeet(fromScreen, endFeet); // 트레이 슬롯 → 보드 평면 발점

            // 하마의 출발 자세 = 트레이 유닛 셀. `StartDropDismount` 는 `_unitPosWorld` 를 시작점으로,
            // `DetachKeyringRemnant` 는 `ring.position` 을 잔류 고리 위치로 캡처한다 — 둘 다 여기서 세운다.
            float totalDrop = _session.unitHeight + cfg.ropeLength * _session.visualScale;
            _onBoard = true; _posInit = true;
            _unitPosWorld = _unitTargetWorld = startFeet + boardN * previewHeight;
            _unitVelWorld = Vector3.zero; // 잔여 스윙 없음 → 반동은 순수 dip(Hermite 접선 0)
            _ringWorld = startFeet + camT.up * totalDrop;
            _lastAimScreenPos = _lastRawScreenPos = (Vector2)mainCamera.WorldToScreenPoint(_ringWorld);

            // 고리·줄 트랜스폼은 평소 Update 의 추종 블록이 세우는데, 이 경로는 그 블록을 **한 번도
            // 지나지 않는다**(같은 프레임에 세션이 끝난다). 여기서 직접 세우지 않으면 잔류 고리가
            // 원점에 남아 화면 구석에서 페이드한다.
            Vector3 headPos = _unitPosWorld + camT.up * _session.unitHeight;
            if (_session.ring != null) _session.ring.position = _ringWorld;
            if (_session.endNode != null) _session.endNode.position = headPos;
            if (_session.cordLine != null)
            {
                if (_session.cordLine.positionCount != 2) _session.cordLine.positionCount = 2;
                _session.cordLine.SetPosition(0, _ringWorld);
                _session.cordLine.SetPosition(1, headPos);
            }

            _debounce = default;
            CommitPlacementAt(targetCell);
        }

        // 화면좌표 → 보드 평면 위 발점(월드). 실패 시 폴백(보통 endFeet).
        private Vector3 ScreenToBoardFeet(Vector2 screen, Vector3 fallback)
        {
            if (mainCamera == null) return fallback;
            var ray = mainCamera.ScreenPointToRay(screen);
            var plane = BoardSpace.RaycastPlane();
            if (plane.Raycast(ray, out float enter) && enter > 0f) return ray.GetPoint(enter);
            return fallback;
        }

        // review fix — 드롭/시뮬 공용 커밋 꼬리. 검증은 TryBeginDefenderDeployment 내부(CanPlaceDefenderAt)가
        // 단일 담당(사전 중복 검증 제거). 성공=deploy 코루틴, 실패=reject 플래시. 양 경로 동작 단일화.
        private void CommitPlacementAt(Vector2Int cell)
        {
            if (!_session.active) return;
            var session = _session;
            if (bridge != null && bridge.TryBeginDefenderDeployment(cell.x, cell.y, session.unit, out var entity))
            {
                // defender-deploy-cutscene unit 8 — 배치 완료는 컷씬보다 절대 우선.
                // 플립북/hold/slide-out 어느 단계든 즉시 숨기고 다음 배치를 위해 틸트까지 원복한다.
                _cutscenePlayer?.ForceStopAndReset();
                // defender-drop-dismount unit 2 — CleanupSession 전에 고스트 실좌표를 캡처해 실유닛
                // 뷰를 하마 궤적으로 날린다. facing 유닛도 병행(계약 8) — aim 은 셀 기준 로직이라
                // 뷰 비행과 무충돌.
                // unit 3 — facing 여부를 먼저 확정: 비-facing 드롭은 스폰 연출을 착지 프레임으로 이관하고
                // (RunDeployment 는 시계만 유지), facing 은 aim 경로 연출 현행 유지(이중 재생 방지).
                //
                // unit 7 (2026-08-19) — **경로 게이트 없음.** 배치 방식 3종(트레이 D&D · 탭투플레이스 ·
                // armed 보드 프레스-드래그)이 같은 착지를 갖는다. 예전엔 여기 `!_simulatedDrag` 가 있었고
                // 그 근거는 "시뮬은 자체 고스트가 이미 날았다" 였는데, 그건 **도착까지**의 이야기고 하마는
                // **도착 이후**(반동→솟음→스틱 착지→스쿼시→고리·줄 잔류)라 겹치지 않았다. 지금은 탭 경로도
                // 고스트를 날리지 않고 **여기서부터** 트레이 셀 → 타일을 난다(SimulateDragTo 주석 참조).
                bool facing = session.unit != null && session.unit.RequiresFacing && _aimController != null;
                bool dismount = StartDropDismount(session.unit, cell, entity, presentAtLanding: !facing);
                // defender-footprint unit 2 — cell = 앵커. 방향 조준(레인 중심)·활성화·연출은
                // 유닛이 실제로 서는 **대표 셀** 기준이다(1×1 은 앵커와 동일).
                var fpPrimary = FootprintMath.PrimaryCell(cell,
                    session.unit != null ? session.unit.Footprint : Vector2Int.one);
                // defender-directional-volley unit 6 — 방향 지정 유닛은 여기서 배치가
                // 끝나지 않는다: 엔티티는 PendingDeployment(전투 미참여)로 스폰된 채
                // 공격방향 페이즈로 넘어가고, 방향이 확정돼야 활성화된다.
                // Begin 이 먼저 슬로우모 lease 를 잡은 뒤 CleanupSession 이 드래그 lease 를
                // 놓으므로 드롭 순간 전투가 정속으로 튀지 않는다(순서 의존).
                if (facing)
                {
                    _aimController.Begin(session.unit, fpPrimary, entity);
                    CleanupSession();
                    PlacementCommitted?.Invoke(session.unit);
                    return;
                }
                CleanupSession();
                PlacementCommitted?.Invoke(session.unit);
                StartCoroutine(RunDeployment(session.unit, fpPrimary, entity, skipPresentation: dismount));
                return;
            }
            bridge?.FlashPlacementReject(cell);
            CleanupSession();
        }

        // defender-drop-dismount unit 2 — 진행 중 하마 비행 등록부(entity→cell). OnDisable/OnDestroy
        // 즉시 완결(오버라이드 clear)용. 코루틴은 자기 키 부재를 보고 자진 종료한다(재배치
        // FinishFlightInstant 패턴 미러). 세션(_session/_sessionGen)과 독립 — 계약 7.
        private readonly System.Collections.Generic.Dictionary<Unity.Entities.Entity, Vector2Int> _activeDismounts = new();

        // 커밋 프레임에 고스트 상태를 plain 값으로 캡처하고 실유닛 뷰 오버라이드 비행을 시작한다.
        // 시작 오버라이드는 **동기** 등록 — 같은 프레임 LateUpdate 피드가 소비해 스폰 위치 1프레임 팝을 막는다.
        // 팝 0 계약(5): 시작 = 고스트 실좌표(_unitPosWorld 그대로), 끝 = 정상 피드 공식 미러
        // (TryGetDefenderRestViewPos) — 변환·상수 없이 양 끝점을 각 렌더러 실좌표로 잡는다.
        // unit 4 — 릴리스 자리에 남는 고리+줄 잔류물. 반동 동안 줄이 비행 유닛을 따라 벙고,
        // 분리 프레임에 위치 동결 → 페이드. 페이드는 per-renderer 색(SpriteRenderer.color /
        // LineRenderer.start·endColor)으로만 — 공유 머티리얼 복제·오염 없음(spec 정정: 복제 불필요).
        private sealed class KeyringRemnant
        {
            public GameObject holder;
            public SpriteRenderer ringSprite;   // 스타일 고리(있으면)
            public LineRenderer ringLine;       // 절차적 고리(폴백)
            public LineRenderer cord;
            public Color ringColor, cordStart, cordEnd;
            public Vector3 ringPos;             // 분리 후에도 고정되는 고리 월드 위치
            public float unitHeight;            // 줄 끝 = 유닛 발 + camUp·이 값(머리)
        }

        private bool StartDropDismount(DefenderUnitData unit, Vector2Int cell, Unity.Entities.Entity entity,
            bool presentAtLanding)
        {
            if (bridge == null || !_onBoard || !_posInit) return false;
            if (mainCamera == null) mainCamera = Camera.main;
            if (mainCamera == null) return false;
            if (!bridge.TryGetDefenderRestViewPos(cell, out var end)) return false;

            var cfg = Cfg;
            float duration = Mathf.Max(0.05f, cfg.dropTotalSeconds);
            // 계약 3 — 드롭 창 ⊆ pending 창: 공중 유닛이 활성(공격/피격/재배치 가능)이 되는 일이 구조적으로 없다.
            if (unit != null && unit.deploymentDuration > 0f)
                duration = Mathf.Min(duration, unit.deploymentDuration);
            float recoilFrac = Mathf.Clamp(cfg.dropRecoilSeconds / duration, 0.02f, 0.6f);

            Vector3 start = _unitPosWorld;
            // F-2 — 릴리스 잔여 스윙 속도를 반동 Hermite 접선으로 흡수(플릭일수록 반동 큼).
            // DismountPoint 의 접선 규약 = 반동 구간 정규화 시간 기준 → 월드속도 × 반동초.
            Vector3 startVel = _unitVelWorld * (recoilFrac * duration);

            bridge.SetDefenderViewOverride(entity, start);
            _activeDismounts[entity] = cell;
            // unit 4 — 곧 파괴될 세션 프리뷰에서 고리+줄을 분리해 잔류물로. 직후 CleanupSession 의
            // Destroy(preview)는 실루엣만 지운다(분리된 서브트리는 root 밖).
            var remnant = DetachKeyringRemnant(duration, recoilFrac);
            StartCoroutine(RunDropDismount(entity, cell, unit, start, startVel, end,
                mainCamera.transform.up, duration, recoilFrac, presentAtLanding, remnant));
            return true;
        }

        private KeyringRemnant DetachKeyringRemnant(float duration, float recoilFrac)
        {
            var ring = _session.ring;
            var cord = _session.cordLine;
            if (ring == null && cord == null) return null; // 폴백 capsule 프리뷰 — 잔류물 없음

            var holder = new GameObject("KeyringRemnant");
            var remnant = new KeyringRemnant { holder = holder, unitHeight = _session.unitHeight };
            if (ring != null)
            {
                ring.SetParent(holder.transform, true);
                remnant.ringPos = ring.position;
                remnant.ringSprite = ring.GetComponent<SpriteRenderer>();
                remnant.ringLine = ring.GetComponent<LineRenderer>();
                if (remnant.ringSprite != null) remnant.ringColor = remnant.ringSprite.color;
                else if (remnant.ringLine != null) remnant.ringColor = remnant.ringLine.startColor;
            }
            if (cord != null)
            {
                cord.transform.SetParent(holder.transform, true);
                remnant.cord = cord;
                remnant.cordStart = cord.startColor;
                remnant.cordEnd = cord.endColor;
            }
            // 고아 방지 하드캡(OnDisable 로 코루틴이 죽어도 잔류물은 자멸) — 정상 경로는 코루틴이 먼저 지운다.
            var cfg = Cfg;
            Destroy(holder, duration + Mathf.Max(cfg.dropCordSnapFade, cfg.dropRingFade) + 0.5f);
            return remnant;
        }

        // 반동 중: 줄이 고리(고정)→비행 유닛 머리를 잇는다. 분리 후: 위치 동결(스냅), 색 알파만 페이드.
        // 반환 false = 페이드 완료(잔류물 파괴됨).
        private bool UpdateKeyringRemnant(KeyringRemnant r, Vector3 unitFeet, Vector3 camUp,
            float elapsed, float recoilSeconds, DragSwaySettings cfg)
        {
            if (r == null || r.holder == null) return false;
            if (elapsed <= recoilSeconds)
            {
                if (r.cord != null)
                {
                    if (r.cord.positionCount != 2) r.cord.positionCount = 2;
                    r.cord.SetPosition(0, r.ringPos);
                    r.cord.SetPosition(1, unitFeet + camUp * r.unitHeight);
                }
                return true;
            }
            float sinceSep = elapsed - recoilSeconds;
            float cordA = 1f - Mathf.Clamp01(sinceSep / Mathf.Max(0.01f, cfg.dropCordSnapFade));
            float ringA = 1f - Mathf.Clamp01(sinceSep / Mathf.Max(0.01f, cfg.dropRingFade));
            if (r.cord != null)
            {
                var s = r.cordStart; s.a *= cordA;
                var e = r.cordEnd; e.a *= cordA;
                r.cord.startColor = s;
                r.cord.endColor = e;
            }
            if (r.ringSprite != null)
            {
                var c = r.ringColor; c.a *= ringA;
                r.ringSprite.color = c;
            }
            else if (r.ringLine != null)
            {
                var c = r.ringColor; c.a *= ringA;
                r.ringLine.startColor = r.ringLine.endColor = c;
            }
            if (cordA <= 0f && ringA <= 0f)
            {
                Destroy(r.holder);
                r.holder = null;
                return false;
            }
            return true;
        }

        private IEnumerator RunDropDismount(Unity.Entities.Entity entity, Vector2Int cell, DefenderUnitData unit,
            Vector3 start, Vector3 startVel, Vector3 end, Vector3 camUp, float duration, float recoilFrac,
            bool presentAtLanding, KeyringRemnant remnant)
        {
            var cfg = Cfg;
            float recoilSeconds = recoilFrac * duration;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                yield return null;
                if (!_activeDismounts.ContainsKey(entity)) yield break; // 외부 즉시 완결(OnDisable 등)
                // 계약 9 — binding 붕괴(판매·맵 teardown·리빌드) 시 물러남. 오버라이드는 맵 리셋이 co-locate clear.
                if (bridge == null || !bridge.TryGetDefenderAt(cell, out var e, out _, out _) || e != entity)
                {
                    AbandonDismount(entity, remnant);
                    yield break;
                }
                // 시계 = unscaled(배치 조작의 연장, 계약 스펙). 시간 이징 없음(선형) — 착지 임팩트 속도는
                // 기하(끝접선 = 3·(end−c2), 순수 -camUp)가 만든다. Out* 이징은 끝속도를 0 으로 죽여
                // "스틱 착지"가 물러진다.
                elapsed += Time.unscaledDeltaTime;
                float raw = Mathf.Clamp01(elapsed / duration);
                // flight-lift-feel unit 3 — 비행 구간만 재매핑한다(반동까지 왜곡하면 힘 모으는 타이밍이
                // 흔들린다). 총 시간은 안 바뀌므로 "비행 창 ⊆ pending 창" 계약이 그대로 산다.
                float f = raw <= recoilFrac
                    ? raw
                    : recoilFrac + (1f - recoilFrac) * KeyringSim.FlightTimeRemap(
                          (raw - recoilFrac) / (1f - recoilFrac), cfg.dropHangPower);
                var p = KeyringSim.DismountPoint(start, startVel, end, camUp,
                    recoilFrac, cfg.dropRecoilDip, cfg.dropArcHeightFactor, cfg.dropArcMinHeight,
                    cfg.dropLaunchControl, cfg.dropLandingHeight, f);
                // flight-lift-feel unit 2 — 기저선(출발→도착 직선) 대비 뜬 높이. 반동 구간의 dip 은
                // 음수라 Max(0) 이 걷어낸다 — 내려앉을 때는 커지지 않는다.
                Vector3 baseline = Vector3.Lerp(start, end, f);
                float lift = Mathf.Max(0f, Vector3.Dot(p - baseline, camUp));
                // 그림자는 유닛이 아니라 기저선 위에 남는다 — 아치가 camUp 이라 유닛 XZ 가 밀린다.
                bridge.SetDefenderViewOverride(entity, p, lift, baseline);
                UpdateKeyringRemnant(remnant, p, camUp, elapsed, recoilSeconds, cfg); // unit 4 — 줄 벙음→스냅 페이드
            }
            // 착지 — 최종점(end)이 정상 피드 공식과 동일 좌표라 clear 직후 프레임이 그대로 이어진다(팝 0).
            _activeDismounts.Remove(entity);
            bridge?.ClearDefenderViewOverride(entity);
            // flight-lift-feel unit 3 — 착지 눌림. 취소 경로(AbandonDismount)는 여기 못 오므로
            // 끊긴 비행에 스쿼시가 터지지 않는다.
            bridge?.PlayLandingSquash(entity, cfg.dropLandingSquash, cfg.dropLandingSquashSeconds);
            bridge?.PulsePlacementHover(cell, true);
            // unit 3 — 스폰 연출(배치 링 펄스·placementVfx·PlayDeploy 스폰애니)을 착지 프레임에 발화.
            // 유닛이 공중인 commit 프레임에 타일에서 링이 터지던 어긋남 제거. **활성화 시계는 무변경**
            // (계약 4) — RunDeployment(skipPresentation)가 deploymentDuration 을 직접 읽어 commit 기준으로
            // 대기한다. 반환 duration 은 여기서 무시(시계는 이미 돌고 있음). facing 은 aim 경로 연출
            // 현행 유지라 presentAtLanding=false(이중 재생 방지 — spec unit 3 정정).
            if (presentAtLanding && bridge != null)
            {
                try { bridge.PlayDeploymentPresentation(unit, cell, entity); }
                catch (System.Exception ex) { Debug.LogException(ex, this); }
            }
            // unit 4 — 잔류 페이드 꼬리: 노브(dropRingFade 등)가 비행보다 길면 착지 후에도 페이드를
            // 마저 굴린다(동결 방지). 기본값에선 착지 전에 끝나 한 번도 안 돈다.
            while (UpdateKeyringRemnant(remnant, end, camUp, elapsed, recoilSeconds, cfg))
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        private void AbandonDismount(Unity.Entities.Entity entity, KeyringRemnant remnant = null)
        {
            _activeDismounts.Remove(entity);
            bridge?.ClearDefenderViewOverride(entity);
            // 잔류물은 즉시 파괴(spec 정정: abandon = teardown 맥락 — 페이드 연출 의미 없음, 붙박이만 방지).
            if (remnant != null && remnant.holder != null) { Destroy(remnant.holder); remnant.holder = null; }
        }

        // OnDisable/OnDestroy 즉시 완결 — 오버라이드를 비우면 정상 피드가 다음 프레임 착지 좌표로 그린다.
        // 살아남은 코루틴(비활성화는 코루틴을 죽이지 않는다)은 키 부재 가드로 자진 종료.
        private void FinishDismountsInstant()
        {
            if (_activeDismounts.Count == 0) return;
            foreach (var kv in _activeDismounts) bridge?.ClearDefenderViewOverride(kv.Key);
            _activeDismounts.Clear();
        }

        private IEnumerator RunDeployment(DefenderUnitData unitData, Vector2Int cell, Unity.Entities.Entity entity,
            bool skipPresentation = false)
        {
            float duration = 0f;
            if (skipPresentation)
            {
                // defender-drop-dismount unit 3 — 연출은 하마 착지 프레임이 발화(RunDropDismount).
                // 여기는 활성화 시계만: PlayDeploymentPresentation 의 반환과 같은 소스(deploymentDuration)를
                // 직접 읽어 commit 기준 대기를 유지한다(계약 4 — 밸런스 무변경, 착지 ≤ 활성화).
                duration = unitData != null ? Mathf.Max(0f, unitData.deploymentDuration) : 0f;
            }
            else if (bridge != null)
            {
                try
                {
                    duration = bridge.PlayDeploymentPresentation(unitData, cell, entity);
                }
                catch (System.Exception ex)
                {
                    Debug.LogException(ex, this);
                }
            }

            if (duration > 0f) yield return new WaitForSeconds(duration);
            float skillDelay = unitData != null ? Mathf.Max(0f, unitData.placementSkillDelay) : 0f;
            if (skillDelay > 0f) yield return new WaitForSeconds(skillDelay);
            bridge?.ActivateDeployedDefender(cell, entity);
        }

        private DragSession BuildSession(DefenderUnitData unitData)
        {
            var session = new DragSession { active = true, unit = unitData };
            if (TryBuildKeyringPreview(unitData, ref session))
                return session;
            session.preview = CreateFallbackPreview(unitData);
            return session;
        }

        private bool TryBuildKeyringPreview(DefenderUnitData unitData, ref DragSession session)
        {
            if (unitData == null || unitData.skeletonDataAsset == null) return false;

            float scale = Mathf.Max(0.01f, unitData.spineVisualScale * BattleBridge.CharacterVisualScale);

            var root = new GameObject($"DragPreview_{unitData.displayName}");

            // 고리+줄 하드웨어는 공통 헬퍼가 만든다(재배치 비행과 단일 소스). 실루엣(Spine)은 아래서 붙인다.
            BuildRingAndCord(root, root.name, scale, out var ringXform, out var cordLr);

            // endNode(머리 위치, 빌보드) → swingPivot(머리 중심 기울임) → spineChild(실루엣).
            var endNode = new GameObject($"{root.name}_End");
            endNode.transform.SetParent(root.transform, false);
            var endBillboard = endNode.AddComponent<Billboard>();
            endBillboard.Setup(BillboardMode.Tilted, BattleBridge.CharacterBillboardTilt);

            var swingPivot = new GameObject($"{root.name}_Swing");
            swingPivot.transform.SetParent(endNode.transform, false);

            var spineChild = new GameObject($"{root.name}_Spine");
            spineChild.transform.SetParent(swingPivot.transform, false);
            spineChild.transform.localScale = Vector3.one * scale;

            var components = SkeletonAnimation.AddToGameObject(spineChild, null);
            var skeleton = components.skeletonAnimation;
            components.skeletonRenderer.SkeletonDataAsset = unitData.skeletonDataAsset;
            components.skeletonRenderer.InitialSkinName = string.IsNullOrEmpty(unitData.spineSkinName) ? "default" : unitData.spineSkinName;
            skeleton.Initialize(true);

            // unit-parts-appearance 1 — 스폰 경로(SpineUnitView)와 동일한 공용 헬퍼로 일원화.
            if (skeleton.Skeleton != null)
                SpineCombinedSkinCache.Apply(skeleton.Skeleton, unitData);

            string animation = ResolveAnimation(skeleton, unitData.dragAnimation, unitData.idleAnimation, unitData.attackAnimation);
            if (!string.IsNullOrEmpty(animation))
                skeleton.AnimationState.SetAnimation(0, animation, true);

            SetPreviewAlpha(skeleton, 1f); // placement-enemy-see-through unit 5 — 드래그 유닛은 불투명(적만 투명해져, 배치 유닛이 최상단 초점)
            var skelRenderer = skeleton.GetComponent<MeshRenderer>();
            if (skelRenderer != null) skelRenderer.sortingOrder = BoardSortOrder.DragPreviewOrder;

            // 실루엣 머리(mesh 상단)를 endNode(=머리 위치)에 자동정렬 — 몸통이 아래로 서고, 발이 보드에 닿는다.
            float unitHeight = scale; // 폴백
            Vector3 charmPos = Vector3.down * Cfg.charmDrop;
            if (skelRenderer != null && skelRenderer.localBounds.size.y > 0.01f)
            {
                var lb = skelRenderer.localBounds;
                charmPos += new Vector3(-lb.center.x * scale, -lb.max.y * scale, 0f);
                unitHeight = lb.size.y * scale;
            }
            spineChild.transform.localPosition = charmPos;

            session.preview = root;
            session.cordLine = cordLr;
            session.ring = ringXform;
            session.endNode = endNode.transform;
            session.swingPivot = swingPivot.transform;
            session.spineChild = spineChild.transform;
            session.skeleton = skeleton; // drag-cancel-affordance unit 0 — 취소 고스트 알파
            session.visualScale = scale;
            session.unitHeight = unitHeight;
            return true;
        }

        private Material CordMaterial()
        {
            if (_cordMaterial == null)
            {
                var shader = Shader.Find("Sprites/Default");
                _cordMaterial = new Material(shader) { name = "KeyringCordMat" };
            }
            return _cordMaterial;
        }

        // 키링 고리(ring)+줄(cord) 하드웨어 공통 생성 — 배치 프리뷰(TryBuildKeyringPreview)와 재배치 비행
        // (CreateKeyringHardware)의 **단일 소스**. 실루엣은 각 호출측이 별도로 붙인다(배치=Spine 자식,
        // 재배치=실제 유닛). 스타일/머티리얼/색/폭/빌보드/order 는 여기 한 곳에서만 정한다.
        private void BuildRingAndCord(GameObject root, string namePrefix, float scale,
            out Transform ring, out LineRenderer cord)
        {
            var st = Cfg.style; // keyring-unify 3 — 스타일. null/슬롯 null = 절차적 폴백.
            // 고리(ring): 스타일 스프라이트가 있으면 SpriteRenderer(홀로), 없으면 로컬 원 LineRenderer 루프.
            var ringGo = new GameObject($"{namePrefix}_Ring");
            ringGo.transform.SetParent(root.transform, false);
            if (st != null && st.ringSprite != null)
            {
                var ringSr = ringGo.AddComponent<SpriteRenderer>();
                ringSr.sprite = st.ringSprite;
                if (st.worldRingMaterial != null) ringSr.sharedMaterial = st.worldRingMaterial;
                ringSr.color = Color.white; // 계약 7 — 스타일 적용 시 틴트 중성화(cordColor 갈색 오염 방지)
                ringSr.sortingOrder = BoardSortOrder.DragPreviewOrder;
                // 지름 = ringRadius*2 — 절차적 원(반경 ringRadius*scale)과 크기 등가.
                float spriteWidth = st.ringSprite.bounds.size.x;
                if (spriteWidth > 1e-4f)
                    ringGo.transform.localScale = Vector3.one * (Cfg.ringRadius * 2f * scale / spriteWidth);
            }
            else
            {
                var ringLr = ringGo.AddComponent<LineRenderer>();
                ringLr.useWorldSpace = false;
                ringLr.loop = true;
                ringLr.numCapVertices = 2;
                ringLr.positionCount = RingSegments;
                for (int i = 0; i < RingSegments; i++)
                {
                    float a = (i / (float)RingSegments) * Mathf.PI * 2f;
                    ringLr.SetPosition(i, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * (Cfg.ringRadius * scale));
                }
                ringLr.sharedMaterial = CordMaterial();
                ringLr.widthMultiplier = Cfg.cordWidth * scale;
                ringLr.startColor = ringLr.endColor = Cfg.cordColor;
                ringLr.sortingOrder = BoardSortOrder.DragPreviewOrder;
            }
            var ringBillboard = ringGo.AddComponent<Billboard>();
            ringBillboard.Setup(BillboardMode.Tilted, BattleBridge.CharacterBillboardTilt);

            // 줄(cord): 월드 LineRenderer, 2점(고리→머리). 스타일 머티리얼(u=길이, _LengthAxis=1) 또는 절차적 단색.
            var cordGo = new GameObject($"{namePrefix}_Cord");
            cordGo.transform.SetParent(root.transform, false);
            cord = cordGo.AddComponent<LineRenderer>();
            cord.useWorldSpace = true;
            cord.numCapVertices = 2;
            cord.positionCount = 2;
            bool styledCord = st != null && st.worldCordMaterial != null;
            cord.sharedMaterial = styledCord ? st.worldCordMaterial : CordMaterial();
            cord.widthMultiplier = Cfg.cordWidth * scale;
            cord.startColor = cord.endColor = styledCord ? Color.white : Cfg.cordColor;
            cord.sortingOrder = BoardSortOrder.DragPreviewOrder - 1;
            ring = ringGo.transform;
        }

        // defender-relocation unit 6 — 재배치 비행이 배치(D&D/탭) 키링과 '동일한' 고리+줄을 재사용하도록
        // 하는 팩토리. 고리/줄 구성은 배치 프리뷰와 **동일 헬퍼**(BuildRingAndCord)를 경유 = 진짜 단일 소스.
        // 실루엣은 재배치의 '실제 유닛'이 담당하므로 하드웨어(빌보드 고리 + 월드 줄)만 만든다.
        // 위치는 호출측(재배치 컨트롤러)이 매 프레임 설정한다. 머티리얼은 공유(파괴 금지) — 호출측은 root 만 파괴.
        public readonly struct KeyringHardware
        {
            public readonly GameObject root;
            public readonly Transform ring;
            public readonly LineRenderer cord;
            public readonly float ropeWorld;   // 고리를 머리 위로 띄우는 월드 길이 (= ropeLength × scale)
            public readonly bool valid;
            public KeyringHardware(GameObject root, Transform ring, LineRenderer cord, float ropeWorld)
            { this.root = root; this.ring = ring; this.cord = cord; this.ropeWorld = ropeWorld; valid = root != null; }
        }

        public KeyringHardware CreateKeyringHardware(DefenderUnitData unitData)
        {
            if (unitData == null) return default;
            float scale = Mathf.Max(0.01f, unitData.spineVisualScale * BattleBridge.CharacterVisualScale);
            var root = new GameObject("RelocationKeyring");
            // 고리+줄은 배치 프리뷰와 공통 헬퍼(BuildRingAndCord)로 만든다 — 룩 단일 소스.
            BuildRingAndCord(root, "RelocationKeyring", scale, out var ring, out var cord);
            return new KeyringHardware(root, ring, cord, Cfg.ropeLength * scale);
        }

        // defender-relocation unit 6 — 재배치 비행이 탭 배치와 '동일한' 던지기 곡선을 공유하도록 하는 래퍼.
        // Cfg(DragSwaySettings)의 곡선 튜닝(arcHeight/lateral/launch/landing)을 순수 헬퍼에 공급한다.
        // 좌표(start/end/camUp/boardRight)는 호출측이 view 공간으로 넘긴다 — 탭 경로(RunSimulatedDrag)와 동일 규약.
        public void ComputeThrowArc(Vector3 startView, Vector3 endView, Vector3 camUp, Vector3 boardRight,
            int seq, out Vector3 controlA, out Vector3 controlB)
        {
            var cfg = Cfg;
            KeyringSim.ThrowArcControls(startView, endView, camUp, boardRight,
                cfg.tapArcHeightFactor, cfg.tapArcLateralFactor, cfg.tapThrowLaunchControl, cfg.tapThrowLandingControl,
                seq, out controlA, out controlB);
        }

        private static string ResolveAnimation(SkeletonAnimation skeleton, params string[] candidates)
        {
            if (skeleton == null || skeleton.Skeleton == null || skeleton.Skeleton.Data == null) return null;
            foreach (var candidate in candidates)
            {
                if (string.IsNullOrEmpty(candidate)) continue;
                if (skeleton.Skeleton.Data.FindAnimation(candidate) != null)
                    return candidate;
            }
            return null;
        }

        private static void SetPreviewAlpha(SkeletonAnimation skeleton, float alpha)
        {
            if (skeleton == null || skeleton.Skeleton == null) return;
            var color = skeleton.Skeleton.GetColor();
            color.a = alpha;
            skeleton.Skeleton.SetColor(color);
        }

        private GameObject CreateFallbackPreview(DefenderUnitData unitData)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = $"DragPreview_{unitData.displayName}";
            go.transform.localScale = Vector3.one * (previewScale * BattleBridge.CharacterVisualScale);
            var collider = go.GetComponent<Collider>();
            if (collider != null) Destroy(collider);

            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                if (_previewMaterial == null)
                {
                    _previewMaterial = RuntimeMaterialFactory.CreateTransparent(Color.white);
                }
                Color color = Color.white;
                if (unitData.visualMaterial != null && unitData.visualMaterial.HasProperty("_BaseColor"))
                    color = unitData.visualMaterial.GetColor("_BaseColor");
                color.a = 1f; // placement-enemy-see-through unit 5 — 폴백 프리뷰도 불투명
                RuntimeMaterialFactory.ApplyColor(_previewMaterial, color);
                renderer.sharedMaterial = _previewMaterial;
            }
            return go;
        }

        private void SetHover(Vector2Int cell, Vector2Int anchor, bool valid)
        {
            bool changed = !_session.hoverTile.HasValue || _session.hoverTile.Value != cell;
            // defender-footprint unit 2 — 자석이 셀 불변인 채 앵커만 옮길 수 있어 앵커 변경도 함께 본다.
            bool anchorChanged = !_session.anchorTile.HasValue || _session.anchorTile.Value != anchor;
            var fpSize = _session.unit != null ? _session.unit.Footprint : Vector2Int.one;
            var primary = FootprintMath.PrimaryCell(anchor, fpSize);
            if (_session.hoverTile.HasValue && _session.hoverTile.Value != cell)
                bridge?.ClearPlacementHover(_session.hoverTile.Value);

            _session.hoverTile = cell;
            _session.anchorTile = anchor;
            _session.isValidTile = valid;
            if (_session.preview != null && !_session.preview.activeSelf)
                _session.preview.SetActive(true);
            // unit 7 rev — 액체 하이라이트가 hover 타일을 **대체**(고정 테두리 + 내부 번짐, 같은 셀에 개체 2개 금지).
            // 끄면 기존 타일 하이라이트로 폴백.
            if (!Cfg.stickyLiquidEnabled)
                bridge?.SetPlacementHover(cell, valid);
            if (changed || anchorChanged)
            {
                // defender-footprint unit 2 — 사거리 링은 유닛이 실제로 설 대표 셀 중심(1×1 은 손가락 셀 동일).
                bridge?.SetPlacementRange(primary, _session.unit);
                bridge?.PulsePlacementHover(cell, valid); // unit 4 — 확정(셀 변경) 팝. 디바운스로 게이팅돼 스팸 아님.
            }
            UpdateGhost(anchor, valid, _session.unit);
        }

        private void ClearHover()
        {
            if (_session.hoverTile.HasValue)
                bridge?.ClearPlacementHover(_session.hoverTile.Value);
            bridge?.ClearPlacementRange();
            bridge?.SetPlacementRangeValidity(true); // unit 3 — 세션 경계 리셋(뷰의 페인트 API 에 리셋을 얹지 않는다)
            bridge?.ClearPlacementStretch(); // unit 7 — 액체 하이라이트 수명은 hover 와 동일
            ClearGhost(); // defender-footprint unit 2 — 고스트 수명도 hover 와 동일
            _session.hoverTile = null;
            _session.anchorTile = null;
            _session.isValidTile = false;
            _debounce = default; // unit 3 — 포커스 해제 시 settle 대기 상태도 리셋(재진입 첫 셀 즉시 확정)
            _session.rejectReason = PlacementRejectReason.None; // unit 4
            if (_rejectLabel != null && _rejectLabel.gameObject.activeSelf)
                _rejectLabel.gameObject.SetActive(false);
        }

        private void CleanupSession()
        {
            _slowmoLease.Dispose(); // time-manager Unit 5 — 슬로우모 해제(멱등)
            bridge?.SetEnemiesDimmed(false); // placement-enemy-see-through — 적 반투명 off(드롭·거부·비활성 모든 종료 경유)
            bridge?.SetPlacementHighlightAboveUnits(false); // unit 6 — 하이라이트 소팅 원복
            bridge?.HidePlacementHighlight(); // placement-eligible-tile-highlight unit 2 — 종료 시 확실히 소거(OnDisable/OnDestroy 포함)
            _placeableHlDesired = false;
            ClearHover();
            bridge?.ClearPlacementRange();
            if (_session.preview != null) Destroy(_session.preview);
            _session = default;
            _sessionGen++; // review fix — 진행 중인 시뮬 코루틴 무효화(세대 불일치 → 자진 종료)
            _posInit = false;
            _onBoard = false;
            ResetCancelVisual();    // drag-cancel-affordance unit 0 — 취소 예고 하드 해제
            _unitVelWorld = Vector3.zero;
            // ui-tweak 2026-07-08 — 클릭 배치 은퇴. 드래그 종료 후 재활성화하지 않는다.
        }

        private void OnDisable()
        {
            if (_cutscenePlayer != null)
                _cutscenePlayer.ForceStopAndReset(); // 비활성화는 고아 root Canvas 잔류 금지
            FinishDismountsInstant(); // defender-drop-dismount unit 2 — 진행 중 하마 비행 즉시 완결
            CleanupSession();
        }

        private void OnDestroy()
        {
            if (_cutscenePlayer != null) _cutscenePlayer.ForceStopAndReset();
            FinishDismountsInstant(); // defender-drop-dismount unit 2
            CleanupSession();
            if (_previewMaterial != null) Destroy(_previewMaterial);
            if (_cordMaterial != null) Destroy(_cordMaterial);
        }
    }
}
