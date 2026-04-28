using Unity.Mathematics;

namespace Wassup.Battle.Combat.Projectile
{
    // Combat→Presentation crossing payload. ProjectileHitSystem enqueues one of
    // these per direct-target impact so the MonoBehaviour view pool can play the
    // configured hit prefab without any ECS reference. Splash secondary targets
    // are intentionally not represented here — the visual is one impact per shot.
    public struct ProjectileHitEvent
    {
        public float3 position;
        public int dataIndex;
    }
}
