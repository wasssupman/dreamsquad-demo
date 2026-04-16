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

    [CreateAssetMenu(fileName = "Projectile", menuName = "Wassup/Projectile", order = 13)]
    public class ProjectileData : ScriptableObject
    {
        public string id;
        public float speed = 10f;
        public float hitThreshold = 0.3f;
        public Mesh visualMesh;
        public Material visualMaterial;
        public float visualScale = 0.3f;

        // 아래 4개는 Phase 3에서 로드만 되고 사용하지 않는다. Phase 4에서 소비.
        public OnHitEffectType onHitEffect = OnHitEffectType.None;
        public float onHitMagnitude;
        public float onHitDuration;
        public float splashRadius;
    }
}
