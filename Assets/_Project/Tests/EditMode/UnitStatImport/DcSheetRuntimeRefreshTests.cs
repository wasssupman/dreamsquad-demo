using NUnit.Framework;
using UnityEngine;
using Wassup.Core;
using Wassup.Data;
using Wassup.Data.StatImport;
using Wassup.BattleCore.Trigger;

namespace Wassup.Tests.EditMode.UnitStatImport
{
    // runtime-stat-refresh unit 3 — dreamcatcher catalog/ref-based apply core,
    // driven without a network via DcSheetRuntimeRefresher.ApplyBodies.
    public class DcSheetRuntimeRefreshTests
    {
        // skill-data-table unit 5 — 탭 계약 = `DcSheetTabs`(DcMechanics 은퇴 · Skills/SkillOwners).
        private static readonly string[] Tabs = DcSheetTabs.Default();

        private static string Body(string rowsJson) => $"{{ \"success\": true, \"data\": [{rowsJson}] }}";
        private const string Empty = @"{ ""success"": true, ""data"": [] }";
        private const string ErrorBody = @"{ ""success"": false, ""errorDetail"": { ""errorCode"": ""INTERNAL_SERVER_ERROR"", ""detailMessage"": ""구글 시트 연동 실패"" } }";

        private static SheetFetcher.Result[] Results(string cards, string effects,
            string attackMods, string skills, string config, string skillRows = Empty, string owners = Empty)
        {
            return new[]
            {
                new SheetFetcher.Result(cards, null),
                new SheetFetcher.Result(effects, null),
                new SheetFetcher.Result(attackMods, null),
                new SheetFetcher.Result(skills, null),
                new SheetFetcher.Result(config, null),
                new SheetFetcher.Result(skillRows, null),
                new SheetFetcher.Result(owners, null),
            };
        }

        [Test]
        public void ApplyBodies_UpdatesCardsSkillsConfigInMemory()
        {
            var card = ScriptableObject.CreateInstance<DreamcatcherCard>();
            card.id = "test_card";
            card.displayName = "OLD";
            card.effects = new[] { new CardEffect { kind = CardBuffKind.AttackDamage, percent = 10f } };

            var skill = ScriptableObject.CreateInstance<SkillData>();
            skill.id = "sk";
            skill.magnitude = 40f;

            var active = ScriptableObject.CreateInstance<DreamcatcherCard>();
            active.id = "active_x";
            active.type = CardType.Active;
            active.skill = skill;

            var awakening = ScriptableObject.CreateInstance<AwakeningConfig>();
            awakening.id = "awk";
            awakening.handSize = 5;

            var rule = ScriptableObject.CreateInstance<DeckRuleConfig>();
            rule.id = "rule";

            var catalog = ScriptableObject.CreateInstance<DreamcatcherCardCatalog>();
            catalog.cards = new[] { card };
            catalog.ruleConfig = rule;

            string log = DcSheetRuntimeRefresher.ApplyBodies(
                Results(
                    Body(@"{ ""id"": ""test_card"", ""displayName"": ""NEW"" }"),
                    Body(@"{ ""cardId"": ""test_card"", ""slot"": 0, ""kind"": ""AttackDamage"", ""percent"": 25 }"),
                    Empty,
                    Body(@"{ ""id"": ""sk"", ""magnitude"": 200 }"),
                    Body(@"{ ""id"": ""awk"", ""handSize"": 4 }")),
                Tabs, catalog, new[] { active }, awakening);

            Assert.AreEqual("NEW", card.displayName, "DcCards flat field applied to catalog card");
            Assert.AreEqual(25f, card.effects[0].percent, "DcCardEffects rebuilt the effect");
            Assert.AreEqual(200f, skill.magnitude, "DcSkills applied to active card's wrapped skill");
            Assert.AreEqual(4, awakening.handSize, "DcConfig applied to AwakeningConfig");
            StringAssert.Contains("Matched", log);

            Object.DestroyImmediate(card);
            Object.DestroyImmediate(skill);
            Object.DestroyImmediate(active);
            Object.DestroyImmediate(awakening);
            Object.DestroyImmediate(rule);
            Object.DestroyImmediate(catalog);
        }

        [Test]
        public void ApplyBodies_UnknownId_LeavesInstancesUntouched()
        {
            var card = ScriptableObject.CreateInstance<DreamcatcherCard>();
            card.id = "test_card";
            card.displayName = "OLD";
            var catalog = ScriptableObject.CreateInstance<DreamcatcherCardCatalog>();
            catalog.cards = new[] { card };

            string log = DcSheetRuntimeRefresher.ApplyBodies(
                Results(Body(@"{ ""id"": ""ghost"", ""displayName"": ""X"" }"),
                    Empty, Empty, Empty, Empty),
                Tabs, catalog, null, null);

            Assert.AreEqual("OLD", card.displayName, "unmatched sheet id must not touch other cards");
            StringAssert.Contains("no match for id='ghost'", log);

            Object.DestroyImmediate(card);
            Object.DestroyImmediate(catalog);
        }

