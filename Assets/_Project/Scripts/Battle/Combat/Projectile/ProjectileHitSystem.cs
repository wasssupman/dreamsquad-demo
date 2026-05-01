using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Wassup.Battle.Effects;
using Wassup.Battle.Units;
using Wassup.Data;

namespace Wassup.Battle.Combat.Projectile
{
    // Resolves a projectile once it is close enough to its target: appends an
    // IncomingDamage entry (Units-owned buffer, used as a Combat→Units event
    // channel per TRD 2.5.2 rule 2) and destroys the projectile entity. The
    // shooter's AttackState is not touched — cooldown reset happens inside
    // AttackSystem at launch time.
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
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
            {
                hitQueue = SystemAPI.GetSingleton<ProjectileHitEventsSingleton>().queue;
            }

            // Phase 4: snapshot all living attack units up-front so a Splash hit
            // can iterate them without doing a nested SystemAPI.Query inside the
            // projectile-iteration loop (Entities 1.4.x does not guarantee that
            // behavior). AttackUnitTag filter keeps HealthBar and other entities
            // out of the AOE pool.
            var aoeQuery = SystemAPI.QueryBuilder().WithAll<AttackUnitTag, LocalTransform>().Build();
            var aoeEntities = aoeQuery.ToEntityArray(Allocator.Temp);
            var aoeTransforms = aoeQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);

            foreach (var (transform, projectile, entity) in
                     SystemAPI.Query<RefRO<LocalTransform>, RefRO<ProjectileState>>()
                              .WithAll<ProjectileTag>()
                              .WithEntityAccess())
            {
                var target = projectile.ValueRO.target;
                if (target == Entity.Null || !transformLookup.HasComponent(target))
                {
                    ecb.DestroyEntity(entity);
                    continue;
                }

                float3 projectilePos = transform.ValueRO.Position;
                float3 targetPos = transformLookup[target].Position;
                float3 delta = targetPos - projectilePos;
                float distSq = delta.x * delta.x + delta.z * delta.z;
                float threshold = projectile.ValueRO.hitThreshold;
                if (distSq > threshold * threshold) continue;

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
                {
                    ecb.AppendToBuffer(target, new IncomingDamage { amount = projectile.ValueRO.damage });
                }

                // Combat→Presentation: enqueue a hit event so the MonoBehaviour
                // view pool can play the configured hit prefab. One enqueue per
                // direct target — splash secondary damage gets no extra VFX
                // (intentional: visual is one impact per shot).
                if (hasHitChannel)
                {
                    hitQueue.Enqueue(new ProjectileHitEvent
                    {
                        position = targetPos,
                        dataIndex = projectile.ValueRO.dataIndex,
                    });
                }

                // Phase 4 Splash AOE: distribute reduced damage to every other
                // AttackUnit within splashRadius of the direct target. The direct
                // target is explicitly skipped to avoid double-damage.
                if (projectile.ValueRO.onHitEffect == OnHitEffectType.Splash &&
                    projectile.ValueRO.splashRadius > 0f)
                {
                    float3 aoeCenter = transformLookup[target].Position;
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
                        {
                            ecb.AppendToBuffer(candidate,
                                new IncomingDamage { amount = splashDamage });
                        }
                    }
                }

                // Visual feedback: pulse the target briefly. We refresh the timer if
                // a prior hit is still in progress rather than overwriting the
                // original scale, so back-to-back hits keep showing the flash.
                if (transformLookup.HasComponent(target))
                {
                    if (hitFlashLookup.HasComponent(target))
                    {
                        ecb.SetComponent(target, new HitFlashTag
                        {
                            remaining = HitFlashDuration,
                            duration = HitFlashDuration,
                            originalScale = hitFlashLookup[target].originalScale,
                        });
                    }
                    else
                    {
                        ecb.AddComponent(target, new HitFlashTag
                        {
                            remaining = HitFlashDuration,
                            duration = HitFlashDuration,
                            originalScale = transformLookup[target].Scale,
                        });
                    }
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
