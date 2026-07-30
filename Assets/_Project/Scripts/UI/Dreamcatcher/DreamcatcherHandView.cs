using System.Collections;
using System.Collections.Generic;
using PrimeTween;
using TMPro;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;
using Wassup.Core;
using Wassup.Core.TimeControl;
using Wassup.Data;
using Wassup.UI.Layout;

namespace Wassup.UI
{
    // dreamcatcher-awakening-hand unit 6 — StS-style hand strip that flips in
    // place of the defender placement strip (mutually exclusive by design).
    //
    // Single owner of the strip↔hand transition state (the gauge button only
    // signals Toggled). While the hand is open a battle slomo lease is held
    // (TimeManager, NEVER 0 — realtime contract); closing disposes it.
    // Phase guard (critic H2): leaving Placement/Battle force-closes the hand,
    // drops any pending drag state, and releases the lease.
    public class DreamcatcherHandView : MonoBehaviour
    {
        public event System.Action HandOpened;
        // 카드 press 로 툴팁이 실제로 뜬 순간. 첫 세션 튜토리얼이 "끌어보세요" 배너를
        // 걷는 신호로 쓴다(지시 이행 = 정보 소진, 배너가 상단 중앙 툴팁을 덮는 것도 방지).
        public event System.Action CardPeeked;

        [SerializeField] private DreamcatcherHandController handController;
        [SerializeField] private AwakeningGaugeView gaugeView;
        [SerializeField] private DefenderSelector defenderSelector;
        // battle-hud-layout 1 rev — 배지가 중앙 핸드와 겹치므로 핸드 오픈 중
        // 스트립과 함께 배지도 퇴장시킨다. 표시 결정은 CostDisplay 가 소유.
        [SerializeField] private CostDisplay costDisplay;
        [SerializeField] private AwakeningConfig config;
        // dreamcatcher-attach-requirement unit 5 — 부착 제한이 UnitId 일 때 카드 문안의
        // "{유닛명} 전용" 접두를 표시명으로 해석한다. null 이면 포매터가 id 로 폴백.
        [SerializeField] private DefenderCatalog defenderCatalog;
        // dreamcatcher-attach-lockon unit 0 — 부착 조준 포커스 연출 노브(dim/리티클/
        // 콜아웃/base-ring/확정비트). 미할당 시 프레젠터 미생성(무회귀).
        [SerializeField] private DreamcatcherFocusConfig focusConfig;
        // unit 7 — card drag targeting (screen ray → board cell → defender).
        [SerializeField] private Wassup.Bridge.BattleBridge bridge;
        [SerializeField] private Camera mainCamera;
        [SerializeField] private TMP_FontAsset labelFont;  // Jua — Korean battle UI
        [SerializeField] private TMP_FontAsset numberFont; // Anton — cost digits
        // action-tray unit 3 — 트레이 공유 외곽 문법(폭/y/fill/border). 미할당 시
        // 기존 단색 배킹 그대로(무회귀 폴백).
        [SerializeField] private Wassup.Data.BattleHudTrayConfig trayConfig;
        [SerializeField] private float flipHalfDuration = 0.14f;
        // hand-deal-in unit 0 — StS/HS 아치 부채 기하 + 스프링 추종.
        // dreamcatcher-hand-card-face unit 1 — 54 → 16: 겹침은 부채 감성만 남기고 이름·본문이
        // 항상 보이게(시인성 개편의 핵심 노브). ⚠ 씬(BattleScene)에 54 가 직렬화돼 있어
        // 코드 기본값만으로는 무효 — 씬 값도 16 으로 갱신해야 한다(unit 2).
        [SerializeField] private float cardOverlap = 16f;  // 카드 겹침(step = CardW - overlap)
        [SerializeField] private float arcHeight = 46f;    // 포물선 아치 높이(가운데 솟음)
        [SerializeField] private float rotMax = 10f;       // 바깥 카드 접선 회전(도)
        [SerializeField] private float handBaseY = 16f;    // 부채 밑단 y
        [SerializeField] private float springK = 14f;      // 스프링 추종 계수(클수록 빠름)
        // hand-deal-in unit 2 — 덱-드로우 딜(하단에서 곡선 상승 → 아치 안착).
        [SerializeField] private float dealStaggerSec = 0.05f;
        [SerializeField] private float dealDurationSec = 0.34f;
        [SerializeField] private float dealStartScale = 0.62f;
        [SerializeField] private float trayFadeSec = 0.12f;
        [SerializeField] private float dealRise = 220f;   // 하단 바깥 시작 깊이
        [SerializeField] private float dealTiltX = 50f;    // 누운 카드 → 세움(원근 ①)
        [SerializeField] private float clusterK = 0.3f;    // 시작 x 모임(덱 뭉침)
        // card-crumple-unfold unit 2 — 딜 안착과 동기로 구김이 풀림(살짝 늦게 끝, D3).
        [SerializeField] private float crumpleUnfoldSec = 0.6f;
        [SerializeField] private float textFadeSec = 0.18f;
        // hand-deal-in unit 4 — 퇴장 침강(딜의 거울: 하단 덱으로 InBack).
        [SerializeField] private float sinkDurationSec = 0.26f;
        [SerializeField] private float sinkStaggerSec = 0.04f;
        // hand-drag-clearance unit 0 — 조준 중에만 손패를 내려 큰 맵의 최하단 행을 드러낸다.
        // 210 = 카드 **헤더(이름) 띠만 남기는** 깊이(사용자 결정 2026-07-28). 바깥 카드 헤더
        // 하단이 화면 y 4 로 내려와 전 카드의 이름이 읽히는 마지막 지점이다. 이때 조준 중
        // 카드 top 은 342-210=132 라 가장 큰 맵의 보드 하단 모서리(167)보다 아래 — 최하단
        // 행 셀이 통째로 드러난다.
        [SerializeField] private float dragClearanceDrop = 210f;
        // 헤드룸과 같은 감성의 스프링(살짝 오버슈트 후 안착). 하강·복귀 공용 — target 만 바뀐다.
        [SerializeField] private float dragClearanceSpring = 320f;
        [SerializeField] private float dragClearanceDamping = 24f;
        // drag-cancel-affordance unit 1 — 취소 판정 rect 높이(패널 로컬, 하단 기준).
        // 310 = 조준 중 카드 부채의 실제 상단: handBaseY 16 + arcHeight 46 + CardH 230 × 확대 1.08.
        // 패널 배경(232)은 부채를 60px 못 덮어 "보이는 손패 ≠ 취소 영역" 이었다. 하강(210) 후
        // top = 32-210+310 = 132 라 가장 큰 맵의 보드 하단 모서리(167)보다 여전히 아래다
        // — hand-drag-clearance 가 연 최하단 행 부착을 되돌리지 않는다.
        [SerializeField] private float cancelZoneHeight = 310f;
        // use-flow unit 0 — A/B 토글: true = 구동작(손패 열림 전체 슬로모), false = 신동작
        // (카드를 잡은 press~release 동안만 슬로모). 매 프레임 폴링이라 Play 중 인스펙터에서
        // 토글하는 즉시 반영된다 — 넣다 뺐다 비교용(사용자 요구 2026-07-29).
        [SerializeField] private bool slomoOnOpen = false;
        // hand-deal-in unit 1 — 눌러서 들기(press-to-lift, 모바일: hover 아님).
        [SerializeField] private float focusRaise = 100f;
        [SerializeField] private float focusScale = 1.28f;
        [SerializeField] private float scatter = 42f;      // 양옆 밀어냄
        [SerializeField] private int scatterNeighbors = 2;
        // hand-deal-in unit 3 — 상시 미세 흔들림(무입력 역동감). 0 이면 정지.
        [SerializeField] private float idleBobY = 5f;
        [SerializeField] private float idleSwayX = 3f;
        [SerializeField] private float idleFreq = 1.6f;
        [SerializeField] private float idlePhase = 0.7f;
        // rev 4 — 카드 드래그 중 호버된 수비수의 스파인 틴트(포커스 대상 시각화).
        // rev 4-4 — 붉은색 계열로 시인성 상향(사용자 확정). 유일한 포커스 표시(타일 하이라이트 제거).
        [SerializeField] private Color unitHoverTint = new Color(1f, 0.28f, 0.22f, 1f);
        // hand-drag-tooltip unit 1 — 드래그 시작 시 성능 툴팁.
        // rev 5 (사용자 결정 2026-07-21) — 화면 상단 중앙 고정. rev 1 의 카드 우측 배치는
        // 손패와 같은 하단 대역이라 모바일 오른손 그립에서 손에 가렸다(실기기 확인).
        [SerializeField] private float tooltipWidth = 480f;
        // unit 4 — 40→24. 상단 여백 1px 은 툴팁 세로 예산 1px 과 등가라(보드 침범이
        // 그만큼 늘어난다), 가장자리 답답함을 감수하고 예산으로 돌렸다.
        [SerializeField] private float tooltipTopOffset = 24f; // 세이프에어리어 상단 모서리 여백
        // unit 6 — 실기기에서 19pt 본문이 눈에 안 들어온다(사용자 2026-07-21). 폰트를
        // 키우면 패널이 세로로 자라므로 카메라 pitch 로 상단 여백을 함께 확보한다.
        [SerializeField] private float tooltipHeaderFont = 27f;
        [SerializeField] private float tooltipBodyFont = 23f;
        [SerializeField] private float tooltipBobY = 6f;   // 플로팅 bob 진폭(카드 idle 문법)
        [SerializeField] private float tooltipBobX = 3f;
        [SerializeField] private float tooltipBobFreq = 1.2f;
        // selection-hand-attach unit 2 — 보드 탭 이동 허용치(px). 전화면 캐처는 IDragHandler 가
        // 없어 이동량 판정을 스스로 해야 한다(critic M2). raw 경로(DcInspectController)와 같은 계열.
        [SerializeField] private float boardTapMoveThreshold = 24f;
        // selection-hand-attach unit 3 — 즉발 거절 움찔(좌우 셰이크) 튜닝.
        [SerializeField] private float flinchStrengthX = 14f;
        [SerializeField] private float flinchDuration = 0.26f;
        [SerializeField] private float flinchFrequency = 14f;
        // unit 14 — 거절 시 카메라 킥. **아주 짧게**(카드 움찔 0.26s 보다 짧아 화면이 먼저
        // 튀고 카드가 이어 흔들린다). 0 이하면 킥 없음 — 거슬리면 여기만 0 으로 끈다.
        [SerializeField] private float rejectKickStrength = 0.45f;
        [SerializeField] private float rejectKickDuration = 0.1f;

        public enum HandState { UnitStrip, Hand }
        public HandState State { get; private set; } = HandState.UnitStrip;
        // hand-deal-in unit 0 — 딜/수렴 시퀀스도 전이 상태(드래그/토글 가드).
        public bool Transitioning => _flip != null || _dealSeq.isAlive;

        // unit 7 consumes these: card slots for drag sources + hand rect for
        // the cancel-region test.
        public RectTransform HandPanelRect => _panel != null ? (RectTransform)_panel.transform : null;

        // drag-cancel-affordance unit 1 — 취소 판정 rect. 패널 **자식**이라 하강(210px)을 자동
        // 승계한다(hand-drag-clearance 계약 1: 취소 rect 는 패널 위치가 단일 소유). 미생성 시
        // 패널로 폴백해 기존 동작을 유지한다 — 슬롯 쪽에 분기를 두지 않기 위해 여기서 흡수한다.
        public RectTransform CancelRect => _cancelZone != null ? _cancelZone : HandPanelRect;
        public IReadOnlyList<CardSlot> Slots => _slots;

        // Drag-slot service surface (unit 7).
        public DreamcatcherHandController Controller => handController;
        public Wassup.Bridge.BattleBridge Bridge => bridge;
        public Camera MainCamera => mainCamera != null ? mainCamera : (mainCamera = Camera.main);
        public AwakeningConfig Config => config;
        public Color UnitHoverTint => unitHoverTint;
        // rev 4-6 — StS식 유닛 타겟팅 화살표(카드 고정 + 카드→포인터 점선 아크).
        public DreamcatcherTargetArrow TargetArrow => _targetArrow;
        // dreamcatcher-attach-lockon — 부착 조준 포커스 연출 facade(dim/링/리티클/콜아웃).
        public DreamcatcherFocusPresenter Focus => _focus;
        public DreamcatcherFocusConfig FocusConfig => focusConfig;

