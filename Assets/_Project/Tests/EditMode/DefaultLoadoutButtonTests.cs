using NUnit.Framework;
using UnityEngine;
using Wassup.Data;
using Wassup.UI;

namespace Wassup.Tests.EditMode
{
    // outgame-login-gate unit 6 — default deck seeding (order, rule size, holes).
    public class DefaultLoadoutButtonTests
    {
        private static DreamcatcherCard Card(string id)
        {
            var c = ScriptableObject.CreateInstance<DreamcatcherCard>();
            c.id = id;
            return c;
        }

        private static DreamcatcherDeck Deck(params DreamcatcherCard[] cards)
        {
            var d = ScriptableObject.CreateInstance<DreamcatcherDeck>();
            d.cards = cards;
            return d;
        }

        [Test]
        public void TruncatesToDeckSize_KeepingAuthoredOrder()
        {
            var deck = Deck(Card("a"), Card("b"), Card("c"), Card("d"));

            var save = DefaultLoadoutButton.BuildDefaultDeck(deck, 2);

            CollectionAssert.AreEqual(new[] { "a", "b" }, save.cardIds);
        }

        [Test]
        public void SkipsNullAndIdlessCards()
        {
            var deck = Deck(Card("a"), null, Card(""), Card("b"));

            var save = DefaultLoadoutButton.BuildDefaultDeck(deck, 10);

            CollectionAssert.AreEqual(new[] { "a", "b" }, save.cardIds);
        }

        [Test]
        public void FewerCardsThanDeckSize_TakesWhatExists()
        {
            var deck = Deck(Card("a"), Card("b"));

            var save = DefaultLoadoutButton.BuildDefaultDeck(deck, 8);

            CollectionAssert.AreEqual(new[] { "a", "b" }, save.cardIds);
        }

        [Test]
        public void NullSource_ReturnsEmptyDeckInsteadOfThrowing()
        {
            var save = DefaultLoadoutButton.BuildDefaultDeck(null, 8);

            Assert.IsNotNull(save.cardIds);
            Assert.AreEqual(0, save.cardIds.Count);
            Assert.AreEqual("deck_1", save.id, "the save still needs an id the profile can select");
        }
    }
}
