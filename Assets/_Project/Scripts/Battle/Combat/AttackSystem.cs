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
            _attackEventsQuery = state.GetEntityQuery(ComponentType.ReadWrite<DefenderAttackEventsSingleton>());
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
            var damageBoostLookup = SystemAPI.GetComponentLookup<DamageBoost>(isReadOnly: true);
            var cooldownReductionLookup = SystemAPI.GetComponentLookup<CooldownReduction>(isReadOnly: true);
            var synergyLookup = SystemAPI.GetComponentLookup<SynergyBuff>(isReadOnly: true);
            var projectileRefLookup = SystemAPI.GetComponentLookup<ProjectileRef>(isReadOnly: true);
            var defenderCcLookup = SystemAPI.GetComponentLookup<DefenderCcData>(isReadOnly: true);
            var defenderTagLookup = SystemAPI.GetComponentLookup<DefenderUnitTag>(isReadOnly: true);
            var blockingHazardCellsLookup = SystemAPI.GetBufferLookup<BlockingHazardCellsBuffer>(isReadOnly: true);
            bool hasFlowField = SystemAPI.TryGetSingleton<Wassup.Battle.Effects.FlowFieldSingleton>(out var flowField);

            // Hoist attack-event singleton writer — defender branch below enqueues a
            // single "defender fired" event per attack to trigger Spine animation for
            // both projectile and melee paths. Enemies never enqueue this event.
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
                float range = attack.ValueRO.range;
                float rangeSq = range * range;
                float bestSq = float.MaxValue;
                Entity bestTarget = Entity.Null;
                float3 bestTargetPos = default;
                int mask = attack.ValueRO.targetMask;
                for (int i = 0; i < targetEntities.Length; i++)
                {
                    if (((int)targetFactions[i].value & mask) == 0) continue;
                    if (targetEntities[i] == attackerEntity) continue;
                    float3 targetPos = targetTransforms[i].Position;
                    float d2 = DistanceSqToTarget(atkPos, targetEntities[i], targetPos, blockingHazardCellsLookup, hasFlowField, flowField, out var nearestPos);
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
                    bool isDefender = defenderTagLookup.HasComponent(attackerEntity);

                    // [Defender only] Enqueue "defender fired" event so Spine/Pool can
                    // trigger attack animation for both projectile and melee paths.
                    if (attackWriter.HasValue && isDefender)
                    {
                        attackWriter.Value.Enqueue(new DefenderAttackEvent
                        {
                            defender = attackerEntity,
                            targetWorld = bestTargetPos,
                        });
                    }

                    // Buff scaling — lookup returns 1.0 implicitly when not present.
                    // Enemies lack these components so they always use raw damage.
                    float damageMul = damageBoostLookup.HasComponent(attackerEntity)
                        ? damageBoostLookup[attackerEntity].multiplier
                        : 1f;
                    float cooldownMul = cooldownReductionLookup.HasComponent(attackerEntity)
                        ? cooldownReductionLookup[attackerEntity].multiplier
                        : 1f;
                    float synergyMul = synergyLookup.HasComponent(attackerEntity)
                        ? synergyLookup[attackerEntity].damageMul
                        : 1f;
                    float emittedDamage = attack.ValueRO.damage * damageMul * synergyMul;

                    // ProjectileRef-bearing attackers stage a spawn request for the
                    // MonoBehaviour drain loop in BattleBridge. Attackers without a
                    // ProjectileRef (enemies and melee defenders) use direct-damage.
                    if (projectileRefLookup.HasComponent(attackerEntity))
                    {
                        var projRef = projectileRefLookup[attackerEntity];
                        ecb.AddComponent(attackerEntity, new ProjectileSpawnRequest
                        {
                            target = bestTarget,
                            origin = atkPos,
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
                        // Melee AoE: N=1 fast path avoids allocation; N>1 hitMask loop
                        // picks additional nearest in-range targets. Enemies default to
                        // attackTargetCount=1 so they always take the fast path.
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
                                    if (targetEntities[i] == attackerEntity) continue;
                                    float d2 = DistanceSqToTarget(atkPos, targetEntities[i], targetTransforms[i].Position, blockingHazardCellsLookup, hasFlowField, flowField, out _);
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
