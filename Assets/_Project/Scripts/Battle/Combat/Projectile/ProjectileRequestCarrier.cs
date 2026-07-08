using Unity.Entities;

namespace Wassup.Battle.Combat.Projectile
{
    // dreamcatcher-unit-trigger Unit 1 — tags a dedicated request-carrier entity
    // whose only job is to hold one ProjectileSpawnRequest that cannot live on
    // the shooter: ProjectileSpawnRequest is a single IComponentData per entity,
    // and the shooter's own attack may stage one in the same frame. The drain
    // destroys carriers outright (instead of RemoveComponent); teardown covers
    // stragglers via DestroyEntitiesByType<ProjectileRequestCarrier>.
    public struct ProjectileRequestCarrier : IComponentData { }
}
