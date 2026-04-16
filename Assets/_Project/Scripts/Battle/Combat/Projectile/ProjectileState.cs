using Unity.Entities;
using Wassup.Data;

namespace Wassup.Battle.Combat.Projectile
{
    // Per-projectile flight data. `damage` is a snapshot taken at launch (already
    // multiplied by any active DamageBoost / Synergy on the shooter); it does not
    // change in flight even if the boost expires before the projectile lands.
    //
    // Phase 4: onHit fields repopulate now that Splash is actually consumed by
    // ProjectileHitSystem. Other OnHitEffectType values remain unimplemented.
    public struct ProjectileState : IComponentData
    {
        public Entity target;
        public float speed;
        public float damage;
        public float hitThreshold;
        public OnHitEffectType onHitEffect;
        public float splashRadius;
        public float splashDamageMul;
    }
}