        [Test]
        public void ApplyBodies_OneTabFails_AppliesHealthyTabsOnly()
        {
            var card = ScriptableObject.CreateInstance<DreamcatcherCard>();
            card.id = "test_card";
            card.displayName = "OLD";
            card.effects = new[] { new CardEffect { kind = CardBuffKind.AttackDamage, percent = 10f } };
            var catalog = ScriptableObject.CreateInstance<DreamcatcherCardCatalog>();
            catalog.cards = new[] { card };

            // DcCards fails (error envelope); DcCardEffects succeeds — partial-update
            // must still rebuild the effect while the failed tab is reported.
            string log = DcSheetRuntimeRefresher.ApplyBodies(
                Results(ErrorBody,
                    Body(@"{ ""cardId"": ""test_card"", ""slot"": 0, ""kind"": ""AttackDamage"", ""percent"": 25 }"),
                    Empty, Empty, Empty),
                Tabs, catalog, null, null);

            Assert.AreEqual(25f, card.effects[0].percent, "healthy DcCardEffects tab must still apply");
            Assert.AreEqual("OLD", card.displayName, "failed DcCards tab must not change flat fields");
            StringAssert.Contains("[DcCards] fetch failed", log);
            StringAssert.Contains("구글 시트 연동 실패", log);

            Object.DestroyImmediate(card);
            Object.DestroyImmediate(catalog);
        }

        // skill-data-table unit 5 — 로그인 자동 import(LoginAutoImport → AllRuntimeRefresher → 이 코어)가 새 두 탭으로 효과 값과
        // 소유 줄을 **메모리에서** 고친다. 같은 호출에서 다른 탭(DcCards)도 그대로 적용된다. 방어유닛 소유자도 같은 형식이다.
        [Test]
        public void ApplyBodies_SkillTabs_UpdateEffectAndOwners_InMemory()
        {
            var card = ScriptableObject.CreateInstance<DreamcatcherCard>();
            card.id = "test_card";
            card.displayName = "OLD";
            TestBindings.Attach(card, new[]
            {
                new DcMechanic
                {
                    trigger = new TriggerSpec { kind = TriggerKind.AttackN, period = 5 },
                    payload = new DcPayloadSpec { kind = EffectKind.SelfTileAoe, magnitude = 20, tileRange = 1 },
                },
            });
            var effect = card.bindings[0].effect;
            effect.id = "test_aoe";
            var unit = ScriptableObject.CreateInstance<DefenderUnitData>();
            unit.id = "test_unit";
            var catalog = ScriptableObject.CreateInstance<DreamcatcherCardCatalog>();
            catalog.cards = new[] { card };

            string log = DcSheetRuntimeRefresher.ApplyBodies(
                Results(Body(@"{ ""id"": ""test_card"", ""displayName"": ""NEW"" }"),
                    Empty, Empty, Empty, Empty,
                    Body(@"{ ""effect_id"": ""test_aoe"", ""damage"": 99, ""radius_tiles"": 3 }"),
                    Body(@"{ ""owner_kind"": ""card"", ""owner_id"": ""test_card"", ""slot"": 0, ""period"": 2 },
                           { ""owner_kind"": ""defender"", ""owner_id"": ""test_unit"", ""slot"": 0, ""trigger"": ""OnPlace"", ""effect_id"": ""test_aoe"" }")),
                Tabs, catalog, null, null, new[] { unit }, null);

            Assert.AreEqual("NEW", card.displayName, "other tabs still apply");
            Assert.AreEqual(99f, effect.values.damage, "Skills 탭이 효과 값을 고친다");
            Assert.AreEqual(3, effect.values.radiusTiles);
            Assert.AreEqual(2, card.bindings[0].trigger.period, "SkillOwners 탭이 카드 소유 줄을 고친다(빈 칸 = 그대로)");
            Assert.AreEqual(TriggerKind.AttackN, card.bindings[0].trigger.kind);
            Assert.AreSame(effect, card.bindings[0].effect);
            Assert.AreEqual(1, unit.bindings.Length, "방어유닛도 같은 형식으로 소유 줄을 얻는다");
            Assert.AreEqual(TriggerKind.OnPlace, unit.bindings[0].trigger.kind);
            Assert.AreSame(effect, unit.bindings[0].effect, "같은 효과 id = 같은 효과 에셋(복사 없음)");
            StringAssert.Contains("[skills-diff]", log);

            Object.DestroyImmediate(effect);
            Object.DestroyImmediate(card);
            Object.DestroyImmediate(unit);
            Object.DestroyImmediate(catalog);
        }

        [Test]
        public void TabContract_RetiresDcMechanics_AndAddsSkillTabs()
        {
            CollectionAssert.DoesNotContain(DcSheetTabs.Default(), "DcMechanics");
            Assert.AreEqual(DcSheetTabs.Count, DcSheetTabs.Default().Length);
            Assert.AreEqual("Skills", DcSheetTabs.Default()[DcSheetTabs.SkillsAt]);
            Assert.AreEqual("SkillOwners", DcSheetTabs.Default()[DcSheetTabs.SkillOwnersAt]);
            Assert.AreEqual("DcSkills", DcSheetTabs.Default()[DcSheetTabs.ActiveSkillsAt], "U18 — 액티브 탭은 남는다");
        }
    }
}
