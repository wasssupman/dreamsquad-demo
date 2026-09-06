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
            // unit 23a — 자기중심 광역의 원점 항은 시전자 몸이다. 라이브 최소치(폭1 = 0.5)를 준다.
            caster = CasterRef.OfUnit(new SkillEntityId(1), Faction.DefenderUnit, bodyRadius: 0.5f);
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

        // attach-range-preview 0a — 광역 자는 **원**(반경 N + 칸 반폭 + 몸)이다. 사거리(발사 명세)와
        // 다른 점은 칸 반폭 0.5 — 「반경 N = N칸 안」이라는 저작 어휘를 지키는 항이다.
        [Test]
        public void Diagonal_InsideTheCircle_IsInRange()
        {
            // (1.5, 1.5) = 2.12 ≤ 2 + 0.5 — 대각도 원 안이면 걸린다.
            var ctx = Ctx(out var caster);
            ctx.Add(2, new float3(1.5f, 0f, 1.5f), Faction.EnemyUnit);

            new AreaTauntSkill().Execute(caster, default, P(2, 4f), ctx);

            Assert.AreEqual(1, TauntCount(ctx));
        }

        [Test]
        public void FarDiagonalCorner_IsOutsideTheCircle()
        {
            // (2, 2) = 2.83 > 2.5 — 옛 사각(체비셰프 2)에선 걸렸던 모서리. 원에선 빠진다.
            var ctx = Ctx(out var caster);
            ctx.Add(2, new float3(2f, 0f, 2f), Faction.EnemyUnit);

            new AreaTauntSkill().Execute(caster, default, P(2, 4f), ctx);

            Assert.AreEqual(0, TauntCount(ctx));
        }

        // unit 23 완료 기준 — **배스티온 실측**. 저작 `Ability_Taunt_Bastion.tileRange = 2` 이고
        // 몸이 1.5(폭3)라 도형 반경은 **3.5** 다. 여기까지가 「자기중심 광역의 원점 항은 시전자 몸」의
        // 구체 수치이고, 이 값이 2.5 로 돌아가면 unit 22·23 이 고친 결함이 재유입된 것이다.
        // ⚠ 일반 멤버십 차등(`AreaCircleMembershipTests`)과 별개로 **이 스킬 경로**를 지난다 —
        //    unit 23 이 진입점을 갈아끼운 자리가 여기라 helper 만 초록이어도 여기가 빨갈 수 있다.
        [Test]
        public void BastionBody_WidensTauntShapeTo3_5()
        {
            var ctx = new TestSkillContext();
            ctx.Add(1, float3.zero, Faction.DefenderUnit, u => u.HasAggroCapacity = true);
            ctx.Add(2, new float3(3.4f, 0f, 0f), Faction.EnemyUnit);   // 3.5 안
            ctx.Add(3, new float3(3.6f, 0f, 0f), Faction.EnemyUnit);   // 3.5 밖
            var bastion = CasterRef.OfUnit(new SkillEntityId(1), Faction.DefenderUnit, bodyRadius: 1.5f);

            new AreaTauntSkill().Execute(bastion, default, P(2, 4f), ctx);

            Assert.AreEqual(1, TauntCount(ctx),
                "3.4 는 안(2 + 1.5) · 3.6 은 밖이어야 한다 — 0 이면 원점 항이 다시 칸 상수다");
            Assert.AreEqual(2, ctx.SimIntents[0].Target.Value, "안쪽 적만 불려야 한다");
        }

        // 대조군 — 같은 저작에 몸만 폭1(0.5)이면 도형 반경은 2.5 이고 3.4 는 **밖**이다.
        // 이 짝이 없으면 위 단언이 「그냥 사거리가 넉넉해서」 통과하는지 구분되지 않는다.
        [Test]
        public void ThinBody_SameAuthoring_DoesNotReach3_4()
        {
            var ctx = new TestSkillContext();
            ctx.Add(1, float3.zero, Faction.DefenderUnit, u => u.HasAggroCapacity = true);
            ctx.Add(2, new float3(3.4f, 0f, 0f), Faction.EnemyUnit);
            var thin = CasterRef.OfUnit(new SkillEntityId(1), Faction.DefenderUnit, bodyRadius: 0.5f);

            new AreaTauntSkill().Execute(thin, default, P(2, 4f), ctx);

            Assert.AreEqual(0, TauntCount(ctx), "폭1 이면 2.5 까지다 — 3.4 는 밖");
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
