using Unity.Entities;

namespace Wassup.Battle.Combat.Projectile
{
    // Per-projectile flight data. `damage` is a snapshot taken at launch (already
    // multiplied by any active DamageBoost on the shooter); it does not change in
    // flight even if the boost expires before the projectile lands.
    public struct ProjectileState : IComponentData
    {
        public Entity target;
        public float speed;
        public float damage;
        public float hitThreshold;
    }
}
