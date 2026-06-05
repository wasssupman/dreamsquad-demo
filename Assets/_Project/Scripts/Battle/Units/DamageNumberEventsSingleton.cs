using Unity.Collections;
using Unity.Entities;

namespace Wassup.Battle.Units
{
    // Queue owned by BattleBridge. DamageApplicationSystem enqueues one event per
    // enemy whose IncomingDamage was applied this frame; BattleBridge drains and
    // spawns floating damage numbers.
    public struct DamageNumberEventsSingleton : IComponentData
    {
        public NativeQueue<DamageNumberEvent> queue;
    }
}
