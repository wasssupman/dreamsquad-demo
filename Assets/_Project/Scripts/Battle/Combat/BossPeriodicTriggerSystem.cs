using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Wassup.Battle.Effects;
using Wassup.Battle.Movement;
using Wassup.Battle.Units;
using Wassup.Battle.Combat.Projectile;

namespace Wassup.Battle.Combat
{
    // nightmare-catcher unit 2 — PeriodicTimer × AreaBarrage arm. Gated on
    // DcTriggerSlot buffer presence only (faction-neutral by construction —
    // no BossTag/DefenderUnitTag in the gate, spec unit 4): any slot carrier
    // with a PeriodicTimer slot ticks here; defender card slots are skipped by
    // trigger-kind dispatch (their periodSeconds is 0 anyway — 계약 9 guard).
    //
    // Fire = one SkyFall×TileAoe carrier request into the existing projectile
    // drain (dc-trigger contract 6: the slot owner's own attack may stage a
    // request the same frame, so a dedicated carrier entity is required).
    // Orthogonal to the basic attack by construction: nothing here touches
    // AttackState / AiState / movement (계약 4).
    //
    // nightmare-whip-aura unit 1 — second payload arm on the same tick:
    // AllyMoveSpeedAura pulses a MoveSpeedMul modifier (TTL) onto same-faction
    // units in range via StatModifierApplyEvents; release is TTL expiry only.
    [BurstCompile]
    [UpdateInGroup(typeof(BattleSimGroup))]
    public partial struct BossPeriodicTriggerSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<DcTriggerSlot>();
            state.RequireForUpdate<FlowFieldSingleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // BattleSimGroup dt — TimeManager Battle-domain scaled (slomo 포함).
            float dt = SystemAPI.Time.DeltaTime;
            var ff = SystemAPI.GetSingleton<FlowFieldSingleton>();

            // Epicenter pool = living defenders. This is the PAYLOAD's faction
            // axis (AreaBarrage strikes the caster's opposing side — spec fixes
            // defenders as both epicenter and victims), not an arm gate.
            var defQuery = SystemAPI.QueryBuilder().WithAll<DefenderUnitTag, LocalTransform>().Build();
            var defTransforms = defQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            var defCells = new NativeArray<int2>(defTransforms.Length, Allocator.Temp);
            for (int i = 0; i < defTransforms.Length; i++)
                defCells[i] = GridMath.WorldToCell(defTransforms[i].Position, ff.tileSize, ff.gridSize, origin: ff.origin);

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // nightmare-whip-aura unit 1 — whip pulse state: Effects channel ref
            // (RW — queue mutation intent) + lazy same-faction pools, built at
            // most once per frame and only when a whip slot actually fires
            // (unlike the eager defender pool above, which the barrage epicenter
            // always needs and which carries no entities).
            bool hasStatEvents = SystemAPI.TryGetSingletonRW<StatModifierApplyEventsSingleton>(out var statEventsRef);
            var whipTargets = new NativeList<int>(Allocator.Temp);
            NativeArray<Entity> whipEnemyEntities = default, whipDefEntities = default;
            NativeArray<int2> whipEnemyCells = default;
            bool whipEnemyPoolBuilt = false, whipDefEntitiesBuilt = false;

