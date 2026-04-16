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
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<AttackState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.Time.DeltaTime;

            // Snapshot attacker entities + positions into native arrays so the inner loop stays Burst-friendly.
            var attackerQuery = SystemAPI.QueryBuilder().WithAll<AttackUnitTag, LocalTransform>().Build();
            var attackers = attackerQuery.ToEntityArray(Allocator.Temp);
            var attackerTransforms = attackerQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);

            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var damageBoostLookup = SystemAPI.GetComponentLookup<DamageBoost>(isReadOnly: true);
            var cooldownReductionLookup = SystemAPI.GetComponentLookup<CooldownReduction>(isReadOnly: true);
            var projectileRefLookup = SystemAPI.GetComponentLookup<ProjectileRef>(isReadOnly: true);

            foreach (var (attack, transform, defenderEntity) in
                     SystemAPI.Query<RefRW<AttackState>, RefRO<LocalTransform>>()
                              .WithAll<DefenderUnitTag>()
                              .WithEntityAccess())
            {
                // Tick cooldown first.
                if (attack.ValueRO.cooldownRemaining > 0f)
                {
                    attack.ValueRW.cooldownRemaining = math.max(0f, attack.ValueRO.cooldownRemaining - dt);
                }

                // Find nearest in-range attacker.
                float3 defPos = transform.ValueRO.Position;
                float range = attack.ValueRO.range;
                float rangeSq = range * range;
                float bestSq = float.MaxValue;
                Entity bestTarget = Entity.Null;
                for (int i = 0; i < attackers.Length; i++)
                {
                    float3 diff = attackerTransforms[i].Position - defPos;
                    float d2 = diff.x * diff.x + diff.z * diff.z;
                    if (d2 <= rangeSq && d2 < bestSq)
                    {
                        bestSq = d2;
                        bestTarget = attackers[i];
                    }
                }

                // Fire if cooldown ready and target exists.
                if (bestTarget != Entity.Null && attack.ValueRO.cooldownRemaining <= 0f)
                {
                    // Effects read-only: DamageBoost scales emitted damage, CooldownReduction
                    // shortens the reset interval. The base AttackState fields stay unmodified
                    // so Combat remains the sole owner of those values.
                    float damageMul = damageBoostLookup.HasComponent(defenderEntity)
                        ? damageBoostLookup[defenderEntity].multiplier
                        : 1f;
                    float cooldownMul = cooldownReductionLookup.HasComponent(defenderEntity)
                        ? cooldownReductionLookup[defenderEntity].multiplier
                        : 1f;
                    float emittedDamage = attack.ValueRO.damage * damageMul;

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
                            assetIndex = projRef.assetIndex,
                        });
                    }
                    else
                    {
                        ecb.AppendToBuffer(bestTarget,
                            new IncomingDamage { amount = emittedDamage });
                    }

                    attack.ValueRW.cooldownRemaining = attack.ValueRO.cooldownDuration * cooldownMul;
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
            attackers.Dispose();
            attackerTransforms.Dispose();
        }
    }
}
