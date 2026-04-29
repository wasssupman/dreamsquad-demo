using NUnit.Framework;
using Unity.Core;
using Unity.Entities;
using Wassup.Battle.Effects;
using Wassup.Battle.Units;

namespace Wassup.Tests.EditMode
{
    public class DotApplySystemTests
    {
        private World _world;
        private EntityManager _em;
        private SimulationSystemGroup _simGroup;

        [SetUp]
        public void SetUp()
        {
            _world = new World("DotApplyTestWorld");
            _em = _world.EntityManager;
            _simGroup = _world.CreateSystemManaged<SimulationSystemGroup>();
            var handle = _world.CreateSystem<DotApplySystem>();
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
        public void Dot_Adds_Damage_Per_Second_Times_DeltaTime()
        {
            var e = _em.CreateEntity();
            _em.AddBuffer<CcEffect>(e);
            _em.AddBuffer<IncomingDamage>(e);
            var cc = _em.GetBuffer<CcEffect>(e);
            cc.Add(new CcEffect { kind = CcKind.DoT, scalar = 10f, remainingTime = 1f });

            Tick(0.25f);

            var damage = _em.GetBuffer<IncomingDamage>(e);
            Assert.AreEqual(1, damage.Length);
            Assert.AreEqual(2.5f, damage[0].amount, 1e-5f);
        }

        [Test]
        public void Non_Dot_Cc_Does_Not_Add_Damage()
        {
            var e = _em.CreateEntity();
            _em.AddBuffer<CcEffect>(e);
            _em.AddBuffer<IncomingDamage>(e);
            var cc = _em.GetBuffer<CcEffect>(e);
            cc.Add(new CcEffect { kind = CcKind.Slow, scalar = 0.4f, remainingTime = 1f });
            cc.Add(new CcEffect { kind = CcKind.Impulse, remainingTime = 1f });

            Tick(0.25f);

            var damage = _em.GetBuffer<IncomingDamage>(e);
            Assert.AreEqual(0, damage.Length);
        }

        [Test]
        public void Multiple_Dot_Entries_All_Contribute()
        {
            var e = _em.CreateEntity();
            _em.AddBuffer<CcEffect>(e);
            _em.AddBuffer<IncomingDamage>(e);
            var cc = _em.GetBuffer<CcEffect>(e);
            cc.Add(new CcEffect { kind = CcKind.DoT, scalar = 10f, remainingTime = 1f });
            cc.Add(new CcEffect { kind = CcKind.DoT, scalar = 20f, remainingTime = 1f });

            Tick(0.1f);

            var damage = _em.GetBuffer<IncomingDamage>(e);
            Assert.AreEqual(2, damage.Length);
            Assert.AreEqual(1f, damage[0].amount, 1e-5f);
            Assert.AreEqual(2f, damage[1].amount, 1e-5f);
        }
    }
}
