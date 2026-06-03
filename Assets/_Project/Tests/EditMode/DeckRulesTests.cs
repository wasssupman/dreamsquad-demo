using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // dreamcatcher-deck-builder Unit 1 — DeckRules validity (exactly 10, unique<=2).
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
                MakeCard("n1", CardCategory.Normal),
                MakeCard("n2", CardCategory.Normal),
                MakeCard("u1", CardCategory.Unique),
                MakeCard("u2", CardCategory.Unique),
                MakeCard("u3", CardCategory.Unique),
            };
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _created) if (o != null) Object.DestroyImmediate(o);
            _created.Clear();
        }

        private DreamcatcherCard MakeCard(string id, CardCategory cat)
        {
            var c = ScriptableObject.CreateInstance<DreamcatcherCard>();
            c.id = id; c.category = cat;
            _created.Add(c);
            return c;
        }

        private static List<string> Repeat(string id, int n) => Enumerable.Repeat(id, n).ToList();

        [Test]
        public void TenNormals_IsValid()
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
        public void TwoUnique_IsValid_ThreeUnique_Invalid()
        {
            var ok = new List<string>(Repeat("n1", 8)) { "u1", "u2" };
            Assert.IsTrue(DeckRules.Validate(ok, _catalog, out _), "2 unique allowed");

            var bad = new List<string>(Repeat("n1", 7)) { "u1", "u2", "u3" };
            Assert.IsFalse(DeckRules.Validate(bad, _catalog, out var reason), "3 unique rejected");
            StringAssert.Contains("unique", reason);
        }

        [Test]
        public void UnknownCard_Invalid()
        {
            var deck = new List<string>(Repeat("n1", 9)) { "ghost" };
            Assert.IsFalse(DeckRules.Validate(deck, _catalog, out var reason));
            StringAssert.Contains("unknown", reason);
        }

        [Test]
        public void UniqueCount_IsAccurate()
        {
            var deck = new List<string>(Repeat("n1", 8)) { "u1", "u2" };
            Assert.AreEqual(2, DeckRules.UniqueCount(deck, _catalog));
            Assert.AreEqual(0, DeckRules.UniqueCount(Repeat("n1", 10), _catalog));
        }
    }
}
