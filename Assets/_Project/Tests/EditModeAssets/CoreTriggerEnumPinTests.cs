using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using Wassup.BattleCore;
using Wassup.BattleCore.Combat;
using Wassup.BattleCore.Trigger;
using Wassup.BattleCoreUnity;
using Wassup.Data;
using Wassup.Skills;

namespace Wassup.Tests.EditMode
{
    // battle-core-rebuild unit 7a — 저작 트리거 어휘 ↔ 코어 미러의 **값·개수·매핑 핀**.
    //
    // 저작 enum(`DcTriggerKind` 10 · `DcPayloadKind` 33 · 게이트 2축 · 공격 수식자)은 시트가 **값으로 왕복**하므로
    // append-only 이고 코어는 같은 번호의 미러를 든다. 어셈블리가 갈려 컴파일러가 못 잡는다 — 6a 의
    // `CoreSkillEnumPinTests` 와 같은 그물을 여기 친다(저작 타입이 이 lane 에서만 보인다 — 헤드리스 lane 밖).
    public class CoreTriggerEnumPinTests
    {
        private static void Pin<TAuthored, TCore>() where TAuthored : Enum where TCore : Enum
        {
            var authored = Enum.GetNames(typeof(TAuthored));
            CollectionAssert.AreEquivalent(Enum.GetNames(typeof(TCore)), authored,
                $"{typeof(TAuthored).Name} ↔ {typeof(TCore).Name} — 한쪽에만 값이 늘었다");
            foreach (var name in authored)
                Assert.AreEqual(Convert.ToInt32(Enum.Parse(typeof(TAuthored), name)),
                                Convert.ToInt32(Enum.Parse(typeof(TCore), name)), $"{name} 의 번호가 갈렸다");
        }

        private static void PinMapping<TAuthored, TCore>(Func<TAuthored, TCore> map) where TAuthored : Enum where TCore : Enum
        {
            foreach (TAuthored a in Enum.GetValues(typeof(TAuthored)))
                Assert.AreEqual(a.ToString(), map(a).ToString(), $"{a} 가 다른 이름으로 옮겨진다");
        }

        [Test] public void 트리거_10() { Pin<DcTriggerKind, TriggerKind>(); Assert.AreEqual(10, Enum.GetValues(typeof(TriggerKind)).Length); PinMapping<DcTriggerKind, TriggerKind>(BindingDefinitionBuilder.ToCoreTrigger); }
        [Test] public void 페이로드_33() { Pin<DcPayloadKind, EffectKind>(); Assert.AreEqual(33, Enum.GetValues(typeof(EffectKind)).Length); PinMapping<DcPayloadKind, EffectKind>(BindingDefinitionBuilder.ToCorePayload); }
        // unified-effect-layer unit 5 — 트리거 주체 축. 이름이 코어와 달라(「남의 배치」 ↔ `Any`) 값과 매핑을 손으로 고정한다.
        [Test]
        public void 트리거_주체()
        {
            Assert.AreEqual(0, (int)DcTriggerSubject.Self, "기본값 0 = 기존 저작 전부");
            Assert.AreEqual(1, (int)DcTriggerSubject.OthersPlacement);
            Assert.AreEqual(2, Enum.GetValues(typeof(DcTriggerSubject)).Length, "값이 늘면 매핑도 늘린다");
            Assert.AreEqual(BindingSubject.Self, BindingDefinitionBuilder.ToCoreSubject(DcTriggerSubject.Self));
            Assert.AreEqual(BindingSubject.Any, BindingDefinitionBuilder.ToCoreSubject(DcTriggerSubject.OthersPlacement));
            Assert.AreEqual(0, (int)BindingSubject.Self);
            Assert.AreEqual(1, (int)BindingSubject.Any);
            Assert.AreEqual(1, (int)BindingSubjectFilter.PlacedDefender);
        }

        [Test] public void 게이트() { Pin<DcGateKind, GateKind>(); Pin<DcGateSubject, GateSubject>(); PinMapping<DcGateKind, GateKind>(BindingDefinitionBuilder.ToCoreGate); PinMapping<DcGateSubject, GateSubject>(BindingDefinitionBuilder.ToCoreGateSubject); }

        [Test]
        public void 공격_수식자는_앞_넷이_미러이고_강공이_하나_더_있다()
        {
            foreach (DcAttackModKind a in Enum.GetValues(typeof(DcAttackModKind)))
            {
                Assert.AreEqual((int)a, Convert.ToInt32(Enum.Parse(typeof(AttackModKind), a.ToString())), a.ToString());
                Assert.AreEqual(a.ToString(), BindingDefinitionBuilder.ToCoreAttackMod(a).ToString());
            }
            Assert.AreEqual(Enum.GetValues(typeof(DcAttackModKind)).Length + 1, Enum.GetValues(typeof(AttackModKind)).Length,
                "강공(HeavyStrike)은 저작상 payload 라 코어 축에만 있다");
        }

