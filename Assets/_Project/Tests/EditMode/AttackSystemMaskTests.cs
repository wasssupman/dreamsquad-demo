using NUnit.Framework;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Wassup.Battle.Combat;
using Wassup.Battle.Units;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    public class AttackSystemMaskTests
    {
        private static void Tick(World world, SimulationSystemGroup simGroup)
        {
            world.SetTime(new TimeData(world.Time.ElapsedTime + 0.016f, 0.016f));
            simGroup.Update();
        }

        private static Entity CreateTarget(
            EntityManager em,
            Faction faction,
            float3 position,
            bool defenderTag = false,
            bool attackerTag = false)
        {
            var e = em.CreateEntity();
            em.AddComponentData(e, LocalTransform.FromPosition(position));
            em.AddComponentData(e, new Health { value = 10f, max = 10f });
            em.AddComponentData(e, new FactionTag { value = faction });
            em.AddBuffer<IncomingDamage>(e);
            if (defenderTag) em.AddComponent<DefenderUnitTag>(e);
            if (attackerTag) em.AddComponent<AttackUnitTag>(e);
            return e;
        }

        private static void AddDamageOutput(EntityManager em, Entity entity, float damage)
        {
            var outputs = em.AddBuffer<AttackOutputElement>(entity);
            outputs.Add(new AttackOutputElement
            {
                value = new AttackOutput
                {
                    kind = AttackOutputKind.Damage,
                    magnitude = damage,
                }
            });
        }

        [Test]
        public void Faction_Bitmask_Arithmetic_Matches_Targeting_Rules()
        {
            Assert.AreEqual(0, (int)Faction.Enemy & (int)Faction.Defender);
            Assert.AreEqual(0, (int)Faction.Enemy & (int)(Faction.Defender | Faction.BlockingHazard));
            Assert.AreNotEqual(0, (int)Faction.BlockingHazard & (int)(Faction.Defender | Faction.BlockingHazard));
            Assert.AreNotEqual(0, (int)Faction.Enemy & (int)Faction.Enemy);
        }

        [Test]
        public void Defender_With_Enemy_Mask_Damages_Enemy_But_Ignores_BlockingHazard()
        {
            using var world = new World("AttackSystemMaskTests_Defender");
            var em = world.EntityManager;
            var simGroup = world.CreateSystemManaged<SimulationSystemGroup>();
            simGroup.AddSystemToUpdateList(world.CreateSystem<AttackSystem>());

            var defender = CreateTarget(em, Faction.Defender, new float3(0f, 0f, 0f), defenderTag: true);
            em.AddComponentData(defender, new AttackState
            {
                range = 5f,
                cooldownDuration = 1f,
                cooldownRemaining = 0f,
                attackTargetCount = 1,
                targetMask = (int)Faction.Enemy,
            });
            AddDamageOutput(em, defender, 3f);

            var hazard = CreateTarget(em, Faction.BlockingHazard, new float3(0.5f, 0f, 0f));
            var enemy = CreateTarget(em, Faction.Enemy, new float3(1f, 0f, 0f), attackerTag: true);

            Tick(world, simGroup);

            Assert.AreEqual(0, em.GetBuffer<IncomingDamage>(hazard).Length);
            var enemyDamage = em.GetBuffer<IncomingDamage>(enemy);
            Assert.AreEqual(1, enemyDamage.Length);
            Assert.AreEqual(3f, enemyDamage[0].amount, 1e-4f);
        }

        [Test]
        public void Enemy_With_Defender_And_Hazard_Mask_Can_Target_BlockingHazard()
        {
            using var world = new World("AttackSystemMaskTests_Enemy");
            var em = world.EntityManager;
            var simGroup = world.CreateSystemManaged<SimulationSystemGroup>();
            simGroup.AddSystemToUpdateList(world.CreateSystem<AttackSystem>());

            var enemy = CreateTarget(em, Faction.Enemy, new float3(0f, 0f, 0f), attackerTag: true);
            em.AddComponentData(enemy, new AttackState
            {
                range = 5f,
                cooldownDuration = 1f,
                cooldownRemaining = 0f,
                attackTargetCount = 1,
                targetMask = (int)(Faction.Defender | Faction.BlockingHazard),
            });
            AddDamageOutput(em, enemy, 4f);

            var hazard = CreateTarget(em, Faction.BlockingHazard, new float3(0.5f, 0f, 0f));
            var defender = CreateTarget(em, Faction.Defender, new float3(2f, 0f, 0f), defenderTag: true);

            Tick(world, simGroup);

            var hazardDamage = em.GetBuffer<IncomingDamage>(hazard);
            Assert.AreEqual(1, hazardDamage.Length);
            Assert.AreEqual(4f, hazardDamage[0].amount, 1e-4f);
            Assert.AreEqual(0, em.GetBuffer<IncomingDamage>(defender).Length);
        }
    }
}
