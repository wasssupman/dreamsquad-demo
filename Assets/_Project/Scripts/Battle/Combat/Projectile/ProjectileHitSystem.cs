using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Wassup.Battle.Effects;
using Wassup.Battle.Movement;
using Wassup.Battle.Units;
using Wassup.Data;

namespace Wassup.Battle.Combat.Projectile
{
    // Payload axis of the projectile pipeline: resolves a projectile once
    // ProjectileMoveSystem has flagged arrival (ProjectileState.impactReached),
    // dispatching on PayloadKind. SingleSplash applies the shooter's outputs to the
    // direct target plus the OnHitEffectType.Splash bonus to nearby enemies.
    //
    // IncomingDamage is a Units-owned buffer used as a Combat→Units event channel
    // per TRD 2.5.2 rule 2. The shooter's AttackState is not touched — cooldown
    // reset happens inside AttackSystem at launch time.
    [BurstCompile]
    [UpdateInGroup(typeof(BattleSimGroup))]
    [UpdateAfter(typeof(ProjectileMoveSystem))]
    public partial struct ProjectileHitSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ProjectileTag>();
        }

        private const float HitFlashDuration = 0.15f;

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var transformLookup = SystemAPI.GetComponentLookup<LocalTransform>(isReadOnly: true);
            var damageBufferLookup = SystemAPI.GetBufferLookup<IncomingDamage>(isReadOnly: false);
            var healBufferLookup = SystemAPI.GetBufferLookup<Wassup.Battle.Units.IncomingHeal>(isReadOnly: false);
            var outputLookup = SystemAPI.GetBufferLookup<AttackOutputElement>(isReadOnly: true);
            var hitFlashLookup = SystemAPI.GetComponentLookup<HitFlashTag>(isReadOnly: true);
            bool hasStatQ = SystemAPI.TryGetSingleton<StatModifierApplyEventsSingleton>(out var statEvents);
            bool hasStackQ = SystemAPI.TryGetSingleton<StackModifierApplyEventsSingleton>(out var stackEvents);

            // Combat→Presentation: hit-VFX channel. May not exist before
            // BattleBridge.EnsureQueriesAndQueues runs (very first frames in
            // tests / dev hot-reload), so guarded by HasSingleton.
            bool hasHitChannel = SystemAPI.HasSingleton<ProjectileHitEventsSingleton>();
            NativeQueue<ProjectileHitEvent> hitQueue = default;
            if (hasHitChannel)
                hitQueue = SystemAPI.GetSingleton<ProjectileHitEventsSingleton>().queue;

            // Snapshot all living attack units up-front so a Splash hit can iterate
            // them without a nested SystemAPI.Query inside the projectile loop.
            // AttackUnitTag filter keeps non-enemy entities out of the AOE pool.
            var aoeQuery = SystemAPI.QueryBuilder().WithAll<AttackUnitTag, LocalTransform>().Build();
            var aoeEntities = aoeQuery.ToEntityArray(Allocator.Temp);
            var aoeTransforms = aoeQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);

            // Grid params for the TileAoe payload (impact cell + candidate cells).
            // Same source MeteorResolutionSystem uses; defaults keep it safe before
            // the flow field exists (early frames / tests). Hoisted out of the loop.
            bool hasFlowField = SystemAPI.TryGetSingleton<FlowFieldSingleton>(out var flowField);
            float tileSize = hasFlowField ? flowField.tileSize : 1f;
            int2 gridSize = hasFlowField ? flowField.gridSize : new int2(128, 128);
            float3 ffOrigin = hasFlowField ? flowField.origin : float3.zero;

            foreach (var (projectile, entity) in
                     SystemAPI.Query<RefRO<ProjectileState>>()
                              .WithAll<ProjectileTag>()
                              .WithEntityAccess())
            {
                if (!projectile.ValueRO.impactReached) continue;

                switch (projectile.ValueRO.payload)
                {
                    case PayloadKind.SingleSplash:
                    {
                        var target = projectile.ValueRO.target;
                        if (target != Entity.Null && transformLookup.HasComponent(target))
                        {
                            float3 targetPos = transformLookup[target].Position;

                            bool handledOutputs = false;
                            if (outputLookup.HasBuffer(entity))
                            {
                                handledOutputs = true;
                                var outputs = outputLookup[entity];
                                for (int i = 0; i < outputs.Length; i++)
                                {
                                    var output = outputs[i].value;
                                    switch (output.kind)
                                    {
                                        case AttackOutputKind.Damage:
                                            if (damageBufferLookup.HasBuffer(target))
                                                ecb.AppendToBuffer(target, new IncomingDamage { amount = output.magnitude });
                                            break;

                                        case AttackOutputKind.Heal:
                                            if (healBufferLookup.HasBuffer(target))
                                                ecb.AppendToBuffer(target, new Wassup.Battle.Units.IncomingHeal { amount = output.magnitude });
                                            break;

                                        case AttackOutputKind.ApplyStat:
                                            if (hasStatQ)
                                                statEvents.queue.Enqueue(new StatModifierApplyEvent
                                                {
                                                    target = target,
                                                    stat = output.stat,
                                                    op = output.op,
                                                    magnitude = output.magnitude,
                                                    duration = output.duration,
                                                    source = entity,
                                                    stackId = 0,
                                                });
                                            break;

                                        case AttackOutputKind.ApplyStack:
                                            if (hasStackQ)
                                                stackEvents.queue.Enqueue(new StackModifierApplyEvent
                                                {
                                                    target = target,
                                                    kind = output.stackKind,
                                                    countDelta = (byte)math.max(1f, output.magnitude),
                                                    maxStack = output.stackMaxStack > 0 ? output.stackMaxStack : (byte)5,
                                                    perAppDuration = output.duration,
                                                    source = entity,
                                                });
                                            break;
                                    }
                                }
                            }

                            if (!handledOutputs && damageBufferLookup.HasBuffer(target))
                                ecb.AppendToBuffer(target, new IncomingDamage { amount = projectile.ValueRO.damage });

                            // Combat→Presentation: one hit event per direct target —
                            // splash secondary damage gets no extra VFX (intentional).
                            if (hasHitChannel)
                                hitQueue.Enqueue(new ProjectileHitEvent
                                {
                                    position = targetPos,
                                    dataIndex = projectile.ValueRO.dataIndex,
                                    payload = PayloadKind.SingleSplash,
                                });

                            // Splash AOE: reduced damage to every other AttackUnit within
                            // splashRadius of the direct target (direct target skipped to
                            // avoid double-damage).
                            if (projectile.ValueRO.onHitEffect == OnHitEffectType.Splash &&
                                projectile.ValueRO.splashRadius > 0f)
                            {
                                float3 aoeCenter = targetPos;
                                float splashRadiusSq = projectile.ValueRO.splashRadius * projectile.ValueRO.splashRadius;
                                float splashDamage = projectile.ValueRO.damage * projectile.ValueRO.splashDamageMul;
                                for (int i = 0; i < aoeEntities.Length; i++)
                                {
                                    var candidate = aoeEntities[i];
                                    if (candidate == target) continue;
                                    float dx = aoeTransforms[i].Position.x - aoeCenter.x;
                                    float dz = aoeTransforms[i].Position.z - aoeCenter.z;
                                    if (dx * dx + dz * dz > splashRadiusSq) continue;
                                    if (damageBufferLookup.HasBuffer(candidate))
                                        ecb.AppendToBuffer(candidate, new IncomingDamage { amount = splashDamage });
                                }
                            }

                            // Visual feedback: pulse the target briefly. Refresh the
                            // timer on back-to-back hits rather than overwriting scale.
                            if (hitFlashLookup.HasComponent(target))
                                ecb.SetComponent(target, new HitFlashTag
                                {
                                    remaining = HitFlashDuration,
                                    duration = HitFlashDuration,
                                    originalScale = hitFlashLookup[target].originalScale,
                                });
                            else
                                ecb.AddComponent(target, new HitFlashTag
                                {
                                    remaining = HitFlashDuration,
                                    duration = HitFlashDuration,
                                    originalScale = transformLookup[target].Scale,
                                });
                        }
                        break;
                    }

                    case PayloadKind.TileAoe:
                    {
                        // Flat AOE to every enemy within impactTileRange of the
                        // cell-locked impact — no direct target, no falloff (shares
                        // the tile-membership rule with the legacy Meteor resolver).
                        // Damage source depends on the spawner: defender-fired = the
                        // pre-summed Damage-output total; skill-fired (Meteor) =
                        // SkillData.magnitude — both snapshotted into state.damage
                        // (contract: no new field). non-Damage outputs are a
                        // follow-up (v1 is Damage-only).
                        float3 impactWorld = projectile.ValueRO.impact;
                        int2 centerCell = GridMath.WorldToCell(impactWorld, tileSize, gridSize, origin: ffOrigin);
                        int tileRange = projectile.ValueRO.impactTileRange;
                        float dmg = projectile.ValueRO.damage;
                        for (int i = 0; i < aoeEntities.Length; i++)
                        {
                            int2 cell = GridMath.WorldToCell(aoeTransforms[i].Position, tileSize, gridSize, origin: ffOrigin);
                            if (!TileAoe.IsInTileRange(cell, centerCell, tileRange)) continue;
                            if (damageBufferLookup.HasBuffer(aoeEntities[i]))
                                ecb.AppendToBuffer(aoeEntities[i], new IncomingDamage { amount = dmg });
                        }

                        // Impact-crater VFX at the cell (not a target position). No
                        // per-target HitFlash: an AOE strike flashing N enemies is
                        // visual noise — matches the Meteor precedent. radiusWorld
                        // snapshots the per-cast AOE radius for the burst visual.
                        if (hasHitChannel)
                            hitQueue.Enqueue(new ProjectileHitEvent
                            {
                                position = impactWorld,
                                dataIndex = projectile.ValueRO.dataIndex,
                                payload = PayloadKind.TileAoe,
                                radiusWorld = tileRange * tileSize,
                            });
                        break;
                    }

                    default:
                        // Unknown payload: no resolution. Unlike MoveSystem's default,
                        // this can't leak — the projectile is consumed unconditionally
                        // just below. Present for parity / intent when a future arm lands.
                        break;
                }

                ecb.DestroyEntity(entity);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
            aoeEntities.Dispose();
            aoeTransforms.Dispose();
        }
    }
}
