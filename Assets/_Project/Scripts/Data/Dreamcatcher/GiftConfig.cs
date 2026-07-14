using UnityEngine;

namespace Wassup.Data
{
    // gift-phase unit 0 — 선물 페이즈(배치 직전) 이벤트/연출 파라미터.
    // "루시드의 선물"(공용 스킬 Active 2장) vs "림의 선물"(무의식 2장) 이벤트를
    // 가중 랜덤으로 고르고, 발라트로식 셔플 연출의 각 구간 타이밍을 담는다.
    // 하드코딩 금지 — 모든 수치는 이 SO 에서. GiftPhaseView / DreamcatcherHandController 가 소비.
    public enum GiftKind { Lucid, Rim }

    [CreateAssetMenu(fileName = "GiftConfig", menuName = "Wassup/GiftConfig", order = 21)]
    public class GiftConfig : ScriptableObject
    {
        [Header("Event weights (Lucid vs Rim, 가중 랜덤)")]
        [Min(0f)] public float lucidWeight = 1f;
        [Min(0f)] public float rimWeight = 1f;

        [Header("Rim gift")]
        [Tooltip("림의 선물이 지급하는 무의식 카드 수")]
        [Min(0)] public int rimGiftCount = 2;

        [Header("Sequence timings (unit 2, seconds, unscaled — 합산 ≤ 6s)")]
        public float introTextSec = 0.6f;
        public float dealInSec = 1.1f;
        public float revealSec = 0.9f;
        public float stackSec = 0.45f;
        public float riffleSec = 0.85f;
        public float fanSec = 0.6f;
        [Tooltip("흡수 가속 케이던스 — 첫 간격/최소 간격/감쇠")]
        public float absorbFirstGapSec = 0.22f;
        public float absorbMinGapSec = 0.07f;
        [Range(0.3f, 1f)] public float absorbDecay = 0.75f;
        public float absorbFlightSec = 0.24f;
        [Tooltip("12번째 카드 피니셔 — 반박자 멈춤")]
        public float finisherPauseSec = 0.15f;

        [Header("Geometry (unit 2, cardsRoot 로컬)")]
        [Min(1)] public int gridCols = 5;
        public Vector2 gridCell = new Vector2(210f, 300f);
        public Vector2 gridCenter = new Vector2(0f, 90f);
        [Tooltip("선물 리빌 센터 무대 — 두 장 좌우 간격 절반 / 높이 / 과시 스케일")]
        public float revealSpreadX = 130f;
        public float revealY = 60f;
        public float revealScale = 1.6f;
        [Tooltip("개입 순간 내 덱 10장이 밀리는 거리(px)")]
        public float presenceNudge = 16f;
        public Vector2 stackCenter = new Vector2(0f, 40f);
        public float stackJitterRotDeg = 5f;
        public float stackJitterOffset = 6f;
        [Tooltip("리플 — 좌우 뭉치 벌림 거리 / 바깥 틸트(도)")]
        public float riffleSplitX = 180f;
        public float riffleTiltDeg = 12f;
        [Tooltip("부채꼴 — 원호 반경 / 전개 각도 / 밑단 y")]
        public float fanRadius = 950f;
        public float fanArcDeg = 44f;
        public float fanBaseY = -330f;
        [Tooltip("수신 앵커 링 — 반경 / 세그먼트 점 크기")]
        public float ringRadius = 80f;
        public float ringDotSize = 16f;

        [Header("Ambience (unit 2, kind별)")]
        public Color lucidAmbientColor = new Color(1f, 0.82f, 0.42f, 0.30f);
        public Color rimAmbientColor = new Color(0.72f, 0.10f, 0.10f, 0.36f);
        [Tooltip("셔플 완료 글로우 리플 알파")]
        [Range(0f, 1f)] public float rippleAlpha = 0.5f;
        public bool vibrateOnReveal = true;

        [Header("Card & frame (unit 1)")]
        public Vector2 cardSize = new Vector2(180f, 252f);
        [Min(1f)] public float frameRadius = 18f;
        [Min(1f)] public float frameBorder = 10f;
        [Min(0f)] public float artInset = 10f;
        [Min(1f)] public float nameFontSize = 22f;
        public Color facePlateColor = new Color(0.10f, 0.13f, 0.22f, 1f);
        [Tooltip("아트 없는 카드의 폴백 채움색 (일반 / 무의식)")]
        public Color fallbackNormalColor = new Color(0.22f, 0.28f, 0.44f, 1f);
        public Color fallbackSubconsciousColor = new Color(0.46f, 0.28f, 0.62f, 1f);
        [Tooltip("루시드 = 금빛, 림 = 붉은색 (프레임 + 뒷면 계열)")]
        public Color lucidFrameColor = new Color(1f, 0.84f, 0.35f, 1f);
        public Color rimFrameColor = new Color(0.92f, 0.22f, 0.20f, 1f);
        [Range(0f, 1f)] [Tooltip("뒷면 덮개가 프레임 색에서 어두워지는 정도")]
        public float backDarken = 0.55f;

        [Header("Holo foil (unit 1)")]
        [Range(0f, 1f)] public float foilIntensity = 0.35f;
        [Range(0f, 4f)] public float foilSpeed = 1.2f;
        [Range(0f, 1f)] public float foilHueShift = 0.06f;

        [Header("Test mode")]
        [Tooltip("테스트 모드에서 연출을 건너뛰고 즉시 배치로")]
        public bool fastForwardInTestMode = true;
    }
}
