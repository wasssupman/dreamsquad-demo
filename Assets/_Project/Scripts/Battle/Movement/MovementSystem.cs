using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Wassup.Battle.Effects;

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
            var slowLookup = SystemAPI.GetComponentLookup<SlowEffect>(isReadOnly: true);
            var tornadoLookup = SystemAPI.GetComponentLookup<TornadoPull>(isReadOnly: true);

            // Phase 7 — snapshot active portals into a NativeArray so the per-attacker
            // loop below does a flat O(n·p) intersection test without nested queries.
            // `p` is 1-2 in normal play (two portal skill slots at most).
            var portalQuery = SystemAPI.QueryBuilder().WithAll<PortalLink>().Build();
            var portals = portalQuery.ToComponentDataArray<PortalLink>(Unity.Collections.Allocator.Temp);

            foreach (var (transform, follow, waypoints, entity) in
                     SystemAPI.Query<RefRW<LocalTransform>, RefRW<PathFollowState>, DynamicBuffer<PathWaypoint>>()
                              .WithNone<PastGoalTag>()
                              .WithEntityAccess())
            {
                float3 current = transform.ValueRO.Position;

                // Phase 7 — Portal: if this attacker is currently inside an entry
                // radius, teleport to the paired exit and advance waypoint index
                // so the next frame continues forward instead of turning back.
                for (int p = 0; p < portals.Length; p++)
                {
                    var portal = portals[p];
                    float pdx = current.x - portal.entryWorld.x;
                    float pdz = current.z - portal.entryWorld.z;
                    if (pdx * pdx + pdz * pdz <= portal.entryRadius * portal.entryRadius)
                    {
                        transform.ValueRW.Position = new float3(portal.exitWorld.x, current.y, portal.exitWorld.z);
                        current = transform.ValueRW.Position;
                        if (portal.exitWaypointIndex >= 0 && portal.exitWaypointIndex <= waypoints.Length)
                            follow.ValueRW.currentWaypointIndex = portal.exitWaypointIndex;
                        break;
                    }
                }

                int idx = follow.ValueRO.currentWaypointIndex;
                if (idx >= waypoints.Length)
                {
                    ecb.AddComponent<PastGoalTag>(entity);
                    continue;
                }

                // Phase 7 — TornadoPull overrides the waypoint step while active.
                // Attacker is yanked toward centerWorld at pullSpeed. Path state
                // stays untouched so after expiry they resume from current Position
                // to the same waypoint.
                if (tornadoLookup.HasComponent(entity))
                {
                    var pull = tornadoLookup[entity];
                    float3 toCenter = pull.centerWorld - current;
                    toCenter.y = 0f;
                    float centerDist = math.length(toCenter);
                    float pullStep = pull.pullSpeed * dt;
                    if (centerDist <= pullStep || centerDist < 1e-4f)
                    {
                        transform.ValueRW.Position = new float3(pull.centerWorld.x, current.y, pull.centerWorld.z);
                    }
                    else
                    {
                        transform.ValueRW.Position = current + math.normalize(toCenter) * pullStep;
                    }
                    continue;
                }

                var target = waypoints[idx].cell;
                float3 targetWorld = new float3(
                    target.x * follow.ValueRO.tileSize,
                    transform.ValueRO.Position.y,
                    target.y * follow.ValueRO.tileSize);
                float3 delta = targetWorld - current;
                float dist = math.length(delta);

                // Effects read-only: SlowEffect multiplies the per-frame step without
                // touching PathFollowState.speed (Movement still owns the base value).
                float slowMul = slowLookup.HasComponent(entity) ? slowLookup[entity].multiplier : 1f;
                float step = follow.ValueRO.speed * slowMul * dt;

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

            portals.Dispose();
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
