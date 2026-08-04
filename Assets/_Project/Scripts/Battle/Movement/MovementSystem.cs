using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Wassup.Battle.Combat;
using Wassup.Battle.Effects;
using Wassup.Battle.Units;

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
            // boss-defender-field unit 2 — 방어유닛-지향 필드(Effects 소유, RO). 부재 시
            // (테스트/티어다운) hunting 이 항상 false 로 떨어져 전원 기존 goal 경로.
            bool hasHuntField = SystemAPI.TryGetSingleton<DefenderFieldSingleton>(out var huntField);
            var bossLookup = SystemAPI.GetComponentLookup<BossTag>(isReadOnly: true);
            var ccLookup = SystemAPI.GetBufferLookup<CcEffect>(isReadOnly: true);
            // leap-flight-state unit 0 — Combat 소유 태그를 RO 로 읽는다(AttackState·EnemyAiState 선례).
            var leapFlightLookup = SystemAPI.GetComponentLookup<LeapFlight>(isReadOnly: true);
            var modifierStatsLookup = SystemAPI.GetComponentLookup<ModifierStats>(isReadOnly: true);
            var hasObstacles = SystemAPI.TryGetSingleton<ObstacleSingleton>(out var obstacleSingleton);

            var portalQuery = SystemAPI.QueryBuilder().WithAll<PortalLink>().Build();
            var portals = portalQuery.ToComponentDataArray<PortalLink>(Allocator.Temp);

            var tornadoQuery = SystemAPI.QueryBuilder().WithAll<TornadoField>().Build();
            var tornadoFields = tornadoQuery.ToComponentDataArray<TornadoField>(Allocator.Temp);

            // aggro-tile-chase unit 2 — Chasing 은 per-enemy chase field(Effects 소유) 하강.
            // 직선 greedy+가디언 위치 추적을 폐기 — 목적지 타일까지 경로가 보장된다.
            var chaseLookup = SystemAPI.GetBufferLookup<AggroChaseCell>(isReadOnly: true);
            // enemy-ai-fsm Unit 2 — EnemyAiState(Combat) RO 소비. 이동/정지를 상태로 결정.
            var aiStateLookup = SystemAPI.GetComponentLookup<EnemyAiState>(isReadOnly: true);
            var behaviorLookup = SystemAPI.GetComponentLookup<EnemyBehavior>(isReadOnly: true);
            // enemy-ai-fsm Unit 7 — Pulse 진동: AttackState(Combat) RO 로 스윙 진행(hitDelayRemaining) 판정.
            var attackStateLookup = SystemAPI.GetComponentLookup<AttackState>(isReadOnly: true);
            // summon-patrol-defender unit 2 — 거점 순찰 아군의 이동 방향(Effects 소유, RO).
            // PatrolFieldSystem 이 Movement 전에 굽는다. 보유 = patrol 아키타입 판별.
            var patrolStepLookup = SystemAPI.GetComponentLookup<PatrolStep>(isReadOnly: true);

            // goal-stability unit 2 — 살아있는 골 셀 집합(맵당 ≤4, 프레임당 재구축 — 동기화 없음).
            // 골 엔티티(Units 소유) RO 쿼리. 존재 = 공성: 그 셀에서 유출(PastGoalTag)을 봉인한다.
            // 붕괴(DeadTag/엔티티 파괴) 즉시 집합에서 빠져 다음 프레임부터 현행 유출로 자동 복귀.
            var aliveGoalCells = new NativeList<int2>(4, Allocator.Temp);
            foreach (var goalPoint in SystemAPI.Query<RefRO<GoalPoint>>().WithNone<DeadTag>())
                aliveGoalCells.Add(goalPoint.ValueRO.cell);

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

                // summon-patrol-defender unit 2 — 거점 순찰 아군. 목적지가 goal/가디언이 아니라
                // 자기 거점 박스라, 아래 goal 판정과 flow-step 을 둘 다 갈아탄다.
                bool patrolling = patrolStepLookup.HasComponent(entity);

                // combat-action-lock — Sleep/Stun 은 자기주도 이동만 정지(외력=impulse/tornado/portal 유지).
                // AiState 직후 조기 계산: Chasing/goal/tornado 분기가 flow-step 전에 continue 하므로.
                // leap-flight-state unit 0 — 도약 비행 중도 같은 규약으로 합류한다(자기주도 이동만
                // 정지, 외력 유지). 같은 변수에 접는 이유: 소비 지점이 전부 동일하고, 출처만 다르다
                // (CC = 남이 건 것 / LeapFlight = 본체 자신의 상태).
                bool locked = (ccLookup.HasBuffer(entity) && CcActionLock.IsLocked(ccLookup[entity]))
                              || leapFlightLookup.HasComponent(entity);

                if (ai == AiState.Standoff) continue; // 정지

                if (ai == AiState.Chasing)
                {
                    if (locked) continue; // 잠/스턴: 제자리(자기주도 self-walk 정지)
                    // aggro-tile-chase unit 2 — chase field(dist) 하강. dir zero = 목적지
                    // (사거리 내 walk 셀, dist 0) 도착 또는 고립 — 정지. 도착 셀은 정의상
                    // 발사 조건 충족 → 다음 틱 EnemyAiStateSystem 이 Standoff 전이.
                    if (chaseLookup.HasBuffer(entity))
                    {
                        var chase = chaseLookup[entity];
                        if (chase.Length == field.gridSize.x * field.gridSize.y)
                        {
                            int2 chaseCell = GridMath.WorldToCell(current, field.tileSize, field.gridSize, origin: field.origin);
                            float2 chaseDir = FlowRecovery.RecoveryDir(
                                chaseCell, chase.Reinterpret<int>().AsNativeArray(), field.gridSize);
                            if (math.lengthsq(chaseDir) > 1e-6f)
                            {
                                float aggroSpeedMul = modifierStatsLookup.HasComponent(entity)
                                    ? modifierStatsLookup[entity].moveSpeedMul : 1f;
                                float3 desiredChase = current + new float3(chaseDir.x, 0f, chaseDir.y)
                                    * (follow.ValueRO.speed * aggroSpeedMul * dt);
                                desiredChase = MovementCellTrim.ClampDisplacement(current, desiredChase, field.tileSize);
                                transform.ValueRW.Position = MovementCellTrim.Apply(
                                    desiredChase, chaseCell, in field, hasObstacles, in obstacleSingleton);
                            }
                        }
                    }
                    continue; // chasing: skip flow/portal/tornado/goal (필드 없으면 정지 — 합성 테스트 월드)
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
                int idx = GridMath.CellIndex(cell, field.gridSize);

                // boss-defender-field unit 2 — 보스 사냥 판정. hunt-dist 유한 = 도달 가능한
                // 방어유닛 존재 → goal flow 대신 defender field 를 따른다. 방어유닛 0 이면
                // DefenderFieldSystem 이 전 셀 MaxValue 로 리셋 → 자동으로 기존 마칭(계약 5).
                bool hunting = hasHuntField && huntField.IsCreated
                    && bossLookup.HasComponent(entity)
                    && huntField.dist[idx] != int.MaxValue;

                // 사냥 중엔 goal 셀을 지나쳐도 누수 안 함(leak-proof) — 방어유닛 전멸 후에만 도달 처리.
                // multi-goal-map — 어느 골이든 도달하면 누수(IsGoalCell = goals 멤버십/goalCell 폴백).
                // summon-patrol-defender unit 2 — 거점 박스 안에 goal 셀이 들어올 수 있다(맵은 매판
                // 랜덤, 배치는 플레이어가 한다). 게이트가 없으면 순찰병에 PastGoalTag 가 붙어
                // ⑴ 이 루프가 WithNone<PastGoalTag> 라 영구 동결, ⑵ UnitLifecycle 의 PastGoal 파괴
                // 루프는 AttackUnitTag 를 요구해 파괴도 안 됨, ⑶ 살아 있으니 SummonerState.current 가
                // 계속 유효해 소환사가 남은 판 내내 재소환하지 못한다. 보스 leak-proof 와 같은 형태.
                // goal-stability unit 2 — 게이트의 세 번째 케이스: 살아있는(안정도>0) 골 셀에서는
                // 유출 대신 공성(적은 남아서 골을 공격, AttackSystem 이 최후순위로 채택).
                if (!hunting && !patrolling && field.IsGoalCell(cell)
                    && !IsAliveGoalCell(cell, in aliveGoalCells))
                {
                    ecb.AddComponent<PastGoalTag>(entity);
                    continue;
                }

                // 3. Tornado pull — aggro-tile-chase unit 3 (계약 7): 이동을 대체하지 않는
                //    **후처리 가산 변위**. trim 을 거치므로 벽/장애물에서 막힌다(C4 해소).
                float3 pullDisplacement = float3.zero;
                for (int t = 0; t < tornadoFields.Length; t++)
                {
                    var fieldT = tornadoFields[t];
                    int2 centerCell = GridMath.WorldToCell(fieldT.centerWorld, field.tileSize, field.gridSize, origin: field.origin);
                    int tileDist = math.max(math.abs(cell.x - centerCell.x), math.abs(cell.y - centerCell.y));
                    if (tileDist > fieldT.tileRange) continue;
                    float3 toCenter = fieldT.centerWorld - current;
                    toCenter.y = 0f;
                    float centerDist = math.length(toCenter);
                    float pullStep = fieldT.pullSpeed * dt;
                    pullDisplacement = (centerDist <= pullStep || centerDist < 1e-4f)
                        ? toCenter                                   // 중심까지 남은 변위 전부
                        : math.normalize(toCenter) * pullStep;
                    break;
                }
                bool hasPull = math.lengthsq(pullDisplacement) > 1e-8f;

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
                    if (locked || !advance)
                    {
                        // 정지(halt/잠금) 중에도 외력(pull)은 적용 — 기존 "Halt 도 당겨짐" 거동 보존.
                        if (hasPull)
                        {
                            float3 desiredPull = MovementCellTrim.ClampDisplacement(current, current + pullDisplacement, field.tileSize);
                            transform.ValueRW.Position = MovementCellTrim.Apply(desiredPull, cell, in field, hasObstacles, in obstacleSingleton);
                        }
                        continue;
                    }
                }

                // 4. Flow field step — patrol 이면 PatrolStep, hunting 이면 defender field,
                //    아니면 goal field. "Marching = 전진, 목적지는 dir 소스가 결정" 계약에
                //    세 번째 소스로 합류한다(AiState 에 값을 추가하지 않는다).
                float2 dir;
                bool zeroFlowRecovery = false;
                if (patrolling)
                {
                    dir = patrolStepLookup[entity].dir;
                    if (math.lengthsq(dir) < 1e-6f)
                    {
                        // 거점 도착·사격 위치 도달·고립 = 정지. goal field 기반 zero-flow recovery 로
                        // 떨어뜨리지 않는다 — 그 dist 는 순찰병의 목적지와 무관하다.
                        if (hasPull)
                        {
                            float3 desiredPull = MovementCellTrim.ClampDisplacement(current, current + pullDisplacement, field.tileSize);
                            transform.ValueRW.Position = MovementCellTrim.Apply(desiredPull, cell, in field, hasObstacles, in obstacleSingleton);
                        }
                        continue;
                    }
                }
                else
                {
                    dir = hunting ? huntField.flow[idx] : field.flow[idx];
                    if (math.lengthsq(dir) < 1e-6f)
                    {
                        zeroFlowRecovery = true;
                        // Zero-flow cell: impulse may have pushed entity into an unreachable cell.
                        // Try 4 cardinal neighbors; move toward the one with the smallest finite dist.
                        // hunting 이면 recovery 도 defender field 의 dist 기준(같은 그리드).
                        // 계산은 FlowRecovery.RecoveryDir 순수함수 (ecs-review M3, EditMode 테스트).
                        float2 recovDir = FlowRecovery.RecoveryDir(cell, hunting ? huntField.dist : field.dist, field.gridSize);
                        if (math.lengthsq(recovDir) < 1e-6f)
                        {
                            // truly isolated cell — 자기주도 이동은 없지만 외력(pull)은 적용 (unit 3).
                            if (hasPull)
                            {
                                float3 desiredPull = MovementCellTrim.ClampDisplacement(current, current + pullDisplacement, field.tileSize);
                                transform.ValueRW.Position = MovementCellTrim.Apply(desiredPull, cell, in field, hasObstacles, in obstacleSingleton);
                            }
                            continue;
                        }
                        dir = recovDir;
                    }
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
                // combat-action-lock — 잠/스턴: 자기주도 flow-step 0, 넉백(impulse)은 유지.
                float3 flowStep = locked ? float3.zero : new float3(stepDir.x, 0, stepDir.y) * follow.ValueRO.speed * speedMul * dt;
                float3 desired = current + flowStep + impulseDisplacement;

                // enemy-tile-movement-integrity unit 1 — 코너 엣지-허깅 측면 복원(target=0 + dead-band).
                // zero-flow recovery 분기는 스킵(이미 교정 이동 중). 임펄스 측면성분은 이 프레임 보존
                // (recenter 는 current 기준 standing 오프셋만 당김 → 넉백은 이후 프레임에 점진 복귀).
                if (!zeroFlowRecovery && !locked)
                    desired += LateralRecenter.Compute(current, cell, stepDir,
                        follow.ValueRO.speed * speedMul, dt, field.tileSize, field.origin);

                // aggro-tile-chase unit 3 — tornado pull 가산 (flow/impulse/recenter 와 합성).
                desired += pullDisplacement;

                // aggro-tile-chase unit 2 — 프레임 변위 상한(터널링 차단), trim 전제 보존.
                desired = MovementCellTrim.ClampDisplacement(current, desired, field.tileSize);
                // Cell-trim (option B): keep impulse/recenter from pushing into wall/obstacle cells.
                desired = MovementCellTrim.Apply(desired, cell, in field, hasObstacles, in obstacleSingleton);

                transform.ValueRW.Position = desired;
            }

            portals.Dispose();
            tornadoFields.Dispose();
            aliveGoalCells.Dispose();
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        // goal-stability unit 2 — 공성 게이트 술어. 맵당 골 ≤4 라 선형 스캔.
        static bool IsAliveGoalCell(int2 cell, in NativeList<int2> aliveGoalCells)
        {
            for (int i = 0; i < aliveGoalCells.Length; i++)
                if (math.all(aliveGoalCells[i] == cell)) return true;
            return false;
        }
    }
}
