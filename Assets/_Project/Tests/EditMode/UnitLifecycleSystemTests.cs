using NUnit.Framework;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Wassup.Battle.Movement;
using Wassup.Battle.Units;

namespace Wassup.Tests.EditMode
{
    public class UnitLifecycleSystemTests
    {
        private World _world;
        private EntityManager _em;
        private SimulationSystemGroup _simGroup;

        [SetUp]
        public void SetUp()
        {
            _world = new World("UnitLifecycleTestWorld");
            _em = _world.EntityManager;

            _simGroup = _world.CreateSystemManaged<SimulationSystemGroup>();
            var movementHandle = _world.CreateSystem<MovementSystem>();
            var lifecycleHandle = _world.CreateSystem<UnitLifecycleSystem>();
            _simGroup.AddSystemToUpdateList(movementHandle);
            _simGroup.AddSystemToUpdateList(lifecycleHandle);
        }

        [TearDown]
        public void TearDown()
        {
            // Dispose the NativeQueue before world teardown to avoid leak warnings.
            var query = _em.CreateEntityQuery(ComponentType.ReadWrite<GoalReachedEventsSingleton>());
            if (query.CalculateEntityCount() > 0)
            {
                var s = query.GetSingleton<GoalReachedEventsSingleton>();
                if (s.queue.IsCreated) s.queue.Dispose();
            }
            query.Dispose();
            var collapsedQuery = _em.CreateEntityQuery(ComponentType.ReadWrite<GoalCollapsedEventsSingleton>());
            if (collapsedQuery.CalculateEntityCount() > 0)
            {
                var s = collapsedQuery.GetSingleton<GoalCollapsedEventsSingleton>();
                if (s.queue.IsCreated) s.queue.Dispose();
            }
            collapsedQuery.Dispose();
            _world?.Dispose();
        }

        // goal-tower-siege unit 1 — AttackState 가 **없는** 적(Runner·Swift 같은 돌격형).
        // 골에 붙어도 아무것도 못 하므로 기존대로 파괴된다.
        private Entity CreateUnitAtGoal()
        {
            var e = _em.CreateEntity();
            _em.AddComponentData(e, LocalTransform.FromPosition(new float3(0f, 0f, 0f)));
            _em.AddComponent<AttackUnitTag>(e);
            _em.AddComponent<PastGoalTag>(e);
            // Phase 9: PathFollowState 축소. PathWaypoint DynamicBuffer 제거.
            // PastGoalTag 이미 있으므로 MovementSystem 의 .WithNone<PastGoalTag>() 에 의해 필터됨.
            _em.AddComponentData(e, new PathFollowState { speed = 1f });
            return e;
        }

        // 공격 수단이 있는 적 — 골에 도달해도 살아남아 타워를 때린다.
        private Entity CreateSiegeUnitAtGoal()
        {
            var e = CreateUnitAtGoal();
            _em.AddComponentData(e, new Wassup.Battle.Combat.AttackState
            {
                range = 1f,
                cooldownDuration = 1f,
                targetMask = (int)Faction.Defender,
                attackTargetCount = 1,
            });
            return e;
        }

        private Entity CreateSingletonEntity()
        {
            var singletonEntity = _em.CreateEntity();
            _em.AddComponentData(singletonEntity, new GoalReachedEventsSingleton
            {
                queue = new NativeQueue<GoalReachedEvent>(Allocator.Persistent)
            });
            return singletonEntity;
        }

        private void Tick()
        {
            _world.SetTime(new TimeData(
                elapsedTime: _world.Time.ElapsedTime + 0.1f,
                deltaTime: 0.1f));
            _simGroup.Update();
        }

        [Test]
        public void Enqueues_GoalReachedEvent_When_Singleton_Present()
        {
            CreateSingletonEntity();
            var unit = CreateUnitAtGoal();

            Tick();

            using var q = _em.CreateEntityQuery(ComponentType.ReadWrite<GoalReachedEventsSingleton>());
            var singleton = q.GetSingleton<GoalReachedEventsSingleton>();
            Assert.AreEqual(1, singleton.queue.Count,
                "should enqueue exactly one event for the unit that reached the goal");

            var evt = singleton.queue.Dequeue();
            Assert.AreEqual(unit, evt.entity,
                "enqueued entity must match the unit that reached the goal");
            Assert.IsFalse(evt.canSiege,
                "AttackState 가 없으면 canSiege=false — 브리지가 자폭 경로로 보낸다");
        }

        // goal-tower-siege unit 1 — 공격 수단이 없는 적만 파괴한다.
        [Test]
        public void Destroys_AttacklessUnit_After_Enqueue()
        {
            CreateSingletonEntity();
            var unit = CreateUnitAtGoal();

            Tick();

            Assert.IsFalse(_em.Exists(unit),
                "AttackState 가 없는 적은 골에 붙어도 아무것도 못 하므로 기존대로 파괴된다");
        }

        // goal-tower-siege unit 1 — 공격 수단이 있는 적은 **살아남아 공성한다.**
        [Test]
        public void KeepsSiegeUnitAlive_AndMarksItOnce()
        {
            CreateSingletonEntity();
            var unit = CreateSiegeUnitAtGoal();

            Tick();

            Assert.IsTrue(_em.Exists(unit), "공성 가능한 적은 골 도달로 파괴되지 않는다");
            Assert.IsTrue(_em.HasComponent<GoalReachedMarker>(unit), "재발화 방지 마커가 붙는다");

            using var q = _em.CreateEntityQuery(ComponentType.ReadWrite<GoalReachedEventsSingleton>());
            var singleton = q.GetSingleton<GoalReachedEventsSingleton>();
            Assert.AreEqual(1, singleton.queue.Count, "첫 틱에 1회 발화");
            var evt = singleton.queue.Dequeue();
            Assert.IsTrue(evt.canSiege, "공성 가능 플래그가 실려야 브리지가 뷰를 지우지 않는다");

            // PastGoalTag 는 영구히 남으므로, 마커가 없으면 매 틱 재발화한다.
            for (int i = 0; i < 3; i++) Tick();
            Assert.AreEqual(0, singleton.queue.Count, "마커 이후로는 재발화하지 않는다");
        }

        [Test]
        public void Does_Not_Enqueue_When_Singleton_Absent()
        {
            // No singleton created — fail-open path.
            var unit = CreateUnitAtGoal();

            // Must not throw, and unit must still be destroyed.
            Assert.DoesNotThrow(() => Tick());
            Assert.IsFalse(_em.Exists(unit),
                "싱글턴이 없어도(fail-open) 공격 수단 없는 적은 파괴된다");
        }

        // battle-structures unit 0 — goal-stability 의 골 붕괴 테스트 2개를 제거했다.
        // 검증 대상인 «골 사망 루프» 가 삭제됐다(GoalPoint 엔티티는 어떤 맵에서도 스폰되지
        // 않아 그 루프는 한 번도 발화하지 않았다). 거점 단위 붕괴는 unit 4 가 새로 짓고,
        // 그때 이 계약을 거점 아키타입으로 다시 세운다.
    }
}
