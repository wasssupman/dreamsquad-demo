using UnityEngine;

namespace Wassup.Data
{
    // unit-health-display — 체력 표기 시각 파라미터의 단일 소스 (하드코딩 금지 규칙).
    // unit 1 은 적 저체력 틴트만 사용. unit 2(마이크로바)/unit 3(타일 게이지) 필드는
    // 해당 unit 에서 이 SO 에 누적한다.
    [CreateAssetMenu(fileName = "HealthDisplayStyle", menuName = "Wassup/HealthDisplayStyle", order = 20)]
    public class HealthDisplayStyle : ScriptableObject
    {
        [Header("Enemy Low-Health Tint")]
        [Tooltip("time = hpRatio (1=만피, 0=빈사). 1.0 쪽 = 정상(白, 무틴트), 0 쪽 = 저체력 색(창백→검붉음).")]
        [SerializeField] private Gradient enemyTintGradient = DefaultEnemyTint();

        [Header("Enemy Hit Micro-Bar (unit 2)")]
        [Tooltip("바 월드 크기 (가로, 세로)")]
        [SerializeField] private Vector2 hitBarSize = new Vector2(0.9f, 0.14f);
        [Tooltip("적 발치(sim y) 위로 바를 올리는 높이. 카메라 평면 기준(월드 아님) — HeadAnchor 참조")]
        // 0.6 = 구 월드기준 1.0 과 화면상 같은 높이(HeadAnchor 등가식).
        [SerializeField] private float hitBarHeadYOffset = 0.6f;
        [Tooltip("피격 후 완전 표시 유지 시간(초)")]
        [SerializeField] private float hitBarHoldSec = 0.8f;
        [Tooltip("유지 종료 후 페이드 시간(초)")]
        [SerializeField] private float hitBarFadeSec = 0.3f;
        [Tooltip("바 배경색(테두리/빈칸)")]
        [SerializeField] private Color hitBarBgColor = new Color(0.08f, 0.08f, 0.09f, 0.8f);
        [Tooltip("fill 색 램프 (time = hpRatio: 1=녹/원색, 0=적)")]
        [SerializeField] private Gradient hitBarFillGradient = DefaultHitBarFill();

        public Vector2 HitBarSize => hitBarSize;
        public float HitBarHeadYOffset => hitBarHeadYOffset;
        public float HitBarHoldSec => Mathf.Max(0f, hitBarHoldSec);
        public float HitBarFadeSec => Mathf.Max(0.01f, hitBarFadeSec);
        public Color HitBarBgColor => hitBarBgColor;

        public Color EvaluateHitBarFill(float ratio)
        {
            float r = SafeRatio01(ratio);
            return hitBarFillGradient != null ? hitBarFillGradient.Evaluate(r) : Color.white;
        }

        [Header("Defender Tile Gauge (unit 3)")]
        [Tooltip("게이지 사각 한 변 길이 = tileSize × 이 비율")]
        [SerializeField] private float gaugeTileFill = 0.9f;
        [Tooltip("테두리 두께(월드)")]
        [SerializeField] private float gaugeThickness = 0.08f;
        [Tooltip("바닥 위로 띄우는 높이(z-fight 방지)")]
        [SerializeField] private float gaugeYOffset = 0.03f;
        [Tooltip("만피(1-eps)면 게이지 숨김")]
        [SerializeField] private bool gaugeHideWhenFull = true;
        [Tooltip("만피 판정 여유")]
        [SerializeField] private float gaugeFullEpsilon = 0.001f;
        [Tooltip("게이지 색 램프 (time = hpRatio: 1=녹, 0=적)")]
        [SerializeField] private Gradient gaugeColorGradient = DefaultHitBarFill();

        public float GaugeTileFill => Mathf.Clamp(gaugeTileFill, 0.1f, 1f);
        public float GaugeThickness => Mathf.Max(0.001f, gaugeThickness);
        public float GaugeYOffset => gaugeYOffset;
        public bool GaugeHideWhenFull => gaugeHideWhenFull;
        public float GaugeFullEpsilon => Mathf.Max(0f, gaugeFullEpsilon);

        public Color EvaluateGaugeColor(float ratio)
        {
            float r = SafeRatio01(ratio);
            return gaugeColorGradient != null ? gaugeColorGradient.Evaluate(r) : Color.white;
        }

        // ratio → tint Color. ratio 는 clamp[0,1] + NaN(=max<=0 division) 가드 후 gradient 평가.
        // 뷰는 이 메서드를 모른다 — BattleBridge 가 호출해 Color 만 뷰에 넘긴다.
        // NaN-safe [0,1] 정규화 — Mathf.Clamp01(NaN)==NaN 트랩 회피. 체력 표기 전역 단일 정의
        // (SO evaluator 3종 + 뷰의 fill 계산이 공유). NaN → 0(빈사).
        public static float SafeRatio01(float ratio)
            => ratio > 0f ? (ratio < 1f ? ratio : 1f) : 0f;

        public Color EvaluateTint(float ratio)
        {
            float r = SafeRatio01(ratio);
            return enemyTintGradient != null ? enemyTintGradient.Evaluate(r) : Color.white;
        }

        // 마이크로바 fill: 만피(1.0)=녹 → 중간=황 → 빈사(0.0)=적. 에셋에서 재저작 가능.
        private static Gradient DefaultHitBarFill()
        {
            var g = new Gradient();
            g.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.85f, 0.18f, 0.15f), 0f),   // 빈사 = 적
                    new GradientColorKey(new Color(0.95f, 0.78f, 0.20f), 0.5f), // 중간 = 황
                    new GradientColorKey(new Color(0.35f, 0.80f, 0.30f), 1f),   // 만피 = 녹
                },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return g;
        }

        // 만피(1.0)=白 → 저체력=창백 → 빈사(0.0)=검붉음. 에셋에서 재저작 가능.
        private static Gradient DefaultEnemyTint()
        {
            var g = new Gradient();
            g.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.55f, 0.10f, 0.10f), 0f),   // 빈사 = 검붉음
                    new GradientColorKey(new Color(0.82f, 0.66f, 0.62f), 0.45f), // 저체력 = 창백/탈색
                    new GradientColorKey(Color.white, 1f),                        // 만피 = 원색(무틴트)
                },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return g;
        }
    }
}
