using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Wassup.Battle.Movement
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct MovementSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PathFollowState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);
            var dt = SystemAPI.Time.DeltaTime;

            foreach (var (transform, follow, waypoints, entity) in
                     SystemAPI.Query<RefRW<LocalTransform>, RefRW<PathFollowState>, DynamicBuffer<PathWaypoint>>()
                              .WithNone<PastGoalTag>()
                              .WithEntityAccess())
            {
                int idx = follow.ValueRO.currentWaypointIndex;
                if (idx >= waypoints.Length)
                {
                    ecb.AddComponent<PastGoalTag>(entity);
                    continue;
                }

                var target = waypoints[idx].cell;
                float3 targetWorld = new float3(
                    target.x * follow.ValueRO.tileSize,
                    transform.ValueRO.Position.y,
                    target.y * follow.ValueRO.tileSize);
                float3 current = transform.ValueRO.Position;
                float3 delta = targetWorld - current;
                float dist = math.length(delta);
                float step = follow.ValueRO.speed * dt;

                if (dist <= step)
                {
                    transform.ValueRW.Position = targetWorld;
                    follow.ValueRW.currentWaypointIndex = idx + 1;
                }
                else
                {
                    transform.ValueRW.Position = current + math.normalize(delta) * step;
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
