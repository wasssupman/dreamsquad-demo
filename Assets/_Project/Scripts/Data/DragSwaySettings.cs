using UnityEngine;
using UnityEngine.Serialization;

namespace Wassup.Data
{
    // keyring-cord-preview — 드래그/탭 배치 프리뷰 튜닝값 묶음(키링 스윙 = 제어형 스프링 진자).
    // 움직임은 자유 물리 시뮬이 아니라 이동범위·각도·스프링·댐핑으로 제어한다(고리 아래 실루엣이 진자처럼 스윙).
    // 컨트롤러가 런타임 AddComponent 라 인스펙터 튜닝이 안 되므로 SO 로 분리. DefenderSelector 에 할당 →
    // Configure 로 주입. 미할당이면 컨트롤러가 클래스 기본값 인스턴스로 폴백. 에셋 편집은 런타임 즉시 반영.
    //
    // 그룹 맵(인스펙터 헤더 순): ① 키링 추종  ② 줄·고리 비주얼  ③ 스타일  ④ 배치 컷신
    //                          ⑤ 컷신 틸트  ⑥ 셀 스냅  ⑦ 탭 배치 시뮬  ⑧ 방향 페이즈
    [CreateAssetMenu(menuName = "Wassup/Drag Sway Settings", fileName = "DragSwaySettings")]
    public class DragSwaySettings : ScriptableObject
    {
        [Header("① 키링 추종 — 고리 밑 유닛이 진자처럼 지연 스윙 (스프링+속도상한)")]
        [Tooltip("줄 길이 — 고리 아래 매달리는 로컬 길이(월드=×visualScale). ↑=길게 늘어짐.")]
        public float ropeLength = 2.0f;
        [Tooltip("최대 기울임각 — 유닛이 흔들리는 각도 상한(deg). ↓=덜 흔들림.")]
        public float maxAngle = 8f;
        [Tooltip("추종 강성 — 목표를 따라붙는 탄성 세기. ↑=팽팽/빠릿, ↓=늘어지고 부드럽게.")]
        public float spring = 100f;
        [Tooltip("감쇠 — 탄성 잔진동 억제. ↑=빨리 멎음, ↓=바운스·출렁 큼(floaty).")]
        public float damping = 2.5f;
        [Tooltip("추종 속도 상한(월드/s) — 빠른 스와이프 시 튀어나감만 방지(탄성은 유지). 0=무제한.")]
        public float maxSpeed = 12f;

        [Header("② 줄·고리 비주얼 — 프리뷰 줄/고리 굵기·색·크기")]
        [Tooltip("줄 폭(로컬, 월드=×visualScale) — 너무 얇으면 sub-pixel 로 렌더 컬링됨.")]
        public float cordWidth = 0.14f;
        [Tooltip("줄 색 — 절차적 폴백 줄/고리 틴트(스타일 스프라이트 있으면 무시).")]
        public Color cordColor = new Color(0.45f, 0.38f, 0.28f, 1f);
        [Tooltip("고리 반경(로컬, 월드=×visualScale).")]
        public float ringRadius = 0.18f;
        [Tooltip("실루엣 추가 드롭(로컬) — 머리는 줄 끝 자동정렬, 이 값은 미세조정. 0=머리가 줄 끝.")]
        public float charmDrop = 0.0f;

        [Header("③ 스타일 — 월드 슬롯 스프라이트(비우면 절차적 폴백)")]
        [Tooltip("키링 스타일 — 월드 슬롯(ringSprite/worldCord·RingMaterial) 사용. 비우면 절차적 폴백(원 루프 + cordColor 단색 줄).")]
        public KeyringStyle style;

        [Header("④ 배치 컷신 — 드래그 시작 시 좌상단 유닛 컷신 on/off")]
        [Tooltip("컷신 on/off — 드래그 배치 시작 시 좌상단 유닛 컷신 재생. 끄면 프레임 있어도 미재생.")]
        public bool enableDeployCutscene = true;

        [Header("⑤ 컷신 틸트 — 스와이프 속도 → 컷신 기울임 피드")]
        [Tooltip("틸트 포화 속도 — 이 화면px/s 스와이프에서 틸트 |1| 포화. ↓=작은 스와이프도 크게 기욺.")]
        public float deployCutsceneSwipeRefSpeed = 1400f;
        [Tooltip("틸트 스무딩 — 스와이프 속도 exp-lerp 계수(0..1). ↑=빠릿(즉각), ↓=부드럽게 지연.")]
        public float deployCutsceneSwipeSmoothing = 0.5f;

