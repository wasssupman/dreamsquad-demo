using NUnit.Framework;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Wassup.Battle.Combat;
using Wassup.Battle.Effects;
using Wassup.Battle.Movement;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    public class MovementSystemTests
    {
        private World _world;
        private EntityManager _em;
        private SimulationSystemGroup _simGroup;
        private Entity _fieldEntity;

        [SetUp]
        public void SetUp()
        {
            _world = new World("MovementSystemTestWorld");
            _em = _world.EntityManager;
            _simGroup = _world.CreateSystemManaged<SimulationSystemGroup>();
            _simGroup.AddSystemToUpdateList(_world.CreateSystem<MovementSystem>());
        }

        [TearDown]
        public void TearDown()
        {
            // NativeArray<float2>/<int> 는 Persistent 로 할당했으므로 명시적 dispose.
            if (_fieldEntity != Entity.Null && _em.Exists(_fieldEntity)
                && _em.HasComponent<FlowFieldSingleton>(_fieldEntity))
            {
                var f = _em.GetComponentData<FlowFieldSingleton>(_fieldEntity);
                f.Dispose();
            }
            _world?.Dispose();
        }

        // 5x1 직선 맵: 모든 cell 이 +x 방향을 가리킴. goal = (4,0).
        private void CreateLinearFlowField(int width = 5, float tileSize = 1f)
        {
            int n = width * 1;
            var flow = new NativeArray<float2>(n, Allocator.Persistent);
            var dist = new NativeArray<int>(n, Allocator.Persistent);
            for (int i = 0; i < width - 1; i++) { flow[i] = new float2(1, 0); dist[i] = (width - 1) - i; }
            flow[width - 1] = float2.zero; dist[width - 1] = 0;

            _fieldEntity = _em.CreateEntity();
            _em.AddComponentData(_fieldEntity, new FlowFieldSingleton
            {
                flow = flow, dist = dist,
                gridSize = new int2(width, 1),
                goalCell = new int2(width - 1, 0),
                tileSize = tileSize, version = 1,
            });
        }

        // 3x3: goal=(2,1)은 +x, waypoint=(1,2)는 +z 로 서로 다른 방향을 준다.
        // 슬롯 선택이 실제 변위로 드러나는 unit 3 증상 픽스처다.
        private void CreateWaypointFlowField(bool waypointReachable = true)
        {
            var gridSize = new int2(3, 3);
            int n = gridSize.x * gridSize.y;
            var flow = new NativeArray<float2>(n * 2, Allocator.Persistent);
            var dist = new NativeArray<int>(n * 2, Allocator.Persistent);
            var walkMask = new NativeArray<byte>(n, Allocator.Persistent);
            for (int i = 0; i < n; i++)
            {
                flow[i] = new float2(1f, 0f);
                dist[i] = 1;
                flow[n + i] = new float2(0f, 1f);
                dist[n + i] = 1;
                walkMask[i] = 1;
            }
            int goalIndex = GridMath.CellIndex(new int2(2, 1), gridSize);
            int waypointIndex = GridMath.CellIndex(new int2(1, 2), gridSize);
            flow[goalIndex] = float2.zero;
            dist[goalIndex] = 0;
            flow[n + waypointIndex] = float2.zero;
            dist[n + waypointIndex] = 0;
            if (!waypointReachable)
                dist[n + GridMath.CellIndex(new int2(1, 1), gridSize)] = int.MaxValue;

            var masks = new NativeArray<byte>(2, Allocator.Persistent);
            masks[0] = (byte)PlacementLayer.Path;
            masks[1] = (byte)PlacementLayer.Path;
            var destinations = new NativeArray<int2>(2, Allocator.Persistent);
            destinations[0] = FlowFieldSingleton.GoalSentinel;
            destinations[1] = new int2(1, 2);
            var waypointCells = new NativeArray<int2>(1, Allocator.Persistent);
            waypointCells[0] = new int2(1, 2);
            var waypointRanges = new NativeArray<int2>(1, Allocator.Persistent);
            waypointRanges[0] = new int2(0, 1);

            _fieldEntity = _em.CreateEntity();
            _em.AddComponentData(_fieldEntity, new FlowFieldSingleton
            {
                flow = flow,
                dist = dist,
                walkMask = walkMask,
                maskValues = masks,
                destCells = destinations,
                waypointCells = waypointCells,
                waypointRanges = waypointRanges,
                gridSize = gridSize,
                goalCell = new int2(2, 1),
                tileSize = 1f,
                version = 1,
            });
        }

        private void CreateLayeredGoalFlowField()
        {
            var gridSize = new int2(3, 3);
            int n = gridSize.x * gridSize.y;
            var flow = new NativeArray<float2>(n * 2, Allocator.Persistent);
            var dist = new NativeArray<int>(n * 2, Allocator.Persistent);
            var walkMask = new NativeArray<byte>(n, Allocator.Persistent);
            var cellLayers = new NativeArray<byte>(n, Allocator.Persistent);
            for (int i = 0; i < n; i++)
            {
                flow[i] = new float2(1f, 0f);       // Path goal 슬롯
                flow[n + i] = new float2(0f, 1f);   // Ground goal 슬롯
                dist[i] = dist[n + i] = 1;
                walkMask[i] = 1;
                cellLayers[i] = (byte)(PlacementLayer.Path | PlacementLayer.Ground);
            }
            int groundGoalIndex = GridMath.CellIndex(new int2(1, 2), gridSize);
            flow[n + groundGoalIndex] = float2.zero;
            dist[n + groundGoalIndex] = 0;

            var masks = new NativeArray<byte>(2, Allocator.Persistent);
            masks[0] = (byte)PlacementLayer.Path;
            masks[1] = (byte)PlacementLayer.Ground;
            var destinations = new NativeArray<int2>(2, Allocator.Persistent);
            destinations[0] = destinations[1] = FlowFieldSingleton.GoalSentinel;

            _fieldEntity = _em.CreateEntity();
            _em.AddComponentData(_fieldEntity, new FlowFieldSingleton
            {
                flow = flow,
                dist = dist,
                walkMask = walkMask,
                cellLayers = cellLayers,
                maskValues = masks,
                destCells = destinations,
                gridSize = gridSize,
                goalCell = new int2(1, 2),
                tileSize = 1f,
                version = 1,
            });
        }

        private Entity CreateUnit(float3 pos, float speed)
        {
            var e = _em.CreateEntity();
            _em.AddComponentData(e, LocalTransform.FromPosition(pos));
            _em.AddComponentData(e, new PathFollowState { speed = speed });
            return e;
        }

        // enemy-ai-fsm Unit 2 — FSM 상태를 직접 부여한 적(EnemyAiStateSystem 없이 MovementSystem 단독 검증).
        private Entity CreateEnemy(float3 pos, float speed, AiState state, EngageMovement engage = EngageMovement.Halt)
        {
            var e = CreateUnit(pos, speed);
            _em.AddComponentData(e, new EnemyAiState { value = state });
            _em.AddComponentData(e, new EnemyBehavior { engageMovement = engage });
            return e;
        }

        private void Tick(float dt)
        {
            _world.SetTime(new TimeData(_world.Time.ElapsedTime + dt, dt));
            _simGroup.Update();
        }

        // aggro-tile-chase unit 2 — 프레임 변위 상한(<0.9타일) 하에서 총 시간을 서브틱으로
        // 나눠 "speed×시간" 기대값을 유지한다(실프레임 조건과 동일).
        private void TickSplit(float total = 1f, int parts = 8)
        {
            for (int i = 0; i < parts; i++) Tick(total / parts);
        }

        [Test]
        public void Moves_Along_Flow_At_Configured_Speed()
        {
            CreateLinearFlowField();
            var e = CreateUnit(new float3(0f, 0f, 0f), speed: 2f);

            TickSplit(1f);

            var pos = _em.GetComponentData<LocalTransform>(e).Position;
            Assert.AreEqual(2f, pos.x, 1e-4f, "2 units at speed 2 in 1s along +x flow");
            Assert.AreEqual(0f, pos.z, 1e-4f, "no drift on z");
            Assert.IsFalse(_em.HasComponent<PastGoalTag>(e));
        }

        [Test]
        public void Adds_PastGoalTag_When_Cell_Matches_GoalCell()
        {
            CreateLinearFlowField();
            var e = CreateUnit(new float3(4f, 0f, 0f), speed: 1f);

            Tick(0.1f);

            Assert.IsTrue(_em.HasComponent<PastGoalTag>(e),
                "MovementSystem must tag unit when WorldToCell result equals goalCell");
        }

        [Test]
        public void Does_Not_Move_On_Unreachable_Cell()
        {
            // Zero-flow cell (e.g., isolated by obstacle). Unit should stay put.
            var flow = new NativeArray<float2>(1, Allocator.Persistent);
            var dist = new NativeArray<int>(1, Allocator.Persistent);
            flow[0] = float2.zero; dist[0] = int.MaxValue;

            _fieldEntity = _em.CreateEntity();
            _em.AddComponentData(_fieldEntity, new FlowFieldSingleton
            {
                flow = flow, dist = dist,
                gridSize = new int2(1, 1),
                goalCell = new int2(99, 99), // different from (0,0)
                tileSize = 1f, version = 1,
            });

            var e = CreateUnit(new float3(0f, 0f, 0f), speed: 5f);
            Tick(1f);

            var pos = _em.GetComponentData<LocalTransform>(e).Position;
            Assert.AreEqual(0f, pos.x, 1e-4f, "zero flow must produce zero movement");
        }

        [Test]
        public void MoveSpeedMul_Halves_Flow_Step()
        {
            CreateLinearFlowField();
            var e = CreateUnit(new float3(0f, 0f, 0f), speed: 2f);
            _em.AddComponentData(e, new ModifierStats { moveSpeedMul = 0.5f });

            TickSplit(1f);

            var pos = _em.GetComponentData<LocalTransform>(e).Position;
            Assert.AreEqual(1f, pos.x, 1e-4f, "MoveSpeedMul 0.5 × speed 2 × 1s = 1.0");
        }

        // enemy-ai-fsm Unit 2 — 이동/정지를 EnemyAiState + engageMovement 로 결정(레거시 movePause 대체).
        // Advance↔Halt 대비가 분기를 실제로 구분함을 보장.
        [Test]
        public void Marching_MovesAlongFlow()
        {
            CreateLinearFlowField();
            var e = CreateEnemy(new float3(0f, 0f, 0f), speed: 2f, AiState.Marching);
            TickSplit(1f);
            Assert.AreEqual(2f, _em.GetComponentData<LocalTransform>(e).Position.x, 1e-4f,
                "Marching → flow 이동");
        }

        [Test]
        public void Engaging_Halt_StopsMovement()
        {
            CreateLinearFlowField();
            var e = CreateEnemy(new float3(0f, 0f, 0f), speed: 2f, AiState.Engaging, EngageMovement.Halt);
            Tick(1f);
            Assert.AreEqual(0f, _em.GetComponentData<LocalTransform>(e).Position.x, 1e-4f,
                "Engaging-Halt → 정지");
        }

        [Test]
        public void Engaging_Advance_MovesAlongFlow()
        {
            CreateLinearFlowField();
            var e = CreateEnemy(new float3(0f, 0f, 0f), speed: 2f, AiState.Engaging, EngageMovement.Advance);
            TickSplit(1f);
            Assert.AreEqual(2f, _em.GetComponentData<LocalTransform>(e).Position.x, 1e-4f,
                "Engaging-Advance → 이동하며 공격");
        }

        [Test]
        public void Standoff_StopsMovement()
        {
            CreateLinearFlowField();
            var e = CreateEnemy(new float3(0f, 0f, 0f), speed: 2f, AiState.Standoff, EngageMovement.Advance);
            Tick(1f);
            Assert.AreEqual(0f, _em.GetComponentData<LocalTransform>(e).Position.x, 1e-4f,
                "Standoff → 정지(engageMovement 무관)");
        }

        // aggro-tile-chase unit 2 — Chasing 은 chase field(dist) 하강. flow(+x)와 반대인
        // -x 목적지(dist 0 = 셀 2)로 하강해 분기를 구분하고, 목적지 셀 도달 시 자연 정지한다.
        [Test]
        public void Chasing_DescendsChaseField_NotFlow_AndStopsAtDestination()
        {
            CreateLinearFlowField(); // flow 는 전부 +x — chase 는 이걸 무시해야 한다
            var e = CreateEnemy(new float3(3f, 0f, 0f), speed: 2f, AiState.Chasing, EngageMovement.Halt);
            // chase field: 셀 2 가 목적지(dist 0), 좌우로 증가 — 적(셀 3)은 -x 로 하강.
            var chase = _em.AddBuffer<AggroChaseCell>(e);
            int[] dists = { 2, 1, 0, 1, 2 };
            for (int i = 0; i < dists.Length; i++) chase.Add(new AggroChaseCell { dist = dists[i] });

            TickSplit(1f);

            var pos = _em.GetComponentData<LocalTransform>(e).Position;
            Assert.Less(pos.x, 3f, "flow(+x) 무시하고 chase field 하강(-x)");
            var cell = GridMath.WorldToCell(pos, 1f, new int2(5, 1));
            Assert.AreEqual(2, cell.x, "목적지(dist 0) 셀 도달 후 정지");
            Assert.AreEqual(0f, pos.z, 1e-4f, "z 드리프트 없음");
        }

        // aggro-tile-chase unit 2 — chase field 없는 Chasing(합성 월드/부착 실패)은 정지.
        [Test]
        public void Chasing_WithoutChaseField_StaysPut()
        {
            CreateLinearFlowField();
            var e = CreateEnemy(new float3(3f, 0f, 0f), speed: 2f, AiState.Chasing, EngageMovement.Halt);
            TickSplit(1f);
            Assert.AreEqual(3f, _em.GetComponentData<LocalTransform>(e).Position.x, 1e-4f,
                "필드 없으면 제자리(직선 추격 폐지)");
        }

        // enemy-ai-fsm Unit 5 — Halt 직교성: Engaging-Halt 정지는 flow 만 막는다.
        // 스킬 캐리어(portal)는 Halt 게이트 이전이라 정지 중에도 텔레포트.
        [Test]
        public void EngagingHalt_StillTeleportsThroughPortal()
        {
            CreateLinearFlowField();
            var portal = _em.CreateEntity();
            _em.AddComponentData(portal, new PortalLink
            {
                entryWorld = new float3(0f, 0f, 0f), exitWorld = new float3(3f, 0f, 0f),
                entryRadius = 0.5f, remaining = 99f,
            });

            var e = CreateEnemy(new float3(0f, 0f, 0f), speed: 2f, AiState.Engaging, EngageMovement.Halt);
            Tick(1f);

            Assert.AreEqual(3f, _em.GetComponentData<LocalTransform>(e).Position.x, 1e-4f,
                "Engaging-Halt 라도 portal 텔레포트는 적용(이후 flow 는 Halt 로 정지)");
        }

        // enemy-ai-fsm Unit 5 — Halt 직교성: 토네이도 pull 도 Halt 게이트 이전이라 정지 중에도 당겨짐.
        [Test]
        public void EngagingHalt_StillPulledByTornado()
        {
            CreateLinearFlowField();
            var tornado = _em.CreateEntity();
            _em.AddComponentData(tornado, new TornadoField
            {
                centerWorld = new float3(3f, 0f, 0f), tileRange = 5, pullSpeed = 2f, remaining = 99f,
            });

            var e = CreateEnemy(new float3(0f, 0f, 0f), speed: 2f, AiState.Engaging, EngageMovement.Halt);
            TickSplit(1f);

            Assert.AreEqual(2f, _em.GetComponentData<LocalTransform>(e).Position.x, 1e-4f,
                "Engaging-Halt 라도 tornado pull 적용(pullSpeed 2 × 1s = 2, unit 3 — 가산 변위+trim)");
        }

        // enemy-ai-fsm Unit 7 — Pulse 진동: Engaging 에서 타격 진행 중(hitDelayRemaining>0)이면
        // 정지(스윙), 회복(==0)이면 flow 전진. 어그로 유무에 따른 A(camp)/B(pulse) 분기의 B 절반.
        [Test]
        public void Engaging_Pulse_AttackInProgress_Stops()
        {
            CreateLinearFlowField();
            var e = CreateEnemy(new float3(0f, 0f, 0f), speed: 2f, AiState.Engaging, EngageMovement.Pulse);
            _em.AddComponentData(e, new AttackState { hitDelaySec = 0.3f, hitDelayRemaining = 0.3f });
            Tick(1f);
            Assert.AreEqual(0f, _em.GetComponentData<LocalTransform>(e).Position.x, 1e-4f,
                "Pulse + 타격 진행중(hitDelayRemaining>0) → 정지(스윙)");
        }

        [Test]
        public void Engaging_Pulse_AttackRecovered_MovesAlongFlow()
        {
            CreateLinearFlowField();
            var e = CreateEnemy(new float3(0f, 0f, 0f), speed: 2f, AiState.Engaging, EngageMovement.Pulse);
            _em.AddComponentData(e, new AttackState { hitDelaySec = 0.3f, hitDelayRemaining = 0f });
            TickSplit(1f);
            Assert.AreEqual(2f, _em.GetComponentData<LocalTransform>(e).Position.x, 1e-4f,
                "Pulse + 타격 회복(hitDelayRemaining==0) → flow 전진");
        }

        // enemy-ai-fsm Unit 7 (ecs-review M1) — A-fork 잠금: 어그로 Standoff 는 engageMovement 무관 정지.
        // Pulse + hitDelayRemaining=0(Engaging이면 전진할 값)이어도 Standoff 가 먼저 정지 → 어그로 시 캠프 보장.
        [Test]
        public void Standoff_Pulse_StaysCamped_IgnoringEngageMovement()
        {
            CreateLinearFlowField();
            var e = CreateEnemy(new float3(0f, 0f, 0f), speed: 2f, AiState.Standoff, EngageMovement.Pulse);
            _em.AddComponentData(e, new AttackState { hitDelaySec = 0.3f, hitDelayRemaining = 0f });
            Tick(1f);
            Assert.AreEqual(0f, _em.GetComponentData<LocalTransform>(e).Position.x, 1e-4f,
                "Standoff → engageMovement 무관 정지(Pulse 적도 어그로 시 캠프). A/B 자동분기의 A 절반");
        }

        // enemy-ai-fsm Unit 7 (ecs-review M2) — Pulse + AttackState 없음 → 전진(degenerate Advance, 안전 fallback).
        [Test]
        public void Engaging_Pulse_NoAttackState_MovesAlongFlow()
        {
            CreateLinearFlowField();
            var e = CreateEnemy(new float3(0f, 0f, 0f), speed: 2f, AiState.Engaging, EngageMovement.Pulse);
            TickSplit(1f);
            Assert.AreEqual(2f, _em.GetComponentData<LocalTransform>(e).Position.x, 1e-4f,
                "Pulse + AttackState 없음 → flow 전진(fallback)");
        }

        [Test]
        public void WaypointFollow_UsesWaypointSlot_ThenAdvancesToGoalSlot()
        {
            CreateWaypointFlowField();
            // waypoint-routing unit 9 — 시작 셀이 (1,1) 이었으나 웨이포인트 (1,2) 와 **인접**이라,
            // 도달 판정이 체비셰프 1 로 완화된 뒤로는 첫 프레임에 이미 «도달» 이 되어 슬롯을
            // 따라갈 구간 자체가 없었다. 이 테스트가 지키는 계약(웨이포인트로 갈 땐 그 슬롯,
            // 마지막 뒤엔 골 슬롯)은 그대로이고 픽스처 기하만 옛 계약을 전제했던 것 —
            // 2칸 떨어진 (1,0) 에서 출발시켜 «따라가는 구간» 을 되살린다.
            var e = CreateUnit(new float3(1f, 0f, 0f), speed: 0.2f);
            _em.AddComponentData(e, new WaypointFollow { pathIndex = 0, index = 0 });

            Tick(1f);
            var towardWaypoint = _em.GetComponentData<LocalTransform>(e).Position;
            Assert.Greater(towardWaypoint.z, 0f, "활성 waypoint 슬롯(+z)을 따라야 한다");
            Assert.AreEqual(1f, towardWaypoint.x, 1e-4f, "goal 슬롯(+x)은 아직 읽지 않는다");

            _em.SetComponentData(e, LocalTransform.FromPosition(new float3(1f, 0f, 2f)));
            Tick(1f);

            var progress = _em.GetComponentData<WaypointFollow>(e);
            var towardGoal = _em.GetComponentData<LocalTransform>(e).Position;
            Assert.AreEqual(1, progress.index, "waypoint 셀 진입 프레임에 진행 인덱스 기록");
            Assert.Greater(towardGoal.x, 1f, "마지막 waypoint 뒤에는 goal 슬롯(+x)으로 즉시 복귀");
        }

        [Test]
        public void WaypointFollow_UnreachableWaypoint_SkipsToGoalSameFrame()
        {
            CreateWaypointFlowField(waypointReachable: false);
            var e = CreateUnit(new float3(1f, 0f, 1f), speed: 0.2f);
            _em.AddComponentData(e, new WaypointFollow { pathIndex = 0, index = 0 });

            Tick(1f);

            var progress = _em.GetComponentData<WaypointFollow>(e);
            var pos = _em.GetComponentData<LocalTransform>(e).Position;
            Assert.AreEqual(1, progress.index, "도달 불가 waypoint 는 건너뛴다");
            Assert.Greater(pos.x, 1f, "마지막 waypoint skip 뒤 goal 슬롯으로 전진");
            Assert.AreEqual(1f, pos.z, 1e-4f);
        }

        [Test]
        public void WaypointFollow_ChasingPreservesProgress()
        {
            CreateWaypointFlowField();
            var e = CreateEnemy(new float3(1f, 0f, 2f), speed: 0.2f, AiState.Chasing);
            _em.AddComponentData(e, new WaypointFollow { pathIndex = 0, index = 0 });

            Tick(1f);

            Assert.AreEqual(0, _em.GetComponentData<WaypointFollow>(e).index,
                "Chasing 분기가 waypoint 진행보다 우선하고 인덱스는 보존");
            Assert.AreEqual(new float3(1f, 0f, 2f), _em.GetComponentData<LocalTransform>(e).Position);

            var ai = _em.GetComponentData<EnemyAiState>(e);
            ai.value = AiState.Marching;
            _em.SetComponentData(e, ai);
            Tick(1f);

            Assert.AreEqual(1, _em.GetComponentData<WaypointFollow>(e).index,
                "추격 해제 뒤 보존한 waypoint부터 재개");
            Assert.Greater(_em.GetComponentData<LocalTransform>(e).Position.x, 1f,
                "마지막 waypoint였다면 goal 슬롯으로 이어진다");
        }

        [Test]
        public void UnitWithoutWaypointFollow_UsesGoalSlot()
        {
            CreateWaypointFlowField();
            var e = CreateUnit(new float3(1f, 0f, 1f), speed: 0.2f);

            Tick(1f);

            var pos = _em.GetComponentData<LocalTransform>(e).Position;
            Assert.Greater(pos.x, 1f, "미저작 적은 현행 goal 슬롯(+x)");
            Assert.AreEqual(1f, pos.z, 1e-4f);
        }

        [Test]
        public void GoalRoute_UsesUnitsTraversalLayerSlot()
        {
            CreateLayeredGoalFlowField();
            var e = CreateUnit(new float3(1f, 0f, 1f), speed: 0.2f);
            var follow = _em.GetComponentData<PathFollowState>(e);
            follow.traversalLayers = (byte)PlacementLayer.Ground;
            _em.SetComponentData(e, follow);

            Tick(1f);

            var pos = _em.GetComponentData<LocalTransform>(e).Position;
            Assert.AreEqual(1f, pos.x, 1e-4f, "Primary Path goal 슬롯을 읽으면 안 된다");
            Assert.Greater(pos.z, 1f, "유닛 통행층과 일치하는 Ground goal 슬롯(+z)을 따라야 한다");
        }
    }
}
