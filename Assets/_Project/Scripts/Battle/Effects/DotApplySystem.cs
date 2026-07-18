using Unity.Burst;
using Unity.Entities;
using Wassup.Battle.Units;

namespace Wassup.Battle.Effects
{
    [BurstCompile]
    [UpdateInGroup(typeof(BattleSimGroup))]
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

            void Execute(ref DynamicBuffer<CcEffect> ccBuffer, ref DynamicBuffer<IncomingDamage> damageBuffer)
            {
                for (int i = 0; i < ccBuffer.Length; i++)
                {
                    var cc = ccBuffer[i];
                    if (cc.kind != CcKind.DoT) continue;

                    if (cc.tickInterval <= 0f)
                    {
                        // 레거시 연속: scalar = DPS
                        damageBuffer.Add(new IncomingDamage { amount = cc.scalar * DeltaTime });
                    }
                    else
                    {
                        // 이산 tick: scalar = tick당 데미지. 청크 1개 = IncomingDamage 1개 = 폰트 1개.
                        int ticks = DotTick.Advance(ref cc.tickTimer, cc.tickInterval, DeltaTime);
                        for (int t = 0; t < ticks; t++)
                            damageBuffer.Add(new IncomingDamage { amount = cc.scalar });
                        ccBuffer[i] = cc; // tickTimer 되쓰기
                    }
                }
            }
        }

        [BurstCompile]
        partial struct DotApplyWithEventsJob : IJobEntity
        {
            public float DeltaTime;
            public Unity.Collections.NativeQueue<HazardRuntimeEvent>.ParallelWriter RuntimeEvents;

            void Execute(Entity entity, ref DynamicBuffer<CcEffect> ccBuffer, ref DynamicBuffer<IncomingDamage> damageBuffer)
            {
                for (int i = 0; i < ccBuffer.Length; i++)
                {
                    var cc = ccBuffer[i];
                    if (cc.kind != CcKind.DoT) continue;

                    if (cc.tickInterval <= 0f)
                    {
                        // 레거시 연속: 프레임당 데미지 1 + RuntimeEvent 1
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
                    else
                    {
                        // 이산 tick: tick당 데미지 1 + RuntimeEvent 1 (프레임당→tick당, 스팸↓)
                        int ticks = DotTick.Advance(ref cc.tickTimer, cc.tickInterval, DeltaTime);
                        for (int t = 0; t < ticks; t++)
                        {
                            damageBuffer.Add(new IncomingDamage { amount = cc.scalar });
                            RuntimeEvents.Enqueue(new HazardRuntimeEvent
                            {
                                eventType = HazardRuntimeEventType.DotDamage,
                                kind = CcKind.DoT,
                                target = entity,
                                scalar = cc.scalar,
                                amount = cc.scalar,
                            });
                        }
                        ccBuffer[i] = cc; // tickTimer 되쓰기
                    }
                }
            }
        }
    }
}