        [Test]
        public void 군중_제어_스택_실드_필터는_번호가_달라_이름으로_옮긴다()
        {
            Assert.AreEqual(SkillCcKind.Stun, BindingDefinitionBuilder.ToSkillCc(DcCcKind.Stun));
            Assert.AreEqual(SkillCcKind.Impulse, BindingDefinitionBuilder.ToSkillCc(DcCcKind.Impulse));
            Assert.AreEqual(SkillCcKind.Sleep, BindingDefinitionBuilder.ToSkillCc(DcCcKind.Sleep));
            foreach (DcStackKind s in Enum.GetValues(typeof(DcStackKind)))
                Assert.AreEqual(s.ToString(), BindingDefinitionBuilder.ToSkillStack(s).ToString());
            Assert.AreEqual(SkillShieldFilter.MostHurt, BindingDefinitionBuilder.ToSkillShieldFilter(ShieldTargetFilter.MinHealth));
        }

        // ── 라이브 저작이 규칙으로 구워지는가 ─────────────────────────────────

        private static T[] All<T>(string filter) where T : UnityEngine.Object
        {
            var list = new List<T>();
            foreach (var g in AssetDatabase.FindAssets(filter))
            {
                var a = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(g));
                if (a != null) list.Add(a);
            }
            return list.ToArray();
        }

        [Test]
        public void 라이브_유닛_능력과_적_악몽이_전부_규칙이_된다()
        {
            var defenders = All<DefenderUnitData>("t:DefenderUnitData");
            var enemies = All<AttackUnitData>("t:AttackUnitData");
            var def = MatchDefinitionBuilder.Build(defenders, enemies, 1, ModeDef.Default());

            int authoredDefender = 0, bakedDefender = 0;
            for (int i = 0; i < defenders.Length; i++)
            {
                var skill = defenders[i].GetAbility<UnitSkillAbility>();
                int expected = skill?.mechanics != null ? skill.mechanics.Length : 0;
                var shield = defenders[i].GetAbility<ShieldCastAbility>();
                if (shield != null && shield.cooldown > 0f && shield.amount > 0f) expected++;
                int got = def.Units[i].Bindings != null ? def.Units[i].Bindings.Length : 0;
                Assert.AreEqual(expected, got, defenders[i].name + " — 저작 규칙이 조용히 빠졌다");
                authoredDefender += expected;
                bakedDefender += got;
            }
            Assert.Greater(bakedDefender, 0, "배치 스킬이 하나도 없다면 테스트가 공허하다");

            for (int i = 0; i < enemies.Length; i++)
            {
                var mech = enemies[i].nightmareMechanics;
                if (mech == null) continue;
                int expected = 0;
                foreach (var m in mech)
                    if (m.payload.kind != DcPayloadKind.SplitOnDeath) expected++;   // 분열 = 무항목(S8, 7d)
                int got = def.Enemies[i].Bindings != null ? def.Enemies[i].Bindings.Length : 0;
                Assert.AreEqual(expected, got, enemies[i].name);
            }
            foreach (var b in def.Bindings)
                Assert.IsNotNull(b.Skill, b.Label + " — 실행자 없는 규칙이 구워졌다");
        }

        [Test]
        public void 궁극기_규칙만_생존당_한_번이고_다른_경계_규칙은_발동_상한이_없다()
        {
            // unit 7d 구현 9 — 「생존당 1회」는 `fireCap 1` 이다. 옛 전투는 `fraction ≥ 0.5` 로 **우연히** 1회였다.
            // 라이브 짱쎈이 한 판에 둘을 다 든다(경계 × 궁극기 + 경계 × 도약·자폭) — 공허하지 않게 둘 다 센다.
            var enemies = All<AttackUnitData>("t:AttackUnitData");
            var def = MatchDefinitionBuilder.Build(All<DefenderUnitData>("t:DefenderUnitData"), enemies, 1, ModeDef.Default());
            int ultimates = 0, others = 0;
            foreach (var b in def.Bindings)
            {
                if (b.Trigger != TriggerKind.HealthThreshold) continue;
                if (def.EffectOf(b).Kind == EffectKind.UltimateLeap) { ultimates++; Assert.AreEqual(1, b.FireCap, b.Label); }
                else { others++; Assert.AreEqual(0, b.FireCap, b.Label + " — 경계 규칙은 다회가 사양"); }
            }
            Assert.Greater(ultimates, 0, "라이브 궁극기가 없다면 테스트가 공허하다");
            Assert.Greater(others, 0);
        }

        [Test]
        public void 캐논_배치_스킬은_한_발이_반경_안_전원에게인_명세를_가리킨다()
        {
            var cannon = AssetDatabase.LoadAssetAtPath<Wassup.Data.ProjectilePatternData>(
                "Assets/_Project/Data/Projectiles/Pattern_Cannon_Strike.asset");
            Assert.IsNotNull(cannon);
            Assert.IsTrue(cannon.fanOutToAllCandidates, "라이브 저작 = 1:1 융단폭격");
            var defenders = All<DefenderUnitData>("t:DefenderUnitData");
            var def = MatchDefinitionBuilder.Build(defenders, Array.Empty<AttackUnitData>(), 1, ModeDef.Default());
            bool found = false;
            foreach (var b in def.Bindings)
            {
                var fx = def.EffectOf(b);
                if (fx.Kind == EffectKind.EmitProjectilePattern && fx.PatternDefIndex >= 0
                    && def.Patterns[fx.PatternDefIndex].Id == cannon.id)
                {
                    found = true;
                    Assert.IsTrue(def.Patterns[fx.PatternDefIndex].FanOutToAllCandidates);
                    Assert.AreEqual(TriggerKind.OnPlace, b.Trigger);
                }
            }
            Assert.IsTrue(found, "캐논 배치 스킬이 규칙으로 안 구워졌다");
        }
    }
}
