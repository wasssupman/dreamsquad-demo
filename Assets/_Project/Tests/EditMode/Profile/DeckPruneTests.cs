using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Wassup.Core;
using Wassup.Data;

namespace Wassup.Tests.EditMode.Profile
{
    // dreamcatcher-card-visibility unit 2 — 숨김 카드 장착 해제의 순수 판정.
    public class DeckPruneTests
    {
        private readonly List<Object> _created = new List<Object>();

        private DreamcatcherCard NewCard(string id, int visible)
        {
            var c = ScriptableObject.CreateInstance<DreamcatcherCard>();
            c.id = id;
            c.visible = visible;
            _created.Add(c);
            return c;
        }

        private DreamcatcherCardCatalog NewCatalog(params DreamcatcherCard[] cards)
        {
            var cat = ScriptableObject.CreateInstance<DreamcatcherCardCatalog>();
            cat.cards = cards;
            _created.Add(cat);
            return cat;
        }

        private static PlayerProfile ProfileWithDeck(params string[] ids)
        {
            return new PlayerProfile
            {
                selectedDeckId = "deck_1",
                dreamcatcherDecks = new List<DreamcatcherPreset>
                {
                    new DreamcatcherPreset { id = "deck_1", cardIds = new List<string>(ids) },
                },
            };
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _created) Object.DestroyImmediate(o);
            _created.Clear();
        }

        [Test]
        public void RemoveHiddenCards_DropsOnlyHidden()
        {
            var catalog = NewCatalog(NewCard("a", 1), NewCard("b", 0), NewCard("c", 1));
            var profile = ProfileWithDeck("a", "b", "c");

            int removed = DeckPrune.RemoveHiddenCards(profile, catalog);

            Assert.AreEqual(1, removed);
            CollectionAssert.AreEqual(new[] { "a", "c" }, profile.dreamcatcherDecks[0].cardIds);
        }

        [Test]
        public void RemoveHiddenCards_NoHidden_ChangesNothing()
        {
            var catalog = NewCatalog(NewCard("a", 1), NewCard("b", 1));
            var profile = ProfileWithDeck("a", "b");

            int removed = DeckPrune.RemoveHiddenCards(profile, catalog);

            Assert.AreEqual(0, removed, "제거가 없으면 0 — 호출처가 저장을 건너뛴다");
            CollectionAssert.AreEqual(new[] { "a", "b" }, profile.dreamcatcherDecks[0].cardIds);
        }

        [Test]
        public void RemoveHiddenCards_UnknownId_IsKept()
        {
            var catalog = NewCatalog(NewCard("a", 1));
            var profile = ProfileWithDeck("a", "ghost");

            int removed = DeckPrune.RemoveHiddenCards(profile, catalog);

            Assert.AreEqual(0, removed);
            CollectionAssert.AreEqual(new[] { "a", "ghost" }, profile.dreamcatcherDecks[0].cardIds,
                "카탈로그가 모르는 id 는 숨김이 아니라 진단 대상이라 보존한다");
        }

        [Test]
        public void RemoveHiddenCards_DuplicateHiddenIds_AllRemoved()
        {
            var catalog = NewCatalog(NewCard("a", 1), NewCard("dup", 0));
            var profile = ProfileWithDeck("dup", "a", "dup");

            int removed = DeckPrune.RemoveHiddenCards(profile, catalog);

            Assert.AreEqual(2, removed, "같은 숨김 카드가 여러 장이면 전부 빠진다");
            CollectionAssert.AreEqual(new[] { "a" }, profile.dreamcatcherDecks[0].cardIds);
        }

        [Test]
        public void RemoveHiddenCards_AppliesToEveryDeck()
        {
            var catalog = NewCatalog(NewCard("a", 1), NewCard("h", 0));
            var profile = ProfileWithDeck("a", "h");
            profile.dreamcatcherDecks.Add(new DreamcatcherPreset { id = "deck_2", cardIds = new List<string> { "h", "h" } });

            int removed = DeckPrune.RemoveHiddenCards(profile, catalog);

            Assert.AreEqual(3, removed, "선택되지 않은 덱도 정리한다");
            CollectionAssert.AreEqual(new[] { "a" }, profile.dreamcatcherDecks[0].cardIds);
            CollectionAssert.IsEmpty(profile.dreamcatcherDecks[1].cardIds);
        }

        [Test]
        public void RemoveHiddenCards_NullCatalog_LeavesDeckIntact()
        {
            var profile = ProfileWithDeck("a", "b");

            int removed = DeckPrune.RemoveHiddenCards(profile, null);

            Assert.AreEqual(0, removed, "배선 오류로 덱을 훼손하지 않는다");
            CollectionAssert.AreEqual(new[] { "a", "b" }, profile.dreamcatcherDecks[0].cardIds);
        }

        [Test]
        public void RemoveHiddenCards_NullProfile_DoesNotThrow()
        {
            var catalog = NewCatalog(NewCard("a", 0));
            Assert.AreEqual(0, DeckPrune.RemoveHiddenCards(null, catalog));
        }
    }
}
