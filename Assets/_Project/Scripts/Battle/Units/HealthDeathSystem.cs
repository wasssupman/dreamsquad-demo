using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace Wassup.Battle.Units
{
    // Converts any zero-HP entity into the shared death path, even when HP was
    // changed outside DamageApplicationSystem.
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(UnitLifecycleSystem))]
    public partial struct HealthDeathSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Health>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (health, entity) in
                     SystemAPI.Query<RefRO<Health>>()
                              .WithNone<DeadTag>()
                              .WithEntityAccess())
            {
                if (health.ValueRO.value <= 0f)
                    ecb.AddComponent<DeadTag>(entity);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
