using NUnit.Framework;
using Unity.Mathematics;
using Wassup.Battle.Units;
using Wassup.Skills;
using Wassup.Skills.Concrete;

namespace Wassup.Tests.EditMode
{
    // skill-layer-migration unit 2c — 판 밖 런타임을 바꾸는 스킬 둘.
    //
    // 이 그물이 지키는 것 둘:
    //  ① **`SimIntent` 를 하나도 안 낸다.** 이 둘은 시뮬레이션 상태를 안 바꾼다 —
    //     하나라도 새면 코스트 획득이 큐를 타고 한 프레임 늦어진다.
    //  ② 퇴화 저작(0 이하)은 조용히 소모된다. **음수를 「뺏기」로 열지 않는다** —
    //     오타 하나가 조용히 플레이어를 벌주는 문이 된다.
    public class MetaSkillsTests
    {
        private static SkillParams P(float magnitude)
            => new SkillParams(magnitude, 0f, 0, 0, SkillParams.NoDataIndex, 0, 0f, 0f, 0f, 0, 0);

        private static TestSkillContext Ctx(out CasterRef caster)
        {
            var ctx = new TestSkillContext();
            ctx.Add(1, float3.zero, Faction.DefenderUnit);
            caster = CasterRef.OfUnit(new SkillEntityId(1), Faction.DefenderUnit);
            return ctx;
        }

        [Test]
        public void GainCost_EmitsExactlyTheAuthoredAmount()
        {
            var ctx = Ctx(out var caster);
            new GainCostSkill().Execute(caster, default, P(3f), ctx);

            Assert.AreEqual(1, ctx.MetaIntents.Count);
            Assert.AreEqual(MetaIntentKind.GainCost, ctx.MetaIntents[0].Kind);
            Assert.AreEqual(3f, ctx.MetaIntents[0].Amount, 1e-4f);
            Assert.AreEqual(0, ctx.SimIntents.Count,
                "판 밖 런타임 스킬이 시뮬 의도를 냈다 — 큐를 타면 한 프레임 늦는다");
        }

        [Test]
        public void ReduceCooldown_EmitsExactlyTheAuthoredAmount()
        {
            var ctx = Ctx(out var caster);
            new ReduceSkillCooldownSkill().Execute(caster, default, P(2f), ctx);

            Assert.AreEqual(1, ctx.MetaIntents.Count);
            Assert.AreEqual(MetaIntentKind.ReduceSkillCooldown, ctx.MetaIntents[0].Kind);
            Assert.AreEqual(2f, ctx.MetaIntents[0].Amount, 1e-4f);
            Assert.AreEqual(0, ctx.SimIntents.Count);
        }

        [Test]
        public void ZeroOrNegative_ConsumesTheFireQuietly()
        {
            var ctx = Ctx(out var caster);
            new GainCostSkill().Execute(caster, default, P(0f), ctx);
            new GainCostSkill().Execute(caster, default, P(-5f), ctx);
            new ReduceSkillCooldownSkill().Execute(caster, default, P(0f), ctx);
            new ReduceSkillCooldownSkill().Execute(caster, default, P(-5f), ctx);

            Assert.AreEqual(0, ctx.MetaIntents.Count,
                "음수가 통과하면 오타 하나가 조용히 플레이어를 벌준다");
        }

        [Test]
        public void TheyDoNotNeedACaster()
        {
            // 판 밖 런타임이라 시전자 정체가 결과에 관여하지 않는다 — 액티브(플레이어
            // 시전)로 열려도 같은 값이 나와야 한다.
            var ctx = new TestSkillContext();
            var player = CasterRef.Player(Faction.DefenderUnit);

            new GainCostSkill().Execute(player, default, P(7f), ctx);

            Assert.AreEqual(1, ctx.MetaIntents.Count);
            Assert.AreEqual(7f, ctx.MetaIntents[0].Amount, 1e-4f);
        }
    }
}
