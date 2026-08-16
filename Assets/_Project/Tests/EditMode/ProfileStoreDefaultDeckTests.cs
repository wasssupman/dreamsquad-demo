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

            Assert.IsNotNull(p.CommittedDeck(), "a fresh install must not start deckless");
            Assert.IsTrue(DeckRules.Validate(p.CommittedDeck().cardIds, cards, out var reason), reason);
        }

        // The squad-only overload is what every pre-existing caller uses; it must keep
        // meaning "do not seed", not "seed an empty deck".
        [Test]
        public void WithoutDeckArgs_SeedsNothing()
        {
            var cat = MakeCatalog("scout", "ranger");

            var p = ProfileStore.LoadOrCreateAt(_path, cat);

            Assert.AreEqual(0, p.dreamcatcherDecks.Count);
            Assert.IsNull(p.CommittedDeck());
        }

        // Seeding fills a hole; it is not a reset. A deck invalidated by a rule change
        // must survive so the gate can report it and the player can fix it.
        [Test]
        public void ExistingCommittedDeck_IsNeverOverwritten()
        {
            var cat = MakeCatalog("scout");
            var cards = MakeCardCatalog(out var authored);

            var seeded = ProfileStore.LoadOrCreateAt(_path, cat, Deck(authored), cards);
            seeded.CommittedDeck().cardIds.RemoveAt(0);          // now invalid (7 of 8)
            var mine = new List<string>(seeded.CommittedDeck().cardIds);
            ProfileStore.SaveAt(_path, seeded);

            var reloaded = ProfileStore.LoadOrCreateAt(_path, cat, Deck(authored), cards);

            CollectionAssert.AreEqual(mine, reloaded.CommittedDeck().cardIds);
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
            Assert.IsNull(p.CommittedDeck());
        }

        // Existing installs predate deck seeding; loading must rescue them too, exactly
        // as EnsureDefaultSquad already rescues an empty squad.
        [Test]
        public void ExistingDecklessProfile_GetsSeededOnLoad()
        {
            var cat = MakeCatalog("scout");
            var cards = MakeCardCatalog(out var authored);

            var legacy = ProfileStore.LoadOrCreateAt(_path, cat);   // saved without a deck
            Assert.IsNull(legacy.CommittedDeck(), "precondition: deckless");

            var reloaded = ProfileStore.LoadOrCreateAt(_path, cat, Deck(authored), cards);

            Assert.IsNotNull(reloaded.CommittedDeck());
            Assert.IsTrue(DeckRules.Validate(reloaded.CommittedDeck().cardIds, cards, out var reason), reason);
        }

        // ---- EnsureDefaultStones (스타터 드림스톤 시드) ----

        private DreamstoneData Stone(string id)
        {
            var s = ScriptableObject.CreateInstance<DreamstoneData>();
            s.id = id;
            _created.Add(s);
            return s;
        }

        [Test]
        public void FreshProfile_SeedsAuthoredStones_InOrder()
        {
            var cat = MakeCatalog("u0");
            var stones = new[] { Stone("s_atk"), Stone("s_as"), Stone("s_cost"), Stone("s_hp") };

            var p = ProfileStore.LoadOrCreateAt(_path, cat, null, null, stones);

            CollectionAssert.AreEqual(new[] { "s_atk", "s_as", "s_cost", "s_hp" },
                p.CommittedSquad().stoneIds);
        }

        [Test]
        public void NoAuthoredStones_LeavesSlotsEmpty()
        {
            var cat = MakeCatalog("u0");

            var p = ProfileStore.LoadOrCreateAt(_path, cat, null, null, null);

            Assert.AreEqual(SquadPreset.StoneSlotCount, p.CommittedSquad().stoneIds.Count);
            CollectionAssert.AreEqual(new[] { "", "", "", "" }, p.CommittedSquad().stoneIds);
        }

        [Test]
        public void SkipsNullAndIdlessStones_PackingRemainingSlots()
        {
            var cat = MakeCatalog("u0");
            var stones = new[] { Stone("a"), null, Stone(""), Stone("b") };

            var p = ProfileStore.LoadOrCreateAt(_path, cat, null, null, stones);

            CollectionAssert.AreEqual(new[] { "a", "b", "", "" }, p.CommittedSquad().stoneIds);
        }

        // 시드는 리셋이 아니다: 플레이어가 스톤을 뺀 상태를 로드할 때마다 되살리면
        // 해제 자체가 불가능해진다. 한 칸이라도 차 있으면 손대지 않는다.
        [Test]
        public void PlayerEquippedStones_AreNeverOverwritten()
        {
            var cat = MakeCatalog("u0");
            var stones = new[] { Stone("s_atk"), Stone("s_as"), Stone("s_cost"), Stone("s_hp") };

            var p = ProfileStore.LoadOrCreateAt(_path, cat, null, null, stones);
            p.CommittedSquad().stoneIds[0] = "player_pick";
            for (int i = 1; i < p.CommittedSquad().stoneIds.Count; i++)
                p.CommittedSquad().stoneIds[i] = "";
            ProfileStore.SaveAt(_path, p);

            var reloaded = ProfileStore.LoadOrCreateAt(_path, cat, null, null, stones);

            CollectionAssert.AreEqual(new[] { "player_pick", "", "", "" },
                reloaded.CommittedSquad().stoneIds);
        }
    }
}
