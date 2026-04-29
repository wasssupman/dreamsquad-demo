using Unity.Burst;
using Unity.Entities;
using Wassup.Battle.Units;

namespace Wassup.Battle.Effects
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(CcApplySystem))]
    [UpdateBefore(typeof(CcDecaySystem))]
    public partial struct DotApplySystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (SystemAPI.TryGetSingleton<HazardRuntimeEventsSingleton>(out var runtimeEvents))
            {
                new DotApplyWithEventsJob
                {
                    DeltaTime = SystemAPI.Time.DeltaTime,
                    RuntimeEvents = runtimeEvents.queue.AsParallelWriter(),
                }.Run();
            }
            else
            {
                new DotApplyJob { DeltaTime = SystemAPI.Time.DeltaTime }.Run();
            }
        }

        [BurstCompile]
        partial struct DotApplyJob : IJobEntity
        {
            public float DeltaTime;

            void Execute(in DynamicBuffer<CcEffect> ccBuffer, ref DynamicBuffer<IncomingDamage> damageBuffer)
            {
                for (int i = 0; i < ccBuffer.Length; i++)
                {
                    var cc = ccBuffer[i];
                    if (cc.kind != CcKind.DoT) continue;
                    damageBuffer.Add(new IncomingDamage { amount = cc.scalar * DeltaTime });
                }
            }
        }

        [BurstCompile]
        partial struct DotApplyWithEventsJob : IJobEntity
        {
            public float DeltaTime;
            public Unity.Collections.NativeQueue<HazardRuntimeEvent>.ParallelWriter RuntimeEvents;

            void Execute(Entity entity, in DynamicBuffer<CcEffect> ccBuffer, ref DynamicBuffer<IncomingDamage> damageBuffer)
            {
                for (int i = 0; i < ccBuffer.Length; i++)
                {
                    var cc = ccBuffer[i];
                    if (cc.kind != CcKind.DoT) continue;
                    float amount = cc.scalar * DeltaTime;
                    damageBuffer.Add(new IncomingDamage { amount = amount });
                    RuntimeEvents.Enqueue(new HazardRuntimeEvent
                    {
                        eventType = HazardRuntimeEventType.DotDamage,
                        kind = CcKind.DoT,
                        target = entity,
                        scalar = cc.scalar,
                        amount = amount,
                    });
                }
            }
        }
    }
}
