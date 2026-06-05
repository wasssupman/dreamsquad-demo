using Unity.Collections;
using Unity.Entities;

namespace Wassup.Battle.Units
{
    // Queue owned by BattleBridge. DamageApplicationSystem enqueues one event per
    // enemy killed by damage this frame; BattleBridge drains and bumps the live
    // score HUD.
    public struct EnemyKilledEventsSingleton : IComponentData
    {
        public NativeQueue<EnemyKilledEvent> queue;
    }
}
