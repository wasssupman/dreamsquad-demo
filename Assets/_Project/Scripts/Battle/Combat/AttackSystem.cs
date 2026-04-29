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
    // For each defender with an AttackState:
    //   - ticks down cooldown
    //   - finds the nearest attack unit within range
    //   - when cooldown is ready and a target is found, appends IncomingDamage to the target's buffer and resets cooldown.
    //
    // Targeting rule: nearest (Euclidean distance on the XZ plane). Fixed for Phase 0.
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
            _attackEventsQuery = state.GetEntityQuery(ComponentType.ReadWrite<DefenderAttackEventsSingleton>());
            _ccEventsQuery = state.GetEntityQuery(ComponentType.ReadWrite<Wassup.Battle.Effects.EnemyCcEventsSingleton>());
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.Time.DeltaTime;

            // Snapshot attackable targets into native arrays so both attacker
            // loops use the same candidate pool and filter by AttackState.targetMask.
            var targetCandidatesQuery = SystemAPI.QueryBuilder()
                .WithAll<FactionTag, Health, LocalTransform>()
                .WithNone<PendingDeployment>()
                .WithNone<DeadTag>()
                .Build();
            var targetEntities = targetCandidatesQuery.ToEntityArray(Allocator.Temp);
            var targetTransforms = targetCandidatesQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            var targetFactions = targetCandidatesQuery.ToComponentDataArray<FactionTag>(Allocator.Temp);

            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var damageBoostLookup = SystemAPI.GetComponentLookup<DamageBoost>(isReadOnly: true);
            var cooldownReductionLookup = SystemAPI.GetComponentLookup<CooldownReduction>(isReadOnly: true);
            var synergyLookup = SystemAPI.GetComponentLookup<SynergyBuff>(isReadOnly: true);
            var projectileRefLookup = SystemAPI.GetComponentLookup<ProjectileRef>(isReadOnly: true);
            var defenderCcLookup = SystemAPI.GetComponentLookup<DefenderCcData>(isReadOnly: true);
            var blockingHazardCellsLookup = SystemAPI.GetBufferLookup<BlockingHazardCellsBuffer>(isReadOnly: true);
            bool hasFlowField = SystemAPI.TryGetSingleton<Wassup.Battle.Effects.FlowFieldSingleton>(out var flowField);

            // Phase 8 §13 follow-up — hoist the attack-event singleton writer so
            // both projectile and melee defender branches below enqueue a single
            // "defender fired" event per attack. Pool drains it to trigger Spine
            // attack animation consistently (melee was previously missed because
            // only ProjectileSpawnRequest carried the hook).
            NativeQueue<DefenderAttackEvent>.ParallelWriter? attackWriter = null;
            if (!_attackEventsQuery.IsEmpty)
            {
                var singleton = _attackEventsQuery.GetSingletonRW<DefenderAttackEventsSingleton>();
                attackWriter = singleton.ValueRW.queue.AsParallelWriter();
            }

            NativeQueue<Wassup.Battle.Effects.EnemyCcEvent>.ParallelWriter? ccWriter = null;
            if (!_ccEventsQuery.IsEmpty)
            {
                var ccSingleton = _ccEventsQuery.GetSingletonRW<Wassup.Battle.Effects.EnemyCcEventsSingleton>();
                ccWriter = ccSingleton.ValueRW.queue.AsParallelWriter();
            }

            foreach (var (attack, transform, defenderEntity) in
                     SystemAPI.Query<RefRW<AttackState>, RefRO<LocalTransform>>()
                              .WithAll<DefenderUnitTag>()
                              .WithNone<PendingDeployment>()
                              .WithEntityAccess())
            {
                // Tick cooldown first.
                if (attack.ValueRO.cooldownRemaining > 0f)
                {
                    attack.ValueRW.cooldownRemaining = math.max(0f, attack.ValueRO.cooldownRemaining - dt);
                }

                // Find nearest in-range target allowed by this attacker's mask.
                float3 defPos = transform.ValueRO.Position;
                float range = attack.ValueRO.range;
                float rangeSq = range * range;
                float bestSq = float.MaxValue;
                Entity bestTarget = Entity.Null;
                float3 bestTargetPos = default;
                int mask = attack.ValueRO.targetMask;
                for (int i = 0; i < targetEntities.Length; i++)
                {
                    if (((int)targetFactions[i].value & mask) == 0) continue;
                    if (targetEntities[i] == defenderEntity) continue;
                    float3 targetPos = targetTransforms[i].Position;
                    float d2 = DistanceSqToTarget(defPos, targetEntities[i], targetPos, blockingHazardCellsLookup, hasFlowField, flowField, out var nearestPos);
                    if (d2 <= rangeSq && d2 < bestSq)
                    {
                        bestSq = d2;
                        bestTarget = targetEntities[i];
                        bestTargetPos = nearestPos;
                    }
                }

                // Fire if cooldown ready and target exists.
                if (bestTarget != Entity.Null && attack.ValueRO.cooldownRemaining <= 0f)
                {
                    // Enqueue "defender fired" event so Spine/Pool can trigger
                    // attack animation for both projectile and melee paths.
                    if (attackWriter.HasValue)
                    {
                        attackWriter.Value.Enqueue(new DefenderAttackEvent
                        {
                            defender = defenderEntity,
                            targetWorld = bestTargetPos,
                        });
                    }
                    // Effects read-only: DamageBoost scales emitted damage, CooldownReduction
                    // shortens the reset interval, SynergyBuff adds adjacency bonus. The base
                    // AttackState fields stay unmodified so Combat remains the sole owner.
                    float damageMul = damageBoostLookup.HasComponent(defenderEntity)
                        ? damageBoostLookup[defenderEntity].multiplier
                        : 1f;
                    float cooldownMul = cooldownReductionLookup.HasComponent(defenderEntity)
                        ? cooldownReductionLookup[defenderEntity].multiplier
                        : 1f;
                    float synergyMul = synergyLookup.HasComponent(defenderEntity)
                        ? synergyLookup[defenderEntity].damageMul
                        : 1f;
                    float emittedDamage = attack.ValueRO.damage * damageMul * synergyMul;

                    // ProjectileRef-bearing defenders stage a spawn request for the
                    // MonoBehaviour drain loop in BattleBridge. Melee / defaults without a
                    // projectile SO keep the Phase 0-2 direct-damage path for regression.
                    if (projectileRefLookup.HasComponent(defenderEntity))
                    {
                        var projRef = projectileRefLookup[defenderEntity];
                        ecb.AddComponent(defenderEntity, new ProjectileSpawnRequest
                        {
                            target = bestTarget,
                            origin = defPos,
                            damage = emittedDamage,
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
                        // Phase 8 §13 follow-up — melee AoE capped by
                        // attackTargetCount. N=1 (default) hits only bestTarget
                        // via a fast path that avoids any NativeArray alloc; N>1
                        // opens the hitMask loop to pick additional nearest
                        // in-range targets. Runtime value on AttackState is
                        // mutable so future level-up/buff can change AoE width
                        // without touching the source SO.
                        int desiredCount = math.max(1, attack.ValueRO.attackTargetCount);
                        if (desiredCount == 1)
                        {
                            // Fast path: single target — no allocation.
                            ecb.AppendToBuffer(bestTarget,
                                new IncomingDamage { amount = emittedDamage });
                        }
                        else
                        {
                            // AoE branch: seed pass 0 with the already-known
                            // nearest (bestTarget) to avoid recomputing, then
                            // iterate the remaining passes over the hitMask.
                            var hitMask = new NativeArray<bool>(targetEntities.Length, Allocator.Temp);
                            int bestIdx = -1;
                            for (int i = 0; i < targetEntities.Length; i++)
                            {
                                if (targetEntities[i] == bestTarget) { bestIdx = i; break; }
                            }
                            if (bestIdx >= 0)
                            {
                                hitMask[bestIdx] = true;
                                ecb.AppendToBuffer(targetEntities[bestIdx],
                                    new IncomingDamage { amount = emittedDamage });
                            }
                            for (int pass = 1; pass < desiredCount; pass++)
                            {
                                float passSq = float.MaxValue;
                                int passIdx = -1;
                                for (int i = 0; i < targetEntities.Length; i++)
                                {
                                    if (hitMask[i]) continue;
                                    if (((int)targetFactions[i].value & mask) == 0) continue;
                                    if (targetEntities[i] == defenderEntity) continue;
                                    float d2 = DistanceSqToTarget(defPos, targetEntities[i], targetTransforms[i].Position, blockingHazardCellsLookup, hasFlowField, flowField, out _);
                                    if (d2 <= rangeSq && d2 < passSq)
                                    {
                                        passSq = d2;
                                        passIdx = i;
                                    }
                                }
                                if (passIdx < 0) break;
                                hitMask[passIdx] = true;
                                ecb.AppendToBuffer(targetEntities[passIdx],
                                    new IncomingDamage { amount = emittedDamage });
                            }
                            hitMask.Dispose();
                        }
                    }

                    attack.ValueRW.cooldownRemaining = attack.ValueRO.cooldownDuration * cooldownMul;

                    if (ccWriter.HasValue && defenderCcLookup.HasComponent(defenderEntity))
                    {
                        var ccData = defenderCcLookup[defenderEntity];
                        if (ccData.knockbackDistance > 0f && ccData.knockbackDuration > 0f)
                        {
                            // Physical collision direction:
                            //   D = projectile travel (defender→enemy)
                            //   E = enemy travel (flow at enemy cell)
                            //   dir = normalize(D - E)  ← relative-velocity impulse direction
                            // Falls back to D when flow field unavailable (tests).
                            float3 D = math.normalizesafe(bestTargetPos - defPos);
                            D.y = 0f;
                            // Guard: defender colocated with target → D≈0 → no meaningful direction.
                            // Skip impulse (otherwise dir would degenerate to -E and push the enemy
                            // backward along its flow path, an unintended side effect).
                            if (math.lengthsq(D) > 1e-6f)
                            {
                                float3 dir;
                                if (SystemAPI.TryGetSingleton<Wassup.Battle.Effects.FlowFieldSingleton>(out var ff))
                                {
                                    var targetCell = GridMath.WorldToCell(bestTargetPos, ff.tileSize, ff.gridSize);
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

            // Phase 4 — enemy→defender loop. Attack units with an AttackState
            // fire directly (no projectile path, no boost/synergy scaling).
            foreach (var (attack, transform, attackerEntity) in
                     SystemAPI.Query<RefRW<AttackState>, RefRO<LocalTransform>>()
                              .WithAll<AttackUnitTag>()
                              .WithEntityAccess())
            {
                if (attack.ValueRO.cooldownRemaining > 0f)
                {
                    attack.ValueRW.cooldownRemaining = math.max(0f, attack.ValueRO.cooldownRemaining - dt);
                }

                float3 atkPos = transform.ValueRO.Position;
                float range = attack.ValueRO.range;
                float rangeSq = range * range;
                float bestSq = float.MaxValue;
                Entity bestTarget = Entity.Null;
                int mask = attack.ValueRO.targetMask;
                for (int i = 0; i < targetEntities.Length; i++)
                {
                    if (((int)targetFactions[i].value & mask) == 0) continue;
                    if (targetEntities[i] == attackerEntity) continue;
                    float d2 = DistanceSqToTarget(atkPos, targetEntities[i], targetTransforms[i].Position, blockingHazardCellsLookup, hasFlowField, flowField, out _);
                    if (d2 <= rangeSq && d2 < bestSq)
                    {
                        bestSq = d2;
                        bestTarget = targetEntities[i];
                    }
                }

                if (bestTarget != Entity.Null && attack.ValueRO.cooldownRemaining <= 0f)
                {
                    ecb.AppendToBuffer(bestTarget,
                        new IncomingDamage { amount = attack.ValueRO.damage });
                    attack.ValueRW.cooldownRemaining = attack.ValueRO.cooldownDuration;
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
                float3 cellWorld = GridMath.CellToWorldCenter(cells[i].cell, flowField.tileSize, fallbackTargetPos.y);
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
