// Spec unit 3 (modifier-framework-and-healer): tick StatModifierSlot remaining,
// remove expired slots, and mark ModifierStatsDirty when any slot expires.
using Unity.Burst;
using Unity.Entities;

namespace Wassup.Battle.Effects
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ModifierApplySystem))]
    [UpdateBefore(typeof(ModifierStatsAggregateSystem))]
    public partial struct StatModifierTickSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.Time.DeltaTime;

            // Query ALL entities that have a StatModifierSlot buffer, regardless of
            // whether ModifierStatsDirty is enabled. Previously querying EnabledRefRW<ModifierStatsDirty>
            // caused entities with dirty=false (after Aggregate reset it) to be skipped,
            // so remaining was never decremented and modifiers lasted forever.
            foreach (var (_, entity) in
                     SystemAPI.Query<RefRO<ModifierStats>>()
                              .WithAll<StatModifierSlot>()
                              .WithEntityAccess())
            {
                var slots = SystemAPI.GetBuffer<StatModifierSlot>(entity);
                bool anyExpired = false;
                for (int i = slots.Length - 1; i >= 0; i--)
                {
                    var s = slots[i];
                    s.header.remaining -= dt;
                    if (s.header.remaining <= 0f)
                    {
                        slots.RemoveAtSwapBack(i);
                        anyExpired = true;
                    }
                    else
                    {
                        slots[i] = s;
                    }
                }
                // When a slot expires, wake Aggregate so it recomputes the stat cache.
                if (anyExpired && SystemAPI.HasComponent<ModifierStatsDirty>(entity))
                    SystemAPI.SetComponentEnabled<ModifierStatsDirty>(entity, true);
            }
        }
    }
}
