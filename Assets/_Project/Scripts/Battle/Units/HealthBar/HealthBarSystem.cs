using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Wassup.Battle.Units.HealthBar
{
    // Mirrors each bar entity's transform from its owner's Health + LocalTransform.
    // Health is read-only — we own only the bar's own transform. If the owner
    // disappears the bar self-destroys so no orphan visuals remain.
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct HealthBarSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<HealthBarTag>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var healthLookup = SystemAPI.GetComponentLookup<Health>(isReadOnly: true);
            var transformLookup = SystemAPI.GetComponentLookup<LocalTransform>(isReadOnly: true);

            foreach (var (transform, bar, entity) in
                     SystemAPI.Query<RefRW<LocalTransform>, RefRO<HealthBarState>>()
                              .WithAll<HealthBarTag>()
                              .WithEntityAccess())
            {
                var owner = bar.ValueRO.owner;
                if (owner == Entity.Null ||
                    !healthLookup.HasComponent(owner) ||
                    !transformLookup.HasComponent(owner))
                {
                    ecb.DestroyEntity(entity);
                    continue;
                }

                var health = healthLookup[owner];
                float ratio = health.max > 0f ? math.clamp(health.value / health.max, 0f, 1f) : 0f;
                float3 ownerPos = transformLookup[owner].Position;

                transform.ValueRW.Position = new float3(ownerPos.x, ownerPos.y + bar.ValueRO.yOffset, ownerPos.z);
                transform.ValueRW.Scale = bar.ValueRO.baseScale * ratio;
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
