using NUnit.Framework;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Wassup.Battle.Effects;
using Wassup.Battle.Units;

namespace Wassup.Tests.EditMode
{
    public class HazardDestroyedEventTests
    {
        private World _world;
        private EntityManager _em;
        private SimulationSystemGroup _simGroup;
        private NativeQueue<HazardDestroyedEvent> _queue;

        [SetUp]
        public void SetUp()
        {
            _world = new World("HazardDestroyedEventTestWorld");
            _em = _world.EntityManager;
            _simGroup = _world.CreateSystemManaged<SimulationSystemGroup>();
            _simGroup.AddSystemToUpdateList(_world.CreateSystem<DamageApplicationSystem>());
            _simGroup.AddSystemToUpdateList(_world.CreateSystem<HealthDeathSystem>());
            _simGroup.AddSystemToUpdateList(_world.CreateSystem<UnitLifecycleSystem>());
        }

        [TearDown]
        public void TearDown()
        {
            if (_queue.IsCreated) _queue.Dispose();
            _world?.Dispose();
        }

        private void Tick()
        {
            _world.SetTime(new TimeData(_world.Time.ElapsedTime + 0.016f, 0.016f));
            _simGroup.Update();
        }

        private void CreateSink()
        {
            _queue = new NativeQueue<HazardDestroyedEvent>(Allocator.Persistent);
            var singleton = _em.CreateEntity();
            _em.AddComponentData(singleton, new HazardDestroyedEventsSingleton { queue = _queue });
        }

        private Entity CreateDeadHazard(int soIndex, int2 centerCell, float3 worldPosition)
        {
            var e = _em.CreateEntity();
            _em.AddComponentData(e, new BlockingHazard { hazardSoIndex = soIndex, maxHp = 25f });
            _em.AddComponentData(e, new Obstacle
            {
                cell = centerCell,
                worldPosition = worldPosition,
                remainingLife = float.PositiveInfinity,
            });
            _em.AddComponentData(e, LocalTransform.FromPosition(worldPosition));
            _em.AddComponent<DeadTag>(e);
            return e;
        }

        private Entity CreateHazardWithHealth(int soIndex, int2 centerCell, float3 worldPosition, float hp)
        {
            var e = _em.CreateEntity();
            _em.AddComponentData(e, new BlockingHazard { hazardSoIndex = soIndex, maxHp = 25f });
            _em.AddComponentData(e, new Obstacle
            {
                cell = centerCell,
                worldPosition = worldPosition,
                remainingLife = float.PositiveInfinity,
            });
            _em.AddComponentData(e, LocalTransform.FromPosition(worldPosition));
            _em.AddComponentData(e, new Health { value = hp, max = 25f });
            return e;
        }

        [Test]
        public void Dead_BlockingHazard_Enqueues_Event_Before_Destroy()
        {
            CreateSink();
            var hazard = CreateDeadHazard(7, new int2(3, 4), new float3(3.5f, 0f, 4.5f));

            Tick();

            Assert.IsFalse(_em.Exists(hazard));
            Assert.AreEqual(1, _queue.Count);

            var evt = _queue.Dequeue();
            Assert.AreEqual(hazard, evt.hazardEntity);
            Assert.AreEqual(7, evt.hazardSoIndex);
            Assert.AreEqual(new int2(3, 4), evt.centerCell);
            Assert.AreEqual(new float3(3.5f, 0f, 4.5f), evt.worldPosition);
        }

        [Test]
        public void Dead_BlockingHazard_Destroyed_When_Sink_Missing()
        {
            var hazard = CreateDeadHazard(2, new int2(1, 1), new float3(1.5f, 0f, 1.5f));

            Assert.DoesNotThrow(Tick);

            Assert.IsFalse(_em.Exists(hazard));
        }

        [Test]
        public void Dead_BlockingHazard_Does_Not_Use_CatchAll_Dead_Branch()
        {
            CreateSink();
            CreateDeadHazard(4, new int2(2, 2), new float3(2.5f, 0f, 2.5f));

            Tick();

            Assert.AreEqual(1, _queue.Count);
        }

        [Test]
        public void ZeroHp_BlockingHazard_Enqueues_Event_And_Destroy()
        {
            CreateSink();
            var hazard = CreateHazardWithHealth(5, new int2(6, 7), new float3(6.5f, 0f, 7.5f), 0f);

            Tick();

            Assert.IsFalse(_em.Exists(hazard));
            Assert.AreEqual(1, _queue.Count);
            var evt = _queue.Dequeue();
            Assert.AreEqual(hazard, evt.hazardEntity);
            Assert.AreEqual(5, evt.hazardSoIndex);
            Assert.AreEqual(new int2(6, 7), evt.centerCell);
        }
    }
}
