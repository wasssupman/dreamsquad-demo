using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Wassup.Core;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // game-start-loadout-gate Unit 0 — LoadoutGate.Check: squad must resolve to
    // exactly SlotCount distinct catalog units, deck must pass DeckRules.
    // The catalog/config are built here (not loaded from the shipped assets) so
    // the tests stay hermetic against balance changes to DeckRuleConfig_Default.
    public class LoadoutGateTests
    {
        private const int DeckSize = 8;

        private readonly List<Object> _created = new List<Object>();
        private DefenderCatalog _units;
        private DreamcatcherCardCatalog _cards;
        private List<LoadoutShortfall> _shortfalls;

        [SetUp]
        public void SetUp()
        {
            _units = ScriptableObject.CreateInstance<DefenderCatalog>();
            _created.Add(_units);
            // 8 units — one more than SlotCount, so tests can overfill/vary.
            _units.units = Enumerable.Range(1, 8).Select(i => MakeUnit($"u{i}")).ToArray();

            var config = ScriptableObject.CreateInstance<DeckRuleConfig>();
            config.deckSize = DeckSize;
            config.maxSquad = -1;
            config.maxUnit = -1;
            _created.Add(config);

            _cards = ScriptableObject.CreateInstance<DreamcatcherCardCatalog>();
            _created.Add(_cards);
            _cards.ruleConfig = config;
            _cards.cards = new[] { MakeCard("c1"), MakeCard("c2") };

            _shortfalls = new List<LoadoutShortfall>();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _created) if (o != null) Object.DestroyImmediate(o);
            _created.Clear();
        }

        private DefenderUnitData MakeUnit(string id)
        {
            var u = ScriptableObject.CreateInstance<DefenderUnitData>();
            u.id = id;
            _created.Add(u);
            return u;
        }

        private DreamcatcherCard MakeCard(string id)
        {
            var c = ScriptableObject.CreateInstance<DreamcatcherCard>();
            c.id = id; c.type = CardType.Unit;
            _created.Add(c);
            return c;
        }

        // Profile with `unitIds` in the squad (padded to SlotCount) and a deck of
        // `cardIds`. Passing null for cardIds means "no deck selected".
        private PlayerProfile MakeProfile(IEnumerable<string> unitIds, IEnumerable<string> cardIds)
        {
            var p = new PlayerProfile();
            var squad = new SquadSave { id = "squad_1" };
            squad.unitIds = unitIds != null ? unitIds.ToList() : new List<string>();
            squad.NormalizeSlots();
            p.squads.Add(squad);
            p.selectedSquadId = squad.id;

            if (cardIds != null)
            {
                var deck = new DeckSave { id = "deck_1", cardIds = cardIds.ToList() };
                p.dreamcatcherDecks.Add(deck);
                p.selectedDeckId = deck.id;
            }
            return p;
        }

        private static List<string> FullSquad() => Enumerable.Range(1, SquadSave.SlotCount).Select(i => $"u{i}").ToList();
        private static List<string> FullDeck() => Enumerable.Repeat("c1", DeckSize).ToList();

        [Test]
        public void FullSquadAndDeck_Passes()
        {
            var p = MakeProfile(FullSquad(), FullDeck());
            Assert.IsTrue(LoadoutGate.Check(p, _units, _cards, _shortfalls));
            CollectionAssert.IsEmpty(_shortfalls);
        }

        [Test]
        public void ShortSquad_ReportsSquadShortfall()
        {
            var p = MakeProfile(FullSquad().Take(5), FullDeck());
            Assert.IsFalse(LoadoutGate.Check(p, _units, _cards, _shortfalls));
            Assert.AreEqual(1, _shortfalls.Count);
            Assert.AreEqual(LoadoutTarget.Squad, _shortfalls[0].target);
            Assert.AreEqual(5, _shortfalls[0].have);
            Assert.AreEqual(SquadSave.SlotCount, _shortfalls[0].need);
        }

        [Test]
        public void NoDeckSelected_ReportsDeckShortfall()
        {
            var p = MakeProfile(FullSquad(), null);
            Assert.IsFalse(LoadoutGate.Check(p, _units, _cards, _shortfalls));
            Assert.AreEqual(1, _shortfalls.Count);
            Assert.AreEqual(LoadoutTarget.Deck, _shortfalls[0].target);
            Assert.AreEqual(0, _shortfalls[0].have);
            Assert.AreEqual(DeckSize, _shortfalls[0].need);
        }

        [Test]
        public void BothUnmet_ReportsSquadFirst()
        {
            var p = MakeProfile(FullSquad().Take(3), FullDeck().Take(2));
            Assert.IsFalse(LoadoutGate.Check(p, _units, _cards, _shortfalls));
            Assert.AreEqual(2, _shortfalls.Count);
            Assert.AreEqual(LoadoutTarget.Squad, _shortfalls[0].target);
            Assert.AreEqual(LoadoutTarget.Deck, _shortfalls[1].target);
        }

        // The regression this gate exists for: renamed unit ids leave a squad that
        // looks full (7 non-empty slots) but deploys nothing, and GameManager then
        // falls back to the legacy draft silently.
        [Test]
        public void StaleUnitIds_DoNotCount()
        {
            var stale = Enumerable.Range(1, SquadSave.SlotCount).Select(i => $"renamed_{i}");
            var p = MakeProfile(stale, FullDeck());
            Assert.IsFalse(LoadoutGate.Check(p, _units, _cards, _shortfalls));
            Assert.AreEqual(1, _shortfalls.Count);
            Assert.AreEqual(LoadoutTarget.Squad, _shortfalls[0].target);
            Assert.AreEqual(0, _shortfalls[0].have);
        }

        // SquadDraw.Resolve dedups before cutting at FieldCount, so a squad with a
        // repeated unit deploys fewer than 7. The gate must agree with it.
        [Test]
        public void DuplicateUnits_CountOnce()
        {
            var dupes = FullSquad();
            dupes[6] = dupes[0];
            var p = MakeProfile(dupes, FullDeck());
            Assert.IsFalse(LoadoutGate.Check(p, _units, _cards, _shortfalls));
            Assert.AreEqual(1, _shortfalls.Count);
            Assert.AreEqual(LoadoutTarget.Squad, _shortfalls[0].target);
            Assert.AreEqual(SquadSave.SlotCount - 1, _shortfalls[0].have);
        }

        // Order matters: SquadDraw.Resolve de-dups and *then* caps at FieldCount, so
        // an over-long list with a leading duplicate still fields 7. Counting the
        // first 7 slots and de-duping afterwards would report 6 and wrongly block a
        // squad the match would happily deploy.
        [Test]
        public void OverlongListWithLeadingDuplicate_MatchesSquadDraw()
        {
            var raw = new List<string> { "u1", "u1", "u2", "u3", "u4", "u5", "u6", "u7" };
            var p = new PlayerProfile();
            var squad = new SquadSave { id = "squad_1", unitIds = raw };  // no NormalizeSlots: hand-edited json
            p.squads.Add(squad);
            p.selectedSquadId = squad.id;
            var deck = new DeckSave { id = "deck_1", cardIds = FullDeck() };
            p.dreamcatcherDecks.Add(deck);
            p.selectedDeckId = deck.id;

            Assert.AreEqual(SquadSave.SlotCount, SquadDraw.Resolve(raw).Count, "precondition: SquadDraw fields 7");
            Assert.IsTrue(LoadoutGate.Check(p, _units, _cards, _shortfalls));
            CollectionAssert.IsEmpty(_shortfalls);
        }

        // A right-sized deck can still be invalid. Counts alone would render "8/8",
        // so the reason string has to survive to the popup.
        [Test]
        public void FullDeckWithUnknownCard_ReportsReason()
        {
            var deck = FullDeck();
            deck[3] = "does_not_exist";
            var p = MakeProfile(FullSquad(), deck);
            Assert.IsFalse(LoadoutGate.Check(p, _units, _cards, _shortfalls));
            Assert.AreEqual(1, _shortfalls.Count);
            Assert.AreEqual(LoadoutTarget.Deck, _shortfalls[0].target);
            Assert.AreEqual(DeckSize, _shortfalls[0].have);
            Assert.AreEqual(DeckSize, _shortfalls[0].need);
            Assert.IsNotEmpty(_shortfalls[0].reason);
            StringAssert.Contains("does_not_exist", _shortfalls[0].reason);
        }

        [Test]
        public void NullProfile_ReportsBoth()
        {
            Assert.IsFalse(LoadoutGate.Check(null, _units, _cards, _shortfalls));
            Assert.AreEqual(2, _shortfalls.Count);
            Assert.AreEqual(LoadoutTarget.Squad, _shortfalls[0].target);
            Assert.AreEqual(0, _shortfalls[0].have);
            Assert.AreEqual(LoadoutTarget.Deck, _shortfalls[1].target);
        }

        [Test]
        public void Check_ClearsCallerList()
        {
            _shortfalls.Add(new LoadoutShortfall(LoadoutTarget.Deck, 99, 99, "stale"));
            var p = MakeProfile(FullSquad(), FullDeck());
            Assert.IsTrue(LoadoutGate.Check(p, _units, _cards, _shortfalls));
            CollectionAssert.IsEmpty(_shortfalls);
        }

        [Test]
        public void NullShortfallList_DoesNotThrow()
        {
            var p = MakeProfile(FullSquad(), FullDeck());
            Assert.IsTrue(LoadoutGate.Check(p, _units, _cards, null));
            Assert.IsFalse(LoadoutGate.Check(null, _units, _cards, null));
        }
    }
}
