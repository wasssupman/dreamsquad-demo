using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Wassup.Battle.Movement;

namespace Wassup.Battle.Effects
{
    [BurstCompile]
    [UpdateInGroup(typeof(BattleSimGroup))]
    [UpdateAfter(typeof(HazardLifetimeSystem))]
    [UpdateBefore(typeof(CcApplySystem))]
    public partial struct ZoneApplySystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<HazardSingleton>();
            state.RequireForUpdate<EnemyCcEventsSingleton>();
            state.RequireForUpdate<FlowFieldSingleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var hazardSingleton = SystemAPI.GetSingleton<HazardSingleton>();
            if (hazardSingleton.cellToEffects.Count() == 0) return;

            var ccQueue = SystemAPI.GetSingleton<EnemyCcEventsSingleton>().queue;
            bool hasStatQueue = SystemAPI.TryGetSingleton<StatModifierApplyEventsSingleton>(out var statEvents);
            bool hasDotQueue = SystemAPI.TryGetSingleton<DotApplyEventsSingleton>(out var dotEvents);
            bool hasRuntimeEvents = SystemAPI.TryGetSingleton<HazardRuntimeEventsSingleton>(out var runtimeEvents);
            var flowField = SystemAPI.GetSingleton<FlowFieldSingleton>();

            foreach (var (transform, entity) in
                     SystemAPI.Query<RefRO<LocalTransform>>()
                              .WithAll<PathFollowState>()
                              .WithEntityAccess())
            {
                int2 cell = GridMath.WorldToCell(transform.ValueRO.Position, flowField.tileSize, flowField.gridSize, origin: flowField.origin);
                if (!hazardSingleton.cellToEffects.TryGetFirstValue(cell, out var effect, out var iterator)) continue;

                do
                {
                    // CcKind.Slow remains in serialized HazardEffect data for SO compatibility.
                    if (effect.kind == CcKind.Slow)
                    {
                        if (hasStatQueue)
                            statEvents.queue.Enqueue(new StatModifierApplyEvent
                            {
                                target = entity,
                                stat = StatKind.MoveSpeedMul,
                                op = CombineOp.Multiplicative,
                                magnitude = effect.param1,
                                duration = effect.restDuration,
                                source = Entity.Null,
                                stackId = 0,
                                origin = ModifierOrigin.Zone,
                            });
                    }
                    else if (effect.kind == CcKind.DoT)
                    {
                        // dot-effect-extraction unit 0 — 지속 피해는 전용 파이프라인으로 빠진다.
                        // CcKind.DoT 는 저작 토큰으로만 남는다(위 Slow 와 같은 형태).
                        if (hasDotQueue)
                            dotEvents.queue.Enqueue(new DotApplyEvent
                            {
                                target = entity,
                                effect = HazardEffectToDotEffect(effect),
                            });
                    }
                    else
                    {
                        ccQueue.Enqueue(new EnemyCcEvent
                        {
                            target = entity,
                            effect = HazardEffectToCcEffect(effect),
                        });
                    }

                    if (hasRuntimeEvents)
                    {
                        runtimeEvents.queue.Enqueue(new HazardRuntimeEvent
                        {
                            eventType = HazardRuntimeEventType.ZoneApply,
                            kind = effect.kind,
                            cell = cell,
                            target = entity,
                            scalar = effect.param1,
                        });
                    }
                } while (hazardSingleton.cellToEffects.TryGetNextValue(out effect, ref iterator));
            }
        }

        private static DotEffect HazardEffectToDotEffect(in HazardEffect hazardEffect)
        {
            return new DotEffect
            {
                flavor = hazardEffect.flavor,
                scalar = hazardEffect.param1,
                remainingTime = hazardEffect.restDuration,
                // tickTimer 는 미설정(0); DotEffectMerge add-path 가 첫 tick 즉발용으로 초기화한다.
                tickInterval = hazardEffect.tickInterval,
            };
        }

        private static CcEffect HazardEffectToCcEffect(in HazardEffect hazardEffect)
        {
            return new CcEffect
            {
                kind = hazardEffect.kind,
                scalar = hazardEffect.param1,
                vector = float3.zero,
                remainingTime = hazardEffect.restDuration,
                // dot-tick-cadence unit 0 — 존 → CC 로 주기 전달. tickTimer 는 미설정(0);
                // CcApplySystem add-path 가 첫 tick 즉발용으로 tickInterval 로 초기화한다.
                tickInterval = hazardEffect.tickInterval,
            };
        }
    }
}
