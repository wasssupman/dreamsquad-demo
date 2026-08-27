using NUnit.Framework;
using Unity.Mathematics;
using Wassup.Battle.Units;
using Wassup.Skills;
using Wassup.Skills.Concrete;

namespace Wassup.Tests.EditMode
{
    // skill-layer-foundation unit 5 — 자장가의 선별 규칙을 **월드 없이** 고정한다.
    //
    // 오늘까지 이 로직은 `BossPeriodicTriggerSystem` 733줄 한복판에 있어서 단위
    // 테스트가 불가능했다 — bare world 를 세우고 보스를 스폰하고 임계까지 체력을 깎아야
    // 한 줄을 검증할 수 있었다. concrete 로 나오면서 처음으로 테스트 표면에 올라왔다.
    //
    // 지키는 것은 **skip-rank 의 의도**다: 「내가 어차피 때릴 대상」만 정확히 빼고,
    // 링 전체를 빼지 않는다. 그 구분이 무너지면 실측에서 "재우는 효과가 발생하지 않는다"
    // 가 재현된다(도넛 판이 그렇게 죽었다).
    public class AreaSleepSkillTests
    {
        private static SkillParams P(int sleepCount, int radius, float duration)
            => new SkillParams(sleepCount, duration, radius, 0, SkillParams.NoDataIndex, 0, 0, 0, 0, 0, 0);

        private static TestSkillContext Ctx(out CasterRef caster, float attackRange = 0f, float targetCount = 0f)
        {
            var ctx = new TestSkillContext();
            ctx.Add(100, new float3(5.5f, 0, 5.5f), Faction.EnemyUnit, u =>
            {
                u.AttackRange = attackRange;
                u.AttackTargetCount = targetCount;
            });
            caster = CasterRef.OfUnit(new SkillEntityId(100), Faction.EnemyUnit);
            return ctx;
        }

        [Test]
        public void SleepsUpToCap_NearestFirst()
        {
            var ctx = Ctx(out var caster);
            ctx.Add(1, new float3(6.5f, 0, 5.5f), Faction.DefenderUnit);   // 1칸
            ctx.Add(2, new float3(7.5f, 0, 5.5f), Faction.DefenderUnit);   // 2칸
            ctx.Add(3, new float3(8.5f, 0, 5.5f), Faction.DefenderUnit);   // 3칸

            new AreaSleepSkill().Execute(caster, SkillTarget.None, P(2, 5, 1.5f), ctx);

            Assert.AreEqual(2, ctx.SimIntents.Count, "cap 만큼만 재운다");
            CollectionAssert.AreEquivalent(
                new[] { 1, 2 },
                new[] { ctx.SimIntents[0].Target.Value, ctx.SimIntents[1].Target.Value },
                "가까운 순으로 골라야 한다");
            Assert.AreEqual((int)SkillCcKind.Sleep, ctx.SimIntents[0].Selector);
            Assert.AreEqual(1.5f, ctx.SimIntents[0].Duration);
        }

        // ⚠ 이 테스트가 이 스킬의 존재 이유다. 「내가 때릴 대상」을 안 빼면
        // 재우자마자 자기 평타가 깨운다.
        [Test]
        public void SkipsTargetsItWouldAttackAnyway_ButOnlyInsideItsRange()
        {
            // 사거리 1칸, 한 번에 1기를 때린다 → 1칸 안의 가장 가까운 하나를 건너뛴다.
            var ctx = Ctx(out var caster, attackRange: 1f, targetCount: 1f);
            ctx.Add(1, new float3(6.5f, 0, 5.5f), Faction.DefenderUnit);   // 1칸 — 내가 때린다
            ctx.Add(2, new float3(8.5f, 0, 5.5f), Faction.DefenderUnit);   // 3칸 — 사거리 밖

            new AreaSleepSkill().Execute(caster, SkillTarget.None, P(1, 5, 1.5f), ctx);

            Assert.AreEqual(1, ctx.SimIntents.Count);
            Assert.AreEqual(2, ctx.SimIntents[0].Target.Value,
                "사거리 안 최근접은 «내가 깨울 대상» 이라 건너뛰고 그 밖을 재워야 한다");
        }

        [Test]
        public void DoesNotSkip_WhenNothingIsInsideAttackRange()
        {
            // 사거리는 1칸인데 후보가 전부 그 밖이면 건너뛸 이유가 없다.
            var ctx = Ctx(out var caster, attackRange: 1f, targetCount: 1f);
            ctx.Add(1, new float3(8.5f, 0, 5.5f), Faction.DefenderUnit);   // 3칸

            new AreaSleepSkill().Execute(caster, SkillTarget.None, P(1, 5, 1.5f), ctx);

            Assert.AreEqual(1, ctx.SimIntents.Count, "사거리 밖이면 건너뛰지 않는다");
            Assert.AreEqual(1, ctx.SimIntents[0].Target.Value);
        }

        [Test]
        public void IgnoresDeadAndPendingAndSelf()
        {
            var ctx = Ctx(out var caster);
            ctx.Add(1, new float3(6.5f, 0, 5.5f), Faction.DefenderUnit, u => u.Dead = true);
            ctx.Add(2, new float3(6.5f, 0, 6.5f), Faction.DefenderUnit, u => u.Pending = true);
            ctx.Add(3, new float3(7.5f, 0, 5.5f), Faction.DefenderUnit);

            new AreaSleepSkill().Execute(caster, SkillTarget.None, P(5, 5, 1.5f), ctx);

            Assert.AreEqual(1, ctx.SimIntents.Count, "시체·배치중은 cap 자리를 차지하면 안 된다");
            Assert.AreEqual(3, ctx.SimIntents[0].Target.Value);
        }

        // **호출자가 곧 소유자다** — 이 spec 의 검증 질문을 도메인 수준에서 묻는다.
        [Test]
        public void SameSkill_SleepsTheOtherSide_WhenADefenderCastsIt()
        {
            var ctx = new TestSkillContext();
            ctx.Add(100, new float3(5.5f, 0, 5.5f), Faction.DefenderUnit);  // 방어유닛이 시전
            ctx.Add(1, new float3(6.5f, 0, 5.5f), Faction.EnemyUnit);
            ctx.Add(2, new float3(6.5f, 0, 6.5f), Faction.DefenderUnit);    // 같은 편 — 안 재운다
            var caster = CasterRef.OfUnit(new SkillEntityId(100), Faction.DefenderUnit);

            new AreaSleepSkill().Execute(caster, SkillTarget.None, P(5, 5, 1.5f), ctx);

            Assert.AreEqual(1, ctx.SimIntents.Count);
            Assert.AreEqual(1, ctx.SimIntents[0].Target.Value,
                "같은 concrete 가 host 진영에 따라 반대편을 잡아야 한다 — 코드 0줄로");
        }

        [Test]
        public void DegenerateAuthoring_DoesNothing()
        {
            var ctx = Ctx(out var caster);
            ctx.Add(1, new float3(6.5f, 0, 5.5f), Faction.DefenderUnit);

            var skill = new AreaSleepSkill();
            skill.Execute(caster, SkillTarget.None, P(0, 5, 1.5f), ctx);   // 인원 0
            skill.Execute(caster, SkillTarget.None, P(1, 0, 1.5f), ctx);   // 반경 0
            skill.Execute(caster, SkillTarget.None, P(1, 5, 0f), ctx);     // 지속 0

            Assert.AreEqual(0, ctx.SimIntents.Count, "저작이 degenerate 면 조용히 소모한다");
        }
    }
}
