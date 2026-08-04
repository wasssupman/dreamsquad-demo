using NUnit.Framework;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Wassup.Battle.Effects;
using Wassup.Battle.Movement;
using Wassup.Battle.Units;

namespace Wassup.Tests.EditMode
{
    // goal-stability unit 2 — 공성 게이트: 살아있는 골 셀에서는 유출(PastGoalTag)이 봉인되고,
    // 붕괴(DeadTag/엔티티 부재) 즉시 현행 유출로 복귀한다. 픽스처는 PatrolSystemIntegrationTests
    // 동형(5x1 직선 flow field, goal=(4,0)). "골 엔티티 없음 = 현행 유출" 대조군은 기존
    // PatrolSystemIntegrationTests.NonPatrol_On_Goal_Cell_Still_Gets_PastGoalTag 가 이미 고정한다.
    public class GoalSiegeGateTests
    {
        private World _world;
        private EntityManager _em;
        private SimulationSystemGroup _simGroup;
        private Entity _fieldEntity = Entity.Null;

        [SetUp]
        public void SetUp()
        {
            _world = new World("GoalSiegeGateTestWorld");
            _em = _world.EntityManager;
            _simGroup = _world.CreateSystemManaged<SimulationSystemGroup>();
            _simGroup.AddSystemToUpdateList(_world.CreateSystem<MovementSystem>());
            CreateLinearFlowField();
        }

        [TearDown]
        public void TearDown()
        {
            if (_fieldEntity != Entity.Null && _em.Exists(_fieldEntity)
                && _em.HasComponent<FlowFieldSingleton>(_fieldEntity))
                _em.GetComponentData<FlowFieldSingleton>(_fieldEntity).Dispose();
            _world?.Dispose();
        }

        private void Tick(float dt = 0.016f)
        {
            _world.SetTime(new TimeData(_world.Time.ElapsedTime + dt, dt));
            _simGroup.Update();
        }

        // 5x1 직선. goal = (4,0).
        private void CreateLinearFlowField(int width = 5)
        {
            int n = width;
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
                tileSize = 1f, version = 1,
            });
        }

        private Entity CreateEnemyOnGoalCell()
        {
            var enemy = _em.CreateEntity();
            _em.AddComponentData(enemy, LocalTransform.FromPosition(new float3(4f, 0f, 0f)));
            _em.AddComponentData(enemy, new PathFollowState { speed = 1f });
            return enemy;
        }

        private Entity CreateGoalEntity(float m = 30f)
        {
            var goal = _em.CreateEntity();
            _em.AddComponentData(goal, new GoalPoint { cell = new int2(4, 0), goalIndex = 0 });
            _em.AddComponentData(goal, new Health { value = m, max = m });
            _em.AddComponentData(goal, new FactionTag { value = Faction.Goal });
            _em.AddComponentData(goal, LocalTransform.FromPosition(new float3(4f, 0f, 0f)));
            return goal;
        }

        [Test]
        public void Enemy_On_AliveGoalCell_DoesNotLeak()
        {
            CreateGoalEntity();
            var enemy = CreateEnemyOnGoalCell();

            Tick();

            Assert.IsFalse(_em.HasComponent<PastGoalTag>(enemy),
                "살아있는 골 셀에서는 유출(PastGoalTag)이 봉인돼야 한다 (공성)");
        }

        [Test]
        public void Enemy_On_GoalCell_WithDeadGoal_Leaks_SameFrame()
        {
            // DeadTag 가 붙은 프레임부터 게이트가 열린다 (HealthDeath → 파괴 사이 1프레임 지연 없음).
            var goal = CreateGoalEntity();
            _em.AddComponent<DeadTag>(goal);
            var enemy = CreateEnemyOnGoalCell();

            Tick();

            Assert.IsTrue(_em.HasComponent<PastGoalTag>(enemy),
                "붕괴(DeadTag)한 골은 즉시 현행 유출 지점으로 복귀해야 한다");
        }

        [Test]
        public void Enemy_On_GoalCell_AfterGoalDestroyed_Leaks()
        {
            var goal = CreateGoalEntity();
            var enemy = CreateEnemyOnGoalCell();

            Tick();
            Assert.IsFalse(_em.HasComponent<PastGoalTag>(enemy), "pre-condition: 공성 중");

            _em.DestroyEntity(goal);
            Tick();

            Assert.IsTrue(_em.HasComponent<PastGoalTag>(enemy),
                "골 엔티티 파괴 후 공성하던 적은 다음 틱부터 유출돼야 한다");
        }

        [Test]
        public void Enemy_On_OtherGoalCell_StillLeaks_WhenDifferentGoalAlive()
        {
            // 멀티골: 살아있는 골은 (0,0) 쪽이 아니라 다른 셀 — (4,0) 도달 적은 기존대로 유출.
            var goal = _em.CreateEntity();
            _em.AddComponentData(goal, new GoalPoint { cell = new int2(0, 0), goalIndex = 1 });
            _em.AddComponentData(goal, new Health { value = 30f, max = 30f });
            _em.AddComponentData(goal, new FactionTag { value = Faction.Goal });
            _em.AddComponentData(goal, LocalTransform.FromPosition(new float3(0f, 0f, 0f)));

            var enemy = CreateEnemyOnGoalCell();

            Tick();

            Assert.IsTrue(_em.HasComponent<PastGoalTag>(enemy),
                "공성 게이트는 셀 단위다 — 다른 골이 살아있어도 이 골(M=0/부재)은 유출");
        }
    }
}
