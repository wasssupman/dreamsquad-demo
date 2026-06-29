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
        private NativeQueue<MovementPauseRequest> _pauseQueue;

        [SetUp]
        public void SetUp()
        {
            _world = new World("MovementSystemTestWorld");
            _em = _world.EntityManager;
            _simGroup = _world.CreateSystemManaged<SimulationSystemGroup>();
            _simGroup.AddSystemToUpdateList(_world.CreateSystem<MovementPauseRequestDrainSystem>());
            _simGroup.AddSystemToUpdateList(_world.CreateSystem<MovementSystem>());
            _pauseQueue = new NativeQueue<MovementPauseRequest>(Allocator.Persistent);
            var pauseSingleton = _em.CreateEntity();
            _em.AddComponentData(pauseSingleton, new MovementPauseRequestEventsSingleton { queue = _pauseQueue });
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
            if (_pauseQueue.IsCreated) _pauseQueue.Dispose();
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

        [Test]
        public void Moves_Along_Flow_At_Configured_Speed()
        {
            CreateLinearFlowField();
            var e = CreateUnit(new float3(0f, 0f, 0f), speed: 2f);

            Tick(1f);

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

            Tick(1f);

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
            Tick(1f);
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
            Tick(1f);
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
    }
}
