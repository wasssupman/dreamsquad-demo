using System.Collections.Generic;
using NUnit.Framework;
using Wassup.Core;

namespace Wassup.Tests.EditMode.Profile
{
    // page-local-presets unit 1 — dirty 판정. 미저장 경고 팝업과 [저장] 버튼 활성이
    // 전부 이 함수에 달려 있다.
    public class PresetDiffTests
    {
        private const string Name = "스쿼드 1";

        private static SquadPreset Stored(params string[] units)
        {
            var p = new SquadPreset { id = "a", name = Name, unitIds = new List<string>(units) };
            p.NormalizeSlots();
            return p;
        }

        private static List<string> Slots(params string[] units)
        {
            var l = new List<string>(units);
            while (l.Count < SquadPreset.SlotCount) l.Add("");
            return l;
        }

        private static List<string> Stones(params string[] ids)
        {
            var l = new List<string>(ids);
            while (l.Count < SquadPreset.StoneSlotCount) l.Add("");
            return l;
        }

        // ---- 스쿼드 -------------------------------------------------------

        [Test]
        public void Squad_IdenticalContent_IsNotDirty()
        {
            var stored = Stored("u0", "u1");
            Assert.IsFalse(PresetDiff.IsSquadDirty(Name, Slots("u0", "u1"), Stones(), stored));
        }

        [Test]
        public void Squad_ReplacedUnit_IsDirty()
        {
            var stored = Stored("u0", "u1");
            Assert.IsTrue(PresetDiff.IsSquadDirty(Name, Slots("u0", "uX"), Stones(), stored));
        }

        // 플래그 방식이면 실패하는 케이스 — 이 테스트가 순수 비교를 쓰는 이유다.
        [Test]
        public void Squad_RemovedThenReAddedToSameSlot_IsNotDirty()
        {
            var stored = Stored("u0", "u1");
            var working = Slots("u0", "u1");

            working[1] = "";      // 뺐다
            working[1] = "u1";    // 같은 자리에 되넣었다

            Assert.IsFalse(PresetDiff.IsSquadDirty(Name, working, Stones(), stored),
                "내용이 원래대로면 dirty 는 꺼져야 한다");
        }

        [Test]
        public void Squad_SwappedSlotPositions_IsDirty()
        {
            var stored = Stored("u0", "", "", "u3");
            var working = Slots("u3", "", "", "u0");   // 같은 집합, 자리만 교환

            Assert.IsTrue(PresetDiff.IsSquadDirty(Name, working, Stones(), stored),
                "슬롯 위치는 의미를 가진다 — 집합 비교가 아니다");
        }

        [Test]
        public void Squad_StoneOnlyChange_IsDirty()
        {
            var stored = Stored("u0");
            Assert.IsTrue(PresetDiff.IsSquadDirty(Name, Slots("u0"), Stones("stone_001"), stored),
                "스톤은 스쿼드 프리셋에 통합돼 있으므로 스톤만 바뀌어도 dirty");
        }

        [Test]
        public void Squad_NameOnlyChange_IsDirty()
        {
            var stored = Stored("u0");
            Assert.IsTrue(PresetDiff.IsSquadDirty("다른 이름", Slots("u0"), Stones(), stored));
        }

        [Test]
        public void Squad_NullVsEmptyString_IsNotDirty()
        {
            var stored = new SquadPreset
            {
                id = "a", name = Name,
                unitIds = new List<string> { "u0", null, null, null, null, null, null },
                stoneIds = new List<string> { null, null, null, null },
            };
            // 저장본은 JSON 왕복으로 null 이 섞일 수 있고 작업본은 "" 를 쓴다.
            Assert.IsFalse(PresetDiff.IsSquadDirty(Name, Slots("u0"), Stones(), stored),
                "null 과 \"\" 는 같은 빈칸이다");
        }

        [Test]
        public void Squad_ShorterStoredList_ComparesAsEmptySlots()
        {
            var stored = new SquadPreset
            {
                id = "a", name = Name,
                unitIds = new List<string> { "u0" },     // 패딩 안 된 상태
                stoneIds = new List<string>(),
            };
            Assert.IsFalse(PresetDiff.IsSquadDirty(Name, Slots("u0"), Stones(), stored));
        }

        [Test]
        public void Squad_StoredNull_EmptyWorking_IsNotDirty()
        {
            Assert.IsFalse(PresetDiff.IsSquadDirty(null, Slots(), Stones(), null));
        }

        [Test]
        public void Squad_StoredNull_NonEmptyWorking_IsDirty()
        {
            Assert.IsTrue(PresetDiff.IsSquadDirty(null, Slots("u0"), Stones(), null));
            Assert.IsTrue(PresetDiff.IsSquadDirty(null, Slots(), Stones("stone_001"), null));
            Assert.IsTrue(PresetDiff.IsSquadDirty("이름만 있음", Slots(), Stones(), null));
        }

        // ---- 드림캐쳐 -----------------------------------------------------

        private static DreamcatcherPreset Deck(params string[] cards)
            => new DreamcatcherPreset { id = "d", name = "덱 1", cardIds = new List<string>(cards) };

        [Test]
        public void Deck_IdenticalContent_IsNotDirty()
        {
            Assert.IsFalse(PresetDiff.IsDeckDirty("덱 1",
                new List<string> { "c0", "c1" }, Deck("c0", "c1")));
        }

        [Test]
        public void Deck_SameSetDifferentOrder_IsDirty()
        {
            Assert.IsTrue(PresetDiff.IsDeckDirty("덱 1",
                new List<string> { "c1", "c0" }, Deck("c0", "c1")),
                "덱 순서는 손패 사이클 순서라 의미를 가진다");
        }

        [Test]
        public void Deck_DifferentLength_IsDirty()
        {
            Assert.IsTrue(PresetDiff.IsDeckDirty("덱 1",
                new List<string> { "c0" }, Deck("c0", "c1")));
            Assert.IsTrue(PresetDiff.IsDeckDirty("덱 1",
                new List<string> { "c0", "c1", "c2" }, Deck("c0", "c1")));
        }

        [Test]
        public void Deck_EmptyEntriesAreIgnored()
        {
            Assert.IsFalse(PresetDiff.IsDeckDirty("덱 1",
                new List<string> { "c0", "", "c1" },
                Deck("c0", null, "c1")));
        }

        [Test]
        public void Deck_NameOnlyChange_IsDirty()
        {
            Assert.IsTrue(PresetDiff.IsDeckDirty("다른 덱", new List<string> { "c0" }, Deck("c0")));
        }

        [Test]
        public void Deck_StoredNull_EmptyWorking_IsNotDirty()
        {
            Assert.IsFalse(PresetDiff.IsDeckDirty(null, new List<string>(), null));
        }

        [Test]
        public void Deck_StoredNull_NonEmptyWorking_IsDirty()
        {
            Assert.IsTrue(PresetDiff.IsDeckDirty(null, new List<string> { "c0" }, null));
        }

        [Test]
        public void Deck_ClearedWorking_IsDirty()
        {
            // 카드를 전부 뺀 상태 — 저장 전이므로 dirty 다. 이 성질이 [되돌리기] 활성
            // 조건(=dirty)을 만들고, [되돌리기]는 이 상태를 저장본으로 복원한다.
            Assert.IsTrue(PresetDiff.IsDeckDirty("덱 1", new List<string>(), Deck("c0", "c1")));
        }
    }
}
