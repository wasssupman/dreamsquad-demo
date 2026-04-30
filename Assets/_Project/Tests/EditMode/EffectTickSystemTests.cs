using NUnit.Framework;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
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
        public void Destroys_Expired_TornadoField_Entity()
        {
            var e = _em.CreateEntity();
            _em.AddComponentData(e, new TornadoField
            {
                centerWorld = float3.zero,
                radius = 2f,
                pullSpeed = 1f,
                remaining = 0.1f,
            });

            Tick(0.5f);

            Assert.IsFalse(_em.Exists(e), "Expired TornadoField carrier entity should be destroyed.");
        }

        [Test]
        public void Keeps_Alive_TornadoField_With_Remaining_Time()
        {
            var e = _em.CreateEntity();
            _em.AddComponentData(e, new TornadoField
            {
                centerWorld = float3.zero,
                radius = 2f,
                pullSpeed = 1f,
                remaining = 2f,
            });

            Tick(0.5f);

            Assert.IsTrue(_em.Exists(e), "Non-expired TornadoField should still exist.");
            Assert.AreEqual(1.5f, _em.GetComponentData<TornadoField>(e).remaining, 1e-5f,
                "TornadoField.remaining should decrease by DeltaTime.");
        }

        [Test]
        public void EffectSpawner_ApplySlow_Writes_CcEffect_Buffer()
        {
            var e = _em.CreateEntity();
            EffectSpawner.ApplySlow(_em, e, duration: 4f, multiplier: 0.5f);

            Assert.IsTrue(_em.HasBuffer<CcEffect>(e));
            var buf = _em.GetBuffer<CcEffect>(e);
            Assert.AreEqual(1, buf.Length);
            Assert.AreEqual(CcKind.Slow, buf[0].kind);
            Assert.AreEqual(0.5f, buf[0].scalar, 1e-5f);
            Assert.AreEqual(4f, buf[0].remainingTime, 1e-5f);
        }

        [Test]
        public void EffectSpawner_ApplySlow_Refreshes_Longer_Remaining()
        {
            var e = _em.CreateEntity();
            EffectSpawner.ApplySlow(_em, e, duration: 4f, multiplier: 0.5f);
            EffectSpawner.ApplySlow(_em, e, duration: 2f, multiplier: 0.25f);

            var buf = _em.GetBuffer<CcEffect>(e);
            Assert.AreEqual(1, buf.Length);
            Assert.AreEqual(4f, buf[0].remainingTime, 1e-5f, "Longer existing remaining should be retained.");
            Assert.AreEqual(0.25f, buf[0].scalar, 1e-5f, "Latest multiplier should win.");
        }
    }
}
