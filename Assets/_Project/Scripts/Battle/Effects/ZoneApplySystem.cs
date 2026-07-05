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

        private static CcEffect HazardEffectToCcEffect(in HazardEffect hazardEffect)
        {
            return new CcEffect
            {
                kind = hazardEffect.kind,
                scalar = hazardEffect.param1,
                vector = float3.zero,
                remainingTime = hazardEffect.restDuration,
            };
        }
    }
}
