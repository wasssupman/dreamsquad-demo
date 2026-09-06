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
        // distance-based-range unit 4c — 추격 접근 보정용(RO). **명시 필드로 둔다** —
        // `SystemAPI.GetComponentLookup` 로컬 형태로 새 lookup 을 추가하면 Burst 에서
        // NRE 로 죽는다(이 프로젝트에서 3회 재발).
        ComponentLookup<Aggroed> _aggroedLookup;
        ComponentLookup<LocalTransform> _guardianTransformLookup;
        // unit 18 — 회오리 당김의 피해자 몸. 로컬 형태 금지 규칙(위 주석) 그대로 필드.
        ComponentLookup<Wassup.Battle.Units.HitRadius> _pullBodyRadiusLookup;
        // enemy-detection-range unit 3 — 사냥 게이트의 새 소스(Combat 소유, RO). 같은 이유로 필드.
        ComponentLookup<Wassup.Battle.Combat.DetectedTarget> _detectedLookup;
        ComponentLookup<Wassup.Battle.Combat.DetectionRange> _detectionRangeLookup;
        // unit 8 — 대상 지향 추격판(유한 반경 감지 전용). `huntField` 와 같은 모양이라 drop-in.
        BufferLookup<Wassup.Battle.Combat.DetectionChaseDist> _chaseDistLookup;
        BufferLookup<Wassup.Battle.Combat.DetectionChaseFlow> _chaseFlowLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PathFollowState>();
            state.RequireForUpdate<FlowFieldSingleton>();
            _aggroedLookup = state.GetComponentLookup<Aggroed>(isReadOnly: true);
            _guardianTransformLookup = state.GetComponentLookup<LocalTransform>(isReadOnly: true);
            _pullBodyRadiusLookup = state.GetComponentLookup<Wassup.Battle.Units.HitRadius>(isReadOnly: true);
            _detectedLookup = state.GetComponentLookup<Wassup.Battle.Combat.DetectedTarget>(isReadOnly: true);
            _detectionRangeLookup = state.GetComponentLookup<Wassup.Battle.Combat.DetectionRange>(isReadOnly: true);
            _chaseDistLookup = state.GetBufferLookup<Wassup.Battle.Combat.DetectionChaseDist>(isReadOnly: true);
            _chaseFlowLookup = state.GetBufferLookup<Wassup.Battle.Combat.DetectionChaseFlow>(isReadOnly: true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            float dt = SystemAPI.Time.DeltaTime;
            _aggroedLookup.Update(ref state);
            _guardianTransformLookup.Update(ref state);

            var field = SystemAPI.GetSingleton<FlowFieldSingleton>();
            // boss-defender-field unit 2 — 방어유닛-지향 필드(Effects 소유, RO). 부재 시
            // (테스트/티어다운) hunting 이 항상 false 로 떨어져 전원 기존 goal 경로.
            bool hasHuntField = SystemAPI.TryGetSingleton<DefenderFieldSingleton>(out var huntField);
            // bonus-wave-pull unit 0 — 사냥 게이트가 BossTag 에서 DefenderHunterTag 로.
            // 보스는 tier == Boss 로 같은 태그를 계속 받아 무회귀다. BossTag 는 이 파일에서
            // 더는 읽지 않는다 — 남은 보스 특권(넉업/CC/어그로 면역)은 다른 시스템 소유.
            var hunterLookup = SystemAPI.GetComponentLookup<DefenderHunterTag>(isReadOnly: true);
            var ccLookup = SystemAPI.GetBufferLookup<CcEffect>(isReadOnly: true);
            // leap-flight-state unit 0 — Combat 소유 태그를 RO 로 읽는다(AttackState·EnemyAiState 선례).
            var leapFlightLookup = SystemAPI.GetComponentLookup<LeapFlight>(isReadOnly: true);
            var modifierStatsLookup = SystemAPI.GetComponentLookup<ModifierStats>(isReadOnly: true);
            _pullBodyRadiusLookup.Update(ref state);   // unit 18 — 회오리 당김 피해자 몸
            _detectedLookup.Update(ref state);
            _detectionRangeLookup.Update(ref state);
            _chaseDistLookup.Update(ref state);
            _chaseFlowLookup.Update(ref state);
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
            // distance-based-range unit 4c — 사냥 레인 접근 보정의 **목표 좌표원**.
            // 사냥에는 `Aggroed` 같은 링크가 없어 최근접 방어유닛을 쓴다.
            // ⚠ **소스를 만든 것과 같은 술어여야 한다**(`DefenderFieldSystem` 의 스냅샷 조건:
            // `Faction.DefenderUnit` + `Health` + `WithNone<PendingDeployment, DeadTag>`).
            // 다르면 「소스는 섰는데 다가갈 대상이 없다」가 생기고, 그게 네 번째 자다.
            //
            // ⚠ 그 술어는 **사격 가능성을 안 본다** — 정지 근거인 `hasFireTarget` 은 `targetMask`·
            // 통행층(`PlacementLayers.CanTarget`)·`EnemyTargetFilter.classMask` 를 더 지난다.
            // 그래서 최근접이 **못 때리는** 방어유닛이면 그쪽으로 다가가 눌러붙을 수 있다.
            // 소스 수집도 같은 필터를 안 걸어 원래 그 칸으로 걸어가 얼었으므로 **선재 결함의
            // 상속**이지 신규가 아니다. 근본은 `DefenderFieldSystem` 과 함께 맞추는 것 — 후속 후보.
            // ⚠ `DefenderFieldSystem:70` 의 「FSM 후보 풀과 동일 조건」은 **stale 이다** —
            // `EnemyAiStateSystem:59` 는 `.WithNone<CoreShielded>()` 를 걸고 저쪽은 안 건다.
            // 오늘 무해(코어는 `Faction.DefenderUnit` 이 아니라 faction 필터에서 먼저 걸린다).
            var huntTargets = new NativeList<float3>(16, Allocator.Temp);
            // 헌터가 없는 프레임엔 스캔 자체를 건너뛴다 — `DefenderFieldSystem:61` 의
            // `hunterQuery.IsEmpty` 조기 반환과 대칭.
            if (hasHuntField
                && !SystemAPI.QueryBuilder().WithAll<Wassup.Battle.Combat.DefenderHunterTag>().Build().IsEmpty)
                foreach (var (huntFaction, huntTf) in
                         SystemAPI.Query<RefRO<FactionTag>, RefRO<LocalTransform>>()
                                  .WithAll<Health>().WithNone<PendingDeployment, DeadTag>())
                {
                    if (((int)huntFaction.ValueRO.value & (int)Faction.DefenderUnit) == 0) continue;
                    huntTargets.Add(huntTf.ValueRO.Position);
                }

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

                // defender-knockback-on-impact unit 2 — 넉백(외력)을 **분기 앞에서** 합성한다.
                // 아래 조기 이탈 경로들이 예전 소비 지점(flow-step 근처)에 닿기 전에 continue
                // 해서 넉백이 통째로 증발했다 — Halt 적 18종 + 도발된 전 적. 못 쓴 impulse 는
                // `CcDecaySystem`(UpdateAfter)이 소비 여부와 무관하게 만료시켜 조용히 사라진다.
                //
                // ⚠ **당김(pull)은 여기로 못 올린다.** 당김은 `cell` 에 의존하고 그 값은 포탈
                // 텔레포트 이후여야 정확한데, Standoff/Chasing 은 포탈 **전에** 이탈한다.
                // 그래서 넉백만 올리고 당김은 제자리에 둔다(추격에 당김을 주는 것은 후속 후보).
                // ⚠ `speedMul` 도 같이 올리지 않는다 — flowStep 전용이라 외력과 무관하다.
                float3 impulseDisplacement = float3.zero;
                if (ccLookup.HasBuffer(entity))
                {
                    var ccBuf = ccLookup[entity];
                    for (int i = 0; i < ccBuf.Length; i++)
                        if (ccBuf[i].kind == CcKind.Impulse)
                            impulseDisplacement += ccBuf[i].vector * dt;
                }
                bool hasImpulse = math.lengthsq(impulseDisplacement) > 1e-8f;

                if (ai == AiState.Standoff)
                {
                    // 정지 — 자기주도 이동 0. 외력은 그대로 받는다(self=0 ≠ 계산 건너뜀).
                    if (hasImpulse)
                        transform.ValueRW.Position = ComposeMove(
                            current, float3.zero, impulseDisplacement,
                            field.tileSize, follow.ValueRO.radius, in nav);
                    continue;
                }

                if (ai == AiState.Chasing)
                {
                    if (locked)
                    {
                        // 잠/스턴: 자기주도 self-walk 만 정지. 외력 유지는 위 :106 계약 그대로.
                        if (hasImpulse)
                            transform.ValueRW.Position = ComposeMove(
                                current, float3.zero, impulseDisplacement,
                                field.tileSize, follow.ValueRO.radius, in nav);
                        continue;
                    }
                    // aggro-tile-chase unit 2 — chase field(dist) 하강. dir zero = 목적지
                    // (사거리 내 walk 셀, dist 0) 도착 또는 고립 — 정지.
                    // ⚠ **「도착 셀은 정의상 발사 조건 충족」은 거짓이다.** 그 «정의상» 은 발사
                    // 판정이 **셀 기준**일 때만 성립했고, unit 4a 가 그걸 월드 원으로 바꿨다.
                    // 소스는 여전히 셀 디스크(`FlowFieldBuilder.CollectDefenderSources`, 체비셰프)라
                    // **원이 정사각형의 모서리를 잘라낸 만큼** 「도착했는데 사거리 밖」인 칸이 남는다.
                    //
                    // ⚠ 이 주석의 옛 판(“**연속 이동 가디언**이 생기는 순간 성립 · 오늘은 저작 0종이라
                    // 도달 불가”)은 **틀렸다.** 어긋남을 만드는 것은 가디언의 이동이 아니라 **적 자신의
                    // 칸 안 위치**다 — 적은 칸에 들어서는 순간 dist 0 을 읽고 멈추므로 늘 그 칸의
                    // **바깥 모서리**에 선다. 타일 고정 가디언 + 사거리 1 로 실측 2.05칸(도달 1.5칸).
                    // 아래 `arrivedAtFiringCell` 보정이 그 구간을 닫는다.
                    // 순찰 이동에는 그 보정이 있고(`PatrolAreaMath.CloseInDir`), **추격 레인에는
                    // unit 4c 가 아래에 넣었다**(`arrivedAtFiringCell` 분기).
                    bool chaseMoved = false;
                    bool arrivedAtFiringCell = false;
                    var firingDist = default(NativeArray<int>);   // 보정이 「소스 밖으로 나가지 않기」를 검사할 때 쓴다
                    if (chaseLookup.HasBuffer(entity))
                    {
                        var chase = chaseLookup[entity];
                        if (chase.Length == field.gridSize.x * field.gridSize.y)
                        {
                            var chaseDist = chase.Reinterpret<int>().AsNativeArray();
                            firingDist = chaseDist;
                            int2 chaseCell = GridMath.WorldToCell(current, field.tileSize, field.gridSize, origin: field.origin);
                            float2 chaseDir = FlowRecovery.RecoveryDir(chaseCell, chaseDist, field.gridSize);
                            // dir zero 는 두 가지다: **사격 칸 도착**(dist 0)과 **고립**(더 나은 이웃 없음).
                            // 보정 대상은 앞의 것 하나뿐이다 — 고립은 unit 4a 이전에도 멈췄다.
                            if (math.lengthsq(chaseDir) <= 1e-6f)
                                arrivedAtFiringCell =
                                    chaseDist[GridMath.CellIndex(chaseCell, field.gridSize)] == 0;
                            if (math.lengthsq(chaseDir) > 1e-6f)
                            {
                                float aggroSpeedMul = modifierStatsLookup.HasComponent(entity)
                                    ? modifierStatsLookup[entity].moveSpeedMul : 1f;
                                float3 chaseStep = new float3(chaseDir.x, 0f, chaseDir.y)
                                    * (follow.ValueRO.speed * aggroSpeedMul * dt);
                                transform.ValueRW.Position = ComposeMove(
                                    current, chaseStep, impulseDisplacement,
                                    field.tileSize, follow.ValueRO.radius, in nav);
                                chaseMoved = true;
                                follow.ValueRW.holdingGround = 0;   // unit 13 — 자기주도 이동함
                                // defender-knockback-on-impact unit 0 — 진행 방향 기록.
                                // chaseDir 은 위 게이트에서 이미 길이 > 1e-6 이 보장된다.
                                follow.ValueRW.lastMoveDir = math.normalize(chaseDir);
                            }
                        }
                    }
                    // distance-based-range unit 4c — 「도착했는데 못 쏜다」 보정.
                    //
                    // 추격 필드의 소스는 **셀 디스크**(`FlowFieldBuilder.CollectDefenderSources`,
                    // 체비셰프)인데 발사 판정은 unit 4a 이후 **월드 원**이다. 원은 정사각형의
                    // 모서리를 잘라내므로 «필드는 도착이라 하고 사거리는 밖이라 하는» 칸이 생긴다.
                    // 그 칸에서는 dist 0 이라 기울기가 없어 자기 이동도 0 — **영구 동결**이다.
                    // (사거리 1 기준 실측: 대각 소스 칸 진입 시 실거리 2.05칸 vs 도달 1.5칸.
                    //  구 체비셰프 판정은 1.45 ≤ 1.5 로 통과했었다 — 이건 unit 4a 의 회귀다.)
                    //
                    // ⚠ **여기서 사거리를 다시 판정하지 않는다.** `ai == Chasing` 자체가
                    // `EnemyAiStateSystem` 이 정본 술어(`AttackReach.InReach`, 대상 몸 포함)로
                    // 방금 계산한 「어그로됐고 사거리 밖」이고, 그 시스템은 `[UpdateBefore]` 라
                    // 값이 신선하다. 자를 하나 더 만드는 순간 그게 다음 교착이다 — 이 결함의
                    // 원인이 정확히 「한 루프 안에 자가 셋」이었다.
                    //
                    // ⚠ 가디언이 움직여 필드가 stale 해지면 적은 **옛 디스크 가장자리에서
                    // 정지한다**(양축 이탈 거부). 근본 수정(셀 변화 시 재굽기)은 여기 없다 —
                    // 오늘 이동 가디언 저작이 0종이라 도달 불가지만, 그 사실에 기대는 주석을
                    // 다시 쓰지 않기 위해 무엇이 안 고쳐졌는지를 적어 둔다.
                    if (!chaseMoved && arrivedAtFiringCell && _aggroedLookup.HasComponent(entity))
                    {
                        var guardian = _aggroedLookup[entity].guardian;
                        if (guardian != Entity.Null && _guardianTransformLookup.HasComponent(guardian))
                        {
                            float closeSpeedMul = modifierStatsLookup.HasComponent(entity)
                                ? modifierStatsLookup[entity].moveSpeedMul : 1f;
                            float2 taken = TryCloseIn(
                                current, _guardianTransformLookup[guardian].Position,
                                follow.ValueRO.speed * closeSpeedMul * dt,
                                in firingDist, field.gridSize, field.tileSize, field.origin,
                                follow.ValueRO.radius, in nav, impulseDisplacement, out float3 closeNext);
                            if (math.lengthsq(taken) > 1e-6f)
                            {
                                transform.ValueRW.Position = closeNext;
                                chaseMoved = true;
                                follow.ValueRW.holdingGround = 0;
                                follow.ValueRW.lastMoveDir = math.normalize(taken);
                            }
                        }
                    }
                    // 추격 필드가 없거나 목적지 도착(dir 0)이라 자기 이동이 0이었어도
                    // 외력은 받는다. 위 self 경로가 이미 합성에 넣었으므로 여기선 안 움직인
                    // 경우만 처리한다(두 번 적용 금지).
                    if (!chaseMoved && hasImpulse)
                        transform.ValueRW.Position = ComposeMove(
                            current, float3.zero, impulseDisplacement,
                            field.tileSize, follow.ValueRO.radius, in nav);
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
                // enemy-detection-range unit 3 — 게이트가 「태그를 가졌나」에서 **「이번 프레임 감지가
                // 성립했나」**로 바뀌었다. 무제한 감지(보스·보너스)는 `DetectionSystem` 이 사실상 늘
                // `hunting = 1` 을 주므로 그쪽 거동은 그대로다.
                //
                // ⚠ `hunterLookup` 은 여기서 소비처가 0 이 됐지만 **지우지 않는다.** 이유는 오직
                // 하나다: **이 `OnUpdate` 안의 `GetComponentLookup` 호출을 지우면 Burst 가 조용히
                // 깨져 NRE 가 난다** — 이 프로젝트에서 네 번 재발했다(memory: burst-lookup-removal-nre).
                // 「다른 시스템이 그 태그를 쓰니까」는 **근거가 아니다**(리뷰 L4) — 남의 쿼리는
                // 이 파일의 lookup 호출과 무관하고, 그렇게 적으면 다음 사람이 「저쪽이 안 쓰게 되면
                // 지워도 되겠네」로 잘못 배운다.
                //
                // enemy-detection-range unit 8 — **사냥 레인이 둘로 갈렸다.** 규칙은 하나지만
                // 「그 적에게 갈 수 있나」를 답하는 필드가 감지 종류마다 다르다:
                //
                //   - **무제한**(보스·보너스) → 공용 사냥판(`huntField`). 그쪽의 진짜 질문이
                //     「**아무** 방어유닛이나」라서 공용 필드가 **정확한 답**이다. 거동 무변.
                //   - **유한 반경** → 대상 지향 추격판(`DetectionChaseDist/Flow`). 감지한 «그»
                //     방어유닛까지, «내» 통행 층으로 구운 것이다.
                //
                // ⚠ 유한 감지가 공용 사냥판을 타던 시절의 결함 둘을 여기서 닫는다: ⑴ 도착지가
                // 감지 대상과 **실측 5.0%** 갈렸고 ⑵ 그 필드가 지상 마스크로만 구워져 **비행이
                // 벽 위에서 조용히 죽었다**(그게 「비행은 감지 대상 밖」으로 오독됐다).
                // 이제 층 분기가 **없다** — 층은 추격판을 굽는 쪽이 `traversalLayers` 로 가져간다.
                bool detectedHunting = _detectedLookup.HasComponent(entity)
                                       && _detectedLookup[entity].hunting != 0;
                bool unlimitedDetection = _detectionRangeLookup.HasComponent(entity)
                                          && _detectionRangeLookup[entity].Unlimited;

                bool huntShared = detectedHunting && unlimitedDetection
                    && hasHuntField && huntField.IsCreated
                    && huntField.dist[idx] != int.MaxValue;

                // 대상 지향 추격판. 길이 검사는 그리드가 바뀐 프레임의 낡은 버퍼를 거른다.
                int cellTotal = field.gridSize.x * field.gridSize.y;
                bool huntTargeted = detectedHunting && !unlimitedDetection
                    && _chaseDistLookup.HasBuffer(entity) && _chaseFlowLookup.HasBuffer(entity)
                    && _chaseDistLookup[entity].Length == cellTotal
                    && _chaseFlowLookup[entity].Length == cellTotal
                    && _chaseDistLookup[entity].Reinterpret<int>()[idx] != int.MaxValue;

                bool hunting = huntShared || huntTargeted;

                // ⚠⚠ **leak-proof 는 `hunting` 과 분리한다 — 무제한 감지 전용이다.**
                // 그대로 두면 유한 반경 감지 적도 골 칸을 밟고 공성으로 안 넘어가고, 그러면 감지가
                // **이 게임의 유일한 패배 통로**(골 → 마음 HP → 스트레스 100 → 남은 시간 몰수)의
                // 조절기가 된다. 「전멸시켜야 골에 간다」는 보스·보너스의 **저작된 성질**이지
                // 매 웨이브 수십 기의 잡몹에게 상속시킬 규칙이 아니다(boss-defender-field 계약).
                // ⚠⚠ **`hunting` 에 묶지 않는다**(리뷰 H2). `hunting` 은 감지 타이머(관성·막힘 해제·
                // 억제)에 따라 매 프레임 꺼질 수 있는데, 그러면 **무제한 사냥꾼이 그 틈에 골을
                // 유출한다.** `Enemy_DreamShard` 는 비보스라 CC 면역이 없어 자장가 한 번으로 그
                // 틈이 열리고, `BonusWaveData` 가 보너스 적에게 무제한을 강제하는 이유(「이 적은
                // 골로 안 간다」)가 조용히 깨진다. 골 유출은 이 게임의 유일한 패배 통로다.
                //
                // 그래서 **옛 술어를 그대로 쓰되 무제한으로만 한정한다** — 오늘 `Unlimited` 인 4종은
                // 옛 `DefenderHunterTag` 부착 4종(보스 3 + DreamShard)과 **정확히 같은 집합**이라
                // 이 형태가 계약 7 의 무회귀를 산술적으로 만족한다.
                bool leakProof = hasHuntField && huntField.IsCreated
                    && _detectionRangeLookup.HasComponent(entity)
                    && _detectionRangeLookup[entity].Unlimited
                    && huntField.dist[idx] != int.MaxValue;

                // 사냥 중엔 goal 셀을 지나쳐도 누수 안 함(leak-proof) — 방어유닛 전멸 후에만 도달 처리.
                // multi-goal-map — 어느 골이든 도달하면 누수(IsGoalCell = goals 멤버십/goalCell 폴백).
                // summon-patrol-defender unit 2 — 거점 박스 안에 goal 셀이 들어올 수 있다(맵은 매판
                // 랜덤, 배치는 플레이어가 한다). 게이트가 없으면 순찰병에 PastGoalTag 가 붙어
                // ⑴ 이 루프가 WithNone<PastGoalTag> 라 영구 동결, ⑵ UnitLifecycle 의 PastGoal 파괴
                // 루프는 AttackUnitTag 를 요구해 파괴도 안 됨, ⑶ 살아 있으니 SummonerState.current 가
                // 계속 유효해 소환사가 남은 판 내내 재소환하지 못한다. 보스 leak-proof 와 같은 형태.
                if (!leakProof && !patrolling && field.IsGoalCell(cell))
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
                    // unit 18 — 「화면은 원인데 판정은 사각형」 마지막 1곳. 원 + 피해자 몸으로
                    // 연속화(광역 자와 같은 물성: 반경 + 칸 반폭 + 몸). 셀 양자화도 함께 소멸.
                    float tInv = field.tileSize > 1e-6f ? 1f / field.tileSize : 1f;
                    float pullBodyR = _pullBodyRadiusLookup.HasComponent(entity) ? _pullBodyRadiusLookup[entity].value : 0f;
                    if (!Wassup.Skills.SkillMath.ReachFromCell(
                            (current.x - fieldT.centerWorld.x) * tInv,
                            (current.z - fieldT.centerWorld.z) * tInv,
                            fieldT.tileRange, pullBodyR)) continue;
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
                        // 정지(halt/잠금) 중에도 외력은 적용 — 기존 "Halt 도 당겨짐" 거동 보존.
                        // unit 2: 그 외력에 **넉백이 빠져 있었다.** 사용자 증상(제자리 공격 중인
                        // 킨들러가 안 밀림)이 정확히 이 자리다 — Halt 는 적 18종의 기본값이다.
                        if (hasPull || hasImpulse)
                            transform.ValueRW.Position = ComposeMove(
                                current, float3.zero, pullDisplacement + impulseDisplacement,
                                field.tileSize, follow.ValueRO.radius, in nav);
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
                        if (hasPull || hasImpulse)
                            transform.ValueRW.Position = ComposeMove(
                                current, float3.zero, pullDisplacement + impulseDisplacement,
                                field.tileSize, follow.ValueRO.radius, in nav);
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
                    if (huntShared)
                    {
                        routeFlow = huntField.flow;
                        routeDist = huntField.dist;
                    }
                    else if (huntTargeted)
                    {
                        // unit 8 — 대상 지향 추격판은 `huntField` 와 **같은 모양**(flow + dist)이라
                        // 여기서 갈아 끼우는 것으로 끝난다. 아래 하강·평활화·접근 보정 코드는
                        // 한 줄도 안 바뀐다 — 그게 이 버퍼가 dist 만 두지 않고 flow 도 같이
                        // 보관하는 이유다(어그로 추격판은 dist 만 두고 `RecoveryDir` 로 내려간다).
                        routeFlow = _chaseFlowLookup[entity].Reinterpret<float2>().AsNativeArray();
                        routeDist = _chaseDistLookup[entity].Reinterpret<int>().AsNativeArray();
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
                            // distance-based-range unit 4c — **사냥 레인의 같은 동결**.
                            // 어그로 레인과 결함도 처방도 같다(소스는 셀 정사각, 발사는 월드 원).
                            // 보스는 `AggroStateSystem` 에서 어그로 면역이라 추격 레인 보정이
                            // 구조적으로 못 닿는다 — 그래서 여기 따로 있다.
                            //
                            // ⚠ 여기서도 사거리를 재판정하지 않는다. `ai == Marching` 이
                            // `EnemyAiStateSystem` 의 `hasFireTarget == false`(정본 술어,
                            // 마스크·통행층·대상 몸 포함)와 같은 뜻이고 [UpdateBefore] 라 신선하다.
                            // ⚠⚠ **`!locked` 가 여기 있어야 한다.** 이 분기는 역사적으로 자기주도
                            // 이동이 0 이라 잠금 게이트(`:flowStep = locked ? zero : …`)보다 앞에
                            // 있어도 안전했다. 보정이 **자기 이동을 넣었으므로** 게이트도 같이
                            // 와야 한다 — 없으면 자장가·동상에 걸린 헌터가 계속 걷고, 도약 비행 중
                            // (`LeapFlight` 도 `locked` 에 접힌다) 위치를 덮어쓴다.
                            // ⚠ CC 면역은 **`BossTag` 전용**이라(`CcApplySystem:37`) 비-보스 헌터
                            // (`Enemy_DreamShard`: tier 0 + `detectionRange = -1`)로 **오늘 재현된다.**
                            //
                            // ⚠ `aiStateLookup`/`attackStateLookup` 보유를 **명시로 요구한다**(fail-closed).
                            // `ai` 기본값이 `Marching`(:128)이고 `EnemyAiStateSystem` 은 `hasAttack`
                            // 이 거짓이면 술어를 **한 번도 안 부른다** — 그 `Marching` 은 「사거리 밖」이
                            // 아니라 **「물어보지도 않았다」**다. 그 상태로 보정이 돌면 사거리를 모르는
                            // 채 방어유닛에 달라붙는다.
                            //
                            // ⚠ **`EnemyAiState` 와 `AttackState` 의 bake 는 성질이 다르다.**
                            // 앞은 무조건이지만(`BattleBridge:10775`), 뒤는 `if (wantsAttack)`
                            // **안**이고(`:10724`) `attackMethod` 를 켜고 `outputs` 를 비우면
                            // **경고 한 줄만 찍고 walk-only 로 구워진다**(`:10710`).
                            // 즉 `attackStateLookup` 가드는 도달 불가한 방어가 아니라 **실제
                            // 저작 실수를 막는 가드**다 — 「어차피 항상 있으니」로 지우지 말 것.
                            // (`DefenderFieldSystem:66` 도 이미 「AttackState 없는 헌터」를 상정한다.)
                            // ⚠ unit 8 rev — `huntTargets` 를 **요구하지 않는다.** 그 목록은
                            // `DefenderHunterTag`(이제 **무제한 전용**) 게이트 뒤에서만 채워지므로,
                            // 보스 없는 일반 웨이브에서는 비어 있다. 대상 지향 사냥은 그 목록이
                            // 아니라 자기 대상을 쓰므로 여기서 막히면 **유한 감지가 보정을 통째로 잃는다**
                            // (「도착했는데 못 쏜다」 영구 동결의 재발).
                            if (hunting && !locked
                                && aiStateLookup.HasComponent(entity) && ai == AiState.Marching
                                && attackStateLookup.HasComponent(entity)
                                && routeDist[idx] == 0
                                && (huntTargets.Length > 0 || huntTargeted))
                            {
                                // 최근접 방어유닛 — 소스 칸을 만든 것이 그중 하나다.
                                float bestSq = float.MaxValue; float3 bestPos = default;
                                bool hasClosePos = false;
                                for (int t = 0; t < huntTargets.Length; t++)
                                {
                                    float hdx = huntTargets[t].x - current.x;
                                    float hdz = huntTargets[t].z - current.z;
                                    float sq = hdx * hdx + hdz * hdz;
                                    if (sq < bestSq) { bestSq = sq; bestPos = huntTargets[t]; hasClosePos = true; }
                                }
                                // unit 8 — 대상 지향 사냥이면 **그 대상**으로 붙는다. 위 최근접은
                                // 공용 사냥판(무제한)용 근사다 — 도착지가 특정되지 않으니 「소스 칸을
                                // 만든 것이 그중 하나」로 추정할 수밖에 없었다. 유한 감지는 대상이
                                // 정해져 있으므로 추정하지 않는다(벽 너머에 더 가까운 유닛이 있어도
                                // 그쪽으로 몸을 기울이지 않는다).
                                if (huntTargeted && _detectedLookup.HasComponent(entity))
                                {
                                    var dt8 = _detectedLookup[entity].target;
                                    if (dt8 != Entity.Null && _guardianTransformLookup.HasComponent(dt8))
                                    { bestPos = _guardianTransformLookup[dt8].Position; hasClosePos = true; }
                                }
                                // ⚠ 관성(grace) 중에는 대상이 비어 있다 — 그때 목록도 비면 붙을
                                // 자리가 없다. 원점(0,0,0)으로 기어가지 않도록 자리를 요구한다.
                                if (hasClosePos)
                                {
                                    float huntSpeedMul = modifierStatsLookup.HasComponent(entity)
                                        ? modifierStatsLookup[entity].moveSpeedMul : 1f;
                                    float2 huntTaken = TryCloseIn(
                                        current, bestPos, follow.ValueRO.speed * huntSpeedMul * dt,
                                        in routeDist, field.gridSize, field.tileSize, field.origin,
                                        follow.ValueRO.radius, in nav,
                                        pullDisplacement + impulseDisplacement, out float3 huntNext);
                                    if (math.lengthsq(huntTaken) > 1e-6f)
                                    {
                                        transform.ValueRW.Position = huntNext;
                                        follow.ValueRW.holdingGround = 0;
                                        follow.ValueRW.lastMoveDir = math.normalize(huntTaken);
                                        continue;
                                    }
                                }
                            }
                            // truly isolated cell — 자기주도 이동은 없지만 외력은 적용 (unit 3).
                            // 사방이 벽이면 clamp/Resolve 가 0으로 접고, 열린 이웃이 있으면
                            // 그쪽으로 밀린다 — 갇힌 유닛을 넉백으로 빼내는 것이 맞는 동작이다.
                            if (hasPull || hasImpulse)
                                transform.ValueRW.Position = ComposeMove(
                                    current, float3.zero, pullDisplacement + impulseDisplacement,
                                    field.tileSize, follow.ValueRO.radius, in nav);
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
                // (unit 2 — impulseDisplacement 합성은 분기 앞으로 올라갔다. 여기 있던 시절엔
                //  조기 이탈 경로 여섯이 이 줄에 닿지 못해 넉백이 그 상태들에서 증발했다.)
                float2 stepDir = math.normalizesafe(dir); // Phase 9: FlowFieldBuilder writes unit vectors;
                                                           // normalizesafe defensively handles future diagonal/non-unit flow
                                                           // and returns zero for <1e-6 magnitude (already guarded above).
                // combat-action-lock — 잠/스턴: 자기주도 flow-step 0, 넉백(impulse)은 유지.
                float3 flowStep = locked ? float3.zero : new float3(stepDir.x, 0, stepDir.y) * follow.ValueRO.speed * speedMul * dt;

                // unit 13 — 자기주도 변위가 실제로 있을 때만 "이동 중". 외력(impulse/pull)만
                // 있는 프레임은 정지로 남는다 — 밀려나는 유닛은 자리를 지키는 쪽이 맞다.
                if (math.lengthsq(flowStep) > 1e-12f)
                {
                    follow.ValueRW.holdingGround = 0;
                    // defender-knockback-on-impact unit 0 — 진행 방향 기록. flowStep 은
                    // 웨이포인트·흐름장·복구를 이미 거친 **최종 자기주도 변위**라, 어느
                    // 경로로 결정됐든 이 한 줄이 실제 진행 방향을 잡는다.
                    follow.ValueRW.lastMoveDir = math.normalize(flowStep.xz);
                }

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

                // unit 2 — 다른 여섯 경로와 **같은 합성 지점**을 쓴다. 자기 이동 = flowStep,
                // 외력 = 넉백 + 토네이도 당김. 변위 상한(터널링 차단)과 벽 밀어넣기 차단은
                // ComposeMove 안에 있다(aggro-tile-chase unit 2·3 의 동작 그대로).
                transform.ValueRW.Position = ComposeMove(
                    current, flowStep, impulseDisplacement + pullDisplacement,
                    field.tileSize, follow.ValueRO.radius, in nav);
            }

            portals.Dispose();
            tornadoFields.Dispose();
            huntTargets.Dispose();
            navScratch.Dispose();
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        // defender-knockback-on-impact unit 2 — **변위 합성의 단일 지점.**
        //
        // 이 3줄이 예전엔 7곳에 복붙돼 있었고 각 복사본이 **서로 다른 힘 부분집합**만
        // 알았다. 넉백이 나중에 추가되면서 메인 한 곳만 갱신되고 나머지 여섯이 뒤처져,
        // 교전·도발·순찰·고립 상태의 적이 통째로 넉백 면역이 됐다. 힘이 하나 더 생겨도
        // 같은 일이 반복되지 않도록 합성을 여기 하나로 모은다.
        //
        // 힘은 두 종류다:
        //   self     = 자기주도 이동(flow / chase / patrol). 「멈춤」 = 이 값이 0.
        //   external = 외력(넉백 + 당김). 상태와 무관하게 항상 적용된다.
        //
        // plain 값만 받고 plain 값을 낸다(제약 10) — 호출처 7곳 + sim-critical 이동이라
        // 추출 기준을 충족한다. clamp 는 터널링 차단(프레임 변위 상한), Resolve 는
        // 벽/장애물 밀어넣기 차단이며 둘 다 기존 동작 그대로다.
        // distance-based-range unit 4c — 「사격 칸에 도착했는데 사거리 밖」일 때의 접근 보정.
        // **어그로 레인과 사냥 레인이 이 하나를 공유한다** — 두 벌이면 조용히 갈리고, 그게
        // 이 결함의 원인(한 루프에 자가 셋)과 같은 클래스다.
        //
        // ⚠ **잠금(CC·도약 비행) 판정은 호출부 소유다.** 이 함수는 기계장치만 공유하고
        // 게이트는 공유하지 않는다 — 두 호출부가 **각자** `!locked` 를 걸어야 한다.
        // (한쪽만 걸었다가 잠긴 헌터가 걷는 결함이 실제로 났다.)
        //
        // 반환 = 실제로 취한 cardinal(zero = 못 움직임). `next` 는 외력까지 합성한 최종 위치.
        // 막힘 판정은 **자기주도 변위만으로** 한다(외력을 섞으면 벽에 막힌 축도 「움직였다」로
        // 읽혀 폴백이 죽고, 가지 않은 방향이 `lastMoveDir` 에 박힌다).
        // 소스 영역(dist 0) 이탈 스텝은 취하지 않는다 — 벗어나면 다음 프레임 필드 하강이
        // 되돌려 왕복이 된다. 단 **외력은 이 불변식 밖**이다(넉백은 원래 소스 안팎을 안 가린다).
        private static float2 TryCloseIn(
            float3 current, float3 targetPos, float closeDist,
            in NativeArray<int> firingDist, int2 gridSize, float tileSize, float3 origin,
            float radius, in NavGrid nav, float3 externalDisp, out float3 next)
        {
            next = current;
            float dx = targetPos.x - current.x;
            float dz = targetPos.z - current.z;
            if (dx * dx + dz * dz <= 1e-6f) return float2.zero;

            Wassup.Battle.Combat.AggroChaseMath.CloseInCardinals(dx, dz, out var primary, out var secondary);
            for (int a = 0; a < 2; a++)
            {
                float2 axis = a == 0 ? primary : secondary;
                float3 probe = ComposeMove(
                    current, new float3(axis.x, 0f, axis.y) * closeDist,
                    float3.zero, tileSize, radius, in nav);
                if (math.lengthsq(probe - current) < 1e-8f) continue;          // 막혔다
                int2 pCell = GridMath.WorldToCell(probe, tileSize, gridSize, origin);
                // fail-closed — 필드가 없으면 이탈 검사를 못 하므로 스텝도 안 취한다.
                if (!firingDist.IsCreated
                    || firingDist[GridMath.CellIndex(pCell, gridSize)] != 0) continue;  // 소스 이탈
                next = ComposeMove(
                    current, new float3(axis.x, 0f, axis.y) * closeDist,
                    externalDisp, tileSize, radius, in nav);
                return axis;
            }
            return float2.zero;
        }

        private static float3 ComposeMove(
            float3 current, float3 selfDisp, float3 externalDisp,
            float tileSize, float radius, in NavGrid nav)
        {
            float3 desired = current + selfDisp + externalDisp;
            desired = MovementCellTrim.ClampDisplacement(current, desired, tileSize);
            return AgentCollision.Resolve(current, desired, radius, in nav);
        }
    }
}
