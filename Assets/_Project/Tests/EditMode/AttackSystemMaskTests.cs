using NUnit.Framework;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Wassup.Battle.Combat;
using Wassup.Battle.Movement;
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
            bool attackerTag = false,
            PlacementLayer traversalLayer = PlacementLayer.None)
        {
            var e = em.CreateEntity();
            em.AddComponentData(e, LocalTransform.FromPosition(position));
            em.AddComponentData(e, new Health { value = 10f, max = 10f });
            em.AddComponentData(e, new FactionTag { value = faction });
            em.AddBuffer<IncomingDamage>(e);
            if (defenderTag) em.AddComponent<DefenderUnitTag>(e);
            if (attackerTag) em.AddComponent<AttackUnitTag>(e);
            if (traversalLayer != PlacementLayer.None)
                em.AddComponentData(e, new PathFollowState
                {
                    traversalLayers = (byte)traversalLayer,
                });
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
            Assert.AreEqual(0, (int)Faction.EnemyUnit & (int)Faction.DefenderUnit);
            Assert.AreEqual(0, (int)Faction.EnemyUnit & (int)(Faction.DefenderUnit | Faction.BlockingHazard));
            Assert.AreNotEqual(0, (int)Faction.BlockingHazard & (int)(Faction.DefenderUnit | Faction.BlockingHazard));
            Assert.AreNotEqual(0, (int)Faction.EnemyUnit & (int)Faction.EnemyUnit);
        }

        [Test]
        public void Defender_With_Enemy_Mask_Damages_Enemy_But_Ignores_BlockingHazard()
        {
            using var world = new World("AttackSystemMaskTests_Defender");
            var em = world.EntityManager;
            var simGroup = world.CreateSystemManaged<SimulationSystemGroup>();
            simGroup.AddSystemToUpdateList(world.CreateSystem<AttackSystem>());

            var defender = CreateTarget(em, Faction.DefenderUnit, new float3(0f, 0f, 0f), defenderTag: true);
            em.AddComponentData(defender, new AttackState
            {
                range = 5f,
                cooldownDuration = 1f,
                cooldownRemaining = 0f,
                attackTargetCount = 1,
                targetMask = (int)Faction.EnemyUnit,
            });
            AddDamageOutput(em, defender, 3f);

            var hazard = CreateTarget(em, Faction.BlockingHazard, new float3(0.5f, 0f, 0f));
            var enemy = CreateTarget(em, Faction.EnemyUnit, new float3(1f, 0f, 0f), attackerTag: true);

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

            var enemy = CreateTarget(em, Faction.EnemyUnit, new float3(0f, 0f, 0f), attackerTag: true);
            em.AddComponentData(enemy, new AttackState
            {
                range = 5f,
                cooldownDuration = 1f,
                cooldownRemaining = 0f,
                attackTargetCount = 1,
                targetMask = (int)(Faction.DefenderUnit | Faction.BlockingHazard),
            });
            AddDamageOutput(em, enemy, 4f);

            var hazard = CreateTarget(em, Faction.BlockingHazard, new float3(0.5f, 0f, 0f));
            var defender = CreateTarget(em, Faction.DefenderUnit, new float3(2f, 0f, 0f), defenderTag: true);

            Tick(world, simGroup);

            var hazardDamage = em.GetBuffer<IncomingDamage>(hazard);
            Assert.AreEqual(1, hazardDamage.Length);
            Assert.AreEqual(4f, hazardDamage[0].amount, 1e-4f);
            Assert.AreEqual(0, em.GetBuffer<IncomingDamage>(defender).Length);
        }

        [Test]
        public void DefenderTargetLayers_PathOnlyAndCombined_SelectEligibleMovers()
        {
            using var world = new World("AttackSystemMaskTests_TraversalLayers");
            var em = world.EntityManager;
            var simGroup = world.CreateSystemManaged<SimulationSystemGroup>();
            simGroup.AddSystemToUpdateList(world.CreateSystem<AttackSystem>());

            var groundDefender = CreateTarget(em, Faction.DefenderUnit, float3.zero, defenderTag: true);
            em.AddComponentData(groundDefender, new AttackState
            {
                range = 5f,
                cooldownDuration = 1f,
                attackTargetCount = 1,
                targetMask = (int)Faction.EnemyUnit,
                targetTraversalLayers = (byte)PlacementLayer.Path,
            });
            AddDamageOutput(em, groundDefender, 3f);

            var nearbyAir = CreateTarget(em, Faction.EnemyUnit, new float3(0.5f, 0f, 0f),
                attackerTag: true, traversalLayer: PlacementLayer.Air);
            var fartherPath = CreateTarget(em, Faction.EnemyUnit, new float3(1f, 0f, 0f),
                attackerTag: true, traversalLayer: PlacementLayer.Path);

            var antiAir = CreateTarget(em, Faction.DefenderUnit, new float3(20f, 0f, 0f), defenderTag: true);
            em.AddComponentData(antiAir, new AttackState
            {
                range = 5f,
                cooldownDuration = 1f,
                attackTargetCount = 2,
                targetMask = (int)Faction.EnemyUnit,
                targetTraversalLayers = (byte)(PlacementLayer.Path | PlacementLayer.Air),
            });
            AddDamageOutput(em, antiAir, 4f);

            var nearbyPath = CreateTarget(em, Faction.EnemyUnit, new float3(20.5f, 0f, 0f),
                attackerTag: true, traversalLayer: PlacementLayer.Path);
            var fartherAir = CreateTarget(em, Faction.EnemyUnit, new float3(21f, 0f, 0f),
                attackerTag: true, traversalLayer: PlacementLayer.Air);

            Tick(world, simGroup);

            Assert.AreEqual(0, em.GetBuffer<IncomingDamage>(nearbyAir).Length,
                "일반 방어유닛은 더 가까운 공중 적도 건너뛴다");
            Assert.AreEqual(3f, em.GetBuffer<IncomingDamage>(fartherPath)[0].amount, 1e-4f);
            Assert.AreEqual(4f, em.GetBuffer<IncomingDamage>(nearbyPath)[0].amount, 1e-4f,
                "대공사수는 Path 적도 유효 대상으로 삼는다");
            Assert.AreEqual(4f, em.GetBuffer<IncomingDamage>(fartherAir)[0].amount, 1e-4f);
        }
    }
}
