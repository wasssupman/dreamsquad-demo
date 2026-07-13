using NUnit.Framework;
using UnityEngine;
using Wassup.Core;
using Wassup.Data;
using Wassup.Data.StatImport;

namespace Wassup.Tests.EditMode.UnitStatImport
{
    // runtime-stat-refresh unit 3 — dreamcatcher catalog/ref-based apply core,
    // driven without a network via DcSheetRuntimeRefresher.ApplyBodies.
    public class DcSheetRuntimeRefreshTests
    {
        private static readonly string[] Tabs =
            { "DcCards", "DcCardEffects", "DcMechanics", "DcAttackMods", "DcSkills", "DcConfig" };

        private static string Body(string rowsJson) => $"{{ \"success\": true, \"data\": [{rowsJson}] }}";
        private const string Empty = @"{ ""success"": true, ""data"": [] }";
        private const string ErrorBody = @"{ ""success"": false, ""errorDetail"": { ""errorCode"": ""INTERNAL_SERVER_ERROR"", ""detailMessage"": ""구글 시트 연동 실패"" } }";

        private static SheetFetcher.Result[] Results(string cards, string effects, string mechanics,
            string attackMods, string skills, string config)
        {
            return new[]
            {
                new SheetFetcher.Result(cards, null),
                new SheetFetcher.Result(effects, null),
                new SheetFetcher.Result(mechanics, null),
                new SheetFetcher.Result(attackMods, null),
                new SheetFetcher.Result(skills, null),
                new SheetFetcher.Result(config, null),
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
                    Empty, Empty, Empty, Empty, Empty),
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
                    Empty, Empty, Empty, Empty),
                Tabs, catalog, null, null);

            Assert.AreEqual(25f, card.effects[0].percent, "healthy DcCardEffects tab must still apply");
            Assert.AreEqual("OLD", card.displayName, "failed DcCards tab must not change flat fields");
            StringAssert.Contains("[DcCards] fetch failed", log);
            StringAssert.Contains("구글 시트 연동 실패", log);

            Object.DestroyImmediate(card);
            Object.DestroyImmediate(catalog);
        }
    }
}
