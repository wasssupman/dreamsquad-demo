using NUnit.Framework;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Wassup.Battle.Units;
using Wassup.Battle.Effects;

namespace Wassup.Tests.EditMode
{
    // skill-layer-migration unit 8 선행 — **시전자가 죽은 뒤에 도는 스킬의 진영.**
    //
    // 자기 죽음 seam(`SkillDispatchLifecycleSystem`)은 정의상 파괴 **뒤**에 돈다.
    // 그래서 드레인 시점엔 시전자 핸들이 죽어 있고 진영을 엔티티에서 못 읽는다.
    // 예전엔 그 자리를 「플레이어 시전 = 방어유닛 편」으로 접었다 — 액티브 카드에는
    // 맞지만 **적의 작별 선물에는 정반대**라, 적이 «적» 을 겨눈다.
    //
    // ⚠ 이 결함이 조용한 이유: 화면에서 폭발은 정상적으로 터진다. 다만 **엉뚱한 편이
    // 맞는다.** 그리고 오늘은 화이트리스트가 `OnDeath` 를 적에게 안 열어 도달 자체가
    // 불가능하다 — 그 문을 여는 것이 unit 8 이므로, 문보다 이 그물이 먼저 있어야 한다.
    public class DeadCasterFactionTests
    {
        // 죽은 적의 자기 죽음 스킬은 **방어유닛**을 겨눈다(자기 진영이 아니라).
        [Test]
        public void LifecycleSeam_DeadEnemyCaster_TargetsDefenders()
        {
            var hit = RunLifecycleAreaCc(Faction.EnemyUnit);
            Assert.AreEqual(1, hit.defenders, "적의 사후 스킬은 방어유닛을 겨눠야 한다");
            Assert.AreEqual(0, hit.enemies,
                "적이 자기 진영을 때렸다 — 죽은 시전자의 진영이 «플레이어»로 접힌 것이다");
        }

        // 대칭 — 죽은 방어유닛 쪽은 종전대로 적을 겨눈다.
        [Test]
        public void LifecycleSeam_DeadDefenderCaster_TargetsEnemies()
        {
            var hit = RunLifecycleAreaCc(Faction.DefenderUnit);
            Assert.AreEqual(1, hit.enemies, "방어유닛의 사후 스킬은 적을 겨눠야 한다");
            Assert.AreEqual(0, hit.defenders);
        }

        // 안 실은 경우(=Faction.None)는 종전 동작을 유지한다 — 액티브 카드가 그 자리다.
        [Test]
        public void LifecycleSeam_NoSnapshot_FallsBackToPlayerSide()
        {
            var hit = RunLifecycleAreaCc(Faction.None);
            Assert.AreEqual(1, hit.enemies, "안 실었으면 종전대로 플레이어(방어유닛) 편으로 접는다");
        }

        private (int enemies, int defenders) RunLifecycleAreaCc(Faction snapshot)
        {
            using var world = new World("DeadCasterFactionTests");
            var em = world.EntityManager;
            var simGroup = world.CreateSystemManaged<SimulationSystemGroup>();
            simGroup.AddSystemToUpdateList(
                world.CreateSystemManaged<Wassup.Battle.Skills.SkillDispatchLifecycleSystem>());
            simGroup.SortSystems();

            var skillQueue = new NativeQueue<Wassup.Battle.Skills.SkillFiredEvent>(Allocator.Persistent);
            em.AddComponentData(em.CreateEntity(),
                new Wassup.Battle.Skills.SkillFiredEventsSingleton { queue = skillQueue });
            var statQueue = new NativeQueue<StatModifierApplyEvent>(Allocator.Persistent);
            em.AddComponentData(em.CreateEntity(),
                new StatModifierApplyEventsSingleton { queue = statQueue });

            // ⚠ **시전자 위치를 안 쓰는 concrete 를 고른다.** 대부분의 광역기는 중심을
            // 「내 발밑」에서 얻는데, 죽은 시전자에겐 그 질의가 실패해 진영 판정에
            // 닿기도 전에 빠져나간다(첫 시도가 그렇게 빈손이었다). 이쪽은 중심이
            // **찍은 칸**이라 남는 변수가 진영 하나다.
            var registry = new Wassup.Skills.SkillRegistry();
            registry.Register(new Wassup.Skills.Concrete.TileStatBurstSkill());
            Wassup.Battle.Skills.SkillDispatchSystemBase.Install(
                registry, new Wassup.Battle.Skills.EcsSkillContext());

            // 양쪽에 한 기씩. 같은 자리에 둬서 «사거리»가 아니라 «진영»만이 답을 가른다.
            var enemy = MakeUnit(em, 1, new float3(0f, 0f, 0f), Faction.EnemyUnit);
            var defender = MakeUnit(em, 2, new float3(0f, 0f, 0f), Faction.DefenderUnit);

            skillQueue.Enqueue(new Wassup.Battle.Skills.SkillFiredEvent
            {
                Seam = Wassup.Battle.Skills.SkillSeam.Lifecycle,
                Caster = Entity.Null,          // 죽었다 — 이것이 이 테스트의 전제다
                CasterFaction = snapshot,
                SkillId = Wassup.Skills.Concrete.TileStatBurstSkill.Id,
                SlotIndex = 0,
                FiredPosition = float3.zero,
                Target = Entity.Null,
                TargetPosition = float3.zero,
                TargetCellA = int2.zero,
                Magnitude = 0.5f,     // 1 이 아니어야 한다(1 = 항등 → 조기 반환)
                Duration = 2f,
                TileRange = 64,       // 격자 기본값에 안 기대도록 넉넉히
                DataIndex = -1,
                PatternIndex = -1,
                HazardDataIndex = -1,
            });

            world.SetTime(new TimeData(0.016d, 0.016f));
            simGroup.Update();

            int enemies = 0, defenders = 0;
            while (statQueue.TryDequeue(out var ev))
            {
                if (ev.target == enemy) enemies++;
                else if (ev.target == defender) defenders++;
            }

            Wassup.Battle.Skills.SkillDispatchSystemBase.Uninstall();
            Wassup.Battle.Skills.SkillDispatchSystemBase.ResetExecutedCount();
            statQueue.Dispose();
            skillQueue.Dispose();
            return (enemies, defenders);
        }

        private static Entity MakeUnit(EntityManager em, int simId, float3 pos, Faction faction)
        {
            var e = em.CreateEntity();
            em.AddComponentData(e, LocalTransform.FromPosition(pos));
            em.AddComponentData(e, new FactionTag { value = faction });
            em.AddComponentData(e, new Health { value = 100f, max = 100f });
            em.AddBuffer<IncomingDamage>(e);
            em.AddBuffer<CcEffect>(e);
            // ⚠ 핸들이 없으면 스킬 레이어에서 이 유닛은 존재하지 않는다(조용한 no-op).
            em.AddComponentData(e, new SimEntityId { value = simId });
            if (faction == Faction.EnemyUnit) em.AddComponent<AttackUnitTag>(e);
            else em.AddComponent<DefenderUnitTag>(e);
            return e;
        }
    }
}
