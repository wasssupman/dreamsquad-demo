using NUnit.Framework;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Wassup.Battle.Combat.Projectile;
using Wassup.Battle.Units;
using Wassup.Data;

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
        public void Hit_Splash_Damages_Neighbors_Excluding_Direct_Target_And_Non_AttackUnit()
        {
            var direct = MakeTarget(new float3(0f, 0f, 0f));
            var nearbyAttacker = MakeTarget(new float3(0.4f, 0f, 0f));      // within splash
            var farAttacker = MakeTarget(new float3(3f, 0f, 0f));           // outside splash
            // Non-attack-unit entity inside splash radius — must not receive damage.
            var decoy = _em.CreateEntity();
            _em.AddComponentData(decoy, LocalTransform.FromPosition(new float3(0.3f, 0f, 0f)));
            _em.AddBuffer<IncomingDamage>(decoy);

            var proj = _em.CreateEntity();
            _em.AddComponent<ProjectileTag>(proj);
            _em.AddComponentData(proj, LocalTransform.FromPosition(new float3(0f, 0f, 0f)));
            _em.AddComponentData(proj, new ProjectileState
            {
                target = direct,
                speed = 0f,
                damage = 100f,
                hitThreshold = 0.1f,
                onHitEffect = OnHitEffectType.Splash,
                splashRadius = 1.0f,
                splashDamageMul = 0.5f,
            });

            Tick(0.016f);

            Assert.IsFalse(_em.Exists(proj), "projectile should consume itself on hit");
            // Direct target: full 100 damage (splash loop excludes it).
            // Rather than read Health (which DamageApplicationSystem would drain),
            // we skip that system in SetUp — so IncomingDamage buffer is the source of truth.
            var directBuf = _em.GetBuffer<IncomingDamage>(direct);
            var nearbyBuf = _em.GetBuffer<IncomingDamage>(nearbyAttacker);
            var farBuf = _em.GetBuffer<IncomingDamage>(farAttacker);
            var decoyBuf = _em.GetBuffer<IncomingDamage>(decoy);
            Assert.AreEqual(1, directBuf.Length, "direct target gets one damage event");
            Assert.AreEqual(100f, directBuf[0].amount, 1e-4f);
            Assert.AreEqual(1, nearbyBuf.Length, "neighbor within radius gets splash");
            Assert.AreEqual(50f, nearbyBuf[0].amount, 1e-4f, "splash damage = damage * splashDamageMul");
            Assert.AreEqual(0, farBuf.Length, "attacker outside splashRadius is untouched");
            Assert.AreEqual(0, decoyBuf.Length, "non-AttackUnit entity must be filtered from splash pool");
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

        // Directly exercises the trajectory/payload seam introduced in
        // projectile-trajectory-payload unit 1: a projectile flies for several
        // frames, arrives on a *later* frame (MoveSystem sets impactReached), and
        // ProjectileHitSystem resolves it that same frame. The other hit tests all
        // resolve on frame 1 or never arrive, so none pin down this hand-off.
        [Test]
        public void Move_Then_Arrives_And_Resolves_On_A_Later_Frame()
        {
            var target = MakeTarget(new float3(10f, 0f, 0f));
            var proj = MakeProjectile(new float3(0f, 0f, 0f), target, speed: 4f, damage: 25f, hitThreshold: 0.5f);

            Tick(1f); // x: 0 → 4, not yet in range
            Assert.IsTrue(_em.Exists(proj), "still flying after frame 1");
            Assert.AreEqual(0, _em.GetBuffer<IncomingDamage>(target).Length, "no damage mid-flight (frame 1)");

            Tick(1f); // x: 4 → 8, still not in range
            Assert.IsTrue(_em.Exists(proj), "still flying after frame 2");
            Assert.AreEqual(0, _em.GetBuffer<IncomingDamage>(target).Length, "no damage mid-flight (frame 2)");

            Tick(1f); // dist 2 <= step 4 → snap to target → arrives → resolves this frame
            Assert.IsFalse(_em.Exists(proj), "projectile arrives and is consumed on a later frame");
            var buffer = _em.GetBuffer<IncomingDamage>(target);
            Assert.AreEqual(1, buffer.Length, "damage applied on the arrival frame");
            Assert.AreEqual(25f, buffer[0].amount, 1e-3f);
        }
    }
}
