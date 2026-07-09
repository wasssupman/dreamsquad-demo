using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // dreamcatcher-deck-builder Unit 1 — DeckRules validity (exactly 10, squad<=2).
    // dreamcatcher-card-taxonomy — cap moved from CardCategory.Unique to CardType.Squad.
    // u* = Squad-type (capped), n* = Unit-type (uncapped, repeatable).
    public class DeckRulesTests
    {
        private readonly List<Object> _created = new List<Object>();
        private DreamcatcherCardCatalog _catalog;

        [SetUp]
        public void SetUp()
        {
            _catalog = ScriptableObject.CreateInstance<DreamcatcherCardCatalog>();
            _created.Add(_catalog);
            _catalog.cards = new[]
            {
                MakeCard("n1", CardType.Unit),
                MakeCard("n2", CardType.Unit),
                MakeCard("u1", CardType.Squad),
                MakeCard("u2", CardType.Squad),
                MakeCard("u3", CardType.Squad),
            };
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _created) if (o != null) Object.DestroyImmediate(o);
            _created.Clear();
        }

        private DreamcatcherCard MakeCard(string id, CardType type)
        {
            var c = ScriptableObject.CreateInstance<DreamcatcherCard>();
            c.id = id; c.type = type;
            _created.Add(c);
            return c;
        }

        private static List<string> Repeat(string id, int n) => Enumerable.Repeat(id, n).ToList();

        [Test]
        public void TenUnits_IsValid()
        {
            Assert.IsTrue(DeckRules.Validate(Repeat("n1", 10), _catalog, out var reason), reason);
            Assert.AreEqual("ok", reason);
        }

        [Test]
        public void NineCards_InvalidCount()
        {
            Assert.IsFalse(DeckRules.Validate(Repeat("n1", 9), _catalog, out var reason));
            StringAssert.Contains("exactly", reason);
        }

        [Test]
        public void ElevenCards_InvalidCount()
        {
            Assert.IsFalse(DeckRules.Validate(Repeat("n1", 11), _catalog, out _));
        }

        [Test]
        public void TwoSquad_IsValid_ThreeSquad_Invalid()
        {
            var ok = new List<string>(Repeat("n1", 8)) { "u1", "u2" };
            Assert.IsTrue(DeckRules.Validate(ok, _catalog, out _), "2 squad allowed");

            var bad = new List<string>(Repeat("n1", 7)) { "u1", "u2", "u3" };
            Assert.IsFalse(DeckRules.Validate(bad, _catalog, out var reason), "3 squad rejected");
            StringAssert.Contains("squad", reason);
        }

        [Test]
        public void UnknownCard_Invalid()
        {
            var deck = new List<string>(Repeat("n1", 9)) { "ghost" };
            Assert.IsFalse(DeckRules.Validate(deck, _catalog, out var reason));
            StringAssert.Contains("unknown", reason);
        }

        [Test]
        public void SquadCount_IsAccurate()
        {
            var deck = new List<string>(Repeat("n1", 8)) { "u1", "u2" };
            Assert.AreEqual(2, DeckRules.SquadCount(deck, _catalog));
            Assert.AreEqual(0, DeckRules.SquadCount(Repeat("n1", 10), _catalog));
        }

        // dreamcatcher-card-taxonomy unit 2 — config drives the real constraint.
        [Test]
        public void Config_OverridesDeckSizeAndCaps()
        {
            var cfg = ScriptableObject.CreateInstance<DeckRuleConfig>();
            _created.Add(cfg);
            cfg.deckSize = 4;
            cfg.maxSquad = 1;   // stricter than default 2
            cfg.maxUnit = -1;   // unlimited
            _catalog.ruleConfig = cfg;

            // deck size now 4
            Assert.IsFalse(DeckRules.Validate(Repeat("n1", 10), _catalog, out _), "old size 10 now invalid");
            Assert.IsTrue(DeckRules.Validate(Repeat("n1", 4), _catalog, out _), "size 4 valid");

            // squad cap now 1
            var oneSquad = new List<string>(Repeat("n1", 3)) { "u1" };
            Assert.IsTrue(DeckRules.Validate(oneSquad, _catalog, out _), "1 squad allowed");
            var twoSquad = new List<string>(Repeat("n1", 2)) { "u1", "u2" };
            Assert.IsFalse(DeckRules.Validate(twoSquad, _catalog, out var reason), "2 squad rejected under cap 1");
            StringAssert.Contains("squad", reason);

            Assert.AreEqual(4, DeckRules.EffectiveDeckSize(_catalog));
            Assert.AreEqual(1, DeckRules.EffectiveMax(_catalog, CardType.Squad));
        }

        // Negative cap = unlimited (no rejection regardless of count).
        [Test]
        public void NegativeCap_MeansUnlimited()
        {
            var cfg = ScriptableObject.CreateInstance<DeckRuleConfig>();
            _created.Add(cfg);
            cfg.deckSize = 5;
            cfg.maxSquad = -1;  // unlimited squad
            cfg.maxUnit = -1;
            _catalog.ruleConfig = cfg;
            var allSquad = new List<string> { "u1", "u2", "u3", "u1", "u2" };
            Assert.IsTrue(DeckRules.Validate(allSquad, _catalog, out var reason), reason);
        }
    }
}
