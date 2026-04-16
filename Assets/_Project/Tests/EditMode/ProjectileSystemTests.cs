using NUnit.Framework;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Wassup.Battle.Combat.Projectile;
using Wassup.Battle.Units;

namespace Wassup.Tests.EditMode
{
    public class ProjectileSystemTests
    {
        private World _world;
        private EntityManager _em;
        private SimulationSystemGroup _simGroup;

        [SetUp]
        public void SetUp()
        {
            _world = new World("ProjectileSystemTestWorld");
            _em = _world.EntityManager;
            _simGroup = _world.CreateSystemManaged<SimulationSystemGroup>();
            _simGroup.AddSystemToUpdateList(_world.CreateSystem<ProjectileMoveSystem>());
            _simGroup.AddSystemToUpdateList(_world.CreateSystem<ProjectileHitSystem>());
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

        private Entity MakeTarget(float3 pos)
        {
            var e = _em.CreateEntity();
            _em.AddComponentData(e, LocalTransform.FromPosition(pos));
            _em.AddComponent<AttackUnitTag>(e);
            _em.AddComponentData(e, new Health { value = 100, max = 100 });
            _em.AddBuffer<IncomingDamage>(e);
            return e;
        }

        private Entity MakeProjectile(float3 origin, Entity target, float speed, float damage, float hitThreshold)
        {
            var e = _em.CreateEntity();
            _em.AddComponent<ProjectileTag>(e);
            _em.AddComponentData(e, LocalTransform.FromPosition(origin));
            _em.AddComponentData(e, new ProjectileState
            {
                target = target,
                speed = speed,
                damage = damage,
                hitThreshold = hitThreshold,
            });
            return e;
        }

        [Test]
        public void Move_Advances_Toward_Target_At_Configured_Speed()
        {
            var target = MakeTarget(new float3(10f, 0f, 0f));
            var proj = MakeProjectile(new float3(0f, 0f, 0f), target, speed: 5f, damage: 10f, hitThreshold: 0.1f);

            Tick(1f); // move 5 units toward (10,0,0)

            var pos = _em.GetComponentData<LocalTransform>(proj).Position;
            Assert.AreEqual(5f, pos.x, 1e-3f);
            Assert.IsTrue(_em.Exists(proj), "projectile must survive when target is far");
        }

        [Test]
        public void Move_Destroys_Projectile_When_Target_Missing()
        {
            var target = MakeTarget(new float3(10f, 0f, 0f));
            var proj = MakeProjectile(new float3(0f, 0f, 0f), target, speed: 5f, damage: 10f, hitThreshold: 0.1f);

            _em.DestroyEntity(target);

            Tick(0.1f);

            Assert.IsFalse(_em.Exists(proj), "projectile must self-destroy when its target no longer exists");
        }

        [Test]
        public void Hit_Appends_IncomingDamage_And_Destroys_Projectile_When_In_Range()
        {
            var target = MakeTarget(new float3(0.1f, 0f, 0f)); // within threshold from origin
            var proj = MakeProjectile(new float3(0f, 0f, 0f), target, speed: 100f, damage: 42f, hitThreshold: 0.5f);

            Tick(0.016f);

            Assert.IsFalse(_em.Exists(proj), "projectile must be destroyed on hit");
            var buffer = _em.GetBuffer<IncomingDamage>(target);
            Assert.AreEqual(1, buffer.Length);
            Assert.AreEqual(42f, buffer[0].amount, 1e-3f);
        }

        [Test]
        public void Hit_Skips_When_Outside_Threshold()
        {
            var target = MakeTarget(new float3(5f, 0f, 0f));
            var proj = MakeProjectile(new float3(0f, 0f, 0f), target, speed: 0f, damage: 10f, hitThreshold: 0.1f);

            Tick(0.016f);

            Assert.IsTrue(_em.Exists(proj), "projectile must keep flying when not yet in range");
            var buffer = _em.GetBuffer<IncomingDamage>(target);
            Assert.AreEqual(0, buffer.Length, "no damage should have been applied");
        }
    }
}
