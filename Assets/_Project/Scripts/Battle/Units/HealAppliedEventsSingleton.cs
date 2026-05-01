using Unity.Collections;
using Unity.Entities;

namespace Wassup.Battle.Units
{
    // Queue owned by BattleBridge. DamageApplicationSystem enqueues applied
    // IncomingHeal pulse positions; BattleBridge drains and plays heal VFX.
    public struct HealAppliedEventsSingleton : IComponentData
    {
        public NativeQueue<HealAppliedEvent> queue;
    }
}
