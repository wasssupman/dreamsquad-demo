using NUnit.Framework;
using Unity.Mathematics;
using Wassup.Battle.Units;
using Wassup.Skills;
using Wassup.Skills.Concrete;

namespace Wassup.Tests.EditMode
{
    // skill-layer-migration unit 2b — 스탯 오라 세 파생의 계약.
    //
    // 로직은 `StatAuraSkill` 하나이고 파생은 **네 축**만 선언한다:
    // 누구에게(아군/상대) · 무슨 스탯(고정/저작) · 자기 포함 · 병합 출처.
    // 이 그물이 지키는 것은 그 네 축이 실제로 갈린다는 것 — 하나로 접히면
    // 보스 채찍과 배치 오라가 서로를 덮는다.
    public class StatAuraSkillTests
    {
        private static SkillParams P(float percent, int radius, float ttl, SkillStatKind stat)
            => new SkillParams(percent, ttl, radius, 0, SkillParams.NoDataIndex, 0, 0f, 0f,
                               0f, 0, 0, 0f, SkillParams.NoDataIndex, (int)stat);

        private static TestSkillContext Ctx(out CasterRef caster)
        {
            var ctx = new TestSkillContext();
            ctx.Add(1, float3.zero, Faction.DefenderUnit);
            caster = CasterRef.OfUnit(new SkillEntityId(1), Faction.DefenderUnit);
            return ctx;
        }

        private static System.Collections.Generic.List<SimIntent> Mods(TestSkillContext ctx)
        {
            var r = new System.Collections.Generic.List<SimIntent>();
            foreach (var it in ctx.SimIntents)
                if (it.Kind == SimIntentKind.ApplyStatModifier) r.Add(it);
            return r;
        }

        // ── 진영 축 ──────────────────────────────────────────────────

        [Test]
        public void AllyAura_BuffsAllies_NotOpponents()
        {
            var ctx = Ctx(out var caster);
            ctx.Add(2, new float3(1f, 0f, 0f), Faction.DefenderUnit);
            ctx.Add(3, new float3(1f, 0f, 1f), Faction.EnemyUnit);

            new AllyStatAuraSkill().Execute(caster, default, P(30f, 3, 6f, SkillStatKind.DamageMul), ctx);

            var m = Mods(ctx);
            // 가디언은 자기도 받는다 → 자기 + 아군 = 2
            Assert.AreEqual(2, m.Count, "아군(+자기)만 받아야 한다");
            foreach (var it in m) Assert.AreNotEqual(3, it.Target.Value, "적이 아군 버프를 받았다");
        }

        [Test]
        public void OpponentAura_DebuffsOpponents_NotAllies()
        {
            var ctx = Ctx(out var caster);
            ctx.Add(2, new float3(1f, 0f, 0f), Faction.DefenderUnit);
            ctx.Add(3, new float3(1f, 0f, 1f), Faction.EnemyUnit);

            new OpponentStatAuraSkill().Execute(caster, default, P(-90f, 3, 1.5f, SkillStatKind.MoveSpeedMul), ctx);

            var m = Mods(ctx);
            Assert.AreEqual(1, m.Count);
            Assert.AreEqual(3, m[0].Target.Value, "상대만 받아야 한다");
        }

        // ── 자기 포함 축 ─────────────────────────────────────────────

        [Test]
        public void GuardianIncludesItself_WhipDoesNot()
        {
            var ctx = Ctx(out var caster);
            ctx.Add(2, new float3(1f, 0f, 0f), Faction.DefenderUnit);

            new AllyStatAuraSkill().Execute(caster, default, P(30f, 3, 6f, SkillStatKind.DamageMul), ctx);
            bool guardianSelf = Mods(ctx).Exists(it => it.Target.Value == 1);

            var ctx2 = Ctx(out var caster2);
            ctx2.Add(2, new float3(1f, 0f, 0f), Faction.DefenderUnit);
            new AllySpeedAuraSkill().Execute(caster2, default, P(20f, 3, 6f, SkillStatKind.MoveSpeedMul), ctx2);
            bool whipSelf = Mods(ctx2).Exists(it => it.Target.Value == 1);

            Assert.IsTrue(guardianSelf, "가디언은 자기도 버프한다(레거시 arm 의 명시 결정)");
            Assert.IsFalse(whipSelf, "보스 채찍은 자기를 뺀다(기존 계약)");
        }

        // ── 스탯 축 ──────────────────────────────────────────────────