        [Header("⑥ 셀 스냅 — 포커스 칸 히스테리시스·throttle·액체 하이라이트")]
        [Tooltip("자석 세기 — 타일 경계 sticky 여유(타일 분수). 떨림 면역=2×margin, 대가로 전환이 margin 만큼 늦음.\n" +
                 "0=순수 반올림(면역 0). 0.2~0.3 권장. 0.5↑=이웃 칸에 깊이 들어가도 안 넘어감(끈적).\n" +
                 "상한 0.95 — 1.0 이상이면 이웃 셀을 건너뛴다(코드에서 clamp).")]
        [Range(0f, 0.95f)]
        public float placementStickMargin = 0.3f;
        [Tooltip("판정 갱신 주기(초) — 이동 중 이 간격마다 현재 칸으로 스텝 갱신. ↓=더 자주(실시간에 가까움). 0=매 프레임.")]
        [Range(0f, 1f)]
        public float placementCommitInterval = 0.5f;
        [Tooltip("끈적 액체 하이라이트 — 포커스 셀 하이라이트를 '고정 테두리 + 손가락 쪽으로 번지는 내부 액체'로 대체.\n" +
                 "히스테리시스(margin)의 시각화: 번짐이 테두리를 넘으면 곧 옆 칸으로 전환된다는 예고.\n" +
                 "끄면 기존 타일 하이라이트. 모양 튜닝 = PlacementLiquidTile.mat, 색 = TilemapMapView(liquid*).")]
        [FormerlySerializedAs("stickyBlobEnabled")]
        public bool stickyLiquidEnabled = true;

        [Header("⑦ 탭 배치 시뮬 — 탭→타일 던지기(시간·거리·곡선·정착)")]
        [Tooltip("비행 기준 시간(초) — 화면 세로 1개 이동 시 이 값. 실제=tray→타일 화면거리 비례(min~max 배 clamp).")]
        [Range(0.2f, 5f)]
        public float tapTravelDuration = 3f;
        [Tooltip("비행시간 하한 배수 — 가까운 타일(짧은 비행).")]
        [Range(0.05f, 1f)]
        public float tapTravelScaleMin = 0.25f;
        [Tooltip("비행시간 상한 배수 — 먼 타일(긴 비행).")]
        [Range(1f, 3f)]
        public float tapTravelScaleMax = 1.5f;
        [Tooltip("arm 하이라이트 색 — 탭 선택된 트레이 슬롯. 확정 팝 valid 색과 톤 맞춤.")]
        public Color armHighlightColor = new Color(0.35f, 1f, 0.9f, 0.28f);
        [Tooltip("곡선 아치 높이(직선거리 배수) — 카메라-up 으로 제어점 띄움, 유닛이 솟았다 내려옴. 0=직선.")]
        [Range(0f, 1f)]
        public float tapArcHeightFactor = 0.32f;
        [Tooltip("곡선 좌우 폭(직선거리 배수) — 매 탭 부호·크기가 달라 경로가 매번 다름. 0=좌우 없음.")]
        [Range(0f, 1f)]
        public float tapArcLateralFactor = 0.22f;
        [Tooltip("던지기 시작 제어점 — x=목표까지 전진 비율, y=아치 높이 배수. 앞·위로 튀어나가는 출발 접선.")]
        public Vector2 tapThrowLaunchControl = new Vector2(0.18f, 1f);
        [Tooltip("던지기 도착 제어점 — x=목표까지 전진 비율, y=아치 높이 배수. 낮게 내려오는 착지 접선.")]
        public Vector2 tapThrowLandingControl = new Vector2(0.72f, 0.22f);
        [Tooltip("탭 비행 후 실제 프리뷰가 최종 발 위치에 정착했다고 보는 거리 오차(월드).")]
        [Range(0f, 1f)]
        public float tapSettleDistance = 0.06f;
        [Tooltip("탭 비행 후 정착했다고 보는 프리뷰 속도 상한(월드/s).")]
        [Range(0f, 5f)]
        public float tapSettleSpeed = 0.4f;
        [Tooltip("탭 비행·정착 중 곡선 목표를 비진동으로 추종하는 SmoothDamp 시간(초). 낮을수록 빠르게 붙음.")]
        [Range(0.01f, 0.3f)]
        [FormerlySerializedAs("tapSettleSmoothTime")]
        public float tapFollowSmoothTime = 0.06f;
        [Tooltip("정착 대기 최대시간(초, unscaled). 초과하면 최종 위치로 정렬 후 즉시 배치.")]
        [Range(0.05f, 1f)]
        public float tapSettleMaxDuration = 0.28f;

        [Header("⑧ 방향 페이즈 — 방향 지정 중 전투 슬로우모")]
        // 방향 지정 컨트롤러는 런타임 AddComponent 라 자체 인스펙터가 없다. 튜닝값이
        // 이 하나뿐이라 전용 SO(구 DirectionAimSettings) 대신 이미 씬에 배선된 여기로 합쳤다.
        [Tooltip("전투 시간 배율 — 방향 지정 중. 드래그 슬로우모를 이어받음. 0 금지(전투가 멈추면 안 된다).")]
        [Range(0.01f, 1f)]
        public float directionAimSlowmoScale = 0.2f;
    }
}
