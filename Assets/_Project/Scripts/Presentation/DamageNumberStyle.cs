using UnityEngine;

namespace Wassup.Presentation
{
    // Serialized tuning bundle for floating damage numbers. Lives on
    // DamageNumberSpawner and is passed by-ref to each DamageNumberView.Play.
    // No hardcoded numbers in the view/spawner — everything tunable here.
    [System.Serializable]
    public class DamageNumberStyle
    {
        [Header("Lifetime / motion")]
        [Tooltip("팝업 총 수명(초)")]
        public float lifetime = 0.8f;
        [Tooltip("수명 동안 위로 이동하는 월드 거리")]
        public float driftUp = 0.7f;

        [Header("Magnitude → size")]
        [Tooltip("정규화 하한: 이 데미지 이하는 최소 크기/색")]
        public float lowDamage = 1f;
        [Tooltip("정규화 상한: 이 데미지 이상은 최대 크기/색")]
        public float highDamage = 50f;
        [Tooltip("월드 TMP 폰트 크기(최소 데미지)")]
        public float minFontSize = 5.2f;
        [Tooltip("월드 TMP 폰트 크기(최대 데미지)")]
        public float maxFontSize = 11.7f;

        [Header("Magnitude → color (흰→노랑→주황→빨강)")]
        public Gradient damageColor = new Gradient();

        [Header("Punch")]
        [Tooltip("큰 히트일수록 스케일 오버슈트 증폭 배수")]
        public float bigHitPunchMul = 1.6f;
        [Tooltip("수명 0→1 동안 스케일 배수(오버슈트 펀치)")]
        public AnimationCurve scaleCurve = new AnimationCurve();
        [Tooltip("수명 0→1 동안 페이스 알파")]
        public AnimationCurve alphaCurve = new AnimationCurve();

        // clamp01 normalized magnitude used for size + color.
        public float Normalize(float amount)
        {
            float span = Mathf.Max(0.0001f, highDamage - lowDamage);
            return Mathf.Clamp01((amount - lowDamage) / span);
        }

        public Color EvaluateColor(float t)
        {
            return damageColor.Evaluate(Mathf.Clamp01(t));
        }

        // Populates sensible defaults when curves/gradient are empty (fresh component).
        public void EnsureDefaults()
        {
            if (scaleCurve == null || scaleCurve.length == 0)
            {
                scaleCurve = new AnimationCurve(
                    new Keyframe(0f, 0.4f),
                    new Keyframe(0.15f, 1.25f),
                    new Keyframe(0.35f, 1.0f),
                    new Keyframe(1f, 0.85f));
            }
            if (alphaCurve == null || alphaCurve.length == 0)
            {
                alphaCurve = new AnimationCurve(
                    new Keyframe(0f, 1f),
                    new Keyframe(0.6f, 1f),
                    new Keyframe(1f, 0f));
            }
            if (damageColor == null || damageColor.colorKeys == null || damageColor.colorKeys.Length == 0)
            {
                damageColor = new Gradient();
                damageColor.SetKeys(
                    new[]
                    {
                        new GradientColorKey(Color.white, 0f),
                        new GradientColorKey(new Color(1f, 0.92f, 0.23f), 0.4f),   // yellow
                        new GradientColorKey(new Color(1f, 0.55f, 0.1f), 0.7f),    // orange
                        new GradientColorKey(new Color(1f, 0.2f, 0.18f), 1f),      // red
                    },
                    new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            }
        }
    }
}
