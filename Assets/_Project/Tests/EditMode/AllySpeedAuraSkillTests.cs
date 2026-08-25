using NUnit.Framework;
using Unity.Mathematics;
using Wassup.Battle.Units;
using Wassup.Skills;
using Wassup.Skills.Concrete;

namespace Wassup.Tests.EditMode
{
    // skill-layer-migration unit 0 — 채찍질. 자장가와 **대칭**인 스킬이다:
    // 같은 반경 선별인데 대상이 반대편이 아니라 같은 편이다.
    // 그 대칭이 코드에서 보이는지를 테스트가 확인한다.
    public class AllySpeedAuraSkillTests
    {
        private static SkillParams P(float percent, int radius, float ttl, int dataIndex = SkillParams.NoDataIndex)
            => new SkillParams(percent, ttl, radius, 0, dataIndex, 0, 0, 0, 0, 0, 0);

        [Test]
        public void BuffsSameFactionInRange_NotTheOtherSide_NotSelf()
        {
            var ctx = new TestSkillContext();
            ctx.Add(100, new float3(5.5f, 0, 5.5f), Faction.EnemyUnit);
            ctx.Add(1, new float3(6.5f, 0, 5.5f), Faction.EnemyUnit);       // 같은 편, 1칸
            ctx.Add(2, new float3(6.5f, 0, 6.5f), Faction.DefenderUnit);    // 반대편
            ctx.Add(3, new float3(20.5f, 0, 5.5f), Faction.EnemyUnit);      // 같은 편, 반경 밖
            var caster = CasterRef.OfUnit(new SkillEntityId(100), Faction.EnemyUnit);

            new AllySpeedAuraSkill().Execute(caster, SkillTarget.None, P(20f, 3, 5f), ctx);

            Assert.AreEqual(1, ctx.SimIntents.Count, "같은 편 · 반경 안 · 자기 제외");
            var e = ctx.SimIntents[0];
            Assert.AreEqual(1, e.Target.Value);
            Assert.AreEqual((int)SkillStatKind.MoveSpeedMul, e.Selector);
            Assert.AreEqual(SkillCombineOp.Multiplicative, e.Op);
            Assert.AreEqual(1.2f, e.Amount, 1e-5f, "저작 20% → 배율 1.2. 여기서 한 번만 변환한다");
            Assert.AreEqual(5f, e.Duration);
            Assert.AreEqual(100, e.Source.Value, "병합 키의 source — 회수 가능성의 조건이다");
        }

        // 「누구든 쓸 수 있다」의 대칭 확인 — 방어유닛이 시전하면 방어유닛을 몰아세운다.
        [Test]
        public void SameSkill_BuffsDefenders_WhenADefenderCastsIt()
        {
            var ctx = new TestSkillContext();
            ctx.Add(100, new float3(5.5f, 0, 5.5f), Faction.DefenderUnit);
            ctx.Add(1, new float3(6.5f, 0, 5.5f), Faction.DefenderUnit);
            ctx.Add(2, new float3(6.5f, 0, 6.5f), Faction.EnemyUnit);
            var caster = CasterRef.OfUnit(new SkillEntityId(100), Faction.DefenderUnit);

            new AllySpeedAuraSkill().Execute(caster, SkillTarget.None, P(20f, 3, 5f), ctx);

            Assert.AreEqual(1, ctx.SimIntents.Count);
            Assert.AreEqual(1, ctx.SimIntents[0].Target.Value);
        }

        // 효과 없는 연출 금지 — 한 명도 못 버프하면 펄스도 없다.
        [Test]
        public void NoTargets_MeansNoVisual()
        {
            var ctx = new TestSkillContext();
            ctx.Add(100, new float3(5.5f, 0, 5.5f), Faction.EnemyUnit);
            var caster = CasterRef.OfUnit(new SkillEntityId(100), Faction.EnemyUnit);

            new AllySpeedAuraSkill().Execute(caster, SkillTarget.None, P(20f, 3, 5f, dataIndex: 7), ctx);

            Assert.AreEqual(0, ctx.SimIntents.Count, "대상 0 이면 버프도 연출도 없다");
        }

        [Test]
        public void Visual_OnlyWhenAuthored()
        {
            var ctx = new TestSkillContext();
            ctx.Add(100, new float3(5.5f, 0, 5.5f), Faction.EnemyUnit);
            ctx.Add(1, new float3(6.5f, 0, 5.5f), Faction.EnemyUnit);
            var caster = CasterRef.OfUnit(new SkillEntityId(100), Faction.EnemyUnit);
            var skill = new AllySpeedAuraSkill();

            skill.Execute(caster, SkillTarget.None, P(20f, 3, 5f), ctx);   // dataIndex = -1
            Assert.AreEqual(1, ctx.SimIntents.Count, "무연출 저작이면 버프만");

            ctx.SimIntents.Clear();
            skill.Execute(caster, SkillTarget.None, P(20f, 3, 5f, dataIndex: 7), ctx);
            Assert.AreEqual(2, ctx.SimIntents.Count);
            Assert.AreEqual(SimIntentKind.PlayVisual, ctx.SimIntents[1].Kind);
        }

        [Test]
        public void DegenerateAuthoring_DoesNothing()
        {
            var ctx = new TestSkillContext();
            ctx.Add(100, new float3(5.5f, 0, 5.5f), Faction.EnemyUnit);
            ctx.Add(1, new float3(6.5f, 0, 5.5f), Faction.EnemyUnit);
            var caster = CasterRef.OfUnit(new SkillEntityId(100), Faction.EnemyUnit);
            var skill = new AllySpeedAuraSkill();

            skill.Execute(caster, SkillTarget.None, P(0f, 3, 5f), ctx);   // 배율 1.0
            skill.Execute(caster, SkillTarget.None, P(20f, 3, 0f), ctx);  // TTL 없음

            Assert.AreEqual(0, ctx.SimIntents.Count);
        }
    }
}
