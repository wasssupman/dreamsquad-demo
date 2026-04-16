using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Wassup.Battle.Combat.Projectile
{
    // Advances each projectile toward its target each frame. When the target no
    // longer exists (destroyed by DamageApplicationSystem or a prior hit) the
    // projectile is destroyed — we never apply damage to a ghost target.
    //
    // Target validity is checked through a read-only ComponentLookup<LocalTransform>.
    // EntityManager.Exists() is not Burst-compatible; HasComponent on the lookup
    // serves the same purpose within a Burst-compiled ISystem.
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct ProjectileMoveSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ProjectileTag>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.Time.DeltaTime;
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var transformLookup = SystemAPI.GetComponentLookup<LocalTransform>(isReadOnly: true);

            foreach (var (transform, projectile, entity) in
                     SystemAPI.Query<RefRW<LocalTransform>, RefRO<ProjectileState>>()
                              .WithAll<ProjectileTag>()
                              .WithEntityAccess())
            {
                var target = projectile.ValueRO.target;
                if (target == Entity.Null || !transformLookup.HasComponent(target))
                {
                    ecb.DestroyEntity(entity);
                    continue;
                }

                float3 currentPos = transform.ValueRO.Position;
                float3 targetPos = transformLookup[target].Position;
                float3 delta = targetPos - currentPos;
                float dist = math.length(delta);
                float step = projectile.ValueRO.speed * dt;

                if (dist <= step)
                {
                    transform.ValueRW.Position = targetPos;
                }
                else
                {
                    transform.ValueRW.Position = currentPos + math.normalize(delta) * step;
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
