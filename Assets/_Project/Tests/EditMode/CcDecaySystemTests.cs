using NUnit.Framework;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Wassup.Battle.Effects;

namespace Wassup.Tests.EditMode
{
    public class CcDecaySystemTests
    {
        private World _world;
        private EntityManager _em;
        private SimulationSystemGroup _simGroup;

        [SetUp]
        public void SetUp()
        {
            _world = new World("CcDecayTestWorld");
            _em = _world.EntityManager;
            _simGroup = _world.CreateSystemManaged<SimulationSystemGroup>();
            var handle = _world.CreateSystem<CcDecaySystem>();
            _simGroup.AddSystemToUpdateList(handle);
        }

        [TearDown]
        public void TearDown()
        {
            _world?.Dispose();
        }

        private void Tick(float dt)
        {
            _world.SetTime(new TimeData(_world.Time.ElapsedTime + dt, dt));
            _simGroup.Update();
        }

        [Test]
        public void Decrements_RemainingTime_By_DeltaTime()
        {
            var e = _em.CreateEntity();
            var buf = _em.AddBuffer<CcEffect>(e);
            buf.Add(new CcEffect { kind = CcKind.Slow, scalar = 0.5f, remainingTime = 2f });

            Tick(0.5f);

            var result = _em.GetBuffer<CcEffect>(e);
            Assert.AreEqual(1, result.Length);
            Assert.AreEqual(1.5f, result[0].remainingTime, 1e-5f);
        }

        [Test]
        public void Removes_Expired_Entry()
        {
            var e = _em.CreateEntity();
            var buf = _em.AddBuffer<CcEffect>(e);
            buf.Add(new CcEffect { kind = CcKind.Slow, scalar = 0.5f, remainingTime = 0.25f });

            Tick(1f);

            var result = _em.GetBuffer<CcEffect>(e);
            Assert.AreEqual(0, result.Length);
        }

        [Test]
        public void Preserves_Other_Entries_When_One_Expires()
        {
            var e = _em.CreateEntity();
            var buf = _em.AddBuffer<CcEffect>(e);
            buf.Add(new CcEffect { kind = CcKind.Slow, scalar = 0.5f, remainingTime = 0.1f });
            buf.Add(new CcEffect { kind = CcKind.Impulse, vector = new float3(1, 0, 0), remainingTime = 5f });

            Tick(1f);

            var result = _em.GetBuffer<CcEffect>(e);
            Assert.AreEqual(1, result.Length);
            Assert.AreEqual(CcKind.Impulse, result[0].kind);
        }
    }
}
