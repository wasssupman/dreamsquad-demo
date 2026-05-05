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
                tileRange = 2,
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
                tileRange = 2,
                pullSpeed = 1f,
                remaining = 2f,
            });

            Tick(0.5f);

            Assert.IsTrue(_em.Exists(e), "Non-expired TornadoField should still exist.");
            Assert.AreEqual(1.5f, _em.GetComponentData<TornadoField>(e).remaining, 1e-5f,
                "TornadoField.remaining should decrease by DeltaTime.");
        }

    }
}
