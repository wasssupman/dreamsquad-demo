using System.Collections.Generic;
using NUnit.Framework;
using Wassup.Core;

namespace Wassup.Tests.EditMode.Profile
{
    // loadout-preset-page unit 1 — PresetApply.WriteToProfile 순수 변이 회귀 커버리지.
    // Unity 오브젝트 불필요(id 문자열 + plain serializable 만 사용).
    public class PresetApplyTests
    {
        private static PlayerProfile MakeProfile(bool withSelectedDeck)
        {
            var p = new PlayerProfile();
            var squad = new SquadSave { id = "squad_1", name = "Squad 1" };
            squad.NormalizeSlots();
            // 스톤 4슬롯을 채워 보존 검증 대상으로 삼는다.
            squad.stoneIds = new List<string> { "stoneA", "stoneB", "stoneC", "stoneD" };
            p.squads = new List<SquadSave> { squad };
            p.selectedSquadId = "squad_1";

            p.dreamcatcherDecks = new List<DeckSave>();
            if (withSelectedDeck)
            {
                var deck = new DeckSave { id = "deck_1", name = "Deck 1" };
                p.dreamcatcherDecks.Add(deck);
                p.selectedDeckId = "deck_1";
            }
            return p;
        }

        private static List<string> Ids(string prefix, int n)
        {
            var list = new List<string>(n);
            for (int i = 0; i < n; i++) list.Add(prefix + i);
            return list;
        }

        [Test]
        public void WriteToProfile_Units_ExactlySevenSlots()
        {
            var p = MakeProfile(withSelectedDeck: true);

            bool ok = PresetApply.WriteToProfile(p, Ids("u", 7), Ids("c", 10));

            Assert.IsTrue(ok);
            var squad = p.SelectedSquad();
            Assert.AreEqual(SquadSave.SlotCount, squad.unitIds.Count);
            for (int i = 0; i < 7; i++) Assert.AreEqual("u" + i, squad.unitIds[i]);
        }

        [Test]
        public void WriteToProfile_ExcessUnits_IgnoredBeyondSeven()
        {
            var p = MakeProfile(withSelectedDeck: true);

            PresetApply.WriteToProfile(p, Ids("u", 9), Ids("c", 10));

            var squad = p.SelectedSquad();
            Assert.AreEqual(SquadSave.SlotCount, squad.unitIds.Count);
            Assert.AreEqual("u6", squad.unitIds[6]); // 7번째까지만
        }

        [Test]
        public void WriteToProfile_FewerUnits_PadsWithEmpty()
        {
            var p = MakeProfile(withSelectedDeck: true);

            PresetApply.WriteToProfile(p, Ids("u", 3), Ids("c", 10));

            var squad = p.SelectedSquad();
            Assert.AreEqual("u2", squad.unitIds[2]);
            for (int i = 3; i < SquadSave.SlotCount; i++) Assert.AreEqual("", squad.unitIds[i]);
        }

        [Test]
        public void WriteToProfile_Cards_WrittenVerbatim()
        {
            var p = MakeProfile(withSelectedDeck: true);

            PresetApply.WriteToProfile(p, Ids("u", 7), Ids("c", 10));

            var deck = p.SelectedDeck();
            Assert.AreEqual(10, deck.cardIds.Count);
            Assert.AreEqual("c0", deck.cardIds[0]);
            Assert.AreEqual("c9", deck.cardIds[9]);
        }

        [Test]
        public void WriteToProfile_ExcessCards_NotCapped()
        {
            var p = MakeProfile(withSelectedDeck: true);

            PresetApply.WriteToProfile(p, Ids("u", 7), Ids("c", 12));

            Assert.AreEqual(12, p.SelectedDeck().cardIds.Count); // 유닛과 비대칭 — 카드는 캡 없음
        }

        [Test]
        public void WriteToProfile_StonesPreserved()
        {
            var p = MakeProfile(withSelectedDeck: true);

            PresetApply.WriteToProfile(p, Ids("u", 7), Ids("c", 10));

            var squad = p.SelectedSquad();
            CollectionAssert.AreEqual(
                new[] { "stoneA", "stoneB", "stoneC", "stoneD" }, squad.stoneIds);
        }

        [Test]
        public void WriteToProfile_NoSelectedDeck_CreatesAndSelectsDeck1()
        {
            var p = MakeProfile(withSelectedDeck: false);
            Assert.IsNull(p.SelectedDeck());

            bool ok = PresetApply.WriteToProfile(p, Ids("u", 7), Ids("c", 10));

            Assert.IsTrue(ok);
            Assert.AreEqual("deck_1", p.selectedDeckId);
            var deck = p.SelectedDeck();
            Assert.IsNotNull(deck);
            Assert.AreEqual(10, deck.cardIds.Count);
        }

        [Test]
        public void WriteToProfile_NullProfile_ReturnsFalse()
        {
            Assert.IsFalse(PresetApply.WriteToProfile(null, Ids("u", 7), Ids("c", 10)));
        }

        [Test]
        public void WriteToProfile_NoSelectedSquad_ReturnsFalse()
        {
            var p = new PlayerProfile { squads = new List<SquadSave>(), selectedSquadId = "" };
            Assert.IsFalse(PresetApply.WriteToProfile(p, Ids("u", 7), Ids("c", 10)));
        }
    }
}
