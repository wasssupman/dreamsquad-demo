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

        [Header("Magnitude → color (청록→스프링그린→골드→오렌지)")]
        public Gradient damageColor = new Gradient();

        [Header("Punch")]
        [Tooltip("큰 히트일수록 스케일 오버슈트 증폭 배수")]
        public float bigHitPunchMul = 1.6f;
        [Tooltip("수명 0→1 동안 스케일 배수(오버슈트 펀치)")]
        public AnimationCurve scaleCurve = new AnimationCurve();
        [Tooltip("수명 0→1 동안 페이스 알파")]
        public AnimationCurve alphaCurve = new AnimationCurve();

        [Header("Placement (grid stagger)")]
        [Tooltip("발치 view 위치에서 머리 위로 올릴 world-up 오프셋. ToView 이후 적용 — sim-Y 가 아니다(BoardSpace 가 sim 높이를 버림).")]
        public float headViewOffset = 1.4f;
        [Tooltip("겹침 방지 격자 셀 크기 (카메라축 투영 world 단위; x=화면 가로, y=화면 세로). Play 에서 4자리 숫자 비겹침으로 튜닝.")]
        public Vector2 cellSize = new Vector2(0.85f, 0.55f);
        [Tooltip("점유 시 빈 셀을 찾는 최대 링 수")]
        public int maxSearchRings = 4;

        [Header("Impact / motion")]
        [Tooltip("정점 그라데이션 상단 밝기 배수 (면색 × 이 값, clamp01)")]
        public float topBoost = 1.35f;
        [Tooltip("대형 히트 셰이크 진폭(월드). 소형 히트(_punchT→0)엔 0 수렴, 수명 초반 감쇠")]
        public float shakeAmp = 0.12f;
        [Tooltip("숫자별 index 결정론 미세 회전 최대 각(도)")]
        public float maxTiltDeg = 6f;

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
            if (NeedsPaletteDefault())
            {
                damageColor = new Gradient();
                damageColor.SetKeys(
                    new[]
                    {
                        new GradientColorKey(new Color(0.208f, 0.878f, 0.816f), 0f),  // cyan #35E0D0
                        new GradientColorKey(new Color(0.420f, 0.941f, 0.420f), 0.4f),// spring green #6BF06B
                        new GradientColorKey(new Color(1f, 0.824f, 0.227f), 0.7f),    // gold #FFD23A
                        new GradientColorKey(new Color(1f, 0.416f, 0.165f), 1f),      // hot orange #FF6A2A
                    },
                    new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            }
        }

        // `new Gradient()` 의 pristine 기본값(2키 모두 흰색)만 미구성으로 보고 팔레트를 채운다.
        // 이 게이트가 없으면(구 Length==0 검사) 흰→흰 기본이 length 2 라 팔레트가 영원히 미적용.
        // 실제 튜닝(키 3개+ 또는 비-흰색)은 보존한다.
        private bool NeedsPaletteDefault()
        {
            if (damageColor == null || damageColor.colorKeys == null || damageColor.colorKeys.Length == 0)
                return true;
            var k = damageColor.colorKeys;
            return k.Length == 2 && IsApproxWhite(k[0].color) && IsApproxWhite(k[1].color);
        }

        private static bool IsApproxWhite(Color c)
        {
            return c.r > 0.99f && c.g > 0.99f && c.b > 0.99f;
        }
    }
}
