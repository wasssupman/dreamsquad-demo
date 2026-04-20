using Unity.Burst;
using Unity.Collections;
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
            state.RequireForUpdate<FlowFieldSingleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            float dt = SystemAPI.Time.DeltaTime;

            var field = SystemAPI.GetSingleton<FlowFieldSingleton>();
            var slowLookup = SystemAPI.GetComponentLookup<SlowEffect>(isReadOnly: true);

            var portalQuery = SystemAPI.QueryBuilder().WithAll<PortalLink>().Build();
            var portals = portalQuery.ToComponentDataArray<PortalLink>(Allocator.Temp);

            var tornadoQuery = SystemAPI.QueryBuilder().WithAll<TornadoField>().Build();
            var tornadoFields = tornadoQuery.ToComponentDataArray<TornadoField>(Allocator.Temp);

            foreach (var (transform, follow, entity) in
                     SystemAPI.Query<RefRW<LocalTransform>, RefRO<PathFollowState>>()
                              .WithNone<PastGoalTag>()
                              .WithEntityAccess())
            {
                float3 current = transform.ValueRO.Position;

                // 1. Portal entry: 내부에 있으면 exit 으로 텔레포트. exitWaypointIndex 제거됨 —
                //    다음 프레임 flow field 가 알아서 방향 공급.
                for (int p = 0; p < portals.Length; p++)
                {
                    var portal = portals[p];
                    float pdx = current.x - portal.entryWorld.x;
                    float pdz = current.z - portal.entryWorld.z;
                    if (pdx * pdx + pdz * pdz <= portal.entryRadius * portal.entryRadius)
                    {
                        transform.ValueRW.Position = new float3(portal.exitWorld.x, current.y, portal.exitWorld.z);
                        current = transform.ValueRW.Position;
                        break;
                    }
                }

                // 2. Current cell lookup + goal 판정
                int2 cell = GridMath.WorldToCell(current, field.tileSize, field.gridSize);
                if (cell.x == field.goalCell.x && cell.y == field.goalCell.y)
                {
                    ecb.AddComponent<PastGoalTag>(entity);
                    continue;
                }

                // 3. Tornado field: pull override (Phase 8 §17 유지).
                bool pulled = false;
                for (int t = 0; t < tornadoFields.Length; t++)
                {
                    var fieldT = tornadoFields[t];
                    float fdx = current.x - fieldT.centerWorld.x;
                    float fdz = current.z - fieldT.centerWorld.z;
                    if (fdx * fdx + fdz * fdz > fieldT.radius * fieldT.radius) continue;
                    float3 toCenter = fieldT.centerWorld - current;
                    toCenter.y = 0f;
                    float centerDist = math.length(toCenter);
                    float pullStep = fieldT.pullSpeed * dt;
                    transform.ValueRW.Position = (centerDist <= pullStep || centerDist < 1e-4f)
                        ? new float3(fieldT.centerWorld.x, current.y, fieldT.centerWorld.z)
                        : current + math.normalize(toCenter) * pullStep;
                    pulled = true;
                    break;
                }
                if (pulled) continue;

                // 4. Flow field step
                int idx = GridMath.CellIndex(cell, field.gridSize);
                float2 dir = field.flow[idx];
                if (math.lengthsq(dir) < 1e-6f) continue; // unreachable: 제자리 유지

                float slowMul = slowLookup.HasComponent(entity) ? slowLookup[entity].multiplier : 1f;
                float step = follow.ValueRO.speed * slowMul * dt;
                transform.ValueRW.Position = current + new float3(dir.x, 0, dir.y) * step;
            }

            portals.Dispose();
            tornadoFields.Dispose();
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
