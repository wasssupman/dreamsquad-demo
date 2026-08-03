using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Wassup.Battle.Movement;
using Wassup.Battle.Units;

namespace Wassup.Battle.Effects
{
    [BurstCompile]
    [UpdateInGroup(typeof(BattleSimGroup))]
    [UpdateAfter(typeof(HazardLifetimeSystem))]
    [UpdateBefore(typeof(CcApplySystem))]
    // battle-sim-extraction unit 0 — 모디파이어 enqueue 의 같은-프레임 적용(캡처 순서)을 선언으로 고정.
    // (기존 Before(CcApply)만으로는 ModifierApply(#9)와 CcApply(#10) 사이 배치가 허용됐다.)
    [UpdateBefore(typeof(ModifierApplySystem))]
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

            // summon-patrol-defender unit 0 — 진영 게이트. 이전엔 `PathFollowState` 보유만으로
            // 존 효과를 걸었는데, 그건 "이동체 = 적"이라는 암묵 전제에 기댄 것이었다
            // (object-pipeline-map Defender 행: "이동 없음(고정) — PathFollowState 미부여").
            // 거점 수비 아군이 그 전제를 깨므로, 아군이 아군 장판에 오폭당하지 않도록
            // 진영을 명시적으로 판정한다. 형태는 HazardCastSystem 의 targetMask 게이트와 같다.
            // 존의 대상 진영은 오늘 적 하나뿐이라 HazardEffect 에 진영 축을 열지 않는다(제약 8) —
            // 아군 대상 존(회복 장판 등)이 실제로 생기면 그때 데이터로 승격한다.
            foreach (var (transform, faction, entity) in
                     SystemAPI.Query<RefRO<LocalTransform>, RefRO<FactionTag>>()
                              .WithAll<PathFollowState>()
                              .WithEntityAccess())
            {
                if (((int)faction.ValueRO.value & (int)Faction.Enemy) == 0) continue;

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
                origin = DotOrigin.Zone,
                element = hazardEffect.element,
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
