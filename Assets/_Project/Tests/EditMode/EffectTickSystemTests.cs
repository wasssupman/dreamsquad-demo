using NUnit.Framework;
using Unity.Core;
using Unity.Entities;
using Wassup.Battle.Effects;

namespace Wassup.Tests.EditMode
{
    public class EffectTickSystemTests
    {
        private World _world;
        private EntityManager _em;
        private SimulationSystemGroup _simGroup;

        [SetUp]
        public void SetUp()
        {
            _world = new World("EffectTickSystemTestWorld");
            _em = _world.EntityManager;
            _simGroup = _world.CreateSystemManaged<SimulationSystemGroup>();
            var handle = _world.CreateSystem<EffectTickSystem>();
            _simGroup.AddSystemToUpdateList(handle);
        }

        [TearDown]
        public void TearDown()
        {
            _world?.Dispose();
        }

        private void Tick(float deltaTime)
        {
            _world.SetTime(new TimeData(_world.Time.ElapsedTime + deltaTime, deltaTime));
            _simGroup.Update();
        }

        [Test]
        public void Decrements_SlowEffect_Remaining_By_DeltaTime()
        {
            var e = _em.CreateEntity();
            _em.AddComponentData(e, new SlowEffect { remaining = 2f, multiplier = 0.5f });

            Tick(0.5f);

            Assert.IsTrue(_em.HasComponent<SlowEffect>(e));
            var remaining = _em.GetComponentData<SlowEffect>(e).remaining;
            Assert.AreEqual(1.5f, remaining, 1e-5f);
        }

        [Test]
        public void Removes_Expired_SlowEffect()
        {
            var e = _em.CreateEntity();
            _em.AddComponentData(e, new SlowEffect { remaining = 0.25f, multiplier = 0.5f });

            Tick(1f);

            Assert.IsFalse(_em.HasComponent<SlowEffect>(e));
        }

        [Test]
        public void Removes_Expired_DamageBoost_And_CooldownReduction_Independently()
        {
            var e = _em.CreateEntity();
            _em.AddComponentData(e, new DamageBoost { remaining = 0.1f, multiplier = 2f });
            _em.AddComponentData(e, new CooldownReduction { remaining = 1f, multiplier = 0.5f });

            Tick(0.5f);

            Assert.IsFalse(_em.HasComponent<DamageBoost>(e));
            Assert.IsTrue(_em.HasComponent<CooldownReduction>(e));
            var cr = _em.GetComponentData<CooldownReduction>(e).remaining;
            Assert.AreEqual(0.5f, cr, 1e-5f);
        }

        [Test]
        public void EffectSpawner_Apply_Slow_Refreshes_Longer_Remaining()
        {
            var e = _em.CreateEntity();
            EffectSpawner.ApplySlow(_em, e, duration: 4f, multiplier: 0.5f);
            EffectSpawner.ApplySlow(_em, e, duration: 2f, multiplier: 0.25f); // shorter duration, new multiplier

            var slow = _em.GetComponentData<SlowEffect>(e);
            Assert.AreEqual(4f, slow.remaining, 1e-5f, "Longer existing remaining should be retained.");
            Assert.AreEqual(0.25f, slow.multiplier, 1e-5f, "Latest multiplier should win.");
        }
    }
}