        // ── selection-hand-attach unit 1 — 선택 파트너 표면 ───────────────────
        // 즉발 부착 대상(= 인스펙트가 선택한 유닛). 선택 상태의 **소유자는
        // DcInspectController** 이고 뷰는 전달만 받는다(계약 1) — 여기서 재판정하지 않는다.
        // 카드 슬롯(unit 3)이 읽어 탭 즉발 타겟으로 쓴다.
        public Entity SelectionTarget { get; private set; } = Entity.Null;
        // 오픈 모드는 **파생값**이다 — 별도 상태로 저장하면 이중 상태가 된다(계약 8).
        public bool InSelectionMode => SelectionTarget != Entity.Null;

        // first-session-tutorial unit 17 rev — 선택 대상이 **잡혔다**는 신호.
        //
        // `HandOpened` 로는 이 사건을 못 잡는다: 그건 닫힘→열림 전이에서만 발화하는데,
        // 항아리로 이미 손패를 연 상태에서 유닛을 탭하면 OpenForSelection 이 `State == Hand`
        // 라 no-op 이라(계약: 선택 전환은 재딜 없음) Open() 도 HandOpened 도 돌지 않는다.
        // 손패가 닫혀 있었으면 이 이벤트는 State 가 아직 UnitStrip 일 때 와서 구독자가 그냥
        // 흘려보내고, 곧이어 HandOpened 가 같은 일을 한다 — 두 신호는 상보적이다.
        public event System.Action SelectionTargetSet;

        // active-ally-zone unit 3 — 선택 중 Active 조준이 시작됐다(= 선택을 놓아야 한다).
        // 슬롯 OnBeginDrag 이 발화하고 DcInspectController 가 구독해 **선택만** 놓는다
        // (손패·조준 슬로모는 유지). `AimingNow()` 폴링으로 대체하지 말 것 — 그 판정엔 배치
        // 방향지정 조준이 포함돼 아무도 요청하지 않은 "방향지정 중 선택 해제" 가 생긴다.
        public event System.Action SelectionReleasedForAim;

        public void NotifySelectionReleasedForAim() => SelectionReleasedForAim?.Invoke();

        public void SetSelectionTarget(Entity target)
        {
            SelectionTarget = target;
            if (target != Entity.Null) SelectionTargetSet?.Invoke();
        }

        public void ClearSelectionTarget()
        {
            SelectionTarget = Entity.Null;
            _pendingSelectionOpen = false; // 선택이 끝났으면 미뤄둔 오픈도 취소
        }

        // 선택 기인 오픈(사용자 결정 1: 항상). 컨트롤러가 SetSelectionTarget 직후 부른다.
        // Transitioning 중이면 무시가 아니라 **래치**다(critic H5): 침강(0.26s+stagger)+strip
        // fold(0.14s) 창에서 Open() 은 mash guard 에 막히는데, 그 사이 유닛을 탭하면 선택은
        // 다 되고 손패만 없는 상태로 굳고 항아리 탭(OnToggled)도 같은 가드라 복구가 안 된다.
        // "빈 보드 탭으로 닫고 곧바로 다른 유닛 탭" 은 이 feature 가 노리는 연속 조작이다.
        public void OpenForSelection()
        {
            if (State == HandState.Hand) { _pendingSelectionOpen = false; return; }
            if (Transitioning) { _pendingSelectionOpen = true; return; }
            Open(selectionDriven: true);
        }

        // selection-hand-attach unit 2 — 손패 오픈 중 보드 탭. 구독자(DcInspectController)가
        // 유닛 픽/빈 보드를 판정해 선택 전환 또는 동시 해제로 라우팅한다(사용자 결정 2·3).
        public event System.Action<Vector2> BoardTapped;

        private void OnBoardTapCaught(Vector2 screenPos)
        {
            if (State != HandState.Hand) return;
            // 구독자가 없으면(미배선/테스트) 기존 바깥 탭 dismiss 로 폴백 — 항아리 단독
            // 사용성을 잃지 않는다(orb-dock unit 4 계약).
            if (BoardTapped != null) BoardTapped.Invoke(screenPos);
            else Close();
        }

        // first-session-tutorial unit 15 — 첫 판 각성 봉인 릴레이. 사실은 AwakeningGaugeView 가
        // 소유하고(튜토리얼이 SetSuppressed 로 세운다) 여기는 옮기기만 한다 — DcInspectController
        // 가 gaugeView 를 직접 참조하려면 신규 씬 배선이 필요한데 BattleScene 을 저장할 수 없다.
        //
        // 이름을 `AwakeningSuppressed` 로 두지 않는다: `Suppressed` 는 "항아리 표시를 끈다"는
        // 소비자 어휘라, 선택을 막는 쪽에서 읽으면 왜 막히는지가 안 보인다. 사실의 이름으로 둔다.
        public bool AwakeningSealedThisMatch => gaugeView != null && gaugeView.IsSuppressed;

        // 컨트롤러가 뷰를 닫는 **유일한 공개 창구**(계약 7 — Close() 는 private 유지).
        public void CloseFromSelection()
        {
            _pendingSelectionOpen = false; // 닫기 의도가 미뤄둔 오픈을 취소한다
            if (State == HandState.UnitStrip) return;
            Close();
        }

        // card-fly-to-target-absorb unit 0 — 커밋 성공 시 손패 카드가 유닛으로
        // 날아가 찰싹 흡수. 발사점/스프라이트는 소비 전 호출부에서 캡처된 값.
        // 타겟은 유닛 뷰 앵커 Transform 을 매프레임 추적(행진 중에도 안착).
        public void FlyCardToUnit(Vector3 startUiWorld, Vector2 ghostSize, Sprite face, Entity host)
        {
            EnsureFlightPresenter();
            if (_flightPresenter == null) return;
            var b = bridge;
            var h = host;
            _flightPresenter.Fly(startUiWorld, ghostSize, face, MainCamera,
                () => (b != null && b.TryGetUnitViewAnchor(h, out var tr) && tr != null)
                    ? tr.position : (Vector3?)null,
                worldPos => FireAbsorbImpact(h, worldPos));
        }

        // card-fly-to-target-absorb unit 2 — 타일/포탈 타겟(Active 스킬 셀 캐스트). 고정 셀
        // view 중심으로 비행(추적 불필요) → 타일 월드에서 같은 찰싹(유닛 없음 → 펀치/플래시 생략).
        public void FlyCardToCell(Vector3 startUiWorld, Vector2 ghostSize, Sprite face, Vector2Int cell)
        {
            EnsureFlightPresenter();
            if (_flightPresenter == null) return;
            var b = bridge;
            var c = cell;
            _flightPresenter.Fly(startUiWorld, ghostSize, face, MainCamera,
                () => b != null ? (Vector3?)b.GridCellToViewCenter(c) : (Vector3?)null,
                FireAbsorbImpactWorld);
        }

        // card-fly-to-target-absorb unit 1 — 닿는 순간 묵직 반응(타겟 월드에서). 메커닉이
        // 게이트(bridge) 경유로 구동. 유닛 케이스는 펀치/흰플래시 추가, 그 외 공통 월드 반응.
        private void FireAbsorbImpact(Entity host, Vector3 worldViewPos)
        {
            if (bridge != null && bridge.TryGetUnitView(host, out var view) && view != null)
            {
                view.PlayPunch();
                view.FlashWhite();
            }
            FireAbsorbImpactWorld(worldViewPos);
        }

        // 공통 월드 반응: 링/버스트 + 찰싹 SFX + 카메라 킥(유닛 유무 무관).
        private void FireAbsorbImpactWorld(Vector3 worldViewPos)
        {
            bridge?.SpawnCardAbsorbVfx(worldViewPos);
            if (SoundManager.Instance != null) SoundManager.Instance.PlayCardAbsorb();
            EnsureCameraDirector()?.Kick();
        }

        // camera-direction unit 0 — 킥은 CameraDirector 채널로 승계. Director 는 config 씬 배선이
        // 필수라 런타임 AddComponent fallback 이 성립하지 않는다 — 미배선이면 1회 경고 + 킥 생략.
        private Wassup.Presentation.CameraDirector _cameraDirector;
        private bool _cameraDirectorWarned;

        private Wassup.Presentation.CameraDirector EnsureCameraDirector()
        {
            if (_cameraDirector != null) return _cameraDirector;
            if (_cameraDirectorWarned) return null; // miss 캐시 — 미배선 씬에서 임팩트마다 재탐색 방지
            var cam = MainCamera;
            if (cam == null) return null;
            _cameraDirector = cam.GetComponent<Wassup.Presentation.CameraDirector>();
            if (_cameraDirector == null)
            {
                Debug.LogWarning("[DreamcatcherHandView] CameraDirector 미배선 — 카메라 킥 생략.", this);
                _cameraDirectorWarned = true;
            }
            return _cameraDirector;
        }

        private void EnsureFlightPresenter()
        {
            if (_flightPresenter != null) return;
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;
            canvas = canvas.rootCanvas != null ? canvas.rootCanvas : canvas;
            var go = new GameObject("CardAbsorbFlight", typeof(RectTransform));
            go.transform.SetParent(canvas.transform, false);
            _flightPresenter = go.AddComponent<CardAbsorbFlightPresenter>();
            _flightPresenter.Init(canvas);
        }

        public class CardSlot
        {
            public GameObject root;
            public RectTransform rect;
            public Image frame;
            public Image art;
            public UiCardFaceMesh face;  // card-crumple-unfold — art = 서브디바이드 크럼플 그래픽
            public GameObject nameTag; // hand-card-face unit 1 — 헤더 밴드(타입 색) 중앙 이름 영역
            public TextMeshProUGUI nameLabel;
            public CanvasGroup nameGroup; // 크럼플 중 숨겼다 펴짐 완료 시 페이드-인(TMP 비크럼플)
            public GameObject tagChip;    // hand-card-face unit 1 — 우상단 대상+역할 칩
            public Image tagBg;
            public TextMeshProUGUI tagLabel;
            public CanvasGroup tagGroup;
            public TextMeshProUGUI bodyLabel; // hand-card-face unit 1 — 하단 효과 본문
            public CanvasGroup bodyGroup;
            public GameObject costBadge;
            public TextMeshProUGUI costLabel;
            public CanvasGroup costGroup;
            public CanvasGroup group;
            public DreamcatcherCardDragSlot dragSlot;
            public Vector2 homePos;        // unit 7 — restore anchor after drag/cancel
            public float homeRotZ;
            // hand-deal-in unit 0 — 스프링 목표(호버/복귀). base = home.
            public Vector2 targetPos;
            public float targetRotZ;
            public float targetScale = 1f;
            public int entryId = -1;       // -1 = empty slot
            public DreamcatcherCard card;
            public bool usable;
            // use-flow unit 1 — 사용 직후 1장 재딜인 트윈이 rect 를 소유 중(스프링/집기 제외).
            public bool redealing;
            // selection-hand-attach unit 3 — 즉발 거절 움찔 트윈이 rect 를 소유 중.
            // redealing 과 같은 이유의 소유 플래그: SpringSlots 가 동시 writer 라 안 막으면
            // 셰이크 진폭이 매 프레임 홈으로 끌려가 뭉개진다(critic M7).
            public bool flinching;
        }

        // dreamcatcher-hand-card-face unit 1 — 카드 면 기하. 본문 16pt floor(계약 8) 예산
        // 확보를 위해 172×200 → 184×230 (5장×184=920 ≤ 패널 980, README rev 1).
        private const float CardW = 184f, CardH = 230f, HeaderH = 64f;
        // 타입×무의식별 투톤 face 스프라이트 캐시(2x 베이크 — 코너/보더 선명도).
        private readonly Dictionary<(CardType type, bool subconscious), Sprite> _faceCache =
            new Dictionary<(CardType, bool), Sprite>();
        private Sprite _chipSprite; // 흰 라운드 칩 공용 스프라이트(틴트는 Image.color 가 담당)

