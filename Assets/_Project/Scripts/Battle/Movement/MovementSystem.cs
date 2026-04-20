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

            // Phase 7 — snapshot active portals into a NativeArray so the per-attacker
            // loop below does a flat O(n·p) intersection test without nested queries.
            // `p` is 1-2 in normal play (two portal skill slots at most).
            var portalQuery = SystemAPI.QueryBuilder().WithAll<PortalLink>().Build();
            var portals = portalQuery.ToComponentDataArray<PortalLink>(Unity.Collections.Allocator.Temp);

            // Phase 8 §17 — continuous tornado fields (replaces per-attacker
            // TornadoPull). Each frame, any attacker inside a field's radius gets
            // pulled toward its center, so enemies that enter mid-duration are
            // also affected (previously the Phase 7 snapshot missed them).
            var tornadoQuery = SystemAPI.QueryBuilder().WithAll<TornadoField>().Build();
            var tornadoFields = tornadoQuery.ToComponentDataArray<TornadoField>(Unity.Collections.Allocator.Temp);

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
                        // Phase 9: exitWaypointIndex 제거됨. 다음 프레임 flow field 가 새 방향을 공급.
                        break;
                    }
                }

                int idx = follow.ValueRO.currentWaypointIndex;
                if (idx >= waypoints.Length)
                {
                    ecb.AddComponent<PastGoalTag>(entity);
                    continue;
                }

                // Phase 8 §17 — continuous TornadoField check. If the attacker
                // is currently inside any active field, override waypoint step
                // with a pull toward the nearest field's center. Path state
                // stays untouched so after the field expires or the attacker
                // exits the radius, they resume from current Position to the
                // same waypoint.
                bool pulled = false;
                for (int t = 0; t < tornadoFields.Length; t++)
                {
                    var field = tornadoFields[t];
                    float fdx = current.x - field.centerWorld.x;
                    float fdz = current.z - field.centerWorld.z;
                    if (fdx * fdx + fdz * fdz > field.radius * field.radius) continue;
                    float3 toCenter = field.centerWorld - current;
                    toCenter.y = 0f;
                    float centerDist = math.length(toCenter);
                    float pullStep = field.pullSpeed * dt;
                    if (centerDist <= pullStep || centerDist < 1e-4f)
                    {
                        transform.ValueRW.Position = new float3(field.centerWorld.x, current.y, field.centerWorld.z);
                    }
                    else
                    {
                        transform.ValueRW.Position = current + math.normalize(toCenter) * pullStep;
                    }
                    pulled = true;
                    break;
                }
                if (pulled) continue;

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
            tornadoFields.Dispose();
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
