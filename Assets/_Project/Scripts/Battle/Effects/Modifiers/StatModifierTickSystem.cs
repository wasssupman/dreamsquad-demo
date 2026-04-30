// Spec unit 3 (modifier-framework-and-healer): tick StatModifierSlot remaining,
// remove expired slots, and mark BuffStatsDirty when any slot expires.
using Unity.Burst;
using Unity.Entities;

namespace Wassup.Battle.Effects
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ModifierApplySystem))]
    [UpdateBefore(typeof(BuffStatsAggregateSystem))]
    public partial struct StatModifierTickSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.Time.DeltaTime;

            // Query entities that have both StatModifierSlot buffer and BuffStatsDirty (enabled or disabled).
            // Use WithEntityAccess so we can fetch a writable buffer via SystemAPI.GetBuffer.
            foreach (var (dirty, entity) in
                     SystemAPI.Query<EnabledRefRW<BuffStatsDirty>>()
                              .WithAll<StatModifierSlot>()
                              .WithEntityAccess())
            {
                var slots = SystemAPI.GetBuffer<StatModifierSlot>(entity);
                bool changed = false;
                for (int i = slots.Length - 1; i >= 0; i--)
                {
                    var s = slots[i];
                    s.header.remaining -= dt;
                    if (s.header.remaining <= 0f)
                    {
                        slots.RemoveAtSwapBack(i);
                        changed = true;
                    }
                    else
                    {
                        slots[i] = s;
                    }
                }
                if (changed)
                    dirty.ValueRW = true;
            }
        }
    }
}
