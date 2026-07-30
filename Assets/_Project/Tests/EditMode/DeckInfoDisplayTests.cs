using NUnit.Framework;
using UnityEngine;
using Wassup.Data;
using Wassup.UI;

namespace Wassup.Tests.EditMode
{
    // tournament-history-deck-view unit 2 — 견고성 계약. 남의 덱을 보는 화면이라
    // "정상적이지 않은" 입력이 사실 전부 정상 입력이다: 내 빌드가 모르는 id, 빈 목록,
    // 미배선 카탈로그, 예상과 다른 개수.
    public class DeckInfoDisplayTests
    {
        private DefenderCatalog _units;
        private DreamstoneCatalog _stones;
        private DreamcatcherCardCatalog _cards;

        [SetUp]
        public void SetUp()
        {
            var unit = ScriptableObject.CreateInstance<DefenderUnitData>();
            unit.id = "u_known";
            unit.displayName = "알려진 유닛";
            unit.rarity = DefenderRarity.Epic;
            unit.desc = "설명";
            _units = ScriptableObject.CreateInstance<DefenderCatalog>();
            _units.units = new[] { unit };

            var stone = ScriptableObject.CreateInstance<DreamstoneData>();
            stone.id = "s_known";
            stone.displayName = "알려진 스톤";
            stone.grade = DreamstoneGrade.Unique;
            _stones = ScriptableObject.CreateInstance<DreamstoneCatalog>();
            _stones.stones = new[] { stone };

            var card = ScriptableObject.CreateInstance<DreamcatcherCard>();
            card.id = "c_known";
            card.displayName = "알려진 카드";
            card.description = "카드 설명";
            _cards = ScriptableObject.CreateInstance<DreamcatcherCardCatalog>();
            _cards.cards = new[] { card };
        }

        [Test]
        public void Resolved_MapsNameAndSub()
        {
            var units = DeckInfoDisplay.Units(new[] { "u_known" }, _units);
            Assert.IsTrue(units[0].Resolved);
            Assert.AreEqual("알려진 유닛", units[0].Name);
            Assert.AreEqual("Epic", units[0].Sub);
            Assert.AreEqual("설명", units[0].Body);

            var stones = DeckInfoDisplay.Stones(new[] { "s_known" }, _stones);
            Assert.AreEqual("알려진 스톤", stones[0].Name);
            Assert.AreEqual("Unique", stones[0].Sub);

            var cards = DeckInfoDisplay.Cards(new[] { "c_known" }, _cards);
            Assert.AreEqual("알려진 카드", cards[0].Name);
            Assert.AreEqual("카드 설명", cards[0].Body);
        }

        [Test]
        public void UnknownId_KeepsSlot_AsRawId()
        {
            // 버리면 7명 스쿼드가 5명으로 보이고, 그게 그 사람의 실제 덱인지 내 카탈로그가
            // 뒤처진 건지 화면에서 구분되지 않는다.
            var items = DeckInfoDisplay.Units(new[] { "u_known", "u_from_the_future" }, _units);

            Assert.AreEqual(2, items.Count);
            Assert.IsFalse(items[1].Resolved);
            Assert.AreEqual("u_from_the_future", items[1].Name);
            Assert.AreEqual("u_from_the_future", items[1].Id);
        }

        [Test]
        public void NullCatalog_RendersEverythingAsRawId()
        {
            var units = DeckInfoDisplay.Units(new[] { "a", "b" }, null);
            var stones = DeckInfoDisplay.Stones(new[] { "c" }, null);
            var cards = DeckInfoDisplay.Cards(new[] { "d" }, null);

            Assert.AreEqual(2, units.Count);
            Assert.IsFalse(units[0].Resolved);
            Assert.AreEqual(1, stones.Count);
            Assert.AreEqual(1, cards.Count);
            Assert.AreEqual("d", cards[0].Name);
        }

        [Test]
        public void NullOrEmptyList_YieldsEmpty()
        {
            Assert.IsEmpty(DeckInfoDisplay.Units(null, _units));
            Assert.IsEmpty(DeckInfoDisplay.Stones(null, _stones));
            Assert.IsEmpty(DeckInfoDisplay.Cards(new string[0], _cards));
        }

        [Test]
        public void BlankIds_AreSkipped_NotRenderedAsEmptyCells()
        {
            var items = DeckInfoDisplay.Units(new[] { "", "u_known", "   ", null }, _units);

            Assert.AreEqual(1, items.Count);
            Assert.AreEqual("u_known", items[0].Id);
        }

        [Test]
        public void Duplicates_ArePreserved_InOrder()
        {
            // 같은 카드 2장·같은 스톤 4개는 설계상 허용이다. 접으면 남의 덱을 잘못
            // 보여준다(그리고 선택이 인덱스 기준이라 슬롯도 어긋난다).
            var cards = DeckInfoDisplay.Cards(new[] { "c_known", "c_unknown", "c_known" }, _cards);

            Assert.AreEqual(3, cards.Count);
            Assert.AreEqual("c_known", cards[0].Id);
            Assert.AreEqual("c_unknown", cards[1].Id);
            Assert.AreEqual("c_known", cards[2].Id);
        }

        [Test]
        public void OverCount_IsRenderedInFull()
        {
            // 고정 슬롯이 아니다 — 덱 크기를 넘겨도 온 만큼 전부 그린다.
            var ids = new string[14];
            for (int i = 0; i < ids.Length; i++) ids[i] = $"c_{i}";

            Assert.AreEqual(14, DeckInfoDisplay.Cards(ids, _cards).Count);
        }

        [Test]
        public void ResolvedButUnnamed_FallsBackToId()
        {
            var nameless = ScriptableObject.CreateInstance<DefenderUnitData>();
            nameless.id = "u_nameless";
            nameless.displayName = "";
            var catalog = ScriptableObject.CreateInstance<DefenderCatalog>();
            catalog.units = new[] { nameless };

            var items = DeckInfoDisplay.Units(new[] { "u_nameless" }, catalog);

            Assert.IsTrue(items[0].Resolved);
            Assert.AreEqual("u_nameless", items[0].Name, "이름 없는 빈 칸이면 무엇인지 알 길이 없다");
        }
    }
}