        [Test]
        public void AuthoredStat_IsHonored()
        {
            var ctx = Ctx(out var caster);
            ctx.Add(2, new float3(1f, 0f, 0f), Faction.EnemyUnit);

            new OpponentStatAuraSkill().Execute(caster, default, P(-50f, 3, 2f, SkillStatKind.AttackSpeedMul), ctx);

            Assert.AreEqual((int)SkillStatKind.AttackSpeedMul, Mods(ctx)[0].Selector);
        }

        [Test]
        public void WhipIgnoresAuthoredStat_BecauseItsNameSaysTheStat()
        {
            // ⚠ 보스 채찍 슬롯은 `buffStat` 을 안 채워 왔다. 저작을 읽기 시작하면
            // 기본값 0(공격력)이 되어 **채찍이 조용히 다른 오라가 된다.**
            var ctx = Ctx(out var caster);
            ctx.Add(2, new float3(1f, 0f, 0f), Faction.DefenderUnit);

            new AllySpeedAuraSkill().Execute(caster, default, P(20f, 3, 6f, SkillStatKind.DamageMul), ctx);

            Assert.AreEqual((int)SkillStatKind.MoveSpeedMul, Mods(ctx)[0].Selector,
                "저작이 무엇이든 채찍은 이동속도다");
        }

        // ── 병합 출처 축 ─────────────────────────────────────────────

        [Test]
        public void Origin_SeparatesWhipFromOnPlaceAuras()
        {
            // 출처는 병합 키의 일부다 — 하나로 묶으면 채찍과 배치 오라가 서로를 덮는다.
            var ctx = Ctx(out var caster);
            ctx.Add(2, new float3(1f, 0f, 0f), Faction.DefenderUnit);
            new AllySpeedAuraSkill().Execute(caster, default, P(20f, 3, 6f, SkillStatKind.MoveSpeedMul), ctx);

            var ctx2 = Ctx(out var caster2);
            ctx2.Add(2, new float3(1f, 0f, 0f), Faction.DefenderUnit);
            new AllyStatAuraSkill().Execute(caster2, default, P(30f, 3, 6f, SkillStatKind.DamageMul), ctx2);

            Assert.AreEqual(SkillModifierOrigin.Boss, Mods(ctx)[0].Origin);
            Assert.AreEqual(SkillModifierOrigin.OnPlace, Mods(ctx2)[0].Origin);
        }

        // ── 값 변환 ──────────────────────────────────────────────────

        [Test]
        public void PercentBecomesMultiplier_Once()
        {
            // 저작은 퍼센트(30 = +30%). 배율 변환은 concrete 가 한 번만 한다 —
            // 어댑터가 또 하면 조용히 제곱된다.
            var ctx = Ctx(out var caster);
            ctx.Add(2, new float3(1f, 0f, 0f), Faction.EnemyUnit);

            new OpponentStatAuraSkill().Execute(caster, default, P(-90f, 3, 1.5f, SkillStatKind.MoveSpeedMul), ctx);

            Assert.AreEqual(0.1f, Mods(ctx)[0].Amount, 1e-4f, "-90% → ×0.1");
        }

        [Test]
        public void DegenerateAuthoring_ConsumesTheFireQuietly()
        {
            var ctx = Ctx(out var caster);
            ctx.Add(2, new float3(1f, 0f, 0f), Faction.EnemyUnit);

            new OpponentStatAuraSkill().Execute(caster, default, P(0f, 3, 2f, SkillStatKind.MoveSpeedMul), ctx);
            new OpponentStatAuraSkill().Execute(caster, default, P(-50f, 3, 0f, SkillStatKind.MoveSpeedMul), ctx);

            Assert.AreEqual(0, Mods(ctx).Count);
        }

        [Test]
        public void OpponentAura_RespectsTraversalLayerGate()
        {
            // 「내가 못 때리는 층」을 감속시킬 수는 없다(도발과 같은 판단).
            var ctx = Ctx(out var caster);
            ctx.Units[1].AttackTraversalLayers = 0x01;
            ctx.Add(2, new float3(1f, 0f, 0f), Faction.EnemyUnit, u => u.TraversalLayers = 0x02);

            new OpponentStatAuraSkill().Execute(caster, default, P(-90f, 3, 1.5f, SkillStatKind.MoveSpeedMul), ctx);

            Assert.AreEqual(0, Mods(ctx).Count);
        }
    }
}