        private GameObject _panel;
        // hand-drag-clearance unit 0 — 하강의 기준선. BuildCanvas 가 실제 배치한 값을 캡처하고
        // (trayConfig 를 다시 읽지 않는다 — 기준은 하나) 리셋/복귀가 전부 이 값으로 돌아온다.
        private float _panelBaseY;
        private float _clearanceOffset;  // 0 = 기준선, -dragClearanceDrop = 완전 하강
        private float _clearanceVel;
        // drag-cancel-affordance unit 1 — 취소 판정 rect(패널 자식, 그림 없음) + 그 안의 힌트 배너.
        private RectTransform _cancelZone;
        // dreamcatcher-orb-dock unit 4 — 손패 오픈 중 보드 영역 탭으로 물러나기(바깥 탭 dismiss).
        private GameObject _dismissCatcher;
        // selection-hand-attach unit 1 — 전이(침강/딜) 중에 들어온 선택 기인 오픈 요청.
        // 전이가 끝나는 첫 프레임에 소비된다(TickPendingSelectionOpen).
        private bool _pendingSelectionOpen;
        private Image _backing;        // tray frame — deal 무대(카드와 별개로 페이드 인)
        private float _backingAlpha = 1f;
        private readonly List<CardSlot> _slots = new List<CardSlot>();
        private bool _built;
        private Coroutine _flip;
        private Sequence _dealSeq;      // hand-deal-in — 딜/수렴 트윈(teardown 에서 Stop)
        // use-flow unit 1 — 사용 직후 1장 재딜인 전용(= Transitioning 비게이트, 연속 사용 비차단).
        private Sequence _redealSeq;
        private readonly List<int> _prevIds = new List<int>();       // OnCardUsed 포즈 diff 용
        private readonly List<Vector3> _prevPose = new List<Vector3>(); // xy=anchoredPos, z=rotZ
        private int _focusIndex = -1;  // hand-deal-in unit 1 — press-lift 대상 슬롯
        private TimeLease _slomoLease;
        private bool _slomoActive; // use-flow unit 0 — TickSlomo 에지 트리거 상태
        private DreamcatcherTargetArrow _targetArrow;
        private DreamcatcherFocusPresenter _focus; // dreamcatcher-attach-lockon
        private CardAbsorbFlightPresenter _flightPresenter; // card-fly unit 0 — lazy, 캔버스 루트 하위
        // Recovered-refresh deferral: rebinding slots mid-drag would snap the
        // floating card home and swap its entryId under the pointer.
        private bool _refreshQueued;
        // hand-drag-tooltip unit 1 — 성능 툴팁 위젯(패널 형제, 전 Graphic 비레이캐스트).
        private GameObject _tooltipRoot;
        private RectTransform _tooltipRect;
        private CanvasGroup _tooltipGroup;
        private TextMeshProUGUI _tooltipHeader;
        private TextMeshProUGUI _tooltipBody;
        private bool _tooltipVisible;
        private string _briefingStatus; // unit 3 — 상태 줄 캐시(매프레임 재레이아웃 방지)
        private Vector2 _tooltipBasePos; // rev 1 — bob 은 base 에 가산(누적 금지)
        private const float TooltipLerpK = 16f;   // 페이드/스케일 추종(스프링 감성)
        // unit 4 — 보드 가림을 줄이려 패딩/간격을 조였다(14→10, 8→4). 세로 예산이
        // 빡빡한 위젯이라 여백보다 본문 줄 수가 우선이다.
        private const float TooltipPad = 10f;
        private const float TooltipHeaderGap = 4f;
        private const float TooltipHiddenScale = 0.92f;

        private void Awake()
        {
            BuildCanvas();
            if (_panel != null) _panel.SetActive(false);
        }

        // dreamcatcher-orb-dock unit 1 — 항아리 독을 트레이 우측에 정렬시키기 위해 트레이
        // RectTransform 을 넘긴다. Start 는 모든 Awake 이후라 DefenderSelector.PanelGO 존재 보장
        // (씬 배선 없이 기존 gaugeView·defenderSelector 참조만으로 연결).
        private void Start()
        {
            if (gaugeView != null && defenderSelector != null && defenderSelector.PanelGO != null)
                gaugeView.BindTray(defenderSelector.PanelGO.transform as RectTransform);
        }

        private void OnEnable()
        {
            if (gaugeView != null) gaugeView.Toggled += OnToggled;
            if (handController != null)
            {
                handController.HandChanged += OnHandChanged;
                handController.GaugeChanged += OnGaugeChangedRefreshDim;
            }
            if (GameManager.Instance != null)
                GameManager.Instance.PhaseChanged += OnPhaseChanged;
        }

        private void OnDisable()
        {
            if (gaugeView != null) gaugeView.Toggled -= OnToggled;
            if (handController != null)
            {
                handController.HandChanged -= OnHandChanged;
                handController.GaugeChanged -= OnGaugeChangedRefreshDim;
            }
            if (GameManager.Instance != null)
                GameManager.Instance.PhaseChanged -= OnPhaseChanged;
            ForceClose(); // idempotent — never leak the slomo lease
        }

        private void OnToggled()
        {
            if (Transitioning) return; // mash guard
            // Toggling mid-interaction drops any drag/portal-aim first (no spend).
            CancelAllCardInteraction();
            if (State == HandState.UnitStrip) { Open(selectionDriven: false); return; }
            // selection-hand-attach unit 9 — 선택 중이라면 각성 버튼은 "손패만 접기" 가 아니라
            // "그만하기" 다(사용자 결정 2026-07-30). 손패만 걷으면 줌+슬로모가 걸린 채 할 일
            // 없는 선택이 남아 빈 보드를 따로 탭해야 풀린다.
            //
            // 발화는 Close() **앞** — 뒤에 두면 Close() 의 FocusCleared 가 리티클을 재주장한
            // 직후 구독자가 선택을 닫아 1프레임 깜빡임 + lease churn 이 생긴다(unit 8 과 동일).
            if (InSelectionMode) SelectionDismissed?.Invoke();
            Close(); // 구독자가 CloseFromSelection 으로 이미 걷었으면 no-op
        }

        private void Update()
        {
            TickTooltip(); // hand-drag-tooltip unit 1 — 상태 무관(퇴장 페이드 완주)
            TickPendingSelectionOpen(); // selection-hand-attach unit 1 — 상태 무관(닫힘 중에도 돈다)
            if (State != HandState.Hand) return;
            // hand-drag-tooltip unit 6 — 손패가 열린 동안만 카메라 헤드룸을 요청한다.
            // 피드 주도 채널이라 닫힘/페이즈 이탈/파괴 어느 경로든 별도 해제 호출 없이
            // 홈 pitch 로 스프링 복귀한다(State 가 UnitStrip 이 되는 순간 피드가 끊긴다).
            EnsureCameraDirector()?.SetHandHeadroom();
            // held = "카드를 잡고 있다" 단일 판정 (press-lift focus / 드래그 / 포탈 2탭 대기).
            // 슬로모(use-flow unit 0)와 손패 하강(hand-drag-clearance)이 이 한 신호로 움직인다:
            // press = 슬로모 ON + 하강, release = OFF + 복귀.
            bool held = _focusIndex >= 0 || AnyInteractionActive();
            TickSlomo(held);
            TickHandClearance(held);
            SpringSlots(); // hand-deal-in unit 0 — 슬롯 target 으로 매프레임 추종
            // ESC = cancel rule (spec unit 7 §6): drop any drag/portal-aim, no spend.
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null || !kb.escapeKey.wasPressedThisFrame) return;
            CancelAllCardInteraction();
        }

        // use-flow unit 0 — 슬로모 리스를 held 신호에 묶는다(에지 트리거). slomoOnOpen=true 면
        // 구동작(손패 열림 전체 슬로모)으로 폴백 — Play 중 인스펙터 토글 즉시 반영(A/B 비교용).
        // 매 프레임 재획득이 아니라 상태가 바뀌는 프레임에만 획득/해제한다.
        private void TickSlomo(bool held)
        {
            bool want = slomoOnOpen || held;
            if (want == _slomoActive) return;
            _slomoActive = want;
            _slomoLease.Dispose();
            if (want)
            {
                float scale = config != null ? Mathf.Max(0.01f, config.slomoTimeScale) : 0.3f;
                _slomoLease = TimeManager.Instance.Request(TimeDomain.Battle, scale, priority: 50);
            }
        }

        // hand-drag-clearance unit 0 — 조준 중에만 손패 패널을 내려 큰 맵의 최하단 행을
        // 드러낸다. 카드 슬롯은 패널 자식이고 취소 판정(HandPanelRect)도 패널 기준이라,
        // 패널 y 하나만 움직이면 카드·취소영역이 함께 따라온다(계약 1·3).
        //
        // 하강·복귀 모두 스프링이다(헤드룸과 같은 감성 — 살짝 오버슈트 후 안착).
        //
        // ⚠ 하강 중 카드가 손가락에서 미끄러지는 함정: ActiveTile/ActivePortal 은 카드가
        // 포인터를 따라가는데 그 재고정이 `Slot.rect.position = screenPos`(스크린 좌표)이고
        // OnDrag 는 포인터가 **움직일 때만** 호출된다. 패널이 그 사이 내려가면 자식인 카드도
        // 딸려 내려가 손가락에서 최대 dragClearanceDrop 만큼 떨어진다(사거리 프리뷰도 함께
        // 어긋난 채 정지). 그래서 패널을 움직이는 순간 그 슬롯의 화면 위치를 보존한다 —
        // 이 보정이 있어야 부드러운 하강과 카드 추종이 양립한다.
        //
        // 상태(IsDragging/IsPortalAiming)는 드래그 슬롯이 소유하고 여기서는 읽기만 한다:
        // 커밋·취소·ESC·닫힘·페이즈 이탈 어느 경로로 끝나든 별도 해제 호출 없이 복귀한다(계약 6).
        private void TickHandClearance(bool held)
        {
            if (_panel == null) return;
            // 카드를 누른 순간(press) 내려가고 뗀 순간 올라온다. press-lift focus 와 드래그가
            // 한 조건으로 이어지는 이유: OnBeginDrag 가 `_dragging = true` 를 `SetFocus(-1)`
            // **보다 먼저** 실행하므로(같은 콜백) 전환에 빈 프레임이 없다. 덕분에 단순 탭 ·
            // 드래그 후 미부착 · 부착 성공이 전부 "포인터를 뗀 순간 복귀" 로 통일된다.
            // 예외는 포탈 2탭 대기(IsPortalAiming) 하나 — 손을 뗐어도 출구를 보드에서
            // 골라야 하므로 하강을 유지한다. (held 판정은 Update 가 계산해 슬로모와 공유.)
            float target = held ? -dragClearanceDrop : 0f;
            // 언더댐핑이라 target 을 스쳐 지난다 — 안착은 변위와 속도를 함께 본다.
            if (Mathf.Abs(_clearanceOffset - target) < 0.05f && Mathf.Abs(_clearanceVel) < 0.05f)
            {
                if (_clearanceOffset == target) return;
                _clearanceOffset = target;
                _clearanceVel = 0f;
            }
            else
            {
                KeyringSim.SpringStep(ref _clearanceOffset, ref _clearanceVel, target,
                    dragClearanceSpring, dragClearanceDamping, 0f, Time.deltaTime);
            }
            ApplyClearanceOffset();
        }

        private void ApplyClearanceOffset()
        {
            // active-dreamcatcher-tile-aim unit 1 — 포인터 추종 카드가 사라져(모든 조준이
            // 카드를 손패에 두고 화살표로 겨눈다) 패널 이동 전 위치를 보존할 슬롯도 없다.
            var rt = (RectTransform)_panel.transform;
            var p = rt.anchoredPosition;
            rt.anchoredPosition = new Vector2(p.x, _panelBaseY + _clearanceOffset);
        }

        // 하강 상태의 하드 리셋. Close() 에는 두지 않는다 — Close 는 패널을 감추지 않고
        // 침강(StartSink)만 시작하는데, 부착 성공 시 HandChanged 가 동기로 발화해 commit()
        // 안에서 Close 가 FlyCard 보다 **먼저** 돌기 때문에, 거기서 리셋하면 가장 흔한 성공
        // 경로에서 손패가 위로 튄 뒤 가라앉고 고스트 발사점과 어긋난다. 대신 패널이 실제로
        // 꺼지거나 재구성되는 지점(Open / ForceClose / OnSinkComplete)에서만 되돌린다.
        private void ResetHandClearance()
        {
            if (_panel == null) return;
            _clearanceOffset = 0f;
            _clearanceVel = 0f;
            var rt = (RectTransform)_panel.transform;
            var p = rt.anchoredPosition;
            if (!Mathf.Approximately(p.y, _panelBaseY)) rt.anchoredPosition = new Vector2(p.x, _panelBaseY);
        }

