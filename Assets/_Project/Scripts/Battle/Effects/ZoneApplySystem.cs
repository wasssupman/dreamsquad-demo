using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Wassup.Battle.Movement;
using Wassup.Battle.Units;
using Wassup.Data;

namespace Wassup.Battle.Effects
{
    [BurstCompile]
    [UpdateInGroup(typeof(BattleSimGroup))]
    [UpdateAfter(typeof(HazardLifetimeSystem))]
    [UpdateBefore(typeof(CcApplySystem))]
    // battle-sim-extraction M0 unit 0 — 순서 박제. **현행 유효 순서를 고정할 뿐 고치지 않는다**
    //   (재배치 판단은 M1 설계의 몫). 근거: docs/spec/battle-sim-extraction/order-capture.md
    //   모디파이어 이벤트 생산자 중 소비자보다 **앞**에 있는 셋 중 하나(같은 프레임 반영).
    [UpdateBefore(typeof(Wassup.Battle.Effects.ModifierApplySystem))]
    public partial struct ZoneApplySystem : ISystem
    {
        // unit 18/19 규율 — 신규 lookup 은 **필드 형태**(로컬 SystemAPI 형태는 Burst NRE 재발 이력).
        Unity.Entities.ComponentLookup<Wassup.Battle.Units.HitRadius> _bodyRadiusLookup;

        // unit 19 — 존 틱 판정 스냅샷: 원(중심·반경) + 효과 버퍼 구간.
        private struct ZoneSnap
        {
            public float2 centerXZ;   // 월드
            public int radiusTiles;
            public int effStart;
            public int effCount;
        }

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EnemyCcEventsSingleton>();
            state.RequireForUpdate<FlowFieldSingleton>();
            _bodyRadiusLookup = state.GetComponentLookup<Wassup.Battle.Units.HitRadius>(isReadOnly: true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _bodyRadiusLookup.Update(ref state);

            var ccQueue = SystemAPI.GetSingleton<EnemyCcEventsSingleton>().queue;
            bool hasStatQueue = SystemAPI.TryGetSingleton<StatModifierApplyEventsSingleton>(out var statEvents);
            bool hasDotQueue = SystemAPI.TryGetSingleton<DotApplyEventsSingleton>(out var dotEvents);
            bool hasRuntimeEvents = SystemAPI.TryGetSingleton<HazardRuntimeEventsSingleton>(out var runtimeEvents);
            var flowField = SystemAPI.GetSingleton<FlowFieldSingleton>();
            float invT = flowField.tileSize > 1e-6f ? 1f / flowField.tileSize : 1f;

            // unit 19 — 셀 해시 브로드페이즈 은퇴. 해저드를 스냅샷하고 피해자 × 존 원으로
            // 연속 판정한다(존 수 ≤ 수십, 피해자 ≤ 수십 — 곱해도 싸다). 겹친 동일 슬롯 존의
            // 적용 순서는 종전(맵 삽입 순서 = 청크 순서)과 같은 결이라 결정론 등급 무변.
            var zones = new Unity.Collections.NativeList<ZoneSnap>(Unity.Collections.Allocator.Temp);
            var zoneEffects = new Unity.Collections.NativeList<HazardEffect>(Unity.Collections.Allocator.Temp);
            foreach (var (hazard, effects) in
                     SystemAPI.Query<RefRO<Hazard>, DynamicBuffer<HazardEffectsBuffer>>())
            {
                if (hazard.ValueRO.radiusTiles < 0 || effects.Length == 0) continue;
                float3 c = GridMath.CellToWorldCenter(
                    hazard.ValueRO.originCell, flowField.tileSize, origin: flowField.origin);
                var snap = new ZoneSnap
                {
                    centerXZ = new float2(c.x, c.z),
                    radiusTiles = hazard.ValueRO.radiusTiles,
                    effStart = zoneEffects.Length,
                    effCount = effects.Length,
                };
                for (int i = 0; i < effects.Length; i++) zoneEffects.Add(effects[i].effect);
                zones.Add(snap);
            }
            if (zones.Length == 0) { zones.Dispose(); zoneEffects.Dispose(); return; }

            // summon-patrol-defender unit 0 — 진영 게이트. 이전엔 `PathFollowState` 보유만으로
            // 존 효과를 걸었는데, 그건 "이동체 = 적"이라는 암묵 전제에 기댄 것이었다
            // (object-pipeline-map Defender 행: "이동 없음(고정) — PathFollowState 미부여").
            // 거점 수비 아군이 그 전제를 깨므로, 아군이 아군 장판에 오폭당하지 않도록
            // 진영을 명시적으로 판정한다. 형태는 HazardCastSystem 의 targetMask 게이트와 같다.
            // 존의 대상 진영은 오늘 적 하나뿐이라 HazardEffect 에 진영 축을 열지 않는다(제약 8) —
            // 아군 대상 존(회복 장판 등)이 실제로 생기면 그때 데이터로 승격한다.
            foreach (var (transform, faction, path, entity) in
                     SystemAPI.Query<RefRO<LocalTransform>, RefRO<FactionTag>, RefRO<PathFollowState>>()
                              .WithEntityAccess())
            {
                if (((int)faction.ValueRO.value & (int)Faction.EnemyUnit) == 0) continue;

                float3 vpos = transform.ValueRO.Position;
                // unit 19 — 멤버십 = 원(반경 + 칸 반폭) + 피해자 몸. 광역·회오리와 같은 자.
                float bodyR = _bodyRadiusLookup.HasComponent(entity) ? _bodyRadiusLookup[entity].value : 0f;
                // 트레이스 페이로드용 피해자 셀(판정 아님 — 로그 축).
                int2 cell = GridMath.WorldToCell(vpos, flowField.tileSize, flowField.gridSize, origin: flowField.origin);
                for (int z = 0; z < zones.Length; z++)
                {
                    var zone = zones[z];
                    if (!Wassup.Skills.SkillMath.InBodyReach(
                            (vpos.x - zone.centerXZ.x) * invT, (vpos.z - zone.centerXZ.y) * invT,
                            zone.radiusTiles, Wassup.Skills.SkillMath.CellHalfWidthTiles, bodyR))
                        continue;
                    for (int ei = 0; ei < zone.effCount; ei++)
                    {
                        var effect = zoneEffects[zone.effStart + ei];
                    if (!PlacementLayers.CanTarget(
                            effect.targetTraversalLayers,
                            path.ValueRO.traversalLayers)) continue;

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
                    }
                }
            }
            zones.Dispose();
            zoneEffects.Dispose();
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
