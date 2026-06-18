using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Wassup.Battle.Combat.Projectile;
using Wassup.Battle.Effects;
using Wassup.Battle.Movement;
using Wassup.Battle.Units;

namespace Wassup.Battle.Combat
{
    // Unified attacker loop — any entity with AttackState + LocalTransform (and no
    // PendingDeployment) participates as an attacker. Defender-specific behaviours
    // (attack-event enqueue, buff scaling, projectile, knockback CC) branch on
    // ComponentLookup.HasComponent so no attacker-tag filtering is needed in the query.
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(MovementSystem))]
    public partial struct AttackSystem : ISystem
    {
        private EntityQuery _attackEventsQuery;
        private EntityQuery _ccEventsQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<AttackState>();
            _attackEventsQuery = state.GetEntityQuery(ComponentType.ReadWrite<UnitAttackVisualEventsSingleton>());
            _ccEventsQuery = state.GetEntityQuery(ComponentType.ReadWrite<Wassup.Battle.Effects.EnemyCcEventsSingleton>());
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.Time.DeltaTime;

            // Snapshot attackable targets into native arrays so the unified attacker
            // loop uses the same candidate pool and filters by AttackState.targetMask.
            var targetCandidatesQuery = SystemAPI.QueryBuilder()
                .WithAll<FactionTag, Health, LocalTransform>()
                .WithNone<PendingDeployment>()
                .WithNone<DeadTag>()
                .Build();
            var targetEntities = targetCandidatesQuery.ToEntityArray(Allocator.Temp);
            var targetTransforms = targetCandidatesQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            var targetFactions = targetCandidatesQuery.ToComponentDataArray<FactionTag>(Allocator.Temp);

            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var projectileRefLookup = SystemAPI.GetComponentLookup<ProjectileRef>(isReadOnly: true);
            var defenderCcLookup = SystemAPI.GetComponentLookup<DefenderCcData>(isReadOnly: true);
            var defenderTagLookup = SystemAPI.GetComponentLookup<DefenderUnitTag>(isReadOnly: true);
            var blockingHazardCellsLookup = SystemAPI.GetBufferLookup<BlockingHazardCellsBuffer>(isReadOnly: true);
            var modifierStatsLookup = SystemAPI.GetComponentLookup<Wassup.Battle.Effects.ModifierStats>(isReadOnly: true);
            var outputBufferLookup = SystemAPI.GetBufferLookup<AttackOutputElement>(isReadOnly: true);
            // aggro-targeting Unit 4 — enemy class filter + priority targeting.
            var defenderClassLookup = SystemAPI.GetComponentLookup<DefenderClassTag>(isReadOnly: true);
            var enemyFilterLookup = SystemAPI.GetComponentLookup<EnemyTargetFilter>(isReadOnly: true);
            // aggro-targeting Unit 5 — aggroed enemy sticky-targets its guardian.
            var aggroLookup = SystemAPI.GetComponentLookup<Aggroed>(isReadOnly: true);
            var aggroTransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(isReadOnly: true);

            bool hasStatQ = SystemAPI.TryGetSingletonRW<Wassup.Battle.Effects.StatModifierApplyEventsSingleton>(out var statModSingleton);
            bool hasStackQ = SystemAPI.TryGetSingletonRW<Wassup.Battle.Effects.StackModifierApplyEventsSingleton>(out var stackModSingleton);
            bool hasFlowField = SystemAPI.TryGetSingleton<Wassup.Battle.Effects.FlowFieldSingleton>(out var flowField);
            bool hasMovementPauseQ = SystemAPI.TryGetSingletonRW<MovementPauseRequestEventsSingleton>(out var movementPauseSingleton);

            // Attack-output log channel — enqueue one event per output-per-target fired.
            NativeQueue<AttackOutputLogEvent>.ParallelWriter? attackOutputLogWriter = null;
            if (SystemAPI.TryGetSingletonRW<AttackOutputLogEventsSingleton>(out var attackOutputLogSingleton))
                attackOutputLogWriter = attackOutputLogSingleton.ValueRW.queue.AsParallelWriter();

            // Hoist attack-event singleton writer — every attacker (defender or
            // enemy) enqueues one event per fire so SpineUnitPool can trigger the
            // attack animation and facing flip uniformly. Defender-specific
            // side effects (cast/attack VFX prefabs) are filtered downstream in
            // BattleBridge by looking up DefenderUnitData for the attacker.
            NativeQueue<UnitAttackVisualEvent>.ParallelWriter? attackWriter = null;
            if (!_attackEventsQuery.IsEmpty)
            {
                var singleton = _attackEventsQuery.GetSingletonRW<UnitAttackVisualEventsSingleton>();
                attackWriter = singleton.ValueRW.queue.AsParallelWriter();
            }

            NativeQueue<Wassup.Battle.Effects.EnemyCcEvent>.ParallelWriter? ccWriter = null;
            if (!_ccEventsQuery.IsEmpty)
            {
                var ccSingleton = _ccEventsQuery.GetSingletonRW<Wassup.Battle.Effects.EnemyCcEventsSingleton>();
                ccWriter = ccSingleton.ValueRW.queue.AsParallelWriter();
            }

            // ─────────────────────────────────────────────────────────────────────
            // Unified attacker loop — defenders and enemies share this single query.
            // Defender-specific branches guard on defenderTagLookup / HasComponent.
            // ─────────────────────────────────────────────────────────────────────
            foreach (var (attack, transform, attackerEntity) in
                     SystemAPI.Query<RefRW<AttackState>, RefRO<LocalTransform>>()
                              .WithNone<PendingDeployment>()
                              .WithEntityAccess())
            {
                // Tick cooldown first.
                if (attack.ValueRO.cooldownRemaining > 0f)
                {
                    attack.ValueRW.cooldownRemaining = math.max(0f, attack.ValueRO.cooldownRemaining - dt);
                }

                // Find nearest in-range target allowed by this attacker's mask.
                float3 atkPos = transform.ValueRO.Position;
                float tileSize = hasFlowField ? flowField.tileSize : 1f;
                int2 gridSize = hasFlowField ? flowField.gridSize : new int2(128, 128);
                float3 ffOrigin = hasFlowField ? flowField.origin : float3.zero;
                int tileRange = GridMath.RangeToTiles(attack.ValueRO.range);
                int2 atkCell = GridMath.WorldToCell(atkPos, tileSize, gridSize, origin: ffOrigin);
                float bestSq = float.MaxValue;
                Entity bestTarget = Entity.Null;
                float3 bestTargetPos = default;
                int mask = attack.ValueRO.targetMask;
                // aggro-targeting Unit 4 — enemy class filter + priority. Defenders have
                // no EnemyTargetFilter → filterMask -1 / prioClass -1 = legacy nearest.
                bool hasFilter = enemyFilterLookup.HasComponent(attackerEntity);
                int filterMask = hasFilter ? enemyFilterLookup[attackerEntity].classMask : -1;
                int prioClass = hasFilter ? enemyFilterLookup[attackerEntity].priorityClass : -1;
                float bestSqPrio = float.MaxValue;
                Entity bestTargetPrio = Entity.Null;
                float3 bestTargetPosPrio = default;
                for (int i = 0; i < targetEntities.Length; i++)
                {
                    if (((int)targetFactions[i].value & mask) == 0) continue;
                    if (targetEntities[i] == attackerEntity) continue;
                    int cclass = defenderClassLookup.HasComponent(targetEntities[i])
                        ? (int)defenderClassLookup[targetEntities[i]].value : -1;
                    if (hasFilter && cclass >= 0 && (filterMask & (1 << cclass)) == 0) continue; // class not allowed
                    float3 targetPos = targetTransforms[i].Position;
                    int2 tgtCell = GridMath.WorldToCell(targetPos, tileSize, gridSize, origin: ffOrigin);
                    int tileDist = math.max(math.abs(tgtCell.x - atkCell.x), math.abs(tgtCell.y - atkCell.y));
                    if (tileDist > tileRange) continue;
                    float d2 = DistanceSqToTarget(atkPos, targetEntities[i], targetPos, blockingHazardCellsLookup, hasFlowField, flowField, out var nearestPos);
                    if (d2 < bestSq)
                    {
                        bestSq = d2;
                        bestTarget = targetEntities[i];
                        bestTargetPos = nearestPos;
                    }
                    if (prioClass >= 0 && cclass == prioClass && d2 < bestSqPrio)
                    {
                        bestSqPrio = d2;
                        bestTargetPrio = targetEntities[i];
                        bestTargetPosPrio = nearestPos;
                    }
                }
                // priority override — prefer a priority-class target if any is in range.
                if (prioClass >= 0 && bestTargetPrio != Entity.Null)
                {
                    bestTarget = bestTargetPrio;
                    bestTargetPos = bestTargetPosPrio;
                }

                // aggro-targeting Unit 5 — sticky override: an aggroed enemy ignores
                // filter/priority/nearest and targets ONLY its guardian, and only when
                // in range (otherwise it holds fire while walking toward the anchor).
                if (aggroLookup.HasComponent(attackerEntity))
                {
                    bestTarget = Entity.Null;
                    var g = aggroLookup[attackerEntity].guardian;
                    if (g != Entity.Null && aggroTransformLookup.HasComponent(g))
                    {
                        float3 gPos = aggroTransformLookup[g].Position;
                        int2 gCell = GridMath.WorldToCell(gPos, tileSize, gridSize, origin: ffOrigin);
                        int gDist = math.max(math.abs(gCell.x - atkCell.x), math.abs(gCell.y - atkCell.y));
                        if (gDist <= tileRange)
                        {
                            bestTarget = g;
                            bestTargetPos = gPos;
                        }
                    }
                }

                // Fire if cooldown ready and target exists.
                if (bestTarget != Entity.Null && attack.ValueRO.cooldownRemaining <= 0f)
                {
                    bool isDefender = defenderTagLookup.HasComponent(attackerEntity);

                    // Unified visual trigger — defenders and enemies enqueue the same
                    // event so SpineUnitPool plays the attack animation regardless
                    // of attacker faction.
                    if (attackWriter.HasValue)
                    {
                        attackWriter.Value.Enqueue(new UnitAttackVisualEvent
                        {
                            attacker = attackerEntity,
                            targetWorld = bestTargetPos,
                        });
                    }

                    float damageMul = modifierStatsLookup.HasComponent(attackerEntity)
                        ? modifierStatsLookup[attackerEntity].damageMul
                        : 1f;
                    float attackSpeedMul = modifierStatsLookup.HasComponent(attackerEntity)
                        ? modifierStatsLookup[attackerEntity].attackSpeedMul
                        : 1f;
                    // All defender/enemy hit effects come through AttackOutputElement.
                    // AttackState.damage remains only as serialized authoring compatibility.
                    bool hasOutputs = outputBufferLookup.HasBuffer(attackerEntity);

                    if (hasOutputs)
                    {
                        var outputs = outputBufferLookup[attackerEntity];

                        if (projectileRefLookup.HasComponent(attackerEntity))
                        {
                            var projRef = projectileRefLookup[attackerEntity];
                            float projectileDamage = 0f;
                            var projectileOutputs = ecb.AddBuffer<ProjectileSpawnOutputElement>(attackerEntity);
                            for (int oi = 0; oi < outputs.Length; oi++)
                            {
                                var o = outputs[oi].value;
                                if (o.kind == Wassup.Data.AttackOutputKind.Damage)
                                {
                                    float amount = o.magnitude * damageMul;
                                    o.magnitude = amount;
                                    projectileDamage += amount;
                                    if (attackOutputLogWriter.HasValue)
                                        attackOutputLogWriter.Value.Enqueue(new AttackOutputLogEvent
                                        {
                                            attacker  = attackerEntity,
                                            kind      = Wassup.Data.AttackOutputKind.Damage,
                                            magnitude = amount,
                                            duration  = 0f,
                                            sourcePos = atkPos,
                                            targetPos = bestTargetPos,
                                        });
                                }
                                projectileOutputs.Add(new ProjectileSpawnOutputElement { value = o });
                            }

                            ecb.AddComponent(attackerEntity, new ProjectileSpawnRequest
                            {
                                target = bestTarget,
                                origin = atkPos,
                                damage = projectileDamage,
                                speed = projRef.speed,
                                hitThreshold = projRef.hitThreshold,
                                visualScale = projRef.visualScale,
                                dataIndex = projRef.dataIndex,
                                onHitEffect = projRef.onHitEffect,
                                splashRadius = projRef.splashRadius,
                                splashDamageMul = projRef.splashDamageMul,
                            });
                        }
                        else
                        {
                            // ── Outputs path ────────────────────────────────────────────────
                            // Collect hit targets (same AoE logic as legacy melee path).
                            int desiredCount = math.max(1, attack.ValueRO.attackTargetCount);
                            var hitTargets = new NativeArray<Entity>(desiredCount, Allocator.Temp);
                            int hitCount = 0;

                            hitTargets[hitCount++] = bestTarget;
                            if (desiredCount > 1)
                            {
                                var hitMaskO = new NativeArray<bool>(targetEntities.Length, Allocator.Temp);
                                int seedIdx = -1;
                                for (int i = 0; i < targetEntities.Length; i++)
                                {
                                    if (targetEntities[i] == bestTarget) { seedIdx = i; break; }
                                }
                                if (seedIdx >= 0) hitMaskO[seedIdx] = true;

                                for (int pass = 1; pass < desiredCount && hitCount < desiredCount; pass++)
                                {
                                    float passSq = float.MaxValue;
                                    int passIdx = -1;
                                    for (int i = 0; i < targetEntities.Length; i++)
                                    {
                                        if (hitMaskO[i]) continue;
                                        if (((int)targetFactions[i].value & mask) == 0) continue;
                                        if (targetEntities[i] == attackerEntity) continue;
                                        int2 tgtCellAoE = GridMath.WorldToCell(targetTransforms[i].Position, tileSize, gridSize, origin: ffOrigin);
                                        int tileDistAoE = math.max(math.abs(tgtCellAoE.x - atkCell.x), math.abs(tgtCellAoE.y - atkCell.y));
                                        if (tileDistAoE > tileRange) continue;
                                        float d2 = DistanceSqToTarget(atkPos, targetEntities[i], targetTransforms[i].Position, blockingHazardCellsLookup, hasFlowField, flowField, out _);
                                        if (d2 < passSq)
                                        {
                                            passSq = d2;
                                            passIdx = i;
                                        }
                                    }
                                    if (passIdx < 0) break;
                                    hitMaskO[passIdx] = true;
                                    hitTargets[hitCount++] = targetEntities[passIdx];
                                }
                                hitMaskO.Dispose();
                            }

                            for (int ti = 0; ti < hitCount; ti++)
                            {
                                Entity hitTarget = hitTargets[ti];
                                for (int oi = 0; oi < outputs.Length; oi++)
                                {
                                    var o = outputs[oi].value;
                                    switch (o.kind)
                                    {
                                        case Wassup.Data.AttackOutputKind.Damage:
                                            ecb.AppendToBuffer(hitTarget,
                                                new IncomingDamage { amount = o.magnitude * damageMul });
                                            if (attackOutputLogWriter.HasValue)
                                                attackOutputLogWriter.Value.Enqueue(new AttackOutputLogEvent
                                                {
                                                    attacker  = attackerEntity,
                                                    kind      = Wassup.Data.AttackOutputKind.Damage,
                                                    magnitude = o.magnitude * damageMul,
                                                    duration  = 0f,
                                                    sourcePos = atkPos,
                                                    targetPos = bestTargetPos,
                                                });
                                            break;

                                        case Wassup.Data.AttackOutputKind.Heal:
                                            ecb.AppendToBuffer(hitTarget, new Wassup.Battle.Units.IncomingHeal { amount = o.magnitude });
                                            if (attackOutputLogWriter.HasValue)
                                                attackOutputLogWriter.Value.Enqueue(new AttackOutputLogEvent
                                                {
                                                    attacker  = attackerEntity,
                                                    kind      = Wassup.Data.AttackOutputKind.Heal,
                                                    magnitude = o.magnitude,
                                                    duration  = 0f,
                                                    sourcePos = atkPos,
                                                    targetPos = bestTargetPos,
                                                });
                                            break;

                                        case Wassup.Data.AttackOutputKind.ApplyStat:
                                            if (hasStatQ)
                                                statModSingleton.ValueRW.queue.Enqueue(new Wassup.Battle.Effects.StatModifierApplyEvent
                                                {
                                                    target    = hitTarget,
                                                    stat      = o.stat,
                                                    op        = o.op,
                                                    magnitude = o.magnitude,
                                                    duration  = o.duration,
                                                    source    = attackerEntity,
                                                    stackId   = 0,
                                                });
                                            if (attackOutputLogWriter.HasValue)
                                                attackOutputLogWriter.Value.Enqueue(new AttackOutputLogEvent
                                                {
                                                    attacker  = attackerEntity,
                                                    kind      = Wassup.Data.AttackOutputKind.ApplyStat,
                                                    magnitude = o.magnitude,
                                                    stat      = o.stat,
                                                    duration  = o.duration,
                                                    sourcePos = atkPos,
                                                    targetPos = bestTargetPos,
                                                });
                                            break;

                                        case Wassup.Data.AttackOutputKind.ApplyStack:
                                            if (hasStackQ)
                                                stackModSingleton.ValueRW.queue.Enqueue(new Wassup.Battle.Effects.StackModifierApplyEvent
                                                {
                                                    target         = hitTarget,
                                                    kind           = o.stackKind,
                                                    countDelta     = (byte)math.max(1f, o.magnitude),
                                                    maxStack       = o.stackMaxStack > 0 ? o.stackMaxStack : (byte)5,
                                                    perAppDuration = o.duration,
                                                    source         = attackerEntity,
                                                });
                                            if (attackOutputLogWriter.HasValue)
                                                attackOutputLogWriter.Value.Enqueue(new AttackOutputLogEvent
                                                {
                                                    attacker   = attackerEntity,
                                                    kind       = Wassup.Data.AttackOutputKind.ApplyStack,
                                                    magnitude  = o.magnitude,
                                                    stackKind  = o.stackKind,
                                                    duration   = o.duration,
                                                    sourcePos  = atkPos,
                                                    targetPos  = bestTargetPos,
                                                });
                                            break;
                                    }
                                }
                            }
                            hitTargets.Dispose();
                        }
                    }

                    float effectiveCooldownMul = attackSpeedMul > 0f ? 1f / attackSpeedMul : 1f;
                    attack.ValueRW.cooldownRemaining = attack.ValueRO.cooldownDuration * effectiveCooldownMul;
                    bool isEnemy = !isDefender;
                    if (isEnemy && hasMovementPauseQ)
                    {
                        var pauseDuration = attack.ValueRO.movePauseOnAttackSec;
                        if (pauseDuration > 0f)
                            movementPauseSingleton.ValueRW.queue.Enqueue(new MovementPauseRequest
                            {
                                target = attackerEntity,
                                duration = pauseDuration,
                            });
                    }

                    // [Defender only] Knockback CC — enemies do not carry DefenderCcData.
                    if (ccWriter.HasValue && defenderCcLookup.HasComponent(attackerEntity))
                    {
                        var ccData = defenderCcLookup[attackerEntity];
                        if (ccData.knockbackDistance > 0f && ccData.knockbackDuration > 0f)
                        {
                            // Physical collision direction:
                            //   D = projectile travel (defender→enemy)
                            //   E = enemy travel (flow at enemy cell)
                            //   dir = normalize(D - E)  ← relative-velocity impulse direction
                            // Falls back to D when flow field unavailable (tests).
                            float3 D = math.normalizesafe(bestTargetPos - atkPos);
                            D.y = 0f;
                            // Guard: attacker colocated with target → D≈0 → no meaningful direction.
                            // Skip impulse (otherwise dir would degenerate to -E and push the enemy
                            // backward along its flow path, an unintended side effect).
                            if (math.lengthsq(D) > 1e-6f)
                            {
                                float3 dir;
                                if (SystemAPI.TryGetSingleton<Wassup.Battle.Effects.FlowFieldSingleton>(out var ff))
                                {
                                    var targetCell = GridMath.WorldToCell(bestTargetPos, ff.tileSize, ff.gridSize, origin: ff.origin);
                                    int fIdx = GridMath.CellIndex(targetCell, ff.gridSize);
                                    float2 flowDir = ff.flow[fIdx];
                                    float3 E = math.normalizesafe(new float3(flowDir.x, 0, flowDir.y));
                                    dir = math.normalizesafe(D - E);
                                    if (math.lengthsq(dir) < 1e-6f)
                                        dir = D; // fallback when D == E (hit from behind)
                                }
                                else
                                {
                                    dir = D;
                                }
                                float speed = ccData.knockbackDistance / ccData.knockbackDuration;
                                ccWriter.Value.Enqueue(new Wassup.Battle.Effects.EnemyCcEvent
                                {
                                    target = bestTarget,
                                    effect = new Wassup.Battle.Effects.CcEffect
                                    {
                                        kind = Wassup.Battle.Effects.CcKind.Impulse,
                                        vector = dir * speed,
                                        remainingTime = ccData.knockbackDuration,
                                    },
                                });
                            }
                        }
                    }
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
            targetEntities.Dispose();
            targetTransforms.Dispose();
            targetFactions.Dispose();
        }

        private static float DistanceSqToTarget(
            float3 attackerPos,
            Entity target,
            float3 fallbackTargetPos,
            BufferLookup<BlockingHazardCellsBuffer> hazardCellsLookup,
            bool hasFlowField,
            Wassup.Battle.Effects.FlowFieldSingleton flowField,
            out float3 nearestTargetPos)
        {
            nearestTargetPos = fallbackTargetPos;
            float3 diff = fallbackTargetPos - attackerPos;
            float bestSq = diff.x * diff.x + diff.z * diff.z;

            if (!hasFlowField || !hazardCellsLookup.HasBuffer(target))
                return bestSq;

            var cells = hazardCellsLookup[target];
            for (int i = 0; i < cells.Length; i++)
            {
                float3 cellWorld = GridMath.CellToWorldCenter(cells[i].cell, flowField.tileSize, fallbackTargetPos.y, origin: flowField.origin);
                diff = cellWorld - attackerPos;
                float d2 = diff.x * diff.x + diff.z * diff.z;
                if (d2 >= bestSq) continue;
                bestSq = d2;
                nearestTargetPos = cellWorld;
            }

            return bestSq;
        }
    }
}
