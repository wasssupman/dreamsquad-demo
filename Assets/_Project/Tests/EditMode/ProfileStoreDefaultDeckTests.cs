using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Wassup.Core;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // outgame-login-gate unit 6 — default deck composition (order, rule size, holes).
    // game-start-loadout-gate unit 1 — BuildDefaultDeck moved from DefaultLoadoutButton
    // to ProfileStore (a dev-only button must not own what a fresh install depends on);
    // seeding tests for EnsureDefaultDeck live here too, next to the definition.
    public class ProfileStoreDefaultDeckTests
    {
        private const int DeckSize = 8;
        private const string DefaultDeckPath =
            "Assets/_Project/Data/Dreamcatcher/DreamcatcherDeck_Default.asset";
        private const string CardCatalogPath =
            "Assets/_Project/Data/Dreamcatcher/DreamcatcherCardCatalog.asset";

        private string _path;
        private readonly List<Object> _created = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            _path = Path.Combine(Path.GetTempPath(),
                "wassup_deckseed_" + System.Guid.NewGuid().ToString("N") + ".json");
        }

        [TearDown]
        public void TearDown()
        {
            if (File.Exists(_path)) File.Delete(_path);
            if (File.Exists(_path + ".bak")) File.Delete(_path + ".bak");
            foreach (var o in _created) if (o != null) Object.DestroyImmediate(o);
            _created.Clear();
        }

        private DreamcatcherCard Card(string id, int visible = 1)
        {
            var c = ScriptableObject.CreateInstance<DreamcatcherCard>();
            c.id = id; c.type = CardType.Unit; c.visible = visible;
            _created.Add(c);
            return c;
        }

        private DreamcatcherDeck Deck(params DreamcatcherCard[] cards)
        {
            var d = ScriptableObject.CreateInstance<DreamcatcherDeck>();
            d.cards = cards;
            _created.Add(d);
            return d;
        }

        private DefenderCatalog MakeCatalog(params string[] ids)
        {
            var cat = ScriptableObject.CreateInstance<DefenderCatalog>();
            _created.Add(cat);
            cat.units = new DefenderUnitData[ids.Length];
            for (int i = 0; i < ids.Length; i++)
            {
                var u = ScriptableObject.CreateInstance<DefenderUnitData>();
                u.id = ids[i];
                _created.Add(u);
                cat.units[i] = u;
            }
            return cat;
        }

        // Catalog holding DeckSize distinct cards + a rule config fixing the size, so
        // these tests do not ride on DeckRuleConfig_Default's balance value.
        private DreamcatcherCardCatalog MakeCardCatalog(out DreamcatcherCard[] cards)
        {
            cards = new DreamcatcherCard[DeckSize];
            for (int i = 0; i < DeckSize; i++) cards[i] = Card($"c{i}");

            var config = ScriptableObject.CreateInstance<DeckRuleConfig>();
            config.deckSize = DeckSize; config.maxSquad = -1; config.maxUnit = -1;
            _created.Add(config);

            var cat = ScriptableObject.CreateInstance<DreamcatcherCardCatalog>();
            _created.Add(cat);
            cat.cards = cards;
            cat.ruleConfig = config;
            return cat;
        }

        // ---- BuildDefaultDeck (composition) ----

        [Test]
        public void TruncatesToDeckSize_KeepingAuthoredOrder()
        {
            var deck = Deck(Card("a"), Card("b"), Card("c"), Card("d"));

            var save = ProfileStore.BuildDefaultDeck(deck, 2);

            CollectionAssert.AreEqual(new[] { "a", "b" }, save.cardIds);
        }

        [Test]
        public void SkipsNullAndIdlessCards()
        {
            var deck = Deck(Card("a"), null, Card(""), Card("b"));

            var save = ProfileStore.BuildDefaultDeck(deck, 10);

            CollectionAssert.AreEqual(new[] { "a", "b" }, save.cardIds);
        }

        [Test]
        public void SkipsHiddenCards_AndContinuesToDeckSize()
        {
            var deck = Deck(Card("a"), Card("hidden", visible: 0), Card("b"), Card("c"));

            var save = ProfileStore.BuildDefaultDeck(deck, 3);

            CollectionAssert.AreEqual(new[] { "a", "b", "c" }, save.cardIds);
        }

        [Test]
        public void FewerCardsThanDeckSize_TakesWhatExists()
        {
            var deck = Deck(Card("a"), Card("b"));

            var save = ProfileStore.BuildDefaultDeck(deck, 8);

            CollectionAssert.AreEqual(new[] { "a", "b" }, save.cardIds);
        }

        [Test]
        public void NullSource_ReturnsEmptyDeckInsteadOfThrowing()
        {
            var save = ProfileStore.BuildDefaultDeck(null, 8);

            Assert.IsNotNull(save.cardIds);
            Assert.AreEqual(0, save.cardIds.Count);
            Assert.AreEqual("deck_1", save.id, "the save still needs an id the profile can select");
        }

        // ---- EnsureDefaultDeck (seeding) ----

        [Test]
        public void FreshInstall_SeedsSelectableValidDeck()
        {
            var cat = MakeCatalog("scout", "ranger", "guardian");
            var cards = MakeCardCatalog(out var authored);

            var p = ProfileStore.LoadOrCreateAt(_path, cat, Deck(authored), cards);

            Assert.IsNotNull(p.SelectedDeck(), "a fresh install must not start deckless");
            Assert.IsTrue(DeckRules.Validate(p.SelectedDeck().cardIds, cards, out var reason), reason);
        }

        // The squad-only overload is what every pre-existing caller uses; it must keep
        // meaning "do not seed", not "seed an empty deck".
        [Test]
        public void WithoutDeckArgs_SeedsNothing()
        {
            var cat = MakeCatalog("scout", "ranger");

            var p = ProfileStore.LoadOrCreateAt(_path, cat);

            Assert.AreEqual(0, p.dreamcatcherDecks.Count);
            Assert.IsNull(p.SelectedDeck());
        }

        // Seeding fills a hole; it is not a reset. A deck invalidated by a rule change
        // must survive so the gate can report it and the player can fix it.
        [Test]
        public void ExistingSelectedDeck_IsNeverOverwritten()
        {
            var cat = MakeCatalog("scout");
            var cards = MakeCardCatalog(out var authored);

            var seeded = ProfileStore.LoadOrCreateAt(_path, cat, Deck(authored), cards);
            seeded.SelectedDeck().cardIds.RemoveAt(0);          // now invalid (7 of 8)
            var mine = new List<string>(seeded.SelectedDeck().cardIds);
            ProfileStore.SaveAt(_path, seeded);

            var reloaded = ProfileStore.LoadOrCreateAt(_path, cat, Deck(authored), cards);

            CollectionAssert.AreEqual(mine, reloaded.SelectedDeck().cardIds);
            Assert.AreEqual(1, reloaded.dreamcatcherDecks.Count, "no second deck should appear");
        }

        [Test]
        public void BrokenSelection_RepairsToFirstDeckWithoutAddingOne()
        {
            var cat = MakeCatalog("scout");
            var cards = MakeCardCatalog(out var authored);

            var p = ProfileStore.LoadOrCreateAt(_path, cat, Deck(authored), cards);
            p.selectedDeckId = "does_not_exist";
            ProfileStore.SaveAt(_path, p);

            var reloaded = ProfileStore.LoadOrCreateAt(_path, cat, Deck(authored), cards);

            Assert.AreEqual(1, reloaded.dreamcatcherDecks.Count);
            Assert.AreEqual(reloaded.dreamcatcherDecks[0].id, reloaded.selectedDeckId);
        }

        [Test]
        public void AuthoredDeckEmpty_SeedsNothing()
        {
            var cat = MakeCatalog("scout");
            var cards = MakeCardCatalog(out _);

            var p = ProfileStore.LoadOrCreateAt(_path, cat, Deck(), cards);

            Assert.AreEqual(0, p.dreamcatcherDecks.Count, "an empty deck helps no one");
            Assert.IsNull(p.SelectedDeck());
        }

        // Existing installs predate deck seeding; loading must rescue them too, exactly
        // as EnsureDefaultSquad already rescues an empty squad.
        [Test]
        public void ExistingDecklessProfile_GetsSeededOnLoad()
        {
            var cat = MakeCatalog("scout");
            var cards = MakeCardCatalog(out var authored);

            var legacy = ProfileStore.LoadOrCreateAt(_path, cat);   // saved without a deck
            Assert.IsNull(legacy.SelectedDeck(), "precondition: deckless");

            var reloaded = ProfileStore.LoadOrCreateAt(_path, cat, Deck(authored), cards);

            Assert.IsNotNull(reloaded.SelectedDeck());
            Assert.IsTrue(DeckRules.Validate(reloaded.SelectedDeck().cardIds, cards, out var reason), reason);
        }

        [Test]
        public void AuthoredDefaultDeck_IsExpectedVisibleValidStarter()
        {
            var source = AssetDatabase.LoadAssetAtPath<DreamcatcherDeck>(DefaultDeckPath);
            var catalog = AssetDatabase.LoadAssetAtPath<DreamcatcherCardCatalog>(CardCatalogPath);
            Assert.IsNotNull(source);
            Assert.IsNotNull(catalog);

            string[] expected =
            {
                "ranger_atk", "poke_needle", "ranger_as", "bouncy_bead", "guardian_as",
                "thornmail", "ranger_hp", "guardian_hp", "farewell", "guardian_fortress",
            };
            CollectionAssert.AreEqual(expected, source.cards.Select(c => c != null ? c.id : null));
            Assert.That(source.cards, Has.All.Matches<DreamcatcherCard>(
                c => c != null && c.visible != 0), "기본 덱은 숨김 카드나 null을 포함하면 안 된다");

            var profile = ProfileStore.CreateDefault(null, source, catalog);
            Assert.IsNotNull(profile.SelectedDeck());
            CollectionAssert.AreEqual(expected, profile.SelectedDeck().cardIds);
            Assert.IsTrue(DeckRules.Validate(profile.SelectedDeck().cardIds, catalog, out var reason), reason);
        }
    }
}
