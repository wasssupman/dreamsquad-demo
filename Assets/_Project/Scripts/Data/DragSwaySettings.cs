using UnityEngine;

namespace Wassup.Data
{
    // keyring-cord-preview — 드래그 프리뷰 키링 스윙(제어형 스프링 진자) + 줄/고리 비주얼 튜닝값.
    // 움직임은 자유 물리 시뮬이 아니라 이동범위·각도·스프링·댐핑으로 제어한다(고리 아래 실루엣이 진자처럼 스윙).
    // 컨트롤러가 런타임 AddComponent 라 인스펙터 튜닝이 안 되므로 SO 로 분리. DefenderSelector 에 할당 →
    // Configure 로 주입. 미할당이면 컨트롤러가 클래스 기본값 인스턴스로 폴백. 에셋 편집은 런타임 즉시 반영.
    [CreateAssetMenu(menuName = "Wassup/Drag Sway Settings", fileName = "DragSwaySettings")]
    public class DragSwaySettings : ScriptableObject
    {
        [Header("추종 (무게추 스프링 + 속도 상한)")]
        [Tooltip("고리→유닛 줄 길이(로컬, 월드=×visualScale). 고리 아래 매달리는 길이.")]
        public float ropeLength = 2.0f;
        [Tooltip("유닛 최대 기울임각(deg). 흔들리는 정도. ↓=덜 흔들림.")]
        public float maxAngle = 8f;
        [Tooltip("추종 강성/탄성(↑=팽팽/빠릿, ↓=늘어지고 부드럽게). 탄성 세기.")]
        public float spring = 100f;
        [Tooltip("감쇠(↓=바운스·출렁 큼/floaty, ↑=빨리 멎음). 탄성 잔진동.")]
        public float damping = 2.5f;
        [Tooltip("추종 최대 속도(월드/s). 빠른 스와이프 시 이 속도로만 제한 → 튀어나감 방지(탄성은 유지). 0=무제한.")]
        public float maxSpeed = 12f;

        [Header("줄/고리 비주얼")]
        [Tooltip("줄 폭(로컬, 월드=×visualScale). 너무 얇으면 sub-pixel 로 렌더 컬링됨.")]
        public float cordWidth = 0.14f;
        [Tooltip("줄 색.")]
        public Color cordColor = new Color(0.45f, 0.38f, 0.28f, 1f);
        [Tooltip("고리(링) 반경(로컬, 월드=×visualScale).")]
        public float ringRadius = 0.18f;
        [Tooltip("실루엣 추가 드롭(로컬). 머리는 줄 끝 자동정렬, 이 값은 미세조정. 0=머리가 줄 끝.")]
        public float charmDrop = 0.0f;

        [Header("스타일 (keyring-unify 3)")]
        [Tooltip("키링 스타일 — 월드 슬롯(ringSprite/worldCord·RingMaterial) 사용. 비우면 절차적 폴백(원 루프 + cordColor 단색 줄).")]
        public KeyringStyle style;

        [Header("배치 컷신 (defender-deploy-cutscene)")]
        [Tooltip("드래그 배치 시작 시 좌상단 유닛 컷신 재생 여부. 끄면 컷신 프레임이 있어도 재생하지 않는다.")]
        public bool enableDeployCutscene = true;

        [Header("배치 컷신 틸트 (depth-parallax)")]
        [Tooltip("이 화면px/s 스와이프에서 틸트 |1| 포화. ↓=작은 스와이프도 크게 기욺.")]
        public float deployCutsceneSwipeRefSpeed = 1400f;
        [Tooltip("스와이프 속도 exp-lerp 계수(0..1). ↑=빠릿(즉각), ↓=부드럽게 지연.")]
        public float deployCutsceneSwipeSmoothing = 0.5f;

        [Header("배치 셀 스냅 (placement-cell-snap)")]
        [Tooltip("타일 경계 sticky 여유(타일 분수) = 자석 세기. 떨림 면역 = 2×margin 타일, 대가로 전환이 margin 만큼 늦음.\n" +
                 "0=순수 반올림(면역 0). 0.2~0.3 권장. 0.5↑ = 손가락이 이웃 칸에 깊이 들어가도 안 넘어감(끈적).\n" +
                 "상한 0.95 — 1.0 이상이면 이웃 셀을 건너뛴다(코드에서 clamp).")]
        [Range(0f, 0.95f)]
        public float placementStickMargin = 0.3f;
        [Tooltip("타일 판정 갱신 주기(초). 이동 중에도 이 간격마다 현재 칸으로 스텝 갱신. ↓=더 자주(실시간에 가까움). 0=매 프레임.")]
        [Range(0f, 1f)]
        public float placementCommitInterval = 0.5f;
        [Tooltip("끈적 액체 하이라이트 — 포커스 셀 하이라이트를 '고정 테두리 + 손가락 쪽으로 번지는 내부 액체'로 대체.\n" +
                 "히스테리시스(margin)의 시각화: 번짐이 테두리를 넘으면 곧 옆 칸으로 전환된다는 예고.\n" +
                 "끄면 기존 타일 하이라이트. 모양 튜닝 = PlacementLiquidTile.mat, 색 = TilemapMapView(liquid*).")]
        public bool stickyBlobEnabled = true;

        [Header("탭 배치 시뮬레이션 (defender-tap-to-place)")]
        [Tooltip("탭 배치 D&D 시뮬 비행 기준 시간(초) — 화면 세로 1개만큼 이동 시 이 값. 실제는 tray→타일 화면거리(카메라 투영)에 비례(min~max 배 clamp).")]
        [Range(0.2f, 5f)]
        public float tapTravelDuration = 3f;
        [Tooltip("거리 비례 비행시간 하한 배수(가까운 타일).")]
        [Range(0.05f, 1f)]
        public float tapTravelScaleMin = 0.25f;
        [Tooltip("거리 비례 비행시간 상한 배수(먼 타일).")]
        [Range(1f, 3f)]
        public float tapTravelScaleMax = 1.5f;
        [Tooltip("트레이 슬롯 arm(탭 선택) 하이라이트 색. 확정 팝 valid 색과 톤 맞춤.")]
        public Color armHighlightColor = new Color(0.35f, 1f, 0.9f, 0.28f);
    }
}