        // Each card eases toward its target (arc base, or press-lifted in unit 1),
        // with a small idle bob/sway (unit 3) added for non-focused cards. Skips
        // slots owned by a transition (deal/sink/flip) or an active drag — those
        // write the rect directly. Realtime dt (timeScale=1, TimeManager domain).
        private void SpringSlots()
        {
            if (Transitioning) return;
            float a = 1f - Mathf.Exp(-Mathf.Max(0.01f, springK) * Time.deltaTime);
            float now = Time.time;
            for (int i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                if (slot.entryId < 0 || OwnedByInteraction(slot) || slot.redealing || slot.flinching) continue;
                // idle 흔들림은 눌러서 든 카드(focus)만 제외 — 그 카드는 안정적으로 들려 있어야.
                Vector2 eff = slot.targetPos;
                if (i != _focusIndex)
                {
                    float ph = now * idleFreq + i * idlePhase;
                    eff += new Vector2(Mathf.Sin(ph * 0.7f) * idleSwayX, Mathf.Sin(ph) * idleBobY);
                }
                var rt = slot.rect;
                rt.anchoredPosition = Vector2.Lerp(rt.anchoredPosition, eff, a);
                float z = Mathf.LerpAngle(rt.localEulerAngles.z, slot.targetRotZ, a);
                rt.localEulerAngles = new Vector3(0f, 0f, z);
                float s = Mathf.Lerp(rt.localScale.x, slot.targetScale, a);
                rt.localScale = new Vector3(s, s, 1f);
            }
        }

        // ── press-to-lift focus (hand-deal-in unit 1, mobile) ─────────────────
        // Drag slots report pointer DOWN/UP (not hover — no hover on touch).
        // Focus only writes slot targets — the spring reads them — so press-lift,
        // idle, drag, and deal all share one motion model.

        public void SetFocus(int index)
        {
            // 손패 상태·전이·다른 카드 드래그 중이 아니고, 실제 카드가 든 슬롯일 때만 focus.
            if (State != HandState.Hand || Transitioning || AnyInteractionActive()) index = -1;
            else if (index >= 0 && (index >= _slots.Count || _slots[index].entryId < 0
                || _slots[index].redealing)) index = -1;
            if (_focusIndex == index) return;
            _focusIndex = index;
            ApplyFocusTargets();
            // deck-sfx — 카드 집기(press-to-lift). 유효 슬롯 focus 시에만(-1=해제는 무음).
            if (index >= 0) SoundManager.Instance?.PlayCardPickup();
        }

        // Release only clears if this slot is still the focused one (overlapping
        // cards can fire down(next) before up(prev)).
        public void ClearFocus(int index)
        {
            if (_focusIndex == index) SetFocus(-1);
        }

        private void ApplyFocusTargets()
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                bool owned = OwnedByInteraction(slot);
                if (!owned) slot.rect.SetSiblingIndex(i); // base z-order (right over left)
                if (slot.entryId < 0 || owned) continue;

