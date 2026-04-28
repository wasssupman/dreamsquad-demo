using Unity.Collections;
using Unity.Entities;

namespace Wassup.Battle.Combat.Projectile
{
    // Singleton entity holding the queue ProjectileHitSystem enqueues into. The
    // BattleBridge owns and disposes the queue (same pattern as the goal /
    // defender-death / meteor-burst / defender-attack channels).
    public struct ProjectileHitEventsSingleton : IComponentData
    {
        public NativeQueue<ProjectileHitEvent> queue;
    }
}
