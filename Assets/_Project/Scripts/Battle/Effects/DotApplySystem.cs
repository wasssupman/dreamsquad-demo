using Unity.Burst;
using Unity.Entities;
using Wassup.Battle.Units;

namespace Wassup.Battle.Effects
{
    // dot-effect-extraction unit 0 — 지속 피해 파이프라인 전체(부여 → 틱 → 감쇠).
    //
    // 감쇠를 CcDecaySystem 에서 가져온 이유: 지속 피해가 자기 버퍼를 갖게 됐으므로 수명 관리도
    // 자기 파이프라인 안에 있어야 한다. 순서는 이관 전과 같다 — 먼저 틱을 지급하고 그 다음
    // remainingTime 을 깎아 만료를 처리한다(그래서 지속 1.35s·주기 1.0s 같은 경계값의 틱 수가
    // 이관 전후로 동일하다).
    [BurstCompile]
    [UpdateInGroup(typeof(BattleSimGroup))]
    [UpdateAfter(typeof(CcApplySystem))]
    [UpdateBefore(typeof(CcDecaySystem))]
    public partial struct DotApplySystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // 1. 부여 — 큐를 드레인해 flavor 별로 병합한다.
            if (SystemAPI.TryGetSingleton<DotApplyEventsSingleton>(out var applyEvents))
            {
                var queue = applyEvents.queue;
                while (queue.TryDequeue(out var evt))
                {
                    if (!state.EntityManager.Exists(evt.target)) continue;
                    if (!state.EntityManager.HasBuffer<DotEffect>(evt.target)) continue;
                    var buffer = state.EntityManager.GetBuffer<DotEffect>(evt.target);
                    DotEffectMerge.Apply(ref buffer, evt.effect);
                }
            }

            // 2. 틱 + 3. 감쇠
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

            void Execute(ref DynamicBuffer<DotEffect> dotBuffer, ref DynamicBuffer<IncomingDamage> damageBuffer)
            {
                // 지급은 **정방향** — 데미지 숫자 순서가 슬롯 순서를 따르게(역순으로 돌면
                // 여러 도트가 걸린 대상의 표시 순서가 조용히 뒤집힌다). 만료 제거는 뒤에서
                // 역순 패스로 분리한다.
                for (int i = 0; i < dotBuffer.Length; i++)
                {
                    var dot = dotBuffer[i];

                    if (dot.tickInterval <= 0f)
                    {
                        // 레거시 연속: scalar = DPS
                        damageBuffer.Add(new IncomingDamage { amount = dot.scalar * DeltaTime });
                    }
                    else
                    {
                        // 이산 tick: scalar = tick당 데미지. 청크 1개 = IncomingDamage 1개 = 폰트 1개.
                        int ticks = DotTick.Advance(ref dot.tickTimer, dot.tickInterval, DeltaTime);
                        for (int t = 0; t < ticks; t++)
                            damageBuffer.Add(new IncomingDamage { amount = dot.scalar });
                    }

                    dot.remainingTime -= DeltaTime;
                    dotBuffer[i] = dot; // tickTimer·remainingTime 되쓰기
                }

                for (int i = dotBuffer.Length - 1; i >= 0; i--)
                    if (dotBuffer[i].remainingTime <= 0f) dotBuffer.RemoveAtSwapBack(i);
            }
        }

        [BurstCompile]
        partial struct DotApplyWithEventsJob : IJobEntity
        {
            public float DeltaTime;
            public Unity.Collections.NativeQueue<HazardRuntimeEvent>.ParallelWriter RuntimeEvents;

            void Execute(Entity entity, ref DynamicBuffer<DotEffect> dotBuffer, ref DynamicBuffer<IncomingDamage> damageBuffer)
            {
                // 지급은 정방향(표시 순서 보존), 만료 제거는 아래 역순 패스.
                for (int i = 0; i < dotBuffer.Length; i++)
                {
                    var dot = dotBuffer[i];

                    if (dot.tickInterval <= 0f)
                    {
                        // 레거시 연속: 프레임당 데미지 1 + RuntimeEvent 1
                        float amount = dot.scalar * DeltaTime;
                        damageBuffer.Add(new IncomingDamage { amount = amount });
                        RuntimeEvents.Enqueue(new HazardRuntimeEvent
                        {
                            eventType = HazardRuntimeEventType.DotDamage,
                            kind = CcKind.DoT, // 런타임 로그 태그로만 남은 값(저작 토큰과 동일)
                            target = entity,
                            scalar = dot.scalar,
                            amount = amount,
                        });
                    }
                    else
                    {
                        // 이산 tick: tick당 데미지 1 + RuntimeEvent 1 (프레임당→tick당, 스팸↓)
                        int ticks = DotTick.Advance(ref dot.tickTimer, dot.tickInterval, DeltaTime);
                        for (int t = 0; t < ticks; t++)
                        {
                            damageBuffer.Add(new IncomingDamage { amount = dot.scalar });
                            RuntimeEvents.Enqueue(new HazardRuntimeEvent
                            {
                                eventType = HazardRuntimeEventType.DotDamage,
                                kind = CcKind.DoT,
                                target = entity,
                                scalar = dot.scalar,
                                amount = dot.scalar,
                            });
                        }
                    }

                    dot.remainingTime -= DeltaTime;
                    dotBuffer[i] = dot;
                }

                for (int i = dotBuffer.Length - 1; i >= 0; i--)
                    if (dotBuffer[i].remainingTime <= 0f) dotBuffer.RemoveAtSwapBack(i);
            }
        }
    }
}
