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
            var ccLookup = SystemAPI.GetBufferLookup<CcEffect>(isReadOnly: true);
            var modifierStatsLookup = SystemAPI.GetComponentLookup<ModifierStats>(isReadOnly: true);
            var attackPauseLookup = SystemAPI.GetComponentLookup<EnemyAttackMovePause>(isReadOnly: false);
            var hasObstacles = SystemAPI.TryGetSingleton<ObstacleSingleton>(out var obstacleSingleton);

            var portalQuery = SystemAPI.QueryBuilder().WithAll<PortalLink>().Build();
            var portals = portalQuery.ToComponentDataArray<PortalLink>(Allocator.Temp);

            var tornadoQuery = SystemAPI.QueryBuilder().WithAll<TornadoField>().Build();
            var tornadoFields = tornadoQuery.ToComponentDataArray<TornadoField>(Allocator.Temp);

            // aggro-targeting Unit 3 — snapshot guardian positions so aggroed enemies
            // can self-walk toward their anchor. Separate RO query avoids aliasing the
            // RW LocalTransform in the movement loop below.
            var aggroLookup = SystemAPI.GetComponentLookup<Aggroed>(isReadOnly: true);
            // aggro-standoff — 적의 공격 사거리(Combat AttackState) RO 읽기. 도달 조건 판정용.
            var attackStateLookup = SystemAPI.GetComponentLookup<Wassup.Battle.Combat.AttackState>(isReadOnly: true);
            var guardianPos = new NativeHashMap<Entity, float3>(16, Allocator.Temp);
            foreach (var (gTransform, gEntity) in
                     SystemAPI.Query<RefRO<LocalTransform>>().WithAll<AggroProvider>().WithEntityAccess())
                guardianPos[gEntity] = gTransform.ValueRO.Position;

            foreach (var (transform, follow, entity) in
                     SystemAPI.Query<RefRW<LocalTransform>, RefRO<PathFollowState>>()
                              .WithNone<PastGoalTag>()
                              .WithEntityAccess())
            {
                float3 current = transform.ValueRO.Position;

                // aggro-targeting / enemy-tile-movement-integrity unit 2 — aggro 의 본질은
                // 이동목표 변경(goal→guardian)뿐. 별도 로코모션이 아니라, guardian 방향 step 을
                // 다른 이동과 같은 cell-trim 에 통과시켜 walk 타일 위에 머물게 한다. non-walk
                // (guardian 의 Place 타일 포함)는 cell-trim 이 벽으로 막아 적은 인접 walk 타일
                // 경계에 정착, 공격은 AttackSystem(sticky-target)이 사거리에서 처리.
                // Ignores EnemyAttackMovePause so it keeps closing on the anchor.
                if (aggroLookup.HasComponent(entity))
                {
                    var guardian = aggroLookup[entity].guardian;
                    if (guardianPos.TryGetValue(guardian, out var gpos))
                    {
                        float3 to = gpos - current; to.y = 0f;
                        float dist = math.length(to);
                        // aggro-standoff — 도달 조건 = 공격범위 안. AttackState.range 안에 들면 이동 종료
                        // (그 자리에서 AttackSystem 이 발사). range 없으면 0 → 경계까지 접근(폴백).
                        float standoff = attackStateLookup.HasComponent(entity)
                            ? attackStateLookup[entity].range : 0f;
                        if (dist > standoff)
                        {
                            float aggroSpeedMul = modifierStatsLookup.HasComponent(entity)
                                ? modifierStatsLookup[entity].moveSpeedMul : 1f;
                            float step = follow.ValueRO.speed * aggroSpeedMul * dt;
                            float3 desiredAggro = (step >= dist)
                                ? new float3(gpos.x, current.y, gpos.z)
                                : current + math.normalize(to) * step;
                            int2 aggroCell = GridMath.WorldToCell(current, field.tileSize, field.gridSize, origin: field.origin);
                            transform.ValueRW.Position = MovementCellTrim.Apply(
                                desiredAggro, aggroCell, in field, hasObstacles, in obstacleSingleton);
                        }
                        continue; // skip flow/portal/tornado/goal/pause while aggroed
                    }
                    // guardian missing (AggroAssignmentSystem should have released) → fall through
                }

                if (attackPauseLookup.HasComponent(entity))
                {
                    var pause = attackPauseLookup[entity];
                    if (pause.remaining > 0f)
                    {
                        pause.remaining = math.max(0f, pause.remaining - dt);
                        attackPauseLookup[entity] = pause;
                        continue;
                    }
                }

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
                int2 cell = GridMath.WorldToCell(current, field.tileSize, field.gridSize, origin: field.origin);
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
                    int2 entityCell = GridMath.WorldToCell(current, field.tileSize, field.gridSize, origin: field.origin);
                    int2 centerCell = GridMath.WorldToCell(fieldT.centerWorld, field.tileSize, field.gridSize, origin: field.origin);
                    int tileDist = math.max(math.abs(entityCell.x - centerCell.x), math.abs(entityCell.y - centerCell.y));
                    if (tileDist > fieldT.tileRange) continue;
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
                bool zeroFlowRecovery = false;
                if (math.lengthsq(dir) < 1e-6f)
                {
                    zeroFlowRecovery = true;
                    // Zero-flow cell: impulse may have pushed entity into an unreachable cell.
                    // Try 4 cardinal neighbors; move toward the one with the smallest finite dist.
                    float2 recovDir = float2.zero;
                    int bestD = field.dist[idx];
                    int2 nb;
                    int d;
                    nb = cell + new int2( 1, 0); if (nb.x < field.gridSize.x) { d = field.dist[GridMath.CellIndex(nb, field.gridSize)]; if (d < bestD) { bestD = d; recovDir = new float2( 1, 0); } }
                    nb = cell + new int2(-1, 0); if (nb.x >= 0)              { d = field.dist[GridMath.CellIndex(nb, field.gridSize)]; if (d < bestD) { bestD = d; recovDir = new float2(-1, 0); } }
                    nb = cell + new int2( 0, 1); if (nb.y < field.gridSize.y) { d = field.dist[GridMath.CellIndex(nb, field.gridSize)]; if (d < bestD) { bestD = d; recovDir = new float2( 0, 1); } }
                    nb = cell + new int2( 0,-1); if (nb.y >= 0)              { d = field.dist[GridMath.CellIndex(nb, field.gridSize)]; if (d < bestD) { bestD = d; recovDir = new float2( 0,-1); } }
                    if (math.lengthsq(recovDir) < 1e-6f) continue; // truly isolated cell
                    dir = recovDir;
                }

                float speedMul = modifierStatsLookup.HasComponent(entity)
                    ? modifierStatsLookup[entity].moveSpeedMul
                    : 1f;
                float3 impulseDisplacement = float3.zero;
                if (ccLookup.HasBuffer(entity))
                {
                    var ccBuf = ccLookup[entity];
                    for (int i = 0; i < ccBuf.Length; i++)
                    {
                        var cc = ccBuf[i];
                        switch (cc.kind)
                        {
                            case CcKind.Impulse: impulseDisplacement += cc.vector * dt; break;
                        }
                    }
                }
                float2 stepDir = math.normalizesafe(dir); // Phase 9: FlowFieldBuilder writes unit vectors;
                                                           // normalizesafe defensively handles future diagonal/non-unit flow
                                                           // and returns zero for <1e-6 magnitude (already guarded above).
                float3 flowStep = new float3(stepDir.x, 0, stepDir.y) * follow.ValueRO.speed * speedMul * dt;
                float3 desired = current + flowStep + impulseDisplacement;

                // enemy-tile-movement-integrity unit 1 — 코너 엣지-허깅 측면 복원(target=0 + dead-band).
                // zero-flow recovery 분기는 스킵(이미 교정 이동 중). 임펄스 측면성분은 이 프레임 보존
                // (recenter 는 current 기준 standing 오프셋만 당김 → 넉백은 이후 프레임에 점진 복귀).
                if (!zeroFlowRecovery)
                    desired += LateralRecenter.Compute(current, cell, stepDir,
                        follow.ValueRO.speed * speedMul, dt, field.tileSize, field.origin);

                // Cell-trim (option B): keep impulse/recenter from pushing into wall/obstacle cells.
                desired = MovementCellTrim.Apply(desired, cell, in field, hasObstacles, in obstacleSingleton);

                transform.ValueRW.Position = desired;
            }

            portals.Dispose();
            tornadoFields.Dispose();
            guardianPos.Dispose();
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