                if (i == _focusIndex)
                {
                    slot.targetPos = slot.homePos + new Vector2(0f, focusRaise);
                    slot.targetRotZ = 0f; // straighten
                    slot.targetScale = slot.usable ? focusScale : 1.06f; // dim 카드는 살짝만
                }
                else
                {
                    float push = 0f;
                    if (_focusIndex >= 0)
                    {
                        int delta = i - _focusIndex, ad = Mathf.Abs(delta);
                        if (ad >= 1 && ad <= scatterNeighbors) push = Mathf.Sign(delta) * scatter / ad;
                    }
                    slot.targetPos = slot.homePos + new Vector2(push, 0f);
                    slot.targetRotZ = slot.homeRotZ;
                    slot.targetScale = 1f;
                }
            }
            if (_focusIndex >= 0 && _focusIndex < _slots.Count)
            {
                var h = _slots[_focusIndex];
                if (!OwnedByInteraction(h)) h.rect.SetAsLastSibling(); // 눌린 카드가 최상단
            }
        }

        private void CancelAllCardInteraction()
        {
            foreach (var slot in _slots)
                if (slot.dragSlot != null && (slot.dragSlot.IsDragging || slot.dragSlot.IsPortalAiming))
                    slot.dragSlot.CancelDrag();
        }

        // ── drag-cancel-affordance unit 1 — 취소 존 ──────────────────────────────
        // 판정 rect 는 패널 자식이라 하강을 자동 승계한다(계약 2). **그림이 없다**(Image 없음) —
        // rev3 에서 힌트 배너를 지웠고, 취소 예고는 상단 브리핑 상태 줄이 단독으로 담당한다.
        private void BuildCancelZone(RectTransform panelRect)
        {
            var zoneGO = new GameObject("CancelZone", typeof(RectTransform));
            zoneGO.transform.SetParent(panelRect, false);
            _cancelZone = (RectTransform)zoneGO.transform;
            _cancelZone.anchorMin = new Vector2(0.5f, 0f);
            _cancelZone.anchorMax = new Vector2(0.5f, 0f);
            _cancelZone.pivot = new Vector2(0.5f, 0f);
            _cancelZone.anchoredPosition = Vector2.zero;
            _cancelZone.sizeDelta = new Vector2(panelRect.sizeDelta.x, cancelZoneHeight);
        }

        // rev3 (사용자 결정 2026-07-30) — 손패를 덮던 취소 힌트 배너를 **삭제**했다. 조준 중 상단
        // 중앙 브리핑이 이미 `여기서 놓으면 취소` 를 상시 표시하므로(StatusFor 의 insideHand 분기)
        // 배너는 같은 문장을 손패 위에 한 번 더 그리는 중복이었고, 카드 이름 띠를 가렸다.
        // 취소 **판정** rect(_cancelZone)는 그대로 남는다 — 지워진 건 표면뿐이다.

        private bool AnyInteractionActive()
        {
            foreach (var slot in _slots)
                if (OwnedByInteraction(slot)) return true;
            return false;
        }

        // hand-deal-in — 이 슬롯을 드래그/포탈-조준이 소유하면 스프링/호버가 손대지 않는다.
        private static bool OwnedByInteraction(CardSlot slot) =>
            slot.dragSlot != null && (slot.dragSlot.IsDragging || slot.dragSlot.IsPortalAiming);

        // ── unit 7 drag-slot services ────────────────────────────────────────

        public bool CanStartDrag(int index)
        {
            if (State != HandState.Hand || Transitioning) return false;
            if (AnyInteractionActive()) return false; // one interaction at a time
            if (index < 0 || index >= _slots.Count) return false;
            var slot = _slots[index];
            return slot.entryId >= 0 && slot.usable && !slot.redealing;
        }

        // hand-drag-tooltip rev 4 — press 툴팁 노출 판정(usable 무관, dim 카드 포함):
        // CanStartDrag 에서 usable 요구만 뺀 것. 타 인터랙션 중이면 불가(드래그
        // 중인 카드의 툴팁을 덮어쓰면 안 된다).
        public bool CanPeek(int index)
        {
            if (State != HandState.Hand || Transitioning) return false;
            if (AnyInteractionActive()) return false;
            if (index < 0 || index >= _slots.Count) return false;
            var slot = _slots[index];
            return slot.entryId >= 0 && slot.card != null && !slot.redealing;
        }

        // selection-hand-attach unit 3 — 즉발 거절 피드백: 카드가 좌우로 움찔(고개 젓기). 부착은
        // 일어나지 않고 차감도 없다는 것을 형태로 전한다(사유 문구는 기존 브리핑 채널이 담당).
        // 트윈이 rect 를 소유하는 동안 flinching 으로 스프링을 물러나게 한다(critic M7) — 끝나면
        // 플래그만 내리고 위치는 스프링이 targetPos(=home)로 되돌린다.
        public void FlinchSlot(int index)
        {
            if (index < 0 || index >= _slots.Count) return;
            var slot = _slots[index];
            if (slot.entryId < 0 || slot.rect == null) return;
            // 드래그/재딜인이 이미 rect 를 쥐고 있으면 끼어들지 않는다.
            if (OwnedByInteraction(slot) || slot.redealing || slot.flinching) return;
            slot.flinching = true;
            var captured = slot;
            Tween.ShakeLocalPosition(slot.rect, new Vector3(flinchStrengthX, 0f, 0f),
                    flinchDuration, frequency: flinchFrequency)
                .OnComplete(() => captured.flinching = false);
            SoundManager.Instance?.PlayCardReturn(); // 거절 = 카드가 제자리로(취소와 같은 계열음)
            // unit 14 — 아주 짧은 카메라 킥을 얹는다. 카드만 움찔하면 손가락 밑이라 놓치기 쉬운데,
            // 화면 전체가 한 번 튀면 "안 먹혔다"가 주변시로도 읽힌다. FeedbackKick 은 앰비언트
            // 토글에 묶이지 않는다(Kick 은 묶여서 현재 에셋에선 no-op 이다).
            EnsureCameraDirector()?.FeedbackKick(rejectKickStrength, rejectKickDuration);
        }

        public void RestoreSlotHome(int index)
        {
            if (index < 0 || index >= _slots.Count) return;
            var slot = _slots[index];
            // use-flow unit 1 fix(마감 리뷰 M2) — 재딜인 트윈이 소유 중인 슬롯은 건드리지
            // 않는다. 커밋 꼬리(EndInteraction→RestoreSlotHome)가 commit() 동기 발화로 방금
            // 시작된 재딜인을 스냅으로 덮고 redealing 잠금까지 풀던 결함(맨 오른쪽 카드
            // 부착 = _index 가 곧 재딜인 슬롯이라 매번 재현). 트윈은 home 에 안착하고
            // 콜백이 잠금을 푼다 — 강제 종료 경로는 StopDeal 의 Complete 가 콜백까지 돌린다.
            if (slot.redealing) return;
            slot.rect.anchoredPosition = slot.homePos;
            slot.rect.localEulerAngles = new Vector3(0f, 0f, slot.homeRotZ);
            slot.rect.localScale = Vector3.one; // rev 4-6 — 화살표 모드 확대 복원
            // hand-deal-in unit 0 — 스프링 목표도 base 로 복원(호버/스프링 재개 시 튐 방지).
            slot.targetPos = slot.homePos;
            slot.targetRotZ = slot.homeRotZ;
            slot.targetScale = 1f;
            // card-crumple-unfold unit 2 — 크럼플은 딜에서만: 평면(Unfold=1)+텍스트 표시 복원.
            if (slot.face != null) slot.face.Unfold = 1f;
            if (slot.nameGroup != null) slot.nameGroup.alpha = 1f;
            if (slot.costGroup != null) slot.costGroup.alpha = 1f;
            if (slot.tagGroup != null) slot.tagGroup.alpha = 1f;   // hand-card-face unit 1
            if (slot.bodyGroup != null) slot.bodyGroup.alpha = 1f;
        }

        // Drag slots call this at every interaction end (commit/cancel) so a
        // Recovered refresh deferred mid-drag can land.
        public void NotifyInteractionEnded()
        {
            if (_refreshQueued && !AnyInteractionActive()) { _refreshQueued = false; Refresh(); }
            InteractionEnded?.Invoke(); // selection-hand-attach unit 4 — 리티클 재주장 트리거 ①
        }

        // ── selection-hand-attach unit 4 — 포커스 세션 핸드오프 신호 ───────────
        // 프레젠터는 단일 세션이다(계약 6). 카드 조준이 선택 리티클을 대체하는 것은 정상이고,
        // 그 세션이 끝나면 선택이 살아 있는 한 리티클을 **재주장**해야 한다.
        //
        // 트리거가 둘인 이유: Focus.End() 호출처가 슬롯 종료 깔때기 하나가 아니다(critic H2).
        //  ① InteractionEnded — 슬롯의 커밋/취소/ESC(NotifyInteractionEnded 깔때기)
        //  ② FocusCleared — 뷰의 Close()/ForceClose() 하드 클리어(항아리 토글·0장 자동 닫힘·
        //     페이즈 이탈·Reset). 슬롯 OnDisable(침강 완료마다) 은 항상 이 둘 뒤에 오므로 함께 덮인다.
        //
        // FocusCleared 는 반드시 End() **뒤에** 발화한다 — 앞에서 발화하면 재주장한 리티클을
        // 바로 이어지는 End() 가 다시 지운다.
        public event System.Action InteractionEnded;
        public event System.Action FocusCleared;

        // selection-hand-attach unit 8 — 카드 사용 결과 쓸 수 있는 카드가 0장이 됐다(자동 닫힘
        // 확정). 구독자(DcInspectController)가 선택까지 풀어 기본 진행 상태로 되돌린다.
        // 발화 지점은 OnCardUsed 안 하나뿐 — 그래서 "사용이 실제로 있었다" 가 신호에 내포된다.
        public event System.Action UsableCardsExhausted;

        // selection-hand-attach unit 9 — 선택 중 각성 버튼(항아리) = "그만하기" 명시 신호.
        // unit 8 이 "자원이 바닥나서 끝났다" 라면 이쪽은 "사용자가 끝내겠다고 말했다" 다.
        // 발화 지점은 OnToggled 의 닫힘 분기 하나뿐(무선택 오픈은 기존 dismiss 그대로).
        public event System.Action SelectionDismissed;

        private void RaiseFocusCleared() => FocusCleared?.Invoke();

        // Battle/Placement 이탈 → 강제 클로즈 (critic H2). Placement 재진입 리셋은
        // HandChanged(Reset) 가 처리.
        private void OnPhaseChanged(GamePhase phase)
        {
            if (phase != GamePhase.Placement && phase != GamePhase.Battle)
                ForceClose();
        }

        private void OnHandChanged(DreamcatcherHandController.HandChangeReason reason)
        {
            switch (reason)
            {
                case DreamcatcherHandController.HandChangeReason.Reset:
                    ForceClose();
                    break;
                case DreamcatcherHandController.HandChangeReason.Used:
                    OnCardUsed(); // use-flow unit 1 — 유지/자동닫힘 분기(구 자동 닫힘 대체)
                    break;
                case DreamcatcherHandController.HandChangeReason.Recovered:
                    // A re-render mid-drag would snap the floating card home and
                    // rebind entryIds under the pointer — defer until it ends.
                    if (AnyInteractionActive()) _refreshQueued = true;
                    else Refresh(); // update only — no state change
                    break;
            }
        }

        private void OnGaugeChangedRefreshDim(int _)
        {
            if (State == HandState.Hand) RefreshUsability();
        }

        // use-flow unit 1 — 사용이 손패를 닫지 않는다(재열기 사이클 제거). 사용 가능 카드가
        // 남으면 유지: 잔류 카드는 옛 포즈에서 새 home 으로 스프링 슬라이드(위치 diff 는
        // entryId 로 판별 — 위치 기반이면 시프트된 카드가 전부 재딜인으로 오판된다), 새로
        // 들어온 카드만 1장 딜인(재장전 감각). 0장이면 자동 닫힘 — 재딜인/pop-in 없이 사용
        // 슬롯만 비우고 침강한다(계약 3: 딜인→침강 연쇄 금지).
        private void OnCardUsed()
        {
            var hand = handController.Hand();
            bool anyUsable = false;
            for (int i = 0; i < hand.Count; i++)
                if (handController.CanUse(hand[i].entryId)) { anyUsable = true; break; }

            if (!anyUsable)
            {
                foreach (var slot in _slots)
                {
                    if (slot.entryId < 0) continue;
                    bool still = false;
                    for (int h = 0; h < hand.Count; h++)
                        if (hand[h].entryId == slot.entryId) { still = true; break; }
                    if (!still) BindEmpty(slot); // 소모된 카드 재표시 금지(고스트가 이미 날아감)
                }
                // selection-hand-attach unit 8 — 카드를 **쓴 결과** 더 쓸 게 없어졌다 → 선택까지
                // 풀어 기본 진행 상태로 돌린다(편의성, 사용자 결정 2026-07-29). 이 신호는
                // OnCardUsed 안에서만 나오므로 "부착을 1회라도 했을 때만" 조건이 자동 충족된다
                // (게이지 0 인 채 선택만 한 경우엔 발화하지 않아 손패가 열린 채 남는다 — 의도).
                //
                // 발화 위치가 계약이다: BindEmpty **뒤**(침강이 소모된 카드를 다시 보이면 안 된다)
                // + Close() **앞**(뒤에 두면 Close 의 FocusCleared 가 리티클을 재주장한 직후
                // 구독자가 선택을 닫아 1프레임 깜빡임 + lease churn 이 생긴다).
                UsableCardsExhausted?.Invoke();
                Close();
                return;
            }

            // 연속 사용 — 직전 재딜인이 아직 날고 있으면 스냅 완주(포즈 캡처 오염 방지).
            if (_redealSeq.isAlive) _redealSeq.Complete();
            _prevIds.Clear();
            _prevPose.Clear();
            foreach (var slot in _slots)
            {
                _prevIds.Add(slot.entryId);
                _prevPose.Add(new Vector3(slot.rect.anchoredPosition.x,
                    slot.rect.anchoredPosition.y, slot.rect.localEulerAngles.z));
            }
            Refresh(); // 재바인딩 + RestoreSlotHome(전 슬롯 home 스냅)
            for (int i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                if (slot.entryId < 0) continue;
                int prevAt = _prevIds.IndexOf(slot.entryId);
                if (prevAt >= 0)
                {
                    // 잔류 카드: 옛 포즈에서 시작 → SpringSlots 가 새 home 으로 끌고 간다.
                    var p = _prevPose[prevAt];
                    slot.rect.anchoredPosition = new Vector2(p.x, p.y);
                    slot.rect.localEulerAngles = new Vector3(0f, 0f, p.z);
                }
                else DealInSlot(slot);
            }
        }

        // 사용 직후 빈 자리 1장 재딜인. StartDeal 의 per-card 블록과 같은 연출이지만
        // _dealSeq(=Transitioning 게이트)를 쓰지 않는다 — 딜인 중에도 다른 카드는 즉시
        // 조작 가능해야 연속 사용이 막히지 않는다. 딜인 중인 슬롯만 redealing 으로 잠근다.
        private void DealInSlot(CardSlot slot)
        {
            SoundManager.Instance?.PlayCardDeal();
            slot.redealing = true;
            var rt = slot.rect;
            rt.anchoredPosition = new Vector2(slot.homePos.x * clusterK, handBaseY - dealRise);
            rt.localScale = Vector3.one * dealStartScale;
            rt.localEulerAngles = new Vector3(dealTiltX, 0f, slot.homeRotZ);
            if (slot.face != null) slot.face.Unfold = 0f;
            if (slot.nameGroup != null) slot.nameGroup.alpha = 0f;
            if (slot.costGroup != null) slot.costGroup.alpha = 0f;
            if (slot.tagGroup != null) slot.tagGroup.alpha = 0f;
            if (slot.bodyGroup != null) slot.bodyGroup.alpha = 0f;
            _redealSeq = Sequence.Create();
            _redealSeq.Group(Tween.UIAnchoredPosition(rt, slot.homePos, dealDurationSec, Ease.OutBack));
            _redealSeq.Group(Tween.Scale(rt, Vector3.one, dealDurationSec, Ease.OutBack));
            _redealSeq.Group(Tween.LocalRotation(rt, Quaternion.Euler(0f, 0f, slot.homeRotZ), dealDurationSec, Ease.OutQuad));
            _redealSeq.Group(Tween.PunchScale(rt, new Vector3(0.06f, -0.10f, 0f), 0.16f, frequency: 2f, startDelay: dealDurationSec));
            if (slot.face != null)
                _redealSeq.Group(Tween.Custom(slot.face, 0f, 1f, crumpleUnfoldSec, (f, u) => f.Unfold = u, Ease.OutQuad));
            float textDelay = Mathf.Max(0f, crumpleUnfoldSec - textFadeSec);
            if (slot.nameGroup != null) _redealSeq.Group(Tween.Alpha(slot.nameGroup, 1f, textFadeSec, Ease.OutQuad, startDelay: textDelay));
            if (slot.costGroup != null) _redealSeq.Group(Tween.Alpha(slot.costGroup, 1f, textFadeSec, Ease.OutQuad, startDelay: textDelay));
            if (slot.tagGroup != null) _redealSeq.Group(Tween.Alpha(slot.tagGroup, 1f, textFadeSec, Ease.OutQuad, startDelay: textDelay));
            if (slot.bodyGroup != null) _redealSeq.Group(Tween.Alpha(slot.bodyGroup, 1f, textFadeSec, Ease.OutQuad, startDelay: textDelay));
            var captured = slot;
            _redealSeq.ChainCallback(() => captured.redealing = false);
        }

        // ── open/close ───────────────────────────────────────────────────────

        // selection-hand-attach unit 1 — 래치 소비. 전이가 끝나고(침강 완주) 선택이 아직
        // 살아 있으면 그때 연다. 페이즈 이탈/선택 해제로 조건이 깨졌으면 조용히 버린다.
        private void TickPendingSelectionOpen()
        {
            if (!_pendingSelectionOpen) return;
            if (!InSelectionMode || State == HandState.Hand) { _pendingSelectionOpen = false; return; }
            if (Transitioning) return;
            Open(selectionDriven: true); // 내부에서 래치를 내린다
        }

        private void Open(bool selectionDriven)
        {
            if (State == HandState.Hand) return;
            _pendingSelectionOpen = false;
            State = HandState.Hand;
            ResetHandClearance(); // hand-drag-clearance unit 0 — 항상 기준선에서 열린다
            if (_dismissCatcher != null) _dismissCatcher.SetActive(true);
            if (gaugeView != null) gaugeView.SetOpen(true);
            Refresh();
            // use-flow unit 0 — 슬로모는 여기서 잡지 않는다. TickSlomo(Update 폴링)가
            // slomoOnOpen || held 를 에지 트리거로 획득/해제한다. 여기는 stale 잔재만 정리.
            _slomoLease.Dispose();
            _slomoActive = false;
            if (costDisplay != null) costDisplay.SetSuppressed(true);
            // hand-deal-in unit 2 — 버튼 pulse(인과 힌트) + strip 접기 → 덱-드로우 딜.
            // selection-hand-attach unit 1 — pulse 는 "항아리를 눌렀다 → 손패가 열렸다" 인과
            // 힌트다. 선택 기인 오픈은 항아리를 누른 게 아니므로 쏘지 않는다(critic L1).
            if (gaugeView != null && !selectionDriven) gaugeView.Pulse();
            if (_flip != null) StopCoroutine(_flip);
            _flip = StartCoroutine(OpenRoutine());
        }

        private void Close()
        {
            if (State == HandState.UnitStrip) return;
            State = HandState.UnitStrip;
            if (_dismissCatcher != null) _dismissCatcher.SetActive(false);
            if (gaugeView != null) gaugeView.SetOpen(false);
            StopDeal();
            CancelAllCardInteraction(); // drop any in-flight drag (no spend)
            _focus?.End(); // dreamcatcher-attach-lockon 계약 #10 — 포커스 오버레이 하드 클리어
            RaiseFocusCleared();        // selection-hand-attach unit 4 — End 뒤에 발화(순서가 요점)
            // hand-drag-tooltip unit 1 — 닫힘 계열은 즉시 숨김(침강 중 형제 잔류 방지).
            HideDragTooltip(immediate: true);
            _focusIndex = -1;
            _slomoLease.Dispose(); // 슬로모 즉시 해제(연출은 realtime) — 폴링 밖 최후 안전망
            _slomoActive = false;
            if (costDisplay != null) costDisplay.SetSuppressed(false);
            // hand-deal-in unit 4 — 딜의 거울: 카드가 하단 덱으로 침강 → strip 폴드 인.
            StartSink();
        }

        // No animation: phase exits, disable, and Placement resets land here.
        private void ForceClose()
        {
            // critic H2 — drop any in-flight drag/pending first (no spend).
            CancelAllCardInteraction();
            // selection-hand-attach unit 1 — 하드 teardown 은 미뤄둔 선택 오픈도 버린다.
            // 남겨두면 페이즈 이탈/Reset 직후 전이가 끝나는 프레임에 손패가 되살아난다.
            // (SelectionTarget 자체는 소유자인 DcInspectController 가 정리한다 — 계약 1.)
            _pendingSelectionOpen = false;
            _focus?.End(); // dreamcatcher-attach-lockon 계약 #10 — 포커스 오버레이 하드 클리어
            RaiseFocusCleared();          // selection-hand-attach unit 4
            HideDragTooltip(immediate: true); // hand-drag-tooltip unit 1
            StopDeal(); // hand-deal-in — 잔류 트윈/late-land 방지
            _slomoLease.Dispose();
            _slomoActive = false;
            if (_flip != null) { StopCoroutine(_flip); _flip = null; }
            State = HandState.UnitStrip;
            ResetHandClearance(); // hand-drag-clearance unit 0 — 하드 teardown(침강 없음)
            if (_dismissCatcher != null) _dismissCatcher.SetActive(false);
            if (gaugeView != null) gaugeView.SetOpen(false);
            // 억제 해제는 무조건 — 표시 여부는 CostDisplay 가 페이즈와 결합해 결정.
            if (costDisplay != null) costDisplay.SetSuppressed(false);
            if (_panel != null)
            {
                var rt = (RectTransform)_panel.transform;
                rt.localEulerAngles = Vector3.zero;
                _panel.SetActive(false);
            }
            var strip = StripPanel();
            if (strip != null)
            {
                ((RectTransform)strip.transform).localEulerAngles = Vector3.zero;
                // Only restore the strip inside the playable window — outside it
                // (Result 등) the selector hides itself via its own events.
                var gm = GameManager.Instance;
                if (gm != null && (gm.CurrentPhase == GamePhase.Placement || gm.CurrentPhase == GamePhase.Battle))
                    strip.SetActive(true);
            }
        }

        private GameObject StripPanel() =>
            defenderSelector != null ? defenderSelector.PanelGO : null;

        // X-axis fold shared by the strip fold-out (OpenRoutine) and fold-in
        // (StripFoldInRoutine). Unscaled time — the fold must not slow under slomo.
        private IEnumerator RotateX(RectTransform rt, float fromDeg, float toDeg)
        {
            float t = 0f;
            float dur = Mathf.Max(0.01f, flipHalfDuration);
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / dur));
                rt.localEulerAngles = new Vector3(Mathf.Lerp(fromDeg, toDeg, k), 0f, 0f);
                yield return null;
            }
            rt.localEulerAngles = new Vector3(toDeg, 0f, 0f);
        }

        // ── deal-in (hand-deal-in unit 2) ─────────────────────────────────────

        // Strip folds edge-on and vanishes, then the hand stages (backing fade)
        // and cards deal up from the deck below the tray — no whole-panel fold-in.
        private IEnumerator OpenRoutine()
        {
            var strip = StripPanel();
            if (strip != null)
            {
                var srt = (RectTransform)strip.transform;
                yield return RotateX(srt, 0f, 90f);
                strip.SetActive(false);
                srt.localEulerAngles = Vector3.zero;
            }
            var prt = (RectTransform)_panel.transform;
            prt.localEulerAngles = Vector3.zero;
            _panel.SetActive(true);
            StartDeal();
            HandOpened?.Invoke();
            _flip = null;
        }

        // Cards rise from a deck below the tray (not the button): each starts
        // clustered off the bottom edge, laid back (X tilt), then springs up to
        // its arc home with an OutBack overshoot + settle squash. Backing fades
        // in as the stage. Unity timeScale stays 1 (TimeManager domain) →
        // PrimeTween's default Time.deltaTime timing is realtime (no slomo drag).
        private void StartDeal()
        {
            StopDeal();
            SoundManager.Instance?.PlayCardDeal(); // deck-sfx — 딜인(덱 드로우) 리플 1회
            _dealSeq = Sequence.Create();
            if (_backing != null)
            {
                var c = _backing.color; c.a = 0f; _backing.color = c;
                _dealSeq.Chain(Tween.Alpha(_backing, _backingAlpha, trayFadeSec, Ease.OutQuad));
            }
            int dealt = 0;
            for (int i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                if (slot.entryId < 0) continue; // deal only real cards
                var rt = slot.rect;
                float jitter = ((i % 3) - 1) * 6f; // index 결정론 흐트러짐
                rt.anchoredPosition = new Vector2(slot.homePos.x * clusterK, handBaseY - dealRise);
                rt.localScale = Vector3.one * dealStartScale;
                rt.localEulerAngles = new Vector3(dealTiltX, 0f, slot.homeRotZ + jitter);
                // card-crumple-unfold unit 2 — 시작 = 구겨짐 + 텍스트/코스트 숨김.
                if (slot.face != null) slot.face.Unfold = 0f;
                if (slot.nameGroup != null) slot.nameGroup.alpha = 0f;
                if (slot.costGroup != null) slot.costGroup.alpha = 0f;
                if (slot.tagGroup != null) slot.tagGroup.alpha = 0f;   // hand-card-face unit 1
                if (slot.bodyGroup != null) slot.bodyGroup.alpha = 0f;
                float d = dealt * dealStaggerSec;
                _dealSeq.Group(Tween.UIAnchoredPosition(rt, slot.homePos, dealDurationSec, Ease.OutBack, startDelay: d));
                _dealSeq.Group(Tween.Scale(rt, Vector3.one, dealDurationSec, Ease.OutBack, startDelay: d));
                _dealSeq.Group(Tween.LocalRotation(rt, Quaternion.Euler(0f, 0f, slot.homeRotZ), dealDurationSec, Ease.OutQuad, startDelay: d));
                // ②-B 안착 squash flex(4버텍스, 메인 트윈과 안 겹치게 안착 직후). 진짜 커브는 후속 spec.
                _dealSeq.Group(Tween.PunchScale(rt, new Vector3(0.06f, -0.10f, 0f), 0.16f, frequency: 2f, startDelay: d + dealDurationSec));
                // 구김 풀림(안착보다 살짝 늦게) + 텍스트/코스트는 펴짐 끝에 페이드-인.
                if (slot.face != null)
                    _dealSeq.Group(Tween.Custom(slot.face, 0f, 1f, crumpleUnfoldSec, (f, u) => f.Unfold = u, Ease.OutQuad, startDelay: d));
                float textDelay = d + Mathf.Max(0f, crumpleUnfoldSec - textFadeSec);
                if (slot.nameGroup != null) _dealSeq.Group(Tween.Alpha(slot.nameGroup, 1f, textFadeSec, Ease.OutQuad, startDelay: textDelay));
                if (slot.costGroup != null) _dealSeq.Group(Tween.Alpha(slot.costGroup, 1f, textFadeSec, Ease.OutQuad, startDelay: textDelay));
                if (slot.tagGroup != null) _dealSeq.Group(Tween.Alpha(slot.tagGroup, 1f, textFadeSec, Ease.OutQuad, startDelay: textDelay));
                if (slot.bodyGroup != null) _dealSeq.Group(Tween.Alpha(slot.bodyGroup, 1f, textFadeSec, Ease.OutQuad, startDelay: textDelay));
                dealt++;
            }
        }

        private void StopDeal()
        {
            if (_dealSeq.isAlive) _dealSeq.Stop();
            // use-flow unit 1 — 재딜인은 스냅 완주(Complete: 콜백이 redealing 을 푼다).
            // Stop 이면 중간 상태(반쯤 구겨진 face/알파)로 굳는다.
            if (_redealSeq.isAlive) _redealSeq.Complete();
        }

        // hand-deal-in — 딜 진행 중 카드를 누르면 즉시 딜을 완주(스냅)시켜 게이트를 연다.
        // 성급한 플레이어는 한 번의 터치로 바로 집고, 기다린 플레이어는 전체 딜 연출을 본다
        // (HS/StS 손맛: 딜 꼬리 대기 제거). Complete = 모든 카드를 home/scale/text 최종 상태로
        // 스냅 → _dealSeq.isAlive false → Transitioning 해제 → 호출처가 같은 프레임에 focus 성공.
        // 반환: 실제로 완주시켰으면 true.
        public bool TryFastForwardDeal()
        {
            if (!_dealSeq.isAlive) return false;
            _dealSeq.Complete();
            return true;
        }

        // Mirror of the deck-draw: cards sink back down to the deck (reverse
        // stagger, InBack + scale-down + backing fade-out), then the defender
        // strip folds back in. ForceClose's hard stop skips OnSinkComplete —
        // it does the panel/strip teardown itself.
        private void StartSink()
        {
            StopDeal();
            _dealSeq = Sequence.Create();
            if (_backing != null)
                _dealSeq.Chain(Tween.Alpha(_backing, 0f, sinkDurationSec * 0.8f, Ease.InQuad));
            int k = 0;
            for (int i = _slots.Count - 1; i >= 0; i--) // 역순 침강
            {
                var slot = _slots[i];
                if (slot.entryId < 0) continue;
                var rt = slot.rect;
                Vector2 dst = new Vector2(slot.homePos.x * clusterK, handBaseY - dealRise);
                float d = k * sinkStaggerSec;
                _dealSeq.Group(Tween.UIAnchoredPosition(rt, dst, sinkDurationSec, Ease.InBack, startDelay: d));
                _dealSeq.Group(Tween.Scale(rt, Vector3.one * dealStartScale, sinkDurationSec, Ease.InBack, startDelay: d));
                k++;
            }
            _dealSeq.ChainCallback(OnSinkComplete);
        }

        private void OnSinkComplete()
        {
            if (_panel != null)
            {
                ((RectTransform)_panel.transform).localEulerAngles = Vector3.zero;
                ResetHandClearance(); // hand-drag-clearance unit 0 — 침강 완료 = 패널이 꺼지는 시점
                _panel.SetActive(false);
            }
            for (int i = 0; i < _slots.Count; i++) RestoreSlotHome(i); // 다음 오픈 대비 home 복원
            var strip = StripPanel();
            if (strip != null)
            {
                strip.SetActive(true);
                if (_flip != null) StopCoroutine(_flip);
                _flip = StartCoroutine(StripFoldInRoutine((RectTransform)strip.transform));
            }
        }

        private IEnumerator StripFoldInRoutine(RectTransform rt)
        {
            rt.localEulerAngles = new Vector3(90f, 0f, 0f);
            yield return RotateX(rt, 90f, 0f);
            rt.localEulerAngles = Vector3.zero;
            _flip = null;
        }

        // ── hand rendering ───────────────────────────────────────────────────

        private void Refresh()
        {
            if (!_built || handController == null) return;
            // use-flow unit 1 fix(마감 리뷰 M1) — 재바인딩 전에 살아있는 재딜인을 스냅 완주.
            // Recovered(host 사망 카드 회수)가 재딜인 도중 오면, 트윈이 rect 를 소유한 채
            // 슬롯 내용만 바뀌어 엉뚱한 카드가 홀로 다시 딜인되는 그림이 됐다. Complete 는
            // 콜백까지 돌려 redealing 잠금도 함께 푼다(OnCardUsed 의 선행 Complete 와 중복
            // 시 no-op).
            if (_redealSeq.isAlive) _redealSeq.Complete();
            _focusIndex = -1; // 재바인딩 시 stale focus 해제(다음 press 가 재설정)
            EnsureSlots(handController.HandSize);
            var hand = handController.Hand();
            for (int i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                RestoreSlotHome(i); // undo any drag/pending float
                if (i < hand.Count)
                    BindCard(slot, hand[i].entryId, hand[i].card);
                else
                    BindEmpty(slot);
            }
            RefreshUsability();
        }

        private void RefreshUsability()
        {
            foreach (var slot in _slots)
            {
                if (slot.entryId < 0) continue;
                slot.usable = handController.CanUse(slot.entryId);
                // hand-card-face rev 1(계약 8) + 2026-07-25 사용자 피드백(solid BG) —
                // 카드 면은 항상 완전 불투명. 사용불가 dim 은 알파가 아니라 face 어둡기만으로
                // 표현한다(알파 dim 은 보드가 비쳐 "투명 카드"로 읽혔고 본문 가독도 죽였다).
                slot.group.alpha = 1f;
                if (slot.face != null)
                    slot.face.color = slot.usable ? Color.white : new Color(0.62f, 0.62f, 0.70f, 1f);
            }
        }

        // hand-card-face unit 1 — 아트 제거: 카드 면 = 투톤 face 스프라이트(타입색 헤더 +
        // 중립 본문 + 무의식 테두리). card.art/skill.uiTint 는 손패에서 더 이상 읽지 않는다
        // (덱빌더는 계속 사용). 텍스트가 정보를 전달한다: 이름(헤더)·태그(대상+역할)·본문(효과).
        private void BindCard(CardSlot slot, int entryId, DreamcatcherCard card)
        {
            slot.entryId = entryId;
            slot.card = card;
            slot.frame.color = Color.clear; // face 가 카드 면 전체 — frame 은 빈 슬롯 표시 전용
            slot.costBadge.SetActive(true);
            slot.costLabel.text = handController.CostOf(card).ToString();

            slot.nameTag.SetActive(true);
            slot.nameLabel.text = card.displayName;

            slot.tagChip.SetActive(true);
            slot.tagLabel.text = CardCategoryStyle.TargetTag(card);
            // 칩 배경 = 헤더색을 어둡게(같은 계열 유지, 흰 텍스트 대비 확보).
            slot.tagBg.color = Color.Lerp(CardCategoryStyle.HandHeader(card.type), Color.black, 0.35f);

            slot.bodyLabel.text = DreamcatcherCardText.BodyLinesOnly(
                card, defenderCatalog != null ? defenderCatalog.DisplayNameOf : (System.Func<string, string>)null);

            slot.art.enabled = true;
            slot.art.sprite = FaceSpriteFor(card);
            slot.art.color = Color.white;
        }

        private Sprite FaceSpriteFor(DreamcatcherCard card)
        {
            var key = (card.type, card.category == CardCategory.Subconscious);
            if (_faceCache.TryGetValue(key, out var sprite) && sprite != null) return sprite;
            sprite = UiRoundedSprite.MakeCardFace(
                (int)(CardW * 2f), (int)(CardH * 2f), radius: 44f, border: 6f,
                CardCategoryStyle.HandHeader(card.type), CardCategoryStyle.HandBody(),
                CardCategoryStyle.HandBorder(card), HeaderH / CardH);
            _faceCache[key] = sprite;
            return sprite;
        }

        private void BindEmpty(CardSlot slot)
        {
            slot.entryId = -1;
            slot.card = null;
            slot.usable = false;
            slot.frame.color = new Color(1f, 1f, 1f, 0.06f); // empty frame
            slot.art.enabled = false;
            slot.nameTag.SetActive(false);
            slot.nameLabel.text = "";
            slot.tagChip.SetActive(false);
            slot.bodyLabel.text = "";
            slot.costBadge.SetActive(false);
            slot.group.alpha = 1f;
        }

        // ── canvas build ─────────────────────────────────────────────────────

        private void BuildCanvas()
        {
            if (_built) return;
            _built = true;

            var roots = UiCanvasSetup.Ensure(gameObject, sortingOrder: 5);

            // card-crumple-unfold — Canvas 는 기본적으로 uv1/uv2 를 셰이더로 안 넘긴다.
            // CardCrumple 셰이더가 접힘 데이터(TEXCOORD1/2)를 읽으려면 채널을 켜야 한다.
            var canvas = GetComponentInChildren<Canvas>(true);
            if (canvas != null)
                canvas.additionalShaderChannels |=
                    AdditionalCanvasShaderChannels.TexCoord1 | AdditionalCanvasShaderChannels.TexCoord2;

            // dreamcatcher-orb-dock unit 4 — 보드 영역 탭 dismiss 캐처. 카드(_panel) 뒤 sibling
            // 이라 카드·backing(드래그취소) 입력은 안 가로채고, 손패 패널 바깥(보드) 탭만 Close.
            // 항아리 독·NextWaveDock 은 order 7 캔버스라 이 order 5 캐처 위에서 정상 동작.
            // selection-hand-attach unit 2 — Button 대신 press-스냅샷 탭 캐처(사유는 그 클래스 주석).
            _dismissCatcher = new GameObject("HandDismissCatcher",
                typeof(RectTransform), typeof(Image), typeof(HandDismissTapCatcher));
            _dismissCatcher.transform.SetParent(roots.SafeAreaRoot, false);
            var dcRt = (RectTransform)_dismissCatcher.transform;
            dcRt.anchorMin = Vector2.zero; dcRt.anchorMax = Vector2.one;
            dcRt.offsetMin = Vector2.zero; dcRt.offsetMax = Vector2.zero;
            var dcImg = _dismissCatcher.GetComponent<Image>();
            dcImg.color = new Color(0f, 0f, 0f, 0.001f); // 투명하지만 raycast 수신
            _dismissCatcher.GetComponent<HandDismissTapCatcher>().Init(
                // press 프레임에 카드 드래그/포탈 조준/Active 조준이 살아 있으면 그 릴리즈는
                // 보드 탭이 아니다(critic H1). AnyInteractionActive 는 private 이라 클로저로 넘긴다.
                () => AnyInteractionActive() || (GameManager.Instance != null && GameManager.Instance.IsAiming),
                OnBoardTapCaught,
                boardTapMoveThreshold);
            _dismissCatcher.transform.SetAsFirstSibling();
            _dismissCatcher.SetActive(false);

            // Bottom-center hand panel.
            _panel = new GameObject("HandPanel", typeof(RectTransform), typeof(Image));
            _panel.transform.SetParent(roots.SafeAreaRoot, false);
            var prt = (RectTransform)_panel.transform;
            prt.anchorMin = new Vector2(0.5f, 0f);
            prt.anchorMax = new Vector2(0.5f, 0f);
            prt.pivot = new Vector2(0.5f, 0f);
            prt.anchoredPosition = new Vector2(0f, trayConfig != null ? trayConfig.anchoredY : 32f);
            prt.sizeDelta = trayConfig != null ? trayConfig.handSize : new Vector2(980f, 232f);
            _panelBaseY = prt.anchoredPosition.y; // hand-drag-clearance unit 0 — 하강 기준선
            var backing = _panel.GetComponent<Image>();
            // action-tray unit 3 — 트레이와 같은 외곽 문법(라운드+골드 엣지+네이비 fill)
            // 으로 "같은 프레임의 앞뒷면" 시각 통일. config 미할당 시 기존 단색 유지.
            if (trayConfig != null)
            {
                backing.sprite = UiRoundedSprite.Make(22f, 2f, trayConfig.fallbackFill, trayConfig.fallbackBorder);
                backing.type = Image.Type.Sliced;
                backing.color = Color.white;
            }
            else
            {
                backing.color = new Color(0.05f, 0.04f, 0.1f, 0.72f);
            }
            // The backing IS the cancel region (unit 7): keep it a raycast target.
            backing.raycastTarget = true;
            // hand-deal-in unit 0 — 딜 무대 페이드용 참조(카드와 별개로 backing 만 페이드).
            _backing = backing;
            _backingAlpha = backing.color.a;

            BuildCancelZone(prt); // drag-cancel-affordance unit 1 — 취소 판정 rect

            // rev 4-6 — 타겟팅 화살표는 패널 뒤 sibling 으로 붙여 카드 위에 그려진다.
            _targetArrow = DreamcatcherTargetArrow.Create(transform);
            _targetArrow.Configure(focusConfig);

            // dreamcatcher-attach-lockon — 포커스 연출 프레젠터(dim/링/리티클/콜아웃/확정).
            // 요소는 캔버스 루트 직속으로 만들고 sibling index 로 레이어를 강제한다(계약 #3).
            if (focusConfig != null)
            {
                _focus = DreamcatcherFocusPresenter.Create(transform, focusConfig, labelFont);
                _focus.Bind(bridge, handController, MainCamera);
                _focus.EnforceSiblingOrder(_targetArrow.transform);
            }

            BuildTooltip(roots.SafeAreaRoot);
        }

        // ── drag tooltip (hand-drag-tooltip unit 1, 위치 rev 5) ──────────────
        // 화면 상단 중앙에 고정으로 뜨는 플로팅 성능 툴팁. 손패는 하단 앵커라
        // 상단 대역에는 손이 닿지 않는다 — 손잡이 무관하게 가림이 해소된다.
        // 슬롯은 내용(카드/코스트/본문)에만 관여하고 위치와 무관하다.
        // 전 Graphic raycastTarget=false (드래그/조준 판정 비간섭).

        private void BuildTooltip(Transform parent)
        {
            _tooltipRoot = new GameObject("DragTooltip", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            _tooltipRoot.transform.SetParent(parent, false);
            _tooltipRect = (RectTransform)_tooltipRoot.transform;
            // rev 5 — 상단 중앙 앵커 + 상단 피벗. 피벗이 상단이라 패널은 아래로
            // 자라고, 카드마다 설명 길이가 달라도 읽기 시작점(상단 모서리)이 고정된다.
            _tooltipRect.anchorMin = new Vector2(0.5f, 1f);
            _tooltipRect.anchorMax = new Vector2(0.5f, 1f);
            _tooltipRect.pivot = new Vector2(0.5f, 1f);

            var bg = _tooltipRoot.GetComponent<Image>();
            // rev 2 (사용자 피드백) — 카드 위에 뜨는 패널이라 불투명 필수(반투명은
            // 카드 아트가 비쳐 텍스트 대비를 죽인다). 부양감은 알파가 아니라
            // bob + 그림자가 담당(HS/StS 툴팁 관례).
            var fill = new Color(0.07f, 0.06f, 0.13f, 1f);
            if (trayConfig != null)
            {
                bg.sprite = UiRoundedSprite.Make(16f, 2f, fill, trayConfig.fallbackBorder);
                bg.type = Image.Type.Sliced;
                bg.color = Color.white;
            }
            else
            {
                bg.color = fill;
            }
            bg.raycastTarget = false;
            var shadow = _tooltipRoot.AddComponent<Shadow>();
            shadow.effectDistance = new Vector2(0f, -5f);
            shadow.effectColor = new Color(0f, 0f, 0f, 0.45f);
            _tooltipGroup = _tooltipRoot.GetComponent<CanvasGroup>();
            _tooltipGroup.alpha = 0f;
            _tooltipGroup.blocksRaycasts = false;
            _tooltipGroup.interactable = false;

            _tooltipHeader = BuildTooltipLabel("Header", tooltipHeaderFont, TextAlignmentOptions.Left);
            _tooltipBody = BuildTooltipLabel("Body", tooltipBodyFont, TextAlignmentOptions.TopLeft);
            _tooltipRoot.SetActive(false);
        }

        private TextMeshProUGUI BuildTooltipLabel(string name, float size, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(_tooltipRoot.transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(TooltipPad, 0f);
            rt.offsetMax = new Vector2(-TooltipPad, 0f);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            if (labelFont != null) tmp.font = labelFont;
            tmp.fontSize = size;
            tmp.color = Color.white;
            tmp.alignment = align;
            tmp.raycastTarget = false;
            return tmp;
        }

        // hand-card-face unit 3 — 툴팁 역할 전환: 카드 설명은 카드 면이 담당하므로 이
        // 위젯은 조작 브리핑이 된다. header = 조작법(AimMode 별 고정 — 드래그 슬롯이
        // 분류 소유), body = 조작 상태(호버/취소영역/포탈 단계, 실시간 갱신).
        public void ShowDragBriefing(string controls, string status)
        {
            if (_tooltipRoot == null || string.IsNullOrEmpty(controls)) return;
            _tooltipHeader.text = controls;
            _briefingStatus = null; // 상태 캐시 무효화 — 아래 갱신이 반드시 레이아웃을 탄다
            UpdateDragBriefingStatus(status);

            // rev 5 — 슬롯과 무관한 상단 중앙 고정. 좌우 플립 없음.
            _tooltipBasePos = new Vector2(0f, -tooltipTopOffset);
            _tooltipRect.anchoredPosition = _tooltipBasePos;

            _tooltipVisible = true;
            if (!_tooltipRoot.activeSelf)
            {
                _tooltipGroup.alpha = 0f;
                _tooltipRect.localScale = new Vector3(TooltipHiddenScale, TooltipHiddenScale, 1f);
                _tooltipRoot.SetActive(true);
            }
            CardPeeked?.Invoke(); // 튜토리얼 훅 유지 — 브리핑 노출 = "끌어보세요" 배너 소진 신호
        }

        // 상태 줄 실시간 갱신 — OnDrag 매프레임 호출되므로 동일 문자열이면 조기 반환.
        public void UpdateDragBriefingStatus(string status)
        {
            if (_tooltipRoot == null || status == _briefingStatus) return;
            _briefingStatus = status;
            _tooltipBody.text = status ?? "";

            // 내용 기반 세로 크기(TMP preferred). 폭은 고정, 라벨은 top-anchored 스택.
            float innerW = tooltipWidth - TooltipPad * 2f;
            float headerH = _tooltipHeader.GetPreferredValues(_tooltipHeader.text, innerW, 0f).y;
            float bodyH = _tooltipBody.GetPreferredValues(_tooltipBody.text, innerW, 0f).y;
            var hrt = (RectTransform)_tooltipHeader.transform;
            hrt.anchoredPosition = new Vector2(0f, -TooltipPad);
            hrt.sizeDelta = new Vector2(hrt.sizeDelta.x, headerH);
            var brt = (RectTransform)_tooltipBody.transform;
            float bodyTop = TooltipPad + headerH + TooltipHeaderGap;
            brt.anchoredPosition = new Vector2(0f, -bodyTop);
            brt.sizeDelta = new Vector2(brt.sizeDelta.x, bodyH);
            _tooltipRect.sizeDelta = new Vector2(tooltipWidth, bodyTop + bodyH + TooltipPad);
        }

        // 멱등 — EndInteraction 은 비드래그 상황(OnDisable 등)에서도 불린다.
        // 커밋/취소는 페이드 아웃, 닫힘 계열(Close/ForceClose)은 immediate.
        public void HideDragTooltip(bool immediate = false)
        {
            if (_tooltipRoot == null) return;
            _tooltipVisible = false;
            if (immediate)
            {
                _tooltipGroup.alpha = 0f;
                _tooltipRoot.SetActive(false);
            }
        }

        private void TickTooltip()
        {
            if (_tooltipRoot == null || !_tooltipRoot.activeSelf) return;
            float a = 1f - Mathf.Exp(-TooltipLerpK * Time.deltaTime);
            _tooltipGroup.alpha = Mathf.Lerp(_tooltipGroup.alpha, _tooltipVisible ? 1f : 0f, a);
            float s = Mathf.Lerp(_tooltipRect.localScale.x, _tooltipVisible ? 1f : TooltipHiddenScale, a);
            _tooltipRect.localScale = new Vector3(s, s, 1f);
            // rev 1 — 카드 idle bob 문법의 둥실거림(base 가산, 누적 없음).
            float t = Time.time;
            _tooltipRect.anchoredPosition = _tooltipBasePos + new Vector2(
                Mathf.Sin(t * tooltipBobFreq * 0.7f) * tooltipBobX,
                Mathf.Sin(t * tooltipBobFreq) * tooltipBobY);
            if (!_tooltipVisible && _tooltipGroup.alpha < 0.02f) _tooltipRoot.SetActive(false);
        }

        private void EnsureSlots(int count)
        {
            if (_slots.Count == count) return;
            foreach (var s in _slots) if (s.root != null) Destroy(s.root);
            _slots.Clear();

            float step = CardW - cardOverlap; // 겹친 부채(음수 간격 효과)
            // drag-cancel-affordance unit 1 — 취소 rect 폭 = **보이는 부채 폭**(패널보다 넓을 수 있다).
            // 카드 수가 확정되는 유일한 지점이라 여기서 맞춘다.
            if (_cancelZone != null && _panel != null)
            {
                float fanWidth = CardW + step * Mathf.Max(0, count - 1);
                _cancelZone.sizeDelta = new Vector2(
                    Mathf.Max(((RectTransform)_panel.transform).sizeDelta.x, fanWidth), cancelZoneHeight);
            }
            for (int i = 0; i < count; i++)
            {
                var slot = new CardSlot();
                slot.root = new GameObject($"Card_{i}", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
                slot.root.transform.SetParent(_panel.transform, false);
                slot.rect = (RectTransform)slot.root.transform;
                slot.rect.anchorMin = new Vector2(0.5f, 0f);
                slot.rect.anchorMax = new Vector2(0.5f, 0f);
                slot.rect.pivot = new Vector2(0.5f, 0f);
                // StS/HS 포물선 아치: t∈[-1,1], 가운데 솟음 + 접선 회전.
                float t = count == 1 ? 0f : (float)i / (count - 1) * 2f - 1f;
                float x = -((count - 1) * step) * 0.5f + i * step;
                float y = handBaseY + arcHeight * (1f - t * t);
                float rotZ = -t * rotMax;
                slot.rect.anchoredPosition = new Vector2(x, y);
                slot.rect.sizeDelta = new Vector2(CardW, CardH);
                slot.rect.localEulerAngles = new Vector3(0f, 0f, rotZ);

                slot.frame = slot.root.GetComponent<Image>();
                slot.group = slot.root.GetComponent<CanvasGroup>();

                // card-crumple-unfold unit 0 — face 는 서브디바이드 Graphic(구김 토대).
                // hand-card-face unit 1 — 아트 → 투톤 face 스프라이트: 카드 면 전체(인셋 0).
                // frame(root Image)·드래그·CanvasGroup 은 그대로.
                var faceGO = new GameObject("Face", typeof(RectTransform), typeof(UiCardFaceMesh));
                faceGO.transform.SetParent(slot.root.transform, false);
                var faceRt = (RectTransform)faceGO.transform;
                faceRt.anchorMin = Vector2.zero;
                faceRt.anchorMax = Vector2.one;
                faceRt.offsetMin = Vector2.zero;
                faceRt.offsetMax = Vector2.zero;
                slot.face = faceGO.GetComponent<UiCardFaceMesh>();
                slot.art = slot.face;
                slot.art.preserveAspect = true;
                slot.art.raycastTarget = false;

                // hand-card-face unit 1 — 이름은 헤더 밴드(타입 색) 중앙(y -32~-64, 코스트/
                // 태그 줄 아래). 배킹 이미지 없음(face 헤더색이 배경), 크럼플 페이드 그룹 유지.
                slot.nameTag = new GameObject("NameTag", typeof(RectTransform), typeof(CanvasGroup));
                slot.nameTag.transform.SetParent(slot.root.transform, false);
                var tagRt = (RectTransform)slot.nameTag.transform;
                tagRt.anchorMin = new Vector2(0f, 1f);
                tagRt.anchorMax = new Vector2(1f, 1f);
                tagRt.pivot = new Vector2(0.5f, 1f);
                tagRt.offsetMin = new Vector2(8f, -HeaderH);
                tagRt.offsetMax = new Vector2(-8f, -32f);
                slot.nameGroup = slot.nameTag.GetComponent<CanvasGroup>(); // 크럼플 중 페이드용

                var nameGO = new GameObject("Name", typeof(RectTransform));
                nameGO.transform.SetParent(slot.nameTag.transform, false);
                var nrt = (RectTransform)nameGO.transform;
                nrt.anchorMin = Vector2.zero;
                nrt.anchorMax = Vector2.one;
                nrt.offsetMin = new Vector2(2f, 0f);
                nrt.offsetMax = new Vector2(-2f, 0f);
                slot.nameLabel = nameGO.AddComponent<TextMeshProUGUI>();
                if (labelFont != null) slot.nameLabel.font = labelFont;
                slot.nameLabel.fontSize = 20;
                slot.nameLabel.enableAutoSizing = true; // 긴 이름 축소 허용
                slot.nameLabel.fontSizeMin = 14;
                slot.nameLabel.fontSizeMax = 20;
                slot.nameLabel.color = Color.white;
                slot.nameLabel.alignment = TextAlignmentOptions.Center;
                // 이름은 한 줄 고정 — 길이는 오토사이즈가 흡수(코드베이스 관례: 랩은 명시 설정).
                slot.nameLabel.textWrappingMode = TextWrappingModes.NoWrap;
                slot.nameLabel.raycastTarget = false;

                // hand-card-face unit 1 — 우상단 대상+역할 칩(타입 색은 무명 코드 — 칩이 해설).
                if (_chipSprite == null)
                    _chipSprite = UiRoundedSprite.Make(11f, 0f, Color.white, Color.white);
                slot.tagChip = new GameObject("TargetTag", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
                slot.tagChip.transform.SetParent(slot.root.transform, false);
                var chipRt = (RectTransform)slot.tagChip.transform;
                chipRt.anchorMin = new Vector2(1f, 1f);
                chipRt.anchorMax = new Vector2(1f, 1f);
                chipRt.pivot = new Vector2(1f, 1f);
                chipRt.anchoredPosition = new Vector2(-8f, -8f);
                chipRt.sizeDelta = new Vector2(96f, 24f);
                slot.tagBg = slot.tagChip.GetComponent<Image>();
                slot.tagBg.sprite = _chipSprite;
                slot.tagBg.type = Image.Type.Sliced;
                slot.tagBg.raycastTarget = false;
                slot.tagGroup = slot.tagChip.GetComponent<CanvasGroup>();

                var tagTextGO = new GameObject("Value", typeof(RectTransform));
                tagTextGO.transform.SetParent(slot.tagChip.transform, false);
                var ttrt = (RectTransform)tagTextGO.transform;
                ttrt.anchorMin = Vector2.zero;
                ttrt.anchorMax = Vector2.one;
                ttrt.offsetMin = new Vector2(4f, 0f);
                ttrt.offsetMax = new Vector2(-4f, 0f);
                slot.tagLabel = tagTextGO.AddComponent<TextMeshProUGUI>();
                if (labelFont != null) slot.tagLabel.font = labelFont;
                slot.tagLabel.fontSize = 15;
                slot.tagLabel.enableAutoSizing = true;
                slot.tagLabel.fontSizeMin = 10;
                slot.tagLabel.fontSizeMax = 15;
                slot.tagLabel.color = Color.white;
                slot.tagLabel.alignment = TextAlignmentOptions.Center;
                slot.tagLabel.textWrappingMode = TextWrappingModes.NoWrap; // 칩 한 줄 고정
                slot.tagLabel.raycastTarget = false;

                // hand-card-face unit 1 — 하단 효과 본문(계약 8: floor 16pt — 실기기 가독 하한).
                var bodyGO = new GameObject("Body", typeof(RectTransform), typeof(CanvasGroup));
                bodyGO.transform.SetParent(slot.root.transform, false);
                var brt = (RectTransform)bodyGO.transform;
                brt.anchorMin = Vector2.zero;
                brt.anchorMax = Vector2.one;
                brt.offsetMin = new Vector2(10f, 8f);
                brt.offsetMax = new Vector2(-10f, -(HeaderH + 4f));
                slot.bodyGroup = bodyGO.GetComponent<CanvasGroup>();
                slot.bodyLabel = bodyGO.AddComponent<TextMeshProUGUI>();
                if (labelFont != null) slot.bodyLabel.font = labelFont;
                slot.bodyLabel.fontSize = 24;
                slot.bodyLabel.enableAutoSizing = true;
                slot.bodyLabel.fontSizeMin = 18; // 계약 8 floor(16) 상회 — 사용자 확정 상향
                slot.bodyLabel.fontSizeMax = 24;
                slot.bodyLabel.color = new Color(0.92f, 0.92f, 0.98f, 1f);
                slot.bodyLabel.alignment = TextAlignmentOptions.TopLeft;
                // 본문은 카드 폭 안에서 줄바꿈 — 랩 명시(기본값 신뢰 금지, 미설정 시 한 줄로
                // 카드 밖 유출). floor 16pt 로도 넘치는 극단 케이스는 말줄임으로 방어.
                slot.bodyLabel.textWrappingMode = TextWrappingModes.Normal;
                slot.bodyLabel.overflowMode = TextOverflowModes.Ellipsis;
                slot.bodyLabel.raycastTarget = false;

                // Cost badge: top-left round chip.
                slot.costBadge = new GameObject("Cost", typeof(RectTransform), typeof(Image));
                slot.costBadge.transform.SetParent(slot.root.transform, false);
                var crt = (RectTransform)slot.costBadge.transform;
                crt.anchorMin = new Vector2(0f, 1f);
                crt.anchorMax = new Vector2(0f, 1f);
                crt.pivot = new Vector2(0.5f, 0.5f);
                crt.anchoredPosition = new Vector2(10f, -10f);
                crt.sizeDelta = new Vector2(44f, 44f);
                var badgeImg = slot.costBadge.GetComponent<Image>();
                badgeImg.color = new Color(0.62f, 0.4f, 1f, 0.95f); // gauge fill color
                badgeImg.raycastTarget = false;
                slot.costGroup = slot.costBadge.AddComponent<CanvasGroup>(); // 크럼플 중 페이드용

                var costTextGO = new GameObject("Value", typeof(RectTransform));
                costTextGO.transform.SetParent(slot.costBadge.transform, false);
                var ctrt = (RectTransform)costTextGO.transform;
                ctrt.anchorMin = Vector2.zero;
                ctrt.anchorMax = Vector2.one;
                ctrt.offsetMin = Vector2.zero;
                ctrt.offsetMax = Vector2.zero;
                slot.costLabel = costTextGO.AddComponent<TextMeshProUGUI>();
                if (numberFont != null) slot.costLabel.font = numberFont;
                slot.costLabel.fontSize = 24;
                slot.costLabel.color = Color.white;
                slot.costLabel.alignment = TextAlignmentOptions.Center;
                slot.costLabel.raycastTarget = false;

                slot.homePos = slot.rect.anchoredPosition;
                slot.homeRotZ = rotZ; // 원값 저장(localEulerAngles.z 360 wrap 회피)
                slot.targetPos = slot.homePos;
                slot.targetRotZ = rotZ;
                slot.targetScale = 1f;
                slot.dragSlot = slot.root.AddComponent<DreamcatcherCardDragSlot>();
                slot.dragSlot.Bind(this, i);

                _slots.Add(slot);
            }
            // card-crumple-unfold — 카드가 실제 렌더되는 캔버스에 uv1/uv2 채널 보장(BuildCanvas 의
            // GetComponentInChildren 이 카드 렌더 캔버스와 다를 수 있어 실제 ancestor 로 확정).
            if (_slots.Count > 0)
            {
                var cv = _slots[0].root.GetComponentInParent<Canvas>();
                if (cv != null)
                    cv.additionalShaderChannels |=
                        AdditionalCanvasShaderChannels.TexCoord1 | AdditionalCanvasShaderChannels.TexCoord2;
            }
            UiLayer.Apply(gameObject);
        }
    }
}
