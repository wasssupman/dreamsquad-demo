using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Wassup.Battle.Combat;

namespace Wassup.Battle.Units
{
    // Drains IncomingDamage buffers into Health on each owning entity. When health crosses zero,
    // the entity gets a DeadTag so UnitLifecycleSystem can destroy it.
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(AttackSystem))]
    public partial struct DamageApplicationSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<IncomingDamage>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (health, damageBuffer, entity) in
                     SystemAPI.Query<RefRW<Health>, DynamicBuffer<IncomingDamage>>()
                              .WithNone<DeadTag>()
                              .WithNone<PendingDeployment>()
                              .WithEntityAccess())
            {
                if (damageBuffer.Length == 0) continue;

                float total = 0f;
                for (int i = 0; i < damageBuffer.Length; i++)
                {
                    total += damageBuffer[i].amount;
                }
                damageBuffer.Clear();

                float newHp = health.ValueRO.value - total;
                health.ValueRW.value = newHp;
                if (newHp <= 0f)
                {
                    ecb.AddComponent<DeadTag>(entity);
                }
            }
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
