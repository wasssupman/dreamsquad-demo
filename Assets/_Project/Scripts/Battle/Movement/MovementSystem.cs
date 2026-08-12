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
            // continuous-agent-movement unit 1·3 — 벽 질의 프레임 뷰. 정적 마스크 + 동적
            // 장애물을 합쳐 조립하고, 아래 모든 충돌 해결이 이것만 본다.
            //
            // traversal-layers unit 5 — **유닛의 통행 층마다 다른 벽**이다. 예전엔 프레임당
            // 하나(Path 전용)였는데, 그러면 Ground 를 여는 유닛이 배치지에 서는 순간 자기
            // 칸이 벽으로 읽혀 영원히 clamp 된다(순찰병이 dir 을 받고도 안 움직였다).
            // 재조립은 `PatrolFieldSystem` 의 BFS 마스크와 같은 **한-칸 메모**다: 청크 순회라
            // 같은 층끼리 모여 있어 실제 재조립은 프레임당 층 종류 수(오늘 2)로 떨어진다.
            var navScratch = new NativeArray<byte>(math.max(1, field.CellCount), Allocator.Temp);
            byte navLayers = 0;   // 0 = 아직 안 만듦 (유효 층 값은 항상 0 이 아니다)
            NavGrid nav = default;
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
            // waypoint-routing unit 3 — 진행 인덱스의 유일 writer 는 Movement.
            var waypointLookup = SystemAPI.GetComponentLookup<WaypointFollow>(isReadOnly: false);
            // instinct-content unit 3 — 거점 목적지(Movement 소유). StructureDestinationSystem 이
            // 이 프레임 앞에서 갱신했고 여기서는 읽기만 한다.
            var structureDestLookup = SystemAPI.GetComponentLookup<StructureDestination>(isReadOnly: true);

            foreach (var (transform, follow, entity) in
                     SystemAPI.Query<RefRW<LocalTransform>, RefRW<PathFollowState>>()
                              .WithNone<PastGoalTag>()
                              .WithEntityAccess())
            {
                float3 current = transform.ValueRO.Position;

                // traversal-layers unit 5 — 이 유닛이 보는 벽. 층이 직전과 같으면 재사용한다.
                // 0 = 미주입(레거시·픽스처) → 계약대로 Path 로 읽어 현행을 재현한다.
                byte entityLayers = follow.ValueRO.traversalLayers;
                if (entityLayers == 0) entityLayers = TraversalSlots.DefaultMask;
                if (entityLayers != navLayers)
                {
                    nav = MovementCellTrim.BuildNavGrid(
                        in field, entityLayers, hasObstacles, in obstacleSingleton, navScratch);
                    navLayers = entityLayers;
                }

                // unit 13 — 기본값은 "정지". 자기주도 변위를 **실제로 적용하는 지점에서만**
                // 내린다(아래 2곳). 케이스를 열거하지 않으므로 새 continue 경로가 생겨도
                // 자동으로 정지에 편입된다 — 열거식이면 분기가 늘 때마다 조용히 샌다.
                follow.ValueRW.holdingGround = 1;

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
                                transform.ValueRW.Position = AgentCollision.Resolve(
                                    current, desiredChase, follow.ValueRO.radius, in nav);
                                follow.ValueRW.holdingGround = 0;   // unit 13 — 자기주도 이동함
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
                if (!hunting && !patrolling && field.IsGoalCell(cell))
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
                            transform.ValueRW.Position = AgentCollision.Resolve(current, desiredPull, follow.ValueRO.radius, in nav);
                        }
                        continue;
                    }
                }

                // 4. Flow field step — patrol 이면 PatrolStep, hunting 이면 defender field,
                //    아니면 goal field. "Marching = 전진, 목적지는 dir 소스가 결정" 계약에
                //    세 번째 소스로 합류한다(AiState 에 값을 추가하지 않는다).
                float2 dir;
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
                            transform.ValueRW.Position = AgentCollision.Resolve(current, desiredPull, follow.ValueRO.radius, in nav);
                        }
                        continue;
                    }
                }
                else
                {
                    // waypoint-routing unit 3 — 기존 방향/회복/평활화 파이프라인은 그대로 두고
                    // 읽는 flow 슬롯만 바꾼다. hunting 은 waypoint 보다 우선한다.
                    // waypoint-routing unit 7 — waypoint 를 끝낸 뒤의 goal 도 이 유닛의 통행층
                    // 슬롯이어야 한다. Primary(Path) 고정이면 Air/Ground 경로 가이드와 실이동이
                    // 마지막 구간에서 갈라진다.
                    int goalSlot = field.SlotFor(FlowFieldSingleton.GoalSentinel, entityLayers);
                    var routeFlow = field.FlowSlot(goalSlot);
                    var routeDist = field.DistSlot(goalSlot);
                    if (hunting)
                    {
                        routeFlow = huntField.flow;
                        routeDist = huntField.dist;
                    }
                    else if (waypointLookup.HasComponent(entity))
                    {
                        var progress = waypointLookup[entity];
                        int waypointCount = field.WaypointCountAt(progress.pathIndex);
                        if (progress.index < waypointCount)
                        {
                            int2 currentWaypoint = field.WaypointAt(progress.pathIndex, progress.index);
                            int currentSlot = field.SlotFor(currentWaypoint, entityLayers);
                            var currentWaypointDist = field.DistSlot(currentSlot);
                            bool reachable = currentWaypointDist[idx] != int.MaxValue;

                            WaypointProgress.Step(
                                cell, currentWaypoint, reachable,
                                progress.index, waypointCount,
                                out int nextIndex, out bool advanced, out bool done);

                            if (advanced)
                            {
                                progress.index = nextIndex;
                                waypointLookup[entity] = progress;
                            }

                            if (!done)
                            {
                                int2 destination = field.WaypointAt(progress.pathIndex, nextIndex);
                                int slot = field.SlotFor(destination, entityLayers);
                                routeFlow = field.FlowSlot(slot);
                                routeDist = field.DistSlot(slot);
                            }
                        }
                    }
                    else if (structureDestLookup.HasComponent(entity))
                    {
                        // instinct-content unit 3 — 거점 목적지. 저작 웨이포인트보다 **뒤**다:
                        // 웨이포인트는 맵이 «이 길로 가라» 고 정한 계약이고, 거점 선택은 그 안의
                        // 전술이다. 순서를 뒤집으면 나중에 저작이 조용히 무시된다.
                        int slot = field.SlotFor(structureDestLookup[entity].cell, entityLayers);
                        var destDist = field.DistSlot(slot);
                        // 그 통행 층으로 못 가는 거점(빈 슬롯 포함)이면 골로 되돌아간다.
                        if (destDist[idx] != int.MaxValue)
                        {
                            routeFlow = field.FlowSlot(slot);
                            routeDist = destDist;
                        }
                    }

                    dir = routeFlow[idx];
                    if (math.lengthsq(dir) < 1e-6f)
                    {
                        // Zero-flow cell: impulse may have pushed entity into an unreachable cell.
                        // Try 4 cardinal neighbors; move toward the one with the smallest finite dist.
                        // hunting 이면 recovery 도 defender field 의 dist 기준(같은 그리드).
                        // 계산은 FlowRecovery.RecoveryDir 순수함수 (ecs-review M3, EditMode 테스트).
                        float2 recovDir = FlowRecovery.RecoveryDir(cell, routeDist, field.gridSize);
                        if (math.lengthsq(recovDir) < 1e-6f)
                        {
                            // truly isolated cell — 자기주도 이동은 없지만 외력(pull)은 적용 (unit 3).
                            if (hasPull)
                            {
                                float3 desiredPull = MovementCellTrim.ClampDisplacement(current, current + pullDisplacement, field.tileSize);
                                transform.ValueRW.Position = AgentCollision.Resolve(current, desiredPull, follow.ValueRO.radius, in nav);
                            }
                            continue;
                        }
                        dir = recovDir;
                    }
                    else
                    {
                        // continuous-agent-movement unit 7·10 — 평활화(string pulling).
                        // 필드는 8방향으로 양자화돼 있어 기울기가 45°가 아니면 대각/직축이
                        // 꺾여 붙는다. 전방 가시점(막히면 코너 꼭짓점)으로 직행해 방향을
                        // 연속으로 만든다. 필드를 **대체하지 않는다** — 후보를 필드가
                        // 만들므로 오목 지형에서도 갇히지 않는다.
                        // 목표점 선택 규칙은 예고 라인과 공유(TryStepTarget) — 갈라지면
                        // "라인 ≠ 이동선" 부류가 재발한다.
                        // 사냥 분기는 defender field 를 따르므로 그쪽 flow 로 후보를 만든다.
                        if (PathSmoothing.TryStepTarget(
                                current, in nav, in routeFlow, follow.ValueRO.radius,
                                PathSmoothing.DefaultLookahead, out float3 aim))
                        {
                            float2 toAim = new float2(aim.x - current.x, aim.z - current.z);
                            if (math.lengthsq(toAim) > 1e-6f) dir = toAim;
                        }
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

                // unit 13 — 자기주도 변위가 실제로 있을 때만 "이동 중". 외력(impulse/pull)만
                // 있는 프레임은 정지로 남는다 — 밀려나는 유닛은 자리를 지키는 쪽이 맞다.
                if (math.lengthsq(flowStep) > 1e-12f) follow.ValueRW.holdingGround = 0;

                // [은퇴] enemy-tile-movement-integrity unit 1 의 LateralRecenter 를 여기서 걷어냈다.
                //
                // 그 장치의 목적은 주석 그대로 **"코너 엣지-허깅 복원"** — 진행방향 수직으로
                // 현재 셀의 중심선 쪽으로 당기는 것이다. 4-이웃 레인 모델에선 맞았지만,
                // continuous-agent-movement 는 **코너에 붙어 도는 것이 목표**라 정면으로 충돌한다.
                //
                // 실측(2026-08-08, 20x14 열린 격자 · 기울기 17:8): 대각 주행 중 현재 셀이 계속
                // 바뀌면서 당김 방향이 뒤집혀 톱니가 난다 — 좌우 꺾임 19회 · 총회전 624° ·
                // 단일 프레임 최대 43.9° 급회전. 제거하면 0회 / 32°, lookahead 를 늘리면 0°.
                // (사용자 제보 "대각선 직행 시 지그재그".)
                //
                // 스폰 측면 분산은 이제 recenter 가 되돌리지 않는다 — 유지되는 편이 낫고,
                // 뭉침은 AgentSeparationSystem(unit 8)이 담당한다.

                // aggro-tile-chase unit 3 — tornado pull 가산 (flow/impulse/recenter 와 합성).
                desired += pullDisplacement;

                // aggro-tile-chase unit 2 — 프레임 변위 상한(터널링 차단), trim 전제 보존.
                desired = MovementCellTrim.ClampDisplacement(current, desired, field.tileSize);
                // Cell-trim (option B): keep impulse/recenter from pushing into wall/obstacle cells.
                desired = AgentCollision.Resolve(current, desired, follow.ValueRO.radius, in nav);

                transform.ValueRW.Position = desired;
            }

            portals.Dispose();
            tornadoFields.Dispose();
            navScratch.Dispose();
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
