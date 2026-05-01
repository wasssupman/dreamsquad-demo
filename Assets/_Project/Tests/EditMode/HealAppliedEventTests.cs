using NUnit.Framework;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Wassup.Battle.Units;

namespace Wassup.Tests.EditMode
{
    public class HealAppliedEventTests
    {
        private World _world;
        private EntityManager _em;
        private SimulationSystemGroup _simGroup;
        private NativeQueue<HealAppliedEvent> _queue;

        [SetUp]
        public void SetUp()
        {
            _world = new World("HealAppliedEventTests");
            _em = _world.EntityManager;
            _simGroup = _world.CreateSystemManaged<SimulationSystemGroup>();
            _simGroup.AddSystemToUpdateList(_world.CreateSystem<DamageApplicationSystem>());

            _queue = new NativeQueue<HealAppliedEvent>(Allocator.Persistent);
            var singleton = _em.CreateEntity();
            _em.AddComponentData(singleton, new HealAppliedEventsSingleton { queue = _queue });
        }

        [TearDown]
        public void TearDown()
        {
            if (_queue.IsCreated) _queue.Dispose();
            _world?.Dispose();
        }

        // ── Helper ───────────────────────────────────────────────────────────────

        private Entity CreateHealableEntity(float3 pos, float hp = 5f, float maxHp = 10f)
        {
            var e = _em.CreateEntity();
            _em.AddComponentData(e, LocalTransform.FromPosition(pos));
            _em.AddComponentData(e, new Health { value = hp, max = maxHp });
            _em.AddBuffer<IncomingDamage>(e);
            _em.AddBuffer<IncomingHeal>(e);
            return e;
        }

        private void Tick(float dt = 0.016f)
        {
            _world.SetTime(new TimeData(_world.Time.ElapsedTime + dt, dt));
            _simGroup.Update();
        }

        // ── Test 1: happy-path — IncomingHeal pulse is enqueued ──────────────────

        [Test]
        public void IncomingHeal_Drain_Enqueues_HealApplied_Event()
        {
            var entity = CreateHealableEntity(new float3(2f, 0f, 3f));
            _em.GetBuffer<IncomingHeal>(entity).Add(new IncomingHeal { amount = 3f });

            Tick();

            Assert.AreEqual(8f, _em.GetComponentData<Health>(entity).value, 1e-4f);
            Assert.AreEqual(0, _em.GetBuffer<IncomingHeal>(entity).Length);
            Assert.AreEqual(1, _queue.Count);

            var evt = _queue.Dequeue();
            Assert.AreEqual(new float3(2f, 0f, 3f), evt.position);
            Assert.AreEqual(3f, evt.amount, 1e-4f);
        }

        // ── Test 2: regen-only → no VFX event ────────────────────────────────────
        // IncomingHeal buffer is empty; only RegenPerSec contributes.
        // Contract: regen does not spam VFX every frame.

        [Test]
        public void RegenOnly_DoesNot_Enqueue_HealApplied()
        {
            var entity = CreateHealableEntity(new float3(0f, 0f, 0f), hp: 5f, maxHp: 10f);
            // Give entity a ModifierStats with regenPerSec so regen applies
            _em.AddComponentData(entity, new Wassup.Battle.Effects.ModifierStats
            {
                dmgTakenMul = 1f,
                regenPerSec = 5f,
            });
            // IncomingHeal buffer intentionally left empty

            Tick();

            Assert.AreEqual(0, _queue.Count, "Regen-only must not produce HealAppliedEvent");
        }

        // ── Test 3: DeadTag → system skips entity, no enqueue ────────────────────

        [Test]
        public void DeadTag_Entity_DoesNot_Enqueue_HealApplied()
        {
            var entity = CreateHealableEntity(new float3(1f, 0f, 0f));
            _em.AddComponentData(entity, new DeadTag());
            _em.GetBuffer<IncomingHeal>(entity).Add(new IncomingHeal { amount = 5f });

            Tick();

            Assert.AreEqual(0, _queue.Count, "Dead entity must not produce HealAppliedEvent");
        }

        // ── Test 4: PendingDeployment → system skips entity, no enqueue ──────────

        [Test]
        public void PendingDeployment_Entity_DoesNot_Enqueue_HealApplied()
        {
            var entity = CreateHealableEntity(new float3(1f, 0f, 0f));
            _em.AddComponentData(entity, new PendingDeployment());
            _em.GetBuffer<IncomingHeal>(entity).Add(new IncomingHeal { amount = 5f });

            Tick();

            Assert.AreEqual(0, _queue.Count, "PendingDeployment entity must not produce HealAppliedEvent");
        }

        // ── Test 5: multi-pulse — amounts summed into one event ───────────────────

        [Test]
        public void MultiPulse_IncomingHeal_Sums_Into_Single_Event()
        {
            var entity = CreateHealableEntity(new float3(0f, 0f, 0f), hp: 1f, maxHp: 100f);
            var buf = _em.GetBuffer<IncomingHeal>(entity);
            buf.Add(new IncomingHeal { amount = 5f });
            buf.Add(new IncomingHeal { amount = 3f });

            Tick();

            Assert.AreEqual(1, _queue.Count, "One event per entity per frame");
            var evt = _queue.Dequeue();
            Assert.AreEqual(8f, evt.amount, 1e-4f, "Pulse amounts must sum");
        }

        // ── Test 6: singleton absent → no crash ───────────────────────────────────
        // Guard: hasHealAppliedQueue path does not throw when singleton missing.

        [Test]
        public void NoSingleton_DoesNot_Throw()
        {
            // Create a separate world without the HealAppliedEventsSingleton
            using var world = new World("NoSingletonTest");
            var em = world.EntityManager;
            var simGroup = world.CreateSystemManaged<SimulationSystemGroup>();
            simGroup.AddSystemToUpdateList(world.CreateSystem<DamageApplicationSystem>());

            var entity = em.CreateEntity();
            em.AddComponentData(entity, LocalTransform.FromPosition(float3.zero));
            em.AddComponentData(entity, new Health { value = 5f, max = 10f });
            em.AddBuffer<IncomingDamage>(entity);
            var healBuf = em.AddBuffer<IncomingHeal>(entity);
            healBuf.Add(new IncomingHeal { amount = 3f });

            Assert.DoesNotThrow(() =>
            {
                world.SetTime(new TimeData(0.016, 0.016f));
                simGroup.Update();
            }, "System must not throw when HealAppliedEventsSingleton is absent");
        }
    }
}
