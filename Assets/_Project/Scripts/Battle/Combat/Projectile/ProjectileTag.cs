using Unity.Entities;

namespace Wassup.Battle.Combat.Projectile
{
    // Identifies an in-flight projectile entity for teardown queries and the
    // ProjectileMove/Hit system filters.
    public struct ProjectileTag : IComponentData { }
}
