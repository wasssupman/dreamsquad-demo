using NUnit.Framework;
using Unity.Mathematics;
using Wassup.Battle.Units;
using Wassup.Skills;
using Wassup.Skills.Concrete;

namespace Wassup.Tests.EditMode
{
    // skill-layer-migration unit 1 — 범위 도발의 도메인 계약.
    //
    // 이 그물이 지키는 것: **이 스킬이 소유하는 판단은 「누구를 부르나」까지**다.
    // 불려온 뒤의 처리(보스 면역·공격 수단 부재·도달 불가)는 어그로 시스템 소유라
    // 여기서 단언하지 않는다 — 단언하면 그 판정이 두 곳에 생긴다.
    public class AreaTauntSkillTests
    {
        private static SkillParams P(int radius, float duration)
            => new SkillParams(0f, duration, radius, 0, SkillParams.NoDataIndex, 0, 0f, 0f, 0f, 0, 0);

        private static TestSkillContext Ctx(out CasterRef caster)
        {
            var ctx = new TestSkillContext();
            ctx.Add(1, float3.zero, Faction.DefenderUnit, u => u.HasAggroCapacity = true);
            caster = CasterRef.OfUnit(new SkillEntityId(1), Faction.DefenderUnit);
            return ctx;
        }

        private static int TauntCount(TestSkillContext ctx)
        {
            int n = 0;
            foreach (var it in ctx.SimIntents) if (it.Kind == SimIntentKind.Taunt) n++;
            return n;
        }

        [Test]
        public void CallsEveryOpponentInRange()
        {
            var ctx = Ctx(out var caster);
            ctx.Add(2, new float3(1f, 0f, 0f), Faction.EnemyUnit);
            ctx.Add(3, new float3(0f, 0f, 2f), Faction.EnemyUnit);

            new AreaTauntSkill().Execute(caster, default, P(3, 4f), ctx);

            Assert.AreEqual(2, TauntCount(ctx));
            Assert.AreEqual(4f, ctx.SimIntents[0].Duration, 1e-4f);
            Assert.AreEqual(1, ctx.SimIntents[0].Source.Value, "어그로는 가디언에게 붙는다");
        }

        [Test]
        public void DoesNotCallAllies()
        {
            // 진영은 caster 에서 파생된다 — 「적」을 이름으로 부르지 않기 때문에
            // 같은 스킬이 적 host 에 실리면 방어유닛을 부른다.
            var ctx = Ctx(out var caster);
            ctx.Add(2, new float3(1f, 0f, 0f), Faction.DefenderUnit);

            new AreaTauntSkill().Execute(caster, default, P(3, 4f), ctx);

            Assert.AreEqual(0, TauntCount(ctx));
        }

        [Test]
        public void OutOfRange_IsNotCalled()
        {
            var ctx = Ctx(out var caster);
            ctx.Add(2, new float3(9f, 0f, 0f), Faction.EnemyUnit);

            new AreaTauntSkill().Execute(caster, default, P(2, 4f), ctx);

            Assert.AreEqual(0, TauntCount(ctx));
        }

        [Test]
        public void Diagonal_IsInRange_BecauseTheMetricIsChebyshev()
        {
            // ⚠ 이 자는 발사 명세와 **다르다**(저긴 유클리드). 도발은 「몇 칸 안」이고
            // 탄이 날아가지 않으므로 대각선이 같은 거리로 센다 — legacy arm 과 같은 자.
            var ctx = Ctx(out var caster);
            ctx.Add(2, new float3(2f, 0f, 2f), Faction.EnemyUnit);

            new AreaTauntSkill().Execute(caster, default, P(2, 4f), ctx);

            Assert.AreEqual(1, TauntCount(ctx));
        }

        [Test]
        public void UnreachableLayer_IsNotCalled()
        {
            // 빼면 **근접 가디언이 하늘의 적을 끌어온다**.
            var ctx = Ctx(out var caster);
            ctx.Units[1].AttackTraversalLayers = 0x01;
            ctx.Add(2, new float3(1f, 0f, 0f), Faction.EnemyUnit, u => u.TraversalLayers = 0x02);

            new AreaTauntSkill().Execute(caster, default, P(3, 4f), ctx);

            Assert.AreEqual(0, TauntCount(ctx));
        }

        [Test]
        public void DeadOrLeaping_IsNotCalled()
        {
            var ctx = Ctx(out var caster);
            ctx.Add(2, new float3(1f, 0f, 0f), Faction.EnemyUnit, u => u.Dead = true);
            ctx.Add(3, new float3(1f, 0f, 1f), Faction.EnemyUnit, u => u.InUltimateLeap = true);

            new AreaTauntSkill().Execute(caster, default, P(3, 4f), ctx);

            Assert.AreEqual(0, TauntCount(ctx));
        }

        [Test]
        public void NotAGuardian_CannotTaunt()
        {
            var ctx = Ctx(out var caster);
            ctx.Units[1].HasAggroCapacity = false;
            ctx.Add(2, new float3(1f, 0f, 0f), Faction.EnemyUnit);

            new AreaTauntSkill().Execute(caster, default, P(3, 4f), ctx);

            Assert.AreEqual(0, TauntCount(ctx));
        }

        [Test]
        public void DegenerateAuthoring_ConsumesTheFireQuietly()
        {
            var ctx = Ctx(out var caster);
            ctx.Add(2, new float3(1f, 0f, 0f), Faction.EnemyUnit);

            new AreaTauntSkill().Execute(caster, default, P(3, 0f), ctx);
            new AreaTauntSkill().Execute(caster, default, P(0, 4f), ctx);

            Assert.AreEqual(0, TauntCount(ctx));
        }
    }
}
