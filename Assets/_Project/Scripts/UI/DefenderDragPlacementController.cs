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
        private Vector2 _lastScreenPos;

        // depth-parallax unit 7 — 스와이프→틸트 피드 상태. _prevScreenPos = 직전 프레임 포인터(속도 델타용),
        // _swipeVelSmoothed = exp-lerp 스무딩 스와이프 속도, _tiltGain = 유닛별 게인(BeginDrag 에서 주입, 컨트롤러 단독 소유).
        private Vector2 _prevScreenPos, _swipeVelSmoothed;
        private float _tiltGain = 1f;

        // 키링 배치 상태: 고리 = 손가락(공중). 유닛 = 보드에 서서 무게추처럼 지연 추종.
        private Vector3 _ringWorld;        // 고리(손가락, 공중)
        private Vector3 _unitTargetWorld;  // 유닛 발 추종 목표. 실제 drag=포인터 발점, tap=곡선 발점. 셀 판정 기준도 이 점.
        private PlacementSnapDebounce.State _debounce; // unit 3: throttle 경과 상태(hoverTile 과 수명 동일)
        // defender-tap-to-place — arm(탭 선택) 상태(단일). 보드 탭 시 이 유닛을 슬롯에서 시뮬 배치.
        private DefenderDragSlot _armedSlot;
        private DefenderUnitData _armedUnit;
        private Vector2 _armedFromScreen;
        private bool _simulatedDrag; // defender-tap-to-place — 시뮬(탭) 경로 표시. 공격 범위 프리뷰 억제.
        // defender-tap-to-place unit 4 — 탭 비행 중 고정할 선택 타일(발밑 추종 대신). unit 5 — 곡선 좌우 변주 인덱스(결정론).
        private Vector2Int? _simFocusCell;
        private int _tapFlightSeq;
        // placement-armed-board-drag unit 0 — armed 유닛의 보드 프레스-드래그-릴리즈 제스처 상태.
        // press 가 보드에서 시작(가드 통과)되면 active, 이동 임계 초과 시 dragging 승격. release 에서
        // dragging=커밋(시뮬 비행) / 아니면 탭(범위 피크는 unit 2). 시간 delta 로 판정하지 않는다(이동량만).
        private bool _boardGestureActive;
        private bool _boardDragging;
        private Vector2 _boardDownScreen;
        // placement-armed-board-drag unit 1 — 스카우트가 현재 표시 중인 셀(변경 감지·소거용). 세션 없는 range-only 경로.
        private Vector2Int? _boardScoutCell;
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
            public float visualScale;
            public float unitHeight;        // 실루엣 월드 높이(발→머리). 머리 오프셋용.
            public Vector2Int? hoverTile;
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
            _simulatedDrag = simulated; // defender-tap-to-place — 시뮬 경로는 첫 UpdateDrag 부터 범위 억제(CleanupSession 이 false 로 리셋한 뒤 재설정)
            if (!simulated) UserDragStarted?.Invoke();
            // time-manager Unit 5 — 드래그 시작 시 전투만 슬로우모. 드롭/취소 시 CleanupSession 에서 해제.
            _slowmoLease = TimeManager.Instance.Request(TimeDomain.Battle, dragSlowmoScale);
            if (mainCamera == null) mainCamera = Camera.main;

            _session = BuildSession(unitData);
            // defender-deploy-cutscene unit 3/8 — 프레임이 있으면 좌하단 컷신 1회 재생(자동 종료).
            // 기능 온/오프는 DragSwaySettings.enableDeployCutscene 로 게이트.
            if (Cfg.enableDeployCutscene && _cutscenePlayer != null &&
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
            _prevScreenPos = screenPosition;
            _swipeVelSmoothed = Vector2.zero;
            bridge?.SetEnemiesDimmed(true); // placement-enemy-see-through — 적 반투명 on
            bridge?.SetPlacementHighlightAboveUnits(true); // unit 6 — 배치 하이라이트를 적 위로
            if (placementInput != null) placementInput.SetClickPlacementEnabled(false);
            UpdateDrag(screenPosition);
        }

        // placement-eligible-tile-highlight unit 2 — 배치 판단 상태(실드래그 또는 탭 arm) 파생 → 하이라이트 토글.
        // 지점마다 show/hide 산탄 대신 원하는 상태를 파생해 idempotent 호출. 탭 비행(_simulatedDrag)은 OFF
        // (range 억제와 일관). 이 파생이 BeginDrag 의 Disarm→재Show 순서의존·_sessionGen 하이재킹을 무관하게 만든다.
        private bool _placeableHlDesired;

        private void UpdatePlacementHighlightState()
        {
            if (bridge == null) return;
            bool desired = (_session.active && !_simulatedDrag) || _armedUnit != null;
            if (desired == _placeableHlDesired) return;
            _placeableHlDesired = desired;
            if (desired) bridge.ShowPlacementHighlight(); else bridge.HidePlacementHighlight();
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
                Vector2 rawVel = (_lastScreenPos - _prevScreenPos) / swipeDt;
                _swipeVelSmoothed = Vector2.Lerp(_swipeVelSmoothed, rawVel, Cfg.deployCutsceneSwipeSmoothing);
                Vector2 tilt = _swipeVelSmoothed / Mathf.Max(Cfg.deployCutsceneSwipeRefSpeed, 1f);
                tilt = Vector2.ClampMagnitude(tilt, 1f) * _tiltGain; // 게인 곱은 컨트롤러가 단독 소유(플레이어는 게인 모름)
                _cutscenePlayer?.SetTilt(tilt);
                _prevScreenPos = _lastScreenPos;
            }

            if (!_session.active || _session.preview == null || _session.endNode == null || mainCamera == null) return;
            if (!_onBoard || !_posInit) return;
            var s = Cfg;
            float dt = Mathf.Max(Time.unscaledDeltaTime, 1e-4f);
            var camT = mainCamera.transform;

            // unit 2·3 — 추종 스텝 전에 포커스 셀을 확정(+디바운스)한다.
            // dt = unscaled(슬로우모 무관 실시간) — 디바운스 타이밍이 배치 슬로우모에 안 끌리게.
            // unit 4 — 탭 비행 중엔 선택 타일에 포커스 고정(실시간 발밑 추종 제거). 스와이프는 발밑 추종 유지.
            ResolveFocusAndTarget(dt, lockCell: _simulatedDrag ? _simFocusCell : null);

            // 실드래그는 무게추 스프링(탄성)+속도 상한으로 지연·스윙을 유지한다.
            // unit 6 rev — 탭 시뮬은 비행부터 정착까지 같은 비진동 추종을 쓴다. 비행 중 고감쇠 스프링에
            // 누적된 오차를 정착 진입 시 갑자기 따라잡던 후반 가속을 제거하고, 최종 정착은 잔여 오차만 닫는다.
            if (_simulatedDrag)
            {
                _unitPosWorld = Vector3.SmoothDamp(_unitPosWorld, _unitTargetWorld, ref _unitVelWorld,
                    Mathf.Max(s.tapFollowSmoothTime, 0.01f), Mathf.Infinity, dt);
            }
            else
            {
                KeyringSim.SpringStep(ref _unitPosWorld, ref _unitVelWorld, _unitTargetWorld,
                    s.spring, s.damping, s.maxSpeed, dt);
            }

            // camera-direction unit 5 rev 3 — 드래그 포커스 피드 = **터치/포인터 스크린 좌표 그대로**
            // (고리/유닛 월드 좌표 아님 — 카메라 되먹임·스프링 출렁임 원천 차단, 스무딩은 Director
            // 쪽 스프링-댐핑). 매 프레임 피드가 계약 — 끊기면(오프보드/세션 종료/파괴) staleness 해제.
            EnsureCameraDirector()?.SetDragFocus(_lastScreenPos);

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
            _lastScreenPos = screenPosition; // unit 4 — 거부 라벨 포인터 추종
            // 발↔고리 화면 세로 거리 = 유닛 키 + 줄 길이. 고리는 손가락에, 유닛은 그만큼 화면 아래 보드에.
            float totalDrop = _session.unitHeight + Cfg.ropeLength * _session.visualScale;

            if (TryComputeRingUnit(screenPosition, totalDrop, out Vector3 ringW, out Vector3 unitTargetW))
            {
                _ringWorld = ringW;
                _unitTargetWorld = unitTargetW; // 추종 목표 = 손가락 바로 아래 발점. 셀 판정도 이 점 기준.
                if (!_posInit) { _unitPosWorld = unitTargetW; _unitVelWorld = Vector3.zero; _posInit = true; }
                _onBoard = true;
                if (_session.preview != null && !_session.preview.activeSelf) _session.preview.SetActive(true);
            }
            else
            {
                _onBoard = false;
                ClearHover();
                if (_session.preview != null) _session.preview.SetActive(false);
            }
        }

        // 손가락 ray → 고리(손가락 위치) + 유닛 발 목표. 수직 분리는 카메라-up(화면 세로) 기준:
        // 고리는 손가락 ray 위, 발은 고리보다 화면상 totalDrop 아래이면서 보드 평면 위에 놓이도록 s 를 푼다.
        // (월드-up 으로 올리면 기울어진 카메라에서 화면상 거의 안 올라가 고리·유닛이 겹친다.)
        private bool TryComputeRingUnit(Vector2 screenPos, float totalDrop, out Vector3 ringW, out Vector3 unitTargetW)
        {
            ringW = default; unitTargetW = default;
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
                Vector3 boardRight = Vector3.ProjectOnPlane(camT.right, N);
                if (boardRight.sqrMagnitude > 1e-8f)
                {
                    boardRight.Normalize();
                    feet -= boardRight * Vector3.Dot(feet - pFinger, boardRight);
                }
            }
            Vector3 nUp = N.normalized;
            if (Vector3.Dot(nUp, camT.position - feet) < 0f) nUp = -nUp;
            unitTargetW = feet + nUp * previewHeight; // 발 = 보드 표면 + 살짝 띄움
            return true;
        }

        // placement-cell-snap — 손가락 바로 아래 발점(_unitTargetWorld)에서 포커스 셀을 확정한다. unit 1 히스테리시스 + unit 3
        // settle-to-commit 으로 판정을 안정화하되, 고스트 자체는 이 발점을 스프링 추종(키링 스윙 유지) —
        // **스냅하지 않는다**(스냅하면 유닛이 셀 중심에 얼어붙어 줄/스윙이 사라짐 — unit 2 회귀). "어느 칸"은 하이라이트가 보여준다.
        private void ResolveFocusAndTarget(float dt, bool forceCommit = false, Vector2Int? lockCell = null)
        {
            Vector2Int cell;
            Vector2 frac = default; // unit 7 — SetHover 뒤 액체 하이라이트 신호 산출에 재사용
            if (lockCell.HasValue)
            {
                // unit 4 — 탭 비행: 발밑 추종/히스테리시스/디바운스 없이 선택 타일에 포커스 고정.
                cell = lockCell.Value;
            }
            else
            {
                // 스윙하는 _unitPosWorld 가 아니라 손가락 바로 아래 발점으로 칸을 정한다 → 흔들림 없이 안정.
                var sim = BoardSpace.ToSim(_unitTargetWorld);
                if (bridge != null)
                {
                    // unit 1 — 매 프레임 반올림 대신 히스테리시스. 이전 포커스 셀(_session.hoverTile — 이미 sticky
                    // 상태, 진실 소스 하나)을 밴드 안에서 유지해 경계 지터를 흡수. frac/gridSize 는 DebugWorldToCell 과 동일 공간.
                    frac = bridge.DebugWorldToCellFractional((Vector3)sim);
                    Vector2Int target = PlacementCellSnap.Resolve(_session.hoverTile, frac, Cfg.placementStickMargin, bridge.DebugGridSize);
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
            }
            // action-tray unit 4 — reason 을 버리지 않고 세션에 보관, 라벨로 구분 표기.
            var reason = PlacementRejectReason.None;
            bool valid = bridge != null && bridge.CanPlaceDefenderAt(cell.x, cell.y, _session.unit, out reason);
            _session.rejectReason = valid ? PlacementRejectReason.None : reason;
            SetHover(cell, valid);
            // unit 7 rev — 끈적 액체 하이라이트: 확정 칸 테두리는 고정, 내부 액체가 손가락 쪽으로 번진다.
            // 신호(dir,t)는 Resolve 와 같은 밴드로 산출 → t=1 이 실제 파열점과 일치.
            if (bridge != null && Cfg.stickyLiquidEnabled)
            {
                if (lockCell.HasValue)
                {
                    // unit 4 — 탭 비행: 손가락 방향 번짐 없이 정적 하이라이트(스트레치 0).
                    bridge.SetPlacementStretch(cell, Vector2.zero, 0f, valid);
                }
                else
                {
                    PlacementCellSnap.EvaluateStretch(cell, frac, Cfg.placementStickMargin, out var bDir, out var bT);
                    bridge.SetPlacementStretch(cell, bDir, bT, valid);
                }
            }
            UpdateRejectLabel();
        }

        // action-tray unit 4 — 사유 매핑: coral X(비용) / amber ■(점유) / neutral —(불가).
        private void UpdateRejectLabel()
        {
            bool show = _session.active && _onBoard && _session.hoverTile.HasValue
                        && !_session.isValidTile && _session.rejectReason != PlacementRejectReason.None;
            if (!show)
            {
                if (_rejectLabel != null && _rejectLabel.gameObject.activeSelf)
                    _rejectLabel.gameObject.SetActive(false);
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
            _rejectLabel.transform.position = new Vector3(_lastScreenPos.x, _lastScreenPos.y + 96f, 0f);
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

        public void EndDrag(Vector2 screenPosition)
        {
            if (!_session.active) return;
            UpdateDrag(screenPosition);
            // review fix — 릴리즈 확정은 throttle tick 을 기다리지 않는다. 손가락 최종 위치를 히스테리시스로만
            // 거른 칸으로 즉시 재해석(하이라이트·팝도 같은 호출에서 갱신 → 표시 칸 == 배치 칸 유지).
            // 없으면 빠른 드롭이 최대 interval(0.5s) 전 stale 칸에 배치되는 회귀.
            if (_onBoard) ResolveFocusAndTarget(0f, forceCommit: true);

            if (_session.hoverTile.HasValue && _session.isValidTile)
            {
                CommitPlacementAt(_session.hoverTile.Value);
                return;
            }
            if (_session.hoverTile.HasValue)
                bridge?.FlashPlacementReject(_session.hoverTile.Value);
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
                // 셀 변환은 bridge.TryScreenToCell 단일 소스(수동 레이캐스트 복제 금지). 보드 밖 프레스는 제스처 개시 안 함.
                if (!bridge.TryScreenToCell(mainCamera, _boardDownScreen, out _)) return;
                _boardGestureActive = true;
                _boardDragging = false;
                CancelTapPlaceRangePeek();   // unit 2 — 직전 탭 배치 range flourish 정지(새 제스처 우선)
            }

            if (!_boardGestureActive) return;

            var cur = pointer.position.ReadValue();
            // 이동량 승격 — 시간이 아니라 거리로 탭/드래그를 가른다(사용자 결정 2026-07-20).
            if (!_boardDragging && Vector2.Distance(cur, _boardDownScreen) >= Mathf.Max(1f, Cfg.boardDragThreshold))
                _boardDragging = true;

            if (pointer.press.wasReleasedThisFrame)
            {
                if (_boardDragging) { CommitBoardDrag(cur); ResetBoardGesture(); }
                // 탭(무이동): 기존 클릭 배치와 동일 액션 — 즉시 배치하되(HandleBoardTap) 공격범위를 착지 셀에 잠깐 노출.
                else { _boardGestureActive = false; _boardDragging = false; HandleBoardTap(cur); }
                return;
            }

            // placement-armed-board-drag unit 1 — 프레스부터 릴리즈 직전까지 range-only 스카우트(손가락 셀 추종).
            UpdateBoardScout(cur);
        }

        // placement-armed-board-drag unit 1 — 세션 없는 range-only 스카우트. 드래그 세션 SetHover 의 표시 계약을
        // 미러하되(범위·팝은 셀 변경 시만, hover 는 매 프레임), 키링 유닛은 띄우지 않는다. 유닛은 트레이에 남는다.
        private void UpdateBoardScout(Vector2 screen)
        {
            if (bridge == null || _armedUnit == null) return;
            if (!bridge.TryScreenToCell(mainCamera, screen, out var cell)) { ClearBoardScout(); return; }

            bool valid = bridge.CanPlaceDefenderAt(cell.x, cell.y, _armedUnit, out _);
            bool changed = !_boardScoutCell.HasValue || _boardScoutCell.Value != cell;
            if (changed && _boardScoutCell.HasValue)
                bridge.ClearPlacementHover(_boardScoutCell.Value); // 이전 셀 hover 정리(액체 비활성 경로)
            _boardScoutCell = cell;

            if (changed)
            {
                bridge.SetPlacementRange(cell, _armedUnit);       // 범위 격자 — 셀 변경 시만 재페인트
                bridge.PulsePlacementHover(cell, valid);          // 확정 팝
            }
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
                bridge.ClearPlacementStretch();
            }
            _boardScoutCell = null;
        }

        // placement-armed-board-drag unit 0 — 드래그 릴리즈 커밋: 유효셀이면 기존 tray→cell 시뮬 비행 재사용.
        private void CommitBoardDrag(Vector2 screen)
        {
            if (!bridge.TryScreenToCell(mainCamera, screen, out var cell)) return; // 보드 밖 릴리즈 = 취소(arm 유지)
            if (bridge.CanPlaceDefenderAt(cell.x, cell.y, _armedUnit, out _))
                SimulateDragTo(_armedUnit, _armedFromScreen, cell); // 내부 BeginDrag 가 Disarm(=arm 해제=배치 확정)
            else
                bridge.FlashPlacementReject(cell); // arm 유지(재시도)
        }

        // placement-armed-board-drag unit 0 — 제스처 상태 리셋(드래그 커밋·무효셀 탭·arm 해제 경유).
        private void ResetBoardGesture()
        {
            _boardGestureActive = false;
            _boardDragging = false;
            ClearBoardScout();  // unit 1 — 스카우트 범위/hover 소거
        }

        // placement-armed-board-drag unit 2 — 탭(무이동 릴리즈) = 기존 클릭 배치와 동일 액션 + 범위 노출.
        // 유효셀: 즉시 비행 배치 + 착지 셀에 범위 flourish. 무효셀: reject + 스카우트 범위 짧게 유지 후 소거(arm 유지).
        private void HandleBoardTap(Vector2 screen)
        {
            if (!bridge.TryScreenToCell(mainCamera, screen, out var cell)) { ResetBoardGesture(); return; } // 보드 밖 = 취소
            var unit = _armedUnit; // SimulateDragTo 내부 BeginDrag→Disarm 이 비우기 전에 캡처
            if (bridge.CanPlaceDefenderAt(cell.x, cell.y, unit, out _))
            {
                SimulateDragTo(unit, _armedFromScreen, cell); // 즉시 비행 배치(내부 BeginDrag 가 스카우트/arm 정리)
                StartTapPlaceRangePeek(cell, unit);           // 비행 시작 후 범위 재노출(재확인 flourish)
            }
            else
            {
                bridge.FlashPlacementReject(cell); // 배치 없이 거부 — arm 유지(재시도)
                ResetBoardGesture();               // 스카우트 범위 즉시 소거(비행 안 하므로 범위 안 남김)
            }
        }

        // placement-armed-board-drag unit 2 — 유효셀 탭 배치의 범위 flourish. 비행이 CleanupSession 으로 범위를
        // 지우는 것과 안 싸우게 매 프레임 범위를 재확인한다. 비행 세션이 사는 동안(=_session.active && _simulatedDrag)
        // 만 유지하고 배치(착지)되면 소거 — 다른 배치 동작과 동일(linger 없음). 자기 flight 의 Disarm 에는 안 죽고
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
            while (_session.active && _simulatedDrag)
            {
                bridge.SetPlacementRange(cell, unit);
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

        // defender-tap-to-place unit 0 — 탭 배치: 트레이(fromScreen)에서 targetCell 로 드래그를 스크립트로 재생.
        // 진짜 드래그처럼 BeginDrag→UpdateDrag(트윈)→확정 을 구동 → 키링/hover/throttle/팝/deploy 전부 재사용.
        public void SimulateDragTo(DefenderUnitData unit, Vector2 fromScreen, Vector2Int targetCell)
        {
            if (unit == null || bridge == null || _session.active) return;
            StartCoroutine(RunSimulatedDrag(unit, fromScreen, targetCell));
        }

        private IEnumerator RunSimulatedDrag(DefenderUnitData unit, Vector2 fromScreen, Vector2Int targetCell)
        {
            BeginDrag(unit, fromScreen, simulated: true); // 시뮬 경로(범위 억제)
            if (!_session.active) yield break;
            int gen = _sessionGen; // review fix — 이 코루틴이 소유한 세션 세대. 새 드래그가 시작되면 불일치 → 물러남.
            if (mainCamera == null) mainCamera = Camera.main;
            var cfg = Cfg;
            var camT = mainCamera.transform;

            // 월드 공간 비행: 유닛 발점을 tray→타일 로 월드에서 직접 트윈하고, 키링(고리/줄/유닛 추종)은 config 대로
            // 따라오게 한다. 스크린 역산·스큐·비행 중 카메라 dolly 에 흔들리지 않는다(이전 스크린 역산 방식의 오배치 원인).
            float totalDrop = _session.unitHeight + cfg.ropeLength * _session.visualScale;
            Vector3 endFeet = bridge.GridCellToViewCenter(targetCell);      // 보드 평면 위 셀 중심(월드)
            Vector3 boardN = BoardSpace.RaycastPlane().normal.normalized;
            if (Vector3.Dot(boardN, camT.position - endFeet) < 0f) boardN = -boardN; // 카메라 쪽
            Vector3 startFeet = ScreenToBoardFeet(fromScreen, endFeet);     // 트레이 슬롯 → 보드 발점(폴백=endFeet)
            // unit 6 — 선택 타일 발 위치가 불변 기준. 유닛/고리 최종점은 여기서 한 번만 파생한다.
            Vector3 unitLift = boardN * previewHeight;
            Vector3 ringLift = camT.up * totalDrop;
            Vector3 finalUnitTarget = endFeet + unitLift;
            Vector3 finalRing = endFeet + ringLift;

            // 비행 시간 = 기준 × (start→end 화면거리 / 화면세로), 0.25~1.5배.
            Vector2 sScr = (Vector2)mainCamera.WorldToScreenPoint(startFeet);
            Vector2 eScr = (Vector2)mainCamera.WorldToScreenPoint(endFeet);
            float distScale = Mathf.Clamp(Vector2.Distance(sScr, eScr) / Mathf.Max(Screen.height, 1f),
                cfg.tapTravelScaleMin, cfg.tapTravelScaleMax);
            float dur = Mathf.Max(cfg.tapTravelDuration * distScale, 0.05f);

            // 렌더 활성 + 추종 시작점.
            _onBoard = true; _posInit = true;
            _simFocusCell = targetCell; // unit 4 — 비행 내내 선택 타일에 포커스 고정
            _unitPosWorld = startFeet + unitLift;
            _unitVelWorld = Vector3.zero;
            if (_session.preview != null && !_session.preview.activeSelf) _session.preview.SetActive(true);

            // unit 6 — 3차 던지기: 시작은 앞·위, 도착은 낮게 두어 상승/하강 접선을 분리한다.
            // unit 5 의 결정론 좌우 변주는 두 제어점에 같은 오프셋으로 유지 → 중간에만 휘고 endpoint 는 정확.
            float throwDistance = Vector3.Distance(startFeet, endFeet);
            const float GoldenRatioConjugate = 0.61803398875f;
            float sequencePhase = (_tapFlightSeq++ + 0.5f) * GoldenRatioConjugate;
            float lateralUnit = (sequencePhase - Mathf.Floor(sequencePhase)) * 2f - 1f; // -1..1
            Vector3 boardRight = Vector3.ProjectOnPlane(camT.right, boardN);
            Vector3 lateralOffset = Vector3.zero;
            if (boardRight.sqrMagnitude > 1e-6f)
                lateralOffset = boardRight.normalized * (throwDistance * cfg.tapArcLateralFactor * lateralUnit);
            float arcHeight = throwDistance * cfg.tapArcHeightFactor;
            Vector2 launchControl = cfg.tapThrowLaunchControl;
            Vector2 landingControl = cfg.tapThrowLandingControl;
            Vector3 controlA = Vector3.Lerp(startFeet, endFeet, launchControl.x)
                               + camT.up * (arcHeight * launchControl.y) + lateralOffset;
            Vector3 controlB = Vector3.Lerp(startFeet, endFeet, landingControl.x)
                               + camT.up * (arcHeight * landingControl.y) + lateralOffset;

            float t = 0f;
            while (t < 1f && _session.active && _sessionGen == gen)
            {
                t += Time.unscaledDeltaTime / dur;
                float linearT = Mathf.Clamp01(t);
                // unit 6 rev — 곡선의 중후반 공간 속도 증가를 시간 이징으로 상쇄한다.
                // 빠르게 던져지고 도착할수록 감속하며, CubicBezier endpoint 는 그대로 정확하다.
                float flightT = 1f - Mathf.Pow(1f - linearT, 3f); // OutCubic
                Vector3 feet = KeyringSim.CubicBezier(startFeet, controlA, controlB, endFeet, flightT);
                _unitTargetWorld = feet + unitLift;                                 // 유닛 추종 목표
                _ringWorld = feet + ringLift;                                       // 고리 = 발 위 totalDrop(camUp)
                _lastScreenPos = (Vector2)mainCamera.WorldToScreenPoint(_ringWorld); // 카메라 포커스 피드
                yield return null;
            }
            if (!_session.active || _sessionGen != gen) yield break; // 세션이 바뀜(새 드래그/정리) → 커밋 없이 물러남

            // unit 6 — 고리는 선택 타일 기준 최종점에 고정. 비행부터 이어진 비진동 추종으로 실제 프리뷰가
            // 거리+속도 조건을 만족할 때까지 짧게 정착한다. 제한시간은 실패 안전망.
            _unitTargetWorld = finalUnitTarget;
            _ringWorld = finalRing;
            _lastScreenPos = (Vector2)mainCamera.WorldToScreenPoint(finalRing);
            float settleElapsed = 0f;
            while (_session.active && _sessionGen == gen)
            {
                bool closeEnough = Vector3.Distance(_unitPosWorld, finalUnitTarget) <= cfg.tapSettleDistance;
                bool slowEnough = _unitVelWorld.magnitude <= cfg.tapSettleSpeed;
                if ((closeEnough && slowEnough) || settleElapsed >= cfg.tapSettleMaxDuration)
                    break;

                settleElapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            if (!_session.active || _sessionGen != gen) yield break;

            // 정상/타임아웃 모두 보정 프레임을 노출하지 않는다. 착지 팝과 공용 커밋 꼬리를 같은 프레임에 실행.
            _unitPosWorld = finalUnitTarget;
            _unitVelWorld = Vector3.zero;
            _debounce = default;
            bridge?.PulsePlacementHover(targetCell, _session.isValidTile);
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
                // defender-directional-volley unit 6 — 방향 지정 유닛은 여기서 배치가
                // 끝나지 않는다: 엔티티는 PendingDeployment(전투 미참여)로 스폰된 채
                // 공격방향 페이즈로 넘어가고, 방향이 확정돼야 활성화된다.
                // Begin 이 먼저 슬로우모 lease 를 잡은 뒤 CleanupSession 이 드래그 lease 를
                // 놓으므로 드롭 순간 전투가 정속으로 튀지 않는다(순서 의존).
                if (session.unit != null && session.unit.RequiresFacing && _aimController != null)
                {
                    _aimController.Begin(session.unit, cell, entity);
                    CleanupSession();
                    PlacementCommitted?.Invoke(session.unit);
                    return;
                }
                CleanupSession();
                PlacementCommitted?.Invoke(session.unit);
                StartCoroutine(RunDeployment(session.unit, cell, entity));
                return;
            }
            bridge?.FlashPlacementReject(cell);
            CleanupSession();
        }

        private IEnumerator RunDeployment(DefenderUnitData unitData, Vector2Int cell, Unity.Entities.Entity entity)
        {
            float duration = 0f;
            if (bridge != null)
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
            var st = Cfg.style; // keyring-unify 3 — 스타일. null/슬롯 null = 절차적 폴백.

            // 고리(ring): 스타일 스프라이트가 있으면 SpriteRenderer(홀로), 없으면 로컬 원 LineRenderer 루프.
            var ringGo = new GameObject($"{root.name}_Ring");
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
            var cordGo = new GameObject($"{root.name}_Cord");
            cordGo.transform.SetParent(root.transform, false);
            var cordLr = cordGo.AddComponent<LineRenderer>();
            cordLr.useWorldSpace = true;
            cordLr.numCapVertices = 2;
            cordLr.positionCount = 2;
            bool styledCord = st != null && st.worldCordMaterial != null;
            cordLr.sharedMaterial = styledCord ? st.worldCordMaterial : CordMaterial();
            cordLr.widthMultiplier = Cfg.cordWidth * scale;
            cordLr.startColor = cordLr.endColor = styledCord ? Color.white : Cfg.cordColor;
            cordLr.sortingOrder = BoardSortOrder.DragPreviewOrder - 1;

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

            var skeleton = spineChild.AddComponent<SkeletonAnimation>();
            skeleton.skeletonDataAsset = unitData.skeletonDataAsset;
            skeleton.initialSkinName = string.IsNullOrEmpty(unitData.spineSkinName) ? "default" : unitData.spineSkinName;
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
            session.ring = ringGo.transform;
            session.endNode = endNode.transform;
            session.swingPivot = swingPivot.transform;
            session.spineChild = spineChild.transform;
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

        private void SetHover(Vector2Int cell, bool valid)
        {
            bool changed = !_session.hoverTile.HasValue || _session.hoverTile.Value != cell;
            if (_session.hoverTile.HasValue && _session.hoverTile.Value != cell)
                bridge?.ClearPlacementHover(_session.hoverTile.Value);

            _session.hoverTile = cell;
            _session.isValidTile = valid;
            if (_session.preview != null && !_session.preview.activeSelf)
                _session.preview.SetActive(true);
            // unit 7 rev — 액체 하이라이트가 hover 타일을 **대체**(고정 테두리 + 내부 번짐, 같은 셀에 개체 2개 금지).
            // 끄면 기존 타일 하이라이트로 폴백.
            if (!Cfg.stickyLiquidEnabled)
                bridge?.SetPlacementHover(cell, valid);
            if (changed)
            {
                // defender-tap-to-place — 탭 시뮬 경로에서는 공격 범위 프리뷰 억제(실제 D&D 만 노출).
                if (!_simulatedDrag) bridge?.SetPlacementRange(cell, _session.unit);
                bridge?.PulsePlacementHover(cell, valid); // unit 4 — 확정(셀 변경) 팝. 디바운스로 게이팅돼 스팸 아님.
            }
        }

        private void ClearHover()
        {
            if (_session.hoverTile.HasValue)
                bridge?.ClearPlacementHover(_session.hoverTile.Value);
            bridge?.ClearPlacementRange();
            bridge?.ClearPlacementStretch(); // unit 7 — 액체 하이라이트 수명은 hover 와 동일
            _session.hoverTile = null;
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
            _simulatedDrag = false; // defender-tap-to-place — 시뮬 표시 해제
            _simFocusCell = null;   // unit 4 — 탭 비행 포커스 고정 해제
            _unitVelWorld = Vector3.zero;
            // ui-tweak 2026-07-08 — 클릭 배치 은퇴. 드래그 종료 후 재활성화하지 않는다.
        }

        private void OnDisable()
        {
            if (_cutscenePlayer != null)
                _cutscenePlayer.ForceStopAndReset(); // 비활성화는 고아 root Canvas 잔류 금지
            CleanupSession();
        }

        private void OnDestroy()
        {
            if (_cutscenePlayer != null) _cutscenePlayer.ForceStopAndReset();
            CleanupSession();
            if (_previewMaterial != null) Destroy(_previewMaterial);
            if (_cordMaterial != null) Destroy(_cordMaterial);
        }
    }
}
