using NUnit.Framework;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Wassup.Battle.Combat;
using Wassup.Battle.Effects;
using Wassup.Battle.Movement;
using Wassup.Battle.Units;

namespace Wassup.Tests.EditMode
{
    // Verifies the read-only Effects ↔ Movement/Combat bridges without
    // coupling back to Effects write semantics. EffectTickSystem is NOT added
    // to the world here so effects stay applied for the full test duration.
    public class EffectIntegrationTests
    {
        [Test]
        public void Movement_Applies_SlowEffect_Multiplier_To_Step()
        {
            using var world = new World("EffectIntegrationTests_Movement");
            var em = world.EntityManager;
            var simGroup = world.CreateSystemManaged<SimulationSystemGroup>();
            simGroup.AddSystemToUpdateList(world.CreateSystem<MovementSystem>());

            var waypoints = new[] { new int2(0, 0), new int2(10, 0) };
            var e = em.CreateEntity();
            em.AddComponentData(e, LocalTransform.FromPosition(new float3(0f, 0f, 0f)));
            em.AddComponentData(e, new PathFollowState
            {
                currentWaypointIndex = 1,
                speed = 2f,
                tileSize = 1f,
            });
            var buf = em.AddBuffer<PathWaypoint>(e);
            foreach (var wp in waypoints) buf.Add(new PathWaypoint { cell = wp });

            // 50% slow → expect half the distance covered in 1s at speed=2 (1.0 unit instead of 2.0).
            em.AddComponentData(e, new SlowEffect { remaining = 5f, multiplier = 0.5f });

            world.SetTime(new TimeData(world.Time.ElapsedTime + 1f, 1f));
            simGroup.Update();

            var pos = em.GetComponentData<LocalTransform>(e).Position;
            Assert.AreEqual(1f, pos.x, 1e-4f, "SlowEffect multiplier 0.5 should halve this frame's step.");
            // Base speed field stays unchanged — Movement still owns it.
            Assert.AreEqual(2f, em.GetComponentData<PathFollowState>(e).speed, 1e-5f);
        }

        [Test]
        public void Combat_Applies_DamageBoost_To_Emitted_Damage_And_CooldownReduction_To_Reset()
        {
            using var world = new World("EffectIntegrationTests_Combat");
            var em = world.EntityManager;
            var simGroup = world.CreateSystemManaged<SimulationSystemGroup>();
            simGroup.AddSystemToUpdateList(world.CreateSystem<AttackSystem>());

            // Defender: cooldown ready, 10 damage base, range 5.
            var defender = em.CreateEntity();
            em.AddComponentData(defender, LocalTransform.FromPosition(new float3(0f, 0f, 0f)));
            em.AddComponent<DefenderUnitTag>(defender);
            em.AddComponentData(defender, new AttackState
            {
                damage = 10f,
                range = 5f,
                cooldownDuration = 4f,
                cooldownRemaining = 0f,
            });
            em.AddComponentData(defender, new DamageBoost { remaining = 10f, multiplier = 2f });
            em.AddComponentData(defender, new CooldownReduction { remaining = 10f, multiplier = 0.5f });

            // Attacker in range at (1,0,0).
            var attacker = em.CreateEntity();
            em.AddComponentData(attacker, LocalTransform.FromPosition(new float3(1f, 0f, 0f)));
            em.AddComponent<AttackUnitTag>(attacker);
            em.AddBuffer<IncomingDamage>(attacker);

            world.SetTime(new TimeData(world.Time.ElapsedTime + 0.016f, 0.016f));
            simGroup.Update();

            // Emitted damage: 10 base × 2.0 boost = 20.
            var incoming = em.GetBuffer<IncomingDamage>(attacker);
            Assert.AreEqual(1, incoming.Length, "attacker should have received exactly one damage event");
            Assert.AreEqual(20f, incoming[0].amount, 1e-4f, "DamageBoost multiplier 2.0 should double the emitted damage");

            // Reset cooldown: 4 base × 0.5 CDR = 2.
            var attackState = em.GetComponentData<AttackState>(defender);
            Assert.AreEqual(2f, attackState.cooldownRemaining, 1e-4f,
                "CooldownReduction multiplier 0.5 should halve the cooldown reset value");
            Assert.AreEqual(10f, attackState.damage, 1e-5f, "AttackState.damage must remain unchanged (Combat-owned).");
            Assert.AreEqual(4f, attackState.cooldownDuration, 1e-5f, "AttackState.cooldownDuration must remain unchanged.");
        }
    }
}
