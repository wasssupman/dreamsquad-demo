using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Wassup.Battle.Combat;
using Wassup.Battle.Effects;

namespace Wassup.Battle.Movement
{
    [BurstCompile]
    [UpdateInGroup(typeof(BattleSimGroup))]
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
            var hasObstacles = SystemAPI.TryGetSingleton<ObstacleSingleton>(out var obstacleSingleton);

            var portalQuery = SystemAPI.QueryBuilder().WithAll<PortalLink>().Build();
            var portals = portalQuery.ToComponentDataArray<PortalLink>(Allocator.Temp);

            var tornadoQuery = SystemAPI.QueryBuilder().WithAll<TornadoField>().Build();
            var tornadoFields = tornadoQuery.ToComponentDataArray<TornadoField>(Allocator.Temp);

            // aggro-targeting Unit 3 — snapshot guardian positions so aggroed enemies
            // can self-walk toward their anchor. Separate RO query avoids aliasing the
            // RW LocalTransform in the movement loop below.
            var aggroLookup = SystemAPI.GetComponentLookup<Aggroed>(isReadOnly: true);
            // enemy-ai-fsm Unit 2 — EnemyAiState(Combat) RO 소비. 이동/정지를 상태로 결정.
            var aiStateLookup = SystemAPI.GetComponentLookup<EnemyAiState>(isReadOnly: true);
            var behaviorLookup = SystemAPI.GetComponentLookup<EnemyBehavior>(isReadOnly: true);
            // enemy-ai-fsm Unit 7 — Pulse 진동: AttackState(Combat) RO 로 스윙 진행(hitDelayRemaining) 판정.
            var attackStateLookup = SystemAPI.GetComponentLookup<AttackState>(isReadOnly: true);
            var guardianPos = new NativeHashMap<Entity, float3>(16, Allocator.Temp);
            foreach (var (gTransform, gEntity) in
                     SystemAPI.Query<RefRO<LocalTransform>>().WithAll<AggroCapacity>().WithEntityAccess())
                guardianPos[gEntity] = gTransform.ValueRO.Position;

            // enemy-hunter-targeting unit 2 — 헌터(보스) 추격 대상 위치 스냅샷.
            // guardianPos 와 동일 패턴: 별도 RO 쿼리라 아래 RW LocalTransform 루프와
            // aliasing 안 함(ComponentLookup<LocalTransform> 은 RW 와 alias → 금지).
            var huntTargetLookup = SystemAPI.GetComponentLookup<HuntTarget>(isReadOnly: true);
            var defenderPos = new NativeHashMap<Entity, float3>(32, Allocator.Temp);
            // WithNone<DeadTag> — FSM 후보 쿼리(EnemyAiStateSystem)와 대칭. 죽은 방어유닛을
            // 추격 anchor 로 잡지 않는다(HuntTarget 은 FSM 이 이미 dead 를 거르지만 대칭 유지).
            foreach (var (dTransform, dEntity) in
                     SystemAPI.Query<RefRO<LocalTransform>>()
                              .WithAll<Wassup.Battle.Units.DefenderUnitTag>()
                              .WithNone<Wassup.Battle.Units.DeadTag>()
                              .WithEntityAccess())
                defenderPos[dEntity] = dTransform.ValueRO.Position;

            foreach (var (transform, follow, entity) in
                     SystemAPI.Query<RefRW<LocalTransform>, RefRO<PathFollowState>>()
                              .WithNone<PastGoalTag>()
                              .WithEntityAccess())
            {
                float3 current = transform.ValueRO.Position;

                // enemy-ai-fsm Unit 2 — 이동을 EnemyAiState 로 결정(상태는 EnemyAiStateSystem 이 Movement 전에 set).
                //  Standoff = 정지(가디언 사거리 도달, 공격은 AttackSystem). Chasing = 가디언 anchor 로 self-walk.
                //  aggro 의 본질은 이동목표 변경(goal→guardian)뿐 — guardian step 을 다른 이동과 같은 cell-trim 에
                //  통과시켜 walk 타일 위에 머물게 한다. 도달 여부 판정은 더 이상 여기서 안 하고 상태가 대신한다.
                AiState ai = aiStateLookup.HasComponent(entity) ? aiStateLookup[entity].value : AiState.Marching;

                if (ai == AiState.Standoff) continue; // 정지

                if (ai == AiState.Chasing)
                {
                    // enemy-hunter-targeting unit 2 — Chasing anchor 소스 분기:
                    // aggro 면 guardian(기존), 아니면 헌터 HuntTarget(신규). step/cell-trim
                    // 로직은 두 소스 공유 — anchor 만 다르다.
                    float3 anchor = default;
                    bool hasAnchor = false;
                    if (aggroLookup.HasComponent(entity)
                        && guardianPos.TryGetValue(aggroLookup[entity].guardian, out var gpos))
                    {
                        anchor = gpos; hasAnchor = true;
                    }
                    else if (huntTargetLookup.HasComponent(entity))
                    {
                        var ht = huntTargetLookup[entity].value;
                        if (ht != Entity.Null && defenderPos.TryGetValue(ht, out var hpos))
                        {
                            anchor = hpos; hasAnchor = true;
                        }
                    }

                    if (hasAnchor)
                    {
                        int2 chaseCell = GridMath.WorldToCell(current, field.tileSize, field.gridSize, origin: field.origin);
                        float chaseSpeedMul = modifierStatsLookup.HasComponent(entity)
                            ? modifierStatsLookup[entity].moveSpeedMul : 1f;
                        float step = follow.ValueRO.speed * chaseSpeedMul * dt;
                        // 직선 추격 + 벽 축분리 슬라이드(순수함수, EditMode 고정).
                        float3 moved = MovementChase.SlideStep(
                            current, anchor, step, chaseCell, in field, hasObstacles, in obstacleSingleton);
                        // softlock 가드(critic): 진행하면 Chasing 유지(continue). fully-boxed
                        // (moved==current, concave/양축 벽)면 continue 하지 않고 아래 flow-march 로
                        // 폴백 → 영구 freeze 대신 goal 로 전진(교전도 leak 도 못 하는 wave-stall 방지).
                        if (math.distancesq(moved, current) > 1e-8f)
                        {
                            transform.ValueRW.Position = moved;
                            continue; // 추격 진행 — flow/portal/goal 스킵
                        }
                        // else: fully-boxed → fall through to flow-march
                    }
                    else
                    {
                        continue; // anchor 없음(타겟 소멸) — 이 프레임 정지, 다음 틱 FSM 재선정
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

                // enemy-ai-fsm Unit 2/7 — Engaging 이동정책. Halt=정지, Advance=flow 전진,
                // Pulse=타격 진행중(hitDelayRemaining>0) 정지·아니면 전진(진동). 정지여도 공격은 AttackSystem.
                if (ai == AiState.Engaging)
                {
                    var engage = behaviorLookup.HasComponent(entity)
                        ? behaviorLookup[entity].engageMovement
                        : Wassup.Data.EngageMovement.Halt;
                    bool advance;
                    if (engage == Wassup.Data.EngageMovement.Advance)
                        advance = true;
                    else if (engage == Wassup.Data.EngageMovement.Pulse)
                        advance = !(attackStateLookup.HasComponent(entity)
                                    && attackStateLookup[entity].hitDelayRemaining > 0f);
                    else
                        advance = false; // Halt
                    if (!advance) continue; // 정지
                }

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
            defenderPos.Dispose();
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