            foreach (var (slotsRef, entity) in
                     SystemAPI.Query<DynamicBuffer<DcTriggerSlot>>().WithEntityAccess())
            {
                // foreach 변수는 readonly — DynamicBuffer 는 뷰 struct 라 로컬
                // 복사가 같은 버퍼 메모리를 가리킨다(CS1654 회피 관용구).
                var slots = slotsRef;
                for (int si = 0; si < slots.Length; si++)
                {
                    var slot = slots[si];
                    if (slot.trigger != Wassup.Data.DcTriggerKind.PeriodicTimer) continue;

                    float elapsed = slot.elapsed;
                    bool fired = DcTrigger.PeriodicTick(ref elapsed, dt, slot.periodSeconds);
                    slot.elapsed = elapsed;
                    if (fired)
                    {
                        if (slot.payload == Wassup.Data.DcPayloadKind.AllyMoveSpeedAura)
                        {
                            // nightmare-whip-aura unit 1 — pulse: same-faction
                            // units within Chebyshev tileRange of the host get a
                            // MoveSpeedMul modifier (TTL=duration) through the
                            // existing Combat→Effects channel. Range exit / host
                            // death release by TTL expiry alone — no revoke
                            // (계약 5). Degenerate authoring (mul 1.0 / no TTL)
                            // consumes the fire quietly (계약 6).
                            if (slot.magnitude != 0f && slot.duration > 0f && hasStatEvents &&
                                SystemAPI.HasComponent<LocalTransform>(entity))
                            {
                                bool hostIsEnemy = SystemAPI.HasComponent<AttackUnitTag>(entity);
                                bool hostIsDefender = !hostIsEnemy && SystemAPI.HasComponent<DefenderUnitTag>(entity);
                                if (hostIsEnemy && !whipEnemyPoolBuilt)
                                {
                                    var enemyQuery = SystemAPI.QueryBuilder().WithAll<AttackUnitTag, LocalTransform>().Build();
                                    whipEnemyEntities = enemyQuery.ToEntityArray(Allocator.Temp);
                                    var enemyTransforms = enemyQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
                                    whipEnemyCells = new NativeArray<int2>(enemyTransforms.Length, Allocator.Temp);
                                    for (int i = 0; i < enemyTransforms.Length; i++)
                                        whipEnemyCells[i] = GridMath.WorldToCell(enemyTransforms[i].Position, ff.tileSize, ff.gridSize, origin: ff.origin);
                                    enemyTransforms.Dispose();
                                    whipEnemyPoolBuilt = true;
                                }
                                if (hostIsDefender && !whipDefEntitiesBuilt)
                                {
                                    // cells = defCells (동일 쿼리 스냅샷) — entities 만 보충.
                                    whipDefEntities = defQuery.ToEntityArray(Allocator.Temp);
                                    whipDefEntitiesBuilt = true;
                                }
                                if (hostIsEnemy || hostIsDefender) // 진영 불명 host = no-op
                                {
                                    var poolEntities = hostIsEnemy ? whipEnemyEntities : whipDefEntities;
                                    var poolCells = hostIsEnemy ? whipEnemyCells : defCells;
                                    int2 hostCell = GridMath.WorldToCell(
                                        SystemAPI.GetComponent<LocalTransform>(entity).Position,
                                        ff.tileSize, ff.gridSize, origin: ff.origin);
                                    AuraPulse.SelectTargets(poolCells, hostCell, slot.tileRange, ref whipTargets);
                                    float mul = 1f + slot.magnitude / 100f;
                                    for (int ti = 0; ti < whipTargets.Length; ti++)
                                    {
                                        var target = poolEntities[whipTargets[ti]];
                                        if (target == entity) continue; // host 자신 제외 — entity 비교 (계약 3)
                                        statEventsRef.ValueRW.queue.Enqueue(new StatModifierApplyEvent
                                        {
                                            target = target,
                                            stat = StatKind.MoveSpeedMul,
                                            op = CombineOp.Multiplicative,
                                            magnitude = mul,
                                            duration = slot.duration,
                                            source = entity,
                                            stackId = 0,
                                        });
                                    }
                                }
                            }
                        }
                        else if (slot.payload != Wassup.Data.DcPayloadKind.AreaBarrage)
                        {
                            // Payload landed without its arm — fail loudly instead
                            // of silently consuming the fire (dc-trigger 선례).
                            UnityEngine.Debug.LogWarning("[BossPeriodicTrigger] PeriodicTimer slot fired with unhandled payload kind.");
                        }
                        else if (defCells.Length > 0)
                        {
                            int idx = BarrageEpicenter.Select(defCells, slot.fireCount, ff.gridSize);
                            if (idx >= 0)
                            {
                                // Cell-lock the epicenter at fire time (SkyFall
                                // impact) — mirror of the Meteor request build
                                // (BattleBridge.ApplyMeteor), minus SO reads:
                                // dataIndex/visualScale were baked into the slot
                                // (unit 5), dropHeight is filled by the drain
                                // (translator — the only seam with SO access).
                                float3 impact = GridMath.CellToWorldCenter(defCells[idx], ff.tileSize, 0f, origin: ff.origin);
                                var carrier = ecb.CreateEntity();
                                ecb.AddComponent(carrier, new ProjectileSpawnRequest
                                {
                                    movement = MovementKind.SkyFall,
                                    payload = PayloadKind.TileAoe,
                                    origin = impact,
                                    impact = impact,
                                    damage = slot.magnitude, // flat — no damageMul (계약 8)
                                    visualScale = slot.visualScale,
                                    dataIndex = slot.projectileDataIndex,
                                    impactTileRange = slot.tileRange,
                                    flightTime = slot.duration, // 낙하 텔레그래프 (unit 0 rev 2)
                                    owner = entity, // 시전자 귀속 — threat 게이트(defender-only)가 걸러냄
                                    targetFaction = ProjectileTargetFaction.Defender, // 유일한 Defender setter (unit 4)
                                });
                                ecb.AddComponent<ProjectileRequestCarrier>(carrier);
                                // Rotation advances only on an actual fire — a
                                // 0-defender no-op keeps the phase (spec §진앙).
                                slot.fireCount++;
                            }
                        }
                        // 0 defenders: fire consumed, timer already carried over
                        // by PeriodicTick (no backlog), fireCount unchanged.
                    }
                    slots[si] = slot;
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
            defTransforms.Dispose();
            defCells.Dispose();
            whipTargets.Dispose();
            if (whipEnemyPoolBuilt) { whipEnemyEntities.Dispose(); whipEnemyCells.Dispose(); }
            if (whipDefEntitiesBuilt) whipDefEntities.Dispose();
        }
    }
}
