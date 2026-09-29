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
    // battle-core-rebuild unit 7a — 저작 어휘 ↔ 코어 어휘의 **매핑 핀** + 라이브 저작이 규칙으로 구워지는가.
    //
    // skill-data-table unit 4 — 트리거 · 효과 · 게이트 · 주체는 저작이 코어 enum 을 **직접** 들어 핀이 필요 없다(거울 은퇴).
    // 남은 핀 = 번호가 다른 어휘(공격 수식자 · CC · 스택 · 실드 필터 — 이름으로 옮긴다).
    public class CoreTriggerEnumPinTests
    {
        [Test]
        public void 공격_수식자는_상시_효과_셋과_이름이_같고_강공이_하나_더_있다()
        {
            // skill-data-table unit 8 — 옛 저작 미러(`DcAttackModKind`)는 은퇴 · 저작 = 효과 종류(상시 수식자 3). 번호는 다르다(이름으로 옮긴다).
            var authored = new[] { EffectKind.ProjectileBounce, EffectKind.FrontmostTarget, EffectKind.DamageVsSleeping };
            foreach (var k in authored)
            {
                var core = BindingDefinitionBuilder.ToCoreAttackMod(k);
                Assert.AreNotEqual(AttackModKind.None, core, k.ToString());
                Assert.AreEqual(k.ToString(), core.ToString());
            }
            foreach (EffectKind k in Enum.GetValues(typeof(EffectKind)))
                if (Array.IndexOf(authored, k) < 0)
                    Assert.AreEqual(AttackModKind.None, BindingDefinitionBuilder.ToCoreAttackMod(k), k + " 는 상시 수식자가 아니다");
            Assert.AreEqual(authored.Length + 2, Enum.GetValues(typeof(AttackModKind)).Length,
                "None + 상시 수식자 셋 + 강공(HeavyStrike — 트리거 저작이라 코어 축에만 있다)");
        }

        [Test]
        public void 군중_제어_스택_실드_필터는_번호가_달라_이름으로_옮긴다()
        {
            // skill-data-table unit 4 — CC · 스택은 효과 값이 스킬 번호를 직접 든다. 옛 번호 ↔ 새 번호 변환은 `EffectSlots` 가 한 곳에서.
            Assert.AreEqual((int)SkillCcKind.Stun, (int)EffectSlots.CcFromLegacy((int)DcCcKind.Stun));
            Assert.AreEqual((int)SkillCcKind.Impulse, (int)EffectSlots.CcFromLegacy((int)DcCcKind.Impulse));
            Assert.AreEqual((int)SkillCcKind.Sleep, (int)EffectSlots.CcFromLegacy((int)DcCcKind.Sleep));
            foreach (DcCcKind c in Enum.GetValues(typeof(DcCcKind)))
                Assert.AreEqual(c, EffectSlots.CcToLegacy(EffectSlots.CcFromLegacy((int)c)), c + " 왕복");
            foreach (DcStackKind s in Enum.GetValues(typeof(DcStackKind)))
            {
                Assert.AreEqual(s.ToString(), EffectSlots.StackFromLegacy((int)s).ToString());
                Assert.AreEqual(s, EffectSlots.StackToLegacy(EffectSlots.StackFromLegacy((int)s)), s + " 왕복");
            }
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
                // skill-data-table unit 4 — 소유 줄(`bindings`) 한 줄 = 규칙 한 줄(실드 캐스트도 소유 줄로 이전됐다).
                int expected = defenders[i].bindings != null ? defenders[i].bindings.Length : 0;
                int got = def.Units[i].Bindings != null ? def.Units[i].Bindings.Length : 0;
                Assert.AreEqual(expected, got, defenders[i].name + " — 저작 규칙이 조용히 빠졌다");
                authoredDefender += expected;
                bakedDefender += got;
            }
            Assert.Greater(bakedDefender, 0, "배치 스킬이 하나도 없다면 테스트가 공허하다");

            for (int i = 0; i < enemies.Length; i++)
            {
                // 분열은 적 고유 값이라 소유 줄에 없다(이전 — `splitUnit` · `splitCount`).
                int expected = enemies[i].bindings != null ? enemies[i].bindings.Length : 0;
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
