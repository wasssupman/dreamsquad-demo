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
            _world?.Dispose();
        }

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
        }

        [Test]
        public void Destroys_Unit_After_Enqueue()
        {
            CreateSingletonEntity();
            var unit = CreateUnitAtGoal();

            Tick();

            Assert.IsFalse(_em.Exists(unit),
                "UnitLifecycleSystem must destroy the entity after enqueuing the event");
        }

        [Test]
        public void Does_Not_Enqueue_When_Singleton_Absent()
        {
            // No singleton created — fail-open path.
            var unit = CreateUnitAtGoal();

            // Must not throw, and unit must still be destroyed.
            Assert.DoesNotThrow(() => Tick());
            Assert.IsFalse(_em.Exists(unit),
                "entity must still be destroyed even when singleton is absent");
        }
    }
}
