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
    // goal-stability unit 2 — goal 은 타겟 최후순위: 사거리 내 non-Goal 유효 후보가 있으면
    // 그쪽이 이기고(거리 무관), 골만 남았을 때만 골을 친다. FocusUntilDead 는 골을 잠그지
    // 않는다(리뷰 M3). 픽스처는 AttackSystemMaskTests 동형.
    public class GoalTargetingPriorityTests
    {
        private static void Tick(World world, SimulationSystemGroup simGroup)
        {
            world.SetTime(new TimeData(world.Time.ElapsedTime + 0.016f, 0.016f));
            simGroup.Update();
        }

        private static Entity CreateTarget(
            EntityManager em, Faction faction, float3 position,
            bool defenderTag = false)
        {
            var e = em.CreateEntity();
            em.AddComponentData(e, LocalTransform.FromPosition(position));
            em.AddComponentData(e, new Health { value = 100f, max = 100f });
            em.AddComponentData(e, new FactionTag { value = faction });
            em.AddBuffer<IncomingDamage>(e);
            if (defenderTag) em.AddComponent<DefenderUnitTag>(e);
            return e;
        }

        private static Entity CreateGoal(EntityManager em, float3 position, float m = 30f)
        {
            var e = CreateTarget(em, Faction.Goal, position);
            em.AddComponentData(e, new GoalPoint
            {
                cell = new int2((int)position.x, (int)position.z),
                goalIndex = 0,
            });
            return e;
        }

        private static Entity CreateEnemyAttacker(
            EntityManager em, float3 position, float cooldown = 1f)
        {
            var e = em.CreateEntity();
            em.AddComponentData(e, LocalTransform.FromPosition(position));
            em.AddComponentData(e, new Health { value = 10f, max = 10f });
            em.AddComponentData(e, new FactionTag { value = Faction.Enemy });
            em.AddComponent<AttackUnitTag>(e);
            em.AddBuffer<IncomingDamage>(e);
            em.AddComponentData(e, new AttackState
            {
                range = 5f,
                cooldownDuration = cooldown,
                cooldownRemaining = 0f,
                attackTargetCount = 1,
                targetMask = (int)(Faction.Defender | Faction.BlockingHazard | Faction.Goal),
            });
            var outputs = em.AddBuffer<AttackOutputElement>(e);
            outputs.Add(new AttackOutputElement
            {
                value = new AttackOutput { kind = AttackOutputKind.Damage, magnitude = 4f },
            });
            return e;
        }

        [Test]
        public void Enemy_PrefersDefender_OverNearerGoal()
        {
            using var world = new World("GoalTargetingPriorityTests_Prefer");
            var em = world.EntityManager;
            var simGroup = world.CreateSystemManaged<SimulationSystemGroup>();
            simGroup.AddSystemToUpdateList(world.CreateSystem<AttackSystem>());

            var enemy = CreateEnemyAttacker(em, new float3(0f, 0f, 0f));
            var goal = CreateGoal(em, new float3(0.5f, 0f, 0f));            // 골이 더 가깝다
            var defender = CreateTarget(em, Faction.Defender, new float3(2f, 0f, 0f), defenderTag: true);

            Tick(world, simGroup);

            Assert.AreEqual(1, em.GetBuffer<IncomingDamage>(defender).Length,
                "사거리 내 non-Goal 후보가 있으면 거리 무관하게 그쪽이 이긴다");
            Assert.AreEqual(0, em.GetBuffer<IncomingDamage>(goal).Length,
                "골은 최후순위 — 방어유닛이 사거리에 있는 동안 맞지 않는다");
        }

        [Test]
        public void Enemy_HitsGoal_WhenOnlyGoalInRange()
        {
            using var world = new World("GoalTargetingPriorityTests_GoalOnly");
            var em = world.EntityManager;
            var simGroup = world.CreateSystemManaged<SimulationSystemGroup>();
            simGroup.AddSystemToUpdateList(world.CreateSystem<AttackSystem>());

            var enemy = CreateEnemyAttacker(em, new float3(0f, 0f, 0f));
            var goal = CreateGoal(em, new float3(1f, 0f, 0f));

            Tick(world, simGroup);

            var damage = em.GetBuffer<IncomingDamage>(goal);
            Assert.AreEqual(1, damage.Length, "골만 사거리에 있으면 골을 친다");
            Assert.AreEqual(4f, damage[0].amount, 1e-4f);
        }

        [Test]
        public void FocusUntilDead_DoesNotLockGoal_AndSwitchesToDefender()
        {
            using var world = new World("GoalTargetingPriorityTests_Focus");
            var em = world.EntityManager;
            var simGroup = world.CreateSystemManaged<SimulationSystemGroup>();
            simGroup.AddSystemToUpdateList(world.CreateSystem<AttackSystem>());

            var enemy = CreateEnemyAttacker(em, new float3(0f, 0f, 0f), cooldown: 0.01f);
            em.AddComponentData(enemy, new EnemyBehavior { targetMode = EnemyTargetMode.FocusUntilDead });
            em.AddComponentData(enemy, new FocusTarget { current = Entity.Null });
            var goal = CreateGoal(em, new float3(1f, 0f, 0f));

            Tick(world, simGroup);

            Assert.AreEqual(1, em.GetBuffer<IncomingDamage>(goal).Length,
                "골만 있으면 골을 친다 (잠금 없이도 사격은 유지)");
            Assert.AreEqual(Entity.Null, em.GetComponentData<FocusTarget>(enemy).current,
                "리뷰 M3 — 골은 FocusUntilDead 잠금에 저장되지 않는다");

            // 방어유닛이 배치되면 잠금이 없으므로 즉시 그쪽으로 전환된다.
            var defender = CreateTarget(em, Faction.Defender, new float3(2f, 0f, 0f), defenderTag: true);
            Tick(world, simGroup);
            Tick(world, simGroup);

            Assert.GreaterOrEqual(em.GetBuffer<IncomingDamage>(defender).Length, 1,
                "골에 잠기지 않았으므로 이후 배치된 방어유닛으로 전환돼야 한다");
            Assert.AreEqual(defender, em.GetComponentData<FocusTarget>(enemy).current,
                "새 잠금은 방어유닛에 걸린다");
        }
    }
}
