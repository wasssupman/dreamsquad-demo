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
        [Tooltip("적 발치(sim y) 위로 바를 올리는 월드 높이")]
        [SerializeField] private float hitBarHeadYOffset = 1.0f;
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
            float r = ratio > 0f ? (ratio < 1f ? ratio : 1f) : 0f;
            return hitBarFillGradient != null ? hitBarFillGradient.Evaluate(r) : Color.white;
        }

        // ratio → tint Color. ratio 는 clamp[0,1] + NaN(=max<=0 division) 가드 후 gradient 평가.
        // 뷰는 이 메서드를 모른다 — BattleBridge 가 호출해 Color 만 뷰에 넘긴다.
        public Color EvaluateTint(float ratio)
        {
            // NaN-safe clamp: Mathf.Clamp01(NaN) 은 NaN 을 그대로 반환하므로 직접 처리.
            float r = ratio > 0f ? (ratio < 1f ? ratio : 1f) : 0f;
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
