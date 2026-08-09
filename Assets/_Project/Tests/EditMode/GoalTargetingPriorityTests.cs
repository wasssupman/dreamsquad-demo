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
    // battle-structures unit 0 — **거점은 일반 후보다.** 타입으로 순위를 뒤집지 않는다.
    //
    // goal-stability unit 2 의 «골은 타겟 최후순위» 계약은 폐기됐다(2026-08-09 사용자 확정).
    // 그 규칙은 «거점 타입이 유닛 타입에 항상 우선/후순위» 라는 전역 규칙이었고, 우선순위는
    // 공격자 쪽 저작(«이 놈은 거점을 우선하나» — unit 1 EnemyTargetFilter)이 정할 문제다.
    // 저작이 같으면 **거리순**이고, 정해진 타겟이 바뀌는 규칙은 TargetPersistence 가 소유한다.
    //
    // 이 스위트가 지키는 것: 마스크에 든 후보는 종류를 묻지 않고 거리로 경쟁한다 · 거점만
    // 남으면 거점을 친다 · 잠금도 거점에 균일하게 걸린다.
    // 아키타입이 브리지 산물과 일치하는지는 GoalTowerArchetypeTests 가 고정한다.
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

        // 라이브 골 타워 아키타입 = DefenderCore 진영 + GoalTowerTag (EnsureGoalTowers 산물).
        private static Entity CreateGoal(EntityManager em, float3 position)
        {
            var e = CreateTarget(em, Faction.DefenderCore, position);
            em.AddComponent<GoalTowerTag>(e);
            return e;
        }

        private static Entity CreateEnemyAttacker(
            EntityManager em, float3 position, float cooldown = 1f)
        {
            var e = em.CreateEntity();
            em.AddComponentData(e, LocalTransform.FromPosition(position));
            em.AddComponentData(e, new Health { value = 10f, max = 10f });
            em.AddComponentData(e, new FactionTag { value = Faction.EnemyUnit });
            em.AddComponent<AttackUnitTag>(e);
            em.AddBuffer<IncomingDamage>(e);
            em.AddComponentData(e, new AttackState
            {
                range = 5f,
                cooldownDuration = cooldown,
                cooldownRemaining = 0f,
                attackTargetCount = 1,
                // 적 base 마스크 — BattleBridge.CreateAttackerEntity 가 굽는 것과 같은 조합.
                targetMask = (int)(Faction.DefenderUnit | Faction.BlockingHazard | Faction.DefenderCore),
            });
            var outputs = em.AddBuffer<AttackOutputElement>(e);
            outputs.Add(new AttackOutputElement
            {
                value = new AttackOutput { kind = AttackOutputKind.Damage, magnitude = 4f },
            });
            return e;
        }

        // 거점이 더 가까우면 거점을 친다 — 종류가 아니라 거리가 정한다.
        [Test]
        public void Enemy_TargetsNearest_EvenWhenNearestIsStructure()
        {
            using var world = new World("GoalTargetingPriorityTests_NearestStructure");
            var em = world.EntityManager;
            var simGroup = world.CreateSystemManaged<SimulationSystemGroup>();
            simGroup.AddSystemToUpdateList(world.CreateSystem<AttackSystem>());

            CreateEnemyAttacker(em, new float3(0f, 0f, 0f));
            var goal = CreateGoal(em, new float3(0.5f, 0f, 0f));             // 거점이 더 가깝다
            var defender = CreateTarget(em, Faction.DefenderUnit, new float3(2f, 0f, 0f), defenderTag: true);

            Tick(world, simGroup);

            Assert.AreEqual(1, em.GetBuffer<IncomingDamage>(goal).Length,
                "거점이 더 가까우면 거점이 맞는다 — 타입 기반 후순위는 폐기됐다");
            Assert.AreEqual(0, em.GetBuffer<IncomingDamage>(defender).Length,
                "더 먼 방어유닛은 이 프레임에 맞지 않는다");
        }

        // 방어유닛이 더 가까우면 방어유닛을 친다 — 같은 규칙의 반대 방향.
        [Test]
        public void Enemy_TargetsNearest_UnitWhenUnitIsNearer()
        {
            using var world = new World("GoalTargetingPriorityTests_NearestUnit");
            var em = world.EntityManager;
            var simGroup = world.CreateSystemManaged<SimulationSystemGroup>();
            simGroup.AddSystemToUpdateList(world.CreateSystem<AttackSystem>());

            CreateEnemyAttacker(em, new float3(0f, 0f, 0f));
            var defender = CreateTarget(em, Faction.DefenderUnit, new float3(0.5f, 0f, 0f), defenderTag: true);
            var goal = CreateGoal(em, new float3(2f, 0f, 0f));

            Tick(world, simGroup);

            Assert.AreEqual(1, em.GetBuffer<IncomingDamage>(defender).Length,
                "방어유닛이 더 가까우면 방어유닛이 맞는다");
            Assert.AreEqual(0, em.GetBuffer<IncomingDamage>(goal).Length);
        }

        [Test]
        public void Enemy_HitsGoal_WhenOnlyGoalInRange()
        {
            using var world = new World("GoalTargetingPriorityTests_GoalOnly");
            var em = world.EntityManager;
            var simGroup = world.CreateSystemManaged<SimulationSystemGroup>();
            simGroup.AddSystemToUpdateList(world.CreateSystem<AttackSystem>());

            CreateEnemyAttacker(em, new float3(0f, 0f, 0f));
            var goal = CreateGoal(em, new float3(1f, 0f, 0f));

            Tick(world, simGroup);

            var damage = em.GetBuffer<IncomingDamage>(goal);
            Assert.AreEqual(1, damage.Length, "거점만 사거리에 있으면 거점을 친다");
            Assert.AreEqual(4f, damage[0].amount, 1e-4f);
        }

        // 잠금도 거점에 균일하게 걸린다 — M3 의 «거점은 잠금 대상이 아니다» 예외는 제거됐다.
        // 유지·해제는 TargetPersistence.KeepsLock 하나가 정한다(죽거나 사거리 이탈).
        [Test]
        public void FocusUntilDead_LocksStructure_LikeAnyOtherTarget()
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

            Assert.AreEqual(1, em.GetBuffer<IncomingDamage>(goal).Length);
            Assert.AreEqual(goal, em.GetComponentData<FocusTarget>(enemy).current,
                "거점도 다른 타겟과 똑같이 잠금에 저장된다");

            // 이후 더 가까이 배치된 방어유닛에게 빼앗기지 않는다 — 잠금이 살아있고 사거리 안이므로.
            var defender = CreateTarget(em, Faction.DefenderUnit, new float3(0.5f, 0f, 0f), defenderTag: true);
            Tick(world, simGroup);
            Tick(world, simGroup);

            Assert.AreEqual(goal, em.GetComponentData<FocusTarget>(enemy).current,
                "FocusUntilDead — 물었으면 죽거나 사거리를 벗어날 때까지 유지한다");
            Assert.AreEqual(0, em.GetBuffer<IncomingDamage>(defender).Length,
                "잠긴 대상이 있으면 더 가까운 후보로 갈아타지 않는다");
        }
    }
}
