using UnityEngine;

namespace Wassup.Data
{
    // Phase 3은 None만 소비한다. Phase 4에서 DoT/Splash/Slow 등으로 확장.
    public enum OnHitEffectType
    {
        None,
        Poison,
        Fire,
        Splash,
        Slow,
    }

    public enum TextureSelectMode
    {
        Random,
        Sequential,
        First,
    }

    public enum ProjectileFacing
    {
        AlongVelocity,
        FixedUp,
        SpinAroundUp,
    }

    [CreateAssetMenu(fileName = "Projectile", menuName = "Wassup/Projectile", order = 13)]
    public class ProjectileData : ScriptableObject
    {
        public string id;
        public float speed = 10f;
        public float hitThreshold = 0.3f;
        public float visualScale = 0.3f;

        [Tooltip("비행체 시각을 view 공간에서 위로 띄우는 높이(월드 유닛). ECS/속도엔 영향 없이 렌더 Y 에만 더한다. 타일에 깔리는 것 방지용.")]
        public float visualHeightOffset = 0f;

        public GameObject projectilePrefab;
        public GameObject hitPrefab;

        public ProjectileFacing facing = ProjectileFacing.AlongVelocity;
        public float spinSpeed = 0f;

        [Header("As-is VFX")]
        [Tooltip("true 면 tint/emission/texture 변종 recolor 를 건너뛰고 프리팹 머티리얼 고유 색을 " +
                 "그대로 쓴다. 이미 색이 완성된 VFX(예: GabrielAguiar) 용. false 면 기존 데이터 recolor.")]
        public bool preserveVfxColors = false;

        [Header("Variation - deterministic")]
        public Color tintColor = Color.white;
        public float emissionMultiplier = 1f;

        [Header("Variation - per-shot random")]
        [Range(0f, 1f)] public float scaleJitter = 0f;
        [Range(0f, 0.5f)] public float hueJitter = 0f;
        [Range(0f, 360f)] public float rotationJitter = 0f;

        [Header("Texture variants")]
        public Texture2D[] textureVariants;
        public TextureSelectMode selectMode = TextureSelectMode.Random;

        [Header("Hit VFX")]
        public float hitVfxLifetime = 0f;

        [Header("Cast VFX")]
        public GameObject castPrefab;
        public float castVfxLifetime = 0f;

        // Phase 3에서 enum 필드만 존재. Phase 4부터 Splash가 실 사용 (Poison/Fire/Slow는
        // 여전히 미구현 자리표시).
        public OnHitEffectType onHitEffect = OnHitEffectType.None;
        public float onHitMagnitude;
        public float onHitDuration;
        public float splashRadius;
        public float splashDamageMul = 0.5f;
    }
}
