using System.Collections.Generic;
using NUnit.Framework;
using Wassup.Core;

namespace Wassup.Tests.EditMode.Profile
{
    // page-local-presets unit 0 — PlayerProfile.NormalizePresets 불변식 + PresetIds.NextId.
    // 순수 로직만 다루므로 Unity 오브젝트/디스크 없이 돈다.
    public class PresetNormalizeTests
    {
        private static PlayerProfile ProfileWith(params SquadPreset[] presets)
        {
            var p = new PlayerProfile();
            p.squads = new List<SquadPreset>(presets);
            return p;
        }

        // ---- 칸 패딩 ------------------------------------------------------

        [Test]
        public void Normalize_PadsUnitAndStoneSlots()
        {
            var p = ProfileWith(new SquadPreset { id = "a", unitIds = new List<string> { "u0" } });
            p.NormalizePresets();

            Assert.AreEqual(SquadPreset.SlotCount, p.squads[0].unitIds.Count);
            Assert.AreEqual(SquadPreset.StoneSlotCount, p.squads[0].stoneIds.Count);
            Assert.AreEqual("u0", p.squads[0].unitIds[0]);
            for (int i = 1; i < SquadPreset.SlotCount; i++)
                Assert.AreEqual("", p.squads[0].unitIds[i], "빈 칸은 null 이 아니라 \"\" 여야 한다");
        }

        [Test]
        public void Normalize_TrimsOverlongSlots()
        {
            var many = new List<string>();
            for (int i = 0; i < 20; i++) many.Add("u" + i);
            var p = ProfileWith(new SquadPreset { id = "a", unitIds = many });
            p.NormalizePresets();

            Assert.AreEqual(SquadPreset.SlotCount, p.squads[0].unitIds.Count);
            Assert.AreEqual("u6", p.squads[0].unitIds[6]);
        }

        [Test]
        public void Normalize_ConvertsNullSlotEntriesToEmptyString()
        {
            var p = ProfileWith(new SquadPreset
            {
                id = "a",
                unitIds = new List<string> { null, "u1", null },
                stoneIds = new List<string> { null, null, null, null },
            });
            p.NormalizePresets();

            Assert.AreEqual("", p.squads[0].unitIds[0]);
            Assert.AreEqual("u1", p.squads[0].unitIds[1]);
            foreach (var s in p.squads[0].stoneIds) Assert.AreEqual("", s);
        }

        // ---- 상한 --------------------------------------------------------

        [Test]
        public void Normalize_TrimsPresetListToMax()
        {
            var p = new PlayerProfile { squads = new List<SquadPreset>() };
            for (int i = 0; i < PlayerProfile.MaxPresets + 5; i++)
                p.squads.Add(new SquadPreset { id = "squad_" + (i + 1) });
            p.dreamcatcherDecks = new List<DreamcatcherPreset>();
            for (int i = 0; i < PlayerProfile.MaxPresets + 3; i++)
                p.dreamcatcherDecks.Add(new DreamcatcherPreset { id = "deck_" + (i + 1) });

            p.NormalizePresets();

            Assert.AreEqual(PlayerProfile.MaxPresets, p.squads.Count);
            Assert.AreEqual(PlayerProfile.MaxPresets, p.dreamcatcherDecks.Count);
            // 앞에서부터 보존 — 뒤를 잘라낸다.
            Assert.AreEqual("squad_1", p.squads[0].id);
            Assert.AreEqual("squad_" + PlayerProfile.MaxPresets, p.squads[PlayerProfile.MaxPresets - 1].id);
        }

        // ---- 확정 포인터 교정 ---------------------------------------------

        [Test]
        public void Normalize_RepairsDanglingCommittedPointer()
        {
            var p = ProfileWith(new SquadPreset { id = "a" }, new SquadPreset { id = "b" });
            p.selectedSquadId = "does_not_exist";
            p.NormalizePresets();

            Assert.AreEqual("a", p.selectedSquadId, "실존하지 않는 확정 포인터는 첫 엔트리로 교정된다");
            Assert.IsNotNull(p.CommittedSquad());
        }

        [Test]
        public void Normalize_KeepsValidCommittedPointer()
        {
            var p = ProfileWith(new SquadPreset { id = "a" }, new SquadPreset { id = "b" });
            p.selectedSquadId = "b";
            p.NormalizePresets();

            Assert.AreEqual("b", p.selectedSquadId, "유효한 확정 포인터는 건드리지 않는다");
        }

        [Test]
        public void Normalize_EmptyListsDoNotThrowAndClearPointer()
        {
            var p = new PlayerProfile { selectedSquadId = "stale", selectedDeckId = "stale" };
            Assert.DoesNotThrow(() => p.NormalizePresets());

            Assert.AreEqual("", p.selectedSquadId);
            Assert.AreEqual("", p.selectedDeckId);
            Assert.IsNull(p.CommittedSquad());
            Assert.IsNull(p.CommittedDeck());
        }

        [Test]
        public void Normalize_NullListsAreCreated()
        {
            var p = new PlayerProfile { squads = null, dreamcatcherDecks = null };
            Assert.DoesNotThrow(() => p.NormalizePresets());

            Assert.IsNotNull(p.squads);
            Assert.IsNotNull(p.dreamcatcherDecks);
        }

        [Test]
        public void Normalize_DropsNullPresetEntries()
        {
            var p = new PlayerProfile
            {
                squads = new List<SquadPreset> { null, new SquadPreset { id = "a" }, null },
            };
            p.NormalizePresets();

            Assert.AreEqual(1, p.squads.Count);
            Assert.AreEqual("a", p.squads[0].id);
        }

        [Test]
        public void Normalize_DeckPresetDropsEmptyCardEntries()
        {
            var p = new PlayerProfile
            {
                dreamcatcherDecks = new List<DreamcatcherPreset>
                {
                    new DreamcatcherPreset { id = "d", cardIds = new List<string> { "c0", null, "", "c1" } },
                },
            };
            p.NormalizePresets();

            CollectionAssert.AreEqual(new[] { "c0", "c1" }, p.dreamcatcherDecks[0].cardIds);
        }

        // ---- id 발급 -----------------------------------------------------

        [Test]
        public void NextId_StartsAtOneWhenEmpty()
        {
            Assert.AreEqual("squad_1", PresetIds.NextId(new List<string>(), "squad_"));
            Assert.AreEqual("squad_1", PresetIds.NextId(null, "squad_"));
        }

        [Test]
        public void NextId_UsesMaxSuffixPlusOne_NotCount()
        {
            // 1,2,3 에서 2를 지운 상태. 개수 기반이면 "squad_3" 을 돌려 살아있는 3과 충돌한다.
            var ids = new List<string> { "squad_1", "squad_3" };
            Assert.AreEqual("squad_4", PresetIds.NextId(ids, "squad_"));
        }

        [Test]
        public void NextId_IgnoresForeignPrefixAndNonNumericSuffix()
        {
            var ids = new List<string> { "deck_9", "squad_2", "squad_abc", "", null };
            Assert.AreEqual("squad_3", PresetIds.NextId(ids, "squad_"));
        }

        [Test]
        public void NextId_HandlesLegacyIdsAsNormalSuffixes()
        {
            // 레거시 프로필의 squad_1/deck_1 도 같은 규칙으로 세어진다.
            Assert.AreEqual("squad_2", PresetIds.NextId(new List<string> { "squad_1" }, "squad_"));
            Assert.AreEqual("deck_2", PresetIds.NextId(new List<string> { "deck_1" }, "deck_"));
        }
    }
}
