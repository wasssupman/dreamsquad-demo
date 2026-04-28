using NUnit.Framework;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Wassup.Battle.Effects;

namespace Wassup.Tests.EditMode
{
    public class CcApplySystemTests
    {
        private World _world;
        private EntityManager _em;
        private SimulationSystemGroup _simGroup;
        private NativeQueue<EnemyCcEvent> _queue;

        [SetUp]
        public void SetUp()
        {
            _world = new World("CcApplyTestWorld");
            _em = _world.EntityManager;
            _simGroup = _world.CreateSystemManaged<SimulationSystemGroup>();
            var handle = _world.CreateSystem<CcApplySystem>();
            _simGroup.AddSystemToUpdateList(handle);

            _queue = new NativeQueue<EnemyCcEvent>(Allocator.Persistent);
            var singletonEntity = _em.CreateEntity();
            _em.AddComponentData(singletonEntity, new EnemyCcEventsSingleton { queue = _queue });
        }

        [TearDown]
        public void TearDown()
        {
            if (_queue.IsCreated) _queue.Dispose();
            _world?.Dispose();
        }

        private void Tick(float dt = 0.016f)
        {
            _world.SetTime(new TimeData(_world.Time.ElapsedTime + dt, dt));
            _simGroup.Update();
        }

        [Test]
        public void Creates_Buffer_And_Adds_Entry_When_None_Exists()
        {
            var e = _em.CreateEntity();
            _queue.Enqueue(new EnemyCcEvent
            {
                target = e,
                effect = new CcEffect { kind = CcKind.Slow, scalar = 0.5f, remainingTime = 2f }
            });

            Tick();

            Assert.IsTrue(_em.HasBuffer<CcEffect>(e));
            var buf = _em.GetBuffer<CcEffect>(e);
            Assert.AreEqual(1, buf.Length);
            Assert.AreEqual(CcKind.Slow, buf[0].kind);
        }

        [Test]
        public void Merges_Same_Kind_Taking_Max_RemainingTime_And_New_Scalar()
        {
            var e = _em.CreateEntity();
            var buf = _em.AddBuffer<CcEffect>(e);
            buf.Add(new CcEffect { kind = CcKind.Slow, scalar = 0.5f, remainingTime = 3f });

            _queue.Enqueue(new EnemyCcEvent
            {
                target = e,
                effect = new CcEffect { kind = CcKind.Slow, scalar = 0.25f, remainingTime = 1f }
            });

            Tick();

            var result = _em.GetBuffer<CcEffect>(e);
            Assert.AreEqual(1, result.Length);
            Assert.AreEqual(3f, result[0].remainingTime, 1e-5f, "longer existing remaining should win");
            Assert.AreEqual(0.25f, result[0].scalar, 1e-5f, "new scalar should overwrite");
        }

        [Test]
        public void Different_Kinds_Accumulate_As_Separate_Entries()
        {
            var e = _em.CreateEntity();
            _em.AddBuffer<CcEffect>(e);

            _queue.Enqueue(new EnemyCcEvent
            {
                target = e,
                effect = new CcEffect { kind = CcKind.Slow, scalar = 0.5f, remainingTime = 2f }
            });
            _queue.Enqueue(new EnemyCcEvent
            {
                target = e,
                effect = new CcEffect { kind = CcKind.Impulse, vector = new float3(1, 0, 0), remainingTime = 0.5f }
            });

            Tick();

            var result = _em.GetBuffer<CcEffect>(e);
            Assert.AreEqual(2, result.Length);
        }
    }
}
