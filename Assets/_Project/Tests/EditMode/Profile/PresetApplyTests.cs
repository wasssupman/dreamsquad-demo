using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Wassup.Core;
using Wassup.Data;

namespace Wassup.Tests.EditMode.Profile
{
    // deck-info-preset-apply unit 0 — 예약 채널 + 이름 규칙 + 적용 필터.
    public class PresetApplyTests
    {
        [SetUp]
        public void SetUp() => PresetApply.Clear();

        [TearDown]
        public void TearDown() => PresetApply.Clear();

        // ---- 카탈로그 헬퍼 ---------------------------------------------------

        private static DefenderCatalog UnitCatalog(params string[] ids)
        {
            var arr = new DefenderUnitData[ids.Length];
            for (int i = 0; i < ids.Length; i++)
            {
                arr[i] = ScriptableObject.CreateInstance<DefenderUnitData>();
                arr[i].id = ids[i];
            }
            var c = ScriptableObject.CreateInstance<DefenderCatalog>();
            c.units = arr;
            return c;
        }

        private static DreamstoneCatalog StoneCatalog(params string[] ids)
        {
            var arr = new DreamstoneData[ids.Length];
            for (int i = 0; i < ids.Length; i++)
            {
                arr[i] = ScriptableObject.CreateInstance<DreamstoneData>();
                arr[i].id = ids[i];
            }
            var c = ScriptableObject.CreateInstance<DreamstoneCatalog>();
            c.stones = arr;
            return c;
        }

        private static DreamcatcherCard Card(string id, CardType type = CardType.Unit,
            CardCategory category = CardCategory.Normal, int visible = 1)
        {
            var c = ScriptableObject.CreateInstance<DreamcatcherCard>();
            c.id = id;
            c.type = type;
            c.category = category;
            c.visible = visible;
            return c;
        }

        private static DreamcatcherCardCatalog CardCatalog(params DreamcatcherCard[] cards)
        {
            var c = ScriptableObject.CreateInstance<DreamcatcherCardCatalog>();
            c.cards = cards;
            return c;
        }

        // ---- 예약 채널 -------------------------------------------------------

        [Test]
        public void Stage_ThenConsumeSameTarget_ReturnsContent_Once()
        {
            PresetApply.Stage(new PresetApply.Request
            {
                target = PresetApply.Target.Squad,
                presetName = "wassup의 덱",
                unitIds = new List<string> { "u_a" },
            });

            Assert.IsTrue(PresetApply.TryConsume(PresetApply.Target.Squad, out var req));
            Assert.AreEqual("wassup의 덱", req.presetName);
            CollectionAssert.AreEqual(new[] { "u_a" }, req.unitIds);

            Assert.IsFalse(PresetApply.TryConsume(PresetApply.Target.Squad, out _),
                "예약은 한 번만 소비된다");
        }

        [Test]
        public void MismatchedTarget_ReturnsFalse_AndClearsReservation()
        {
            PresetApply.Stage(new PresetApply.Request { target = PresetApply.Target.Squad });

            Assert.IsFalse(PresetApply.TryConsume(PresetApply.Target.Dreamcatcher, out _));
            Assert.IsFalse(PresetApply.HasPending,
                "대상이 달라도 지운다 — 유령 예약이 한참 뒤 매칭 진입에서 되살아나면 안 된다");
        }

        [Test]
        public void StageTwice_LastOneWins()
        {
            PresetApply.Stage(new PresetApply.Request { target = PresetApply.Target.Squad, presetName = "첫째" });
            PresetApply.Stage(new PresetApply.Request { target = PresetApply.Target.Squad, presetName = "둘째" });

            Assert.IsTrue(PresetApply.TryConsume(PresetApply.Target.Squad, out var req));
            Assert.AreEqual("둘째", req.presetName);
        }

        [Test]
        public void Stage_CopiesLists_SoLaterMutationDoesNotLeakIn()
        {
            var live = new List<string> { "u_a" };
            PresetApply.Stage(new PresetApply.Request
            {
                target = PresetApply.Target.Squad,
                unitIds = live,
            });

            live.Add("u_INJECTED");   // 패널이 든 Payload 의 리스트가 나중에 변해도

            Assert.IsTrue(PresetApply.TryConsume(PresetApply.Target.Squad, out var req));
            CollectionAssert.AreEqual(new[] { "u_a" }, req.unitIds, "예약 내용은 불변이다(복제)");
        }

        [Test]
        public void Clear_DropsReservation()
        {
            PresetApply.Stage(new PresetApply.Request { target = PresetApply.Target.Squad });
            Assert.IsTrue(PresetApply.HasPending, "precondition");

            PresetApply.Clear();   // = 도메인 리로드 off 대비 세션 시작 리셋 훅

            Assert.IsFalse(PresetApply.HasPending);
        }

        // ---- 이름 규칙 -------------------------------------------------------

        [Test]
        public void DeckName_UsesOwner_FallsBackWhenBlank()
        {
            Assert.AreEqual("wassup의 덱", PresetApply.DeckName("wassup"));
            Assert.AreEqual("wassup의 덱", PresetApply.DeckName("  wassup  "), "앞뒤 공백은 정리한다");
            Assert.AreEqual("불러온 덱", PresetApply.DeckName(null));
            Assert.AreEqual("불러온 덱", PresetApply.DeckName("   "));
        }

        [Test]
        public void UniqueName_AppendsNumberOnCollision()
        {
            var names = new List<string> { "스쿼드 1" };
            Assert.AreEqual("wassup의 덱", PresetApply.UniqueName(names, "wassup의 덱"), "미충돌은 그대로");

            names.Add("wassup의 덱");
            Assert.AreEqual("wassup의 덱 2", PresetApply.UniqueName(names, "wassup의 덱"));

            names.Add("wassup의 덱 2");
            Assert.AreEqual("wassup의 덱 3", PresetApply.UniqueName(names, "wassup의 덱"));
        }

        // ---- FilterUnits -----------------------------------------------------

        [Test]
        public void FilterUnits_DropsUnresolved_KeepsOrder()
        {
            var catalog = UnitCatalog("u_a", "u_b");

            var kept = PresetApply.FilterUnits(
                new[] { "u_a", "u_GHOST", "u_b" }, catalog, out int dropped);

            CollectionAssert.AreEqual(new[] { "u_a", "u_b" }, kept);
            Assert.AreEqual(1, dropped);
        }

        [Test]
        public void FilterUnits_RemovesDuplicates_FirstOccurrenceWins()
        {
            var catalog = UnitCatalog("u_a");

            var kept = PresetApply.FilterUnits(new[] { "u_a", "u_a" }, catalog, out int dropped);

            CollectionAssert.AreEqual(new[] { "u_a" }, kept,
                "페이지 ToggleUnit 이 같은 유닛의 두 번째 편성을 막는다 — 필터도 같은 규칙");
            Assert.AreEqual(1, dropped);
        }

        [Test]
        public void FilterUnits_CapsAtSlotCount()
        {
            var ids = new List<string>();
            var all = new List<string>();
            for (int i = 0; i < 9; i++) { ids.Add("u_" + i); all.Add("u_" + i); }
            var catalog = UnitCatalog(all.ToArray());

            var kept = PresetApply.FilterUnits(ids, catalog, out int dropped);

            Assert.AreEqual(SquadPreset.SlotCount, kept.Count);
            Assert.AreEqual(9 - SquadPreset.SlotCount, dropped);
        }

        [Test]
        public void FilterUnits_EmptySlotsAreNotItems()
        {
            var catalog = UnitCatalog("u_a");

            var kept = PresetApply.FilterUnits(new[] { "", "u_a", null, "  " }, catalog, out int dropped);

            CollectionAssert.AreEqual(new[] { "u_a" }, kept);
            Assert.AreEqual(0, dropped, "빈 슬롯은 제외 안내 대상이 아니다");
        }

        // ---- FilterStones ----------------------------------------------------

        [Test]
        public void FilterStones_KeepsDuplicates_UpToSlotCount()
        {
            var catalog = StoneCatalog("s_uniq");

            var four = PresetApply.FilterStones(
                new[] { "s_uniq", "s_uniq", "s_uniq", "s_uniq" }, catalog, out int dropped4);
            Assert.AreEqual(4, four.Count, "같은 유니크 스톤 4개는 설계상 허용이다");
            Assert.AreEqual(0, dropped4);

            var five = PresetApply.FilterStones(
                new[] { "s_uniq", "s_uniq", "s_uniq", "s_uniq", "s_uniq" }, catalog, out int dropped5);
            Assert.AreEqual(SquadPreset.StoneSlotCount, five.Count);
            Assert.AreEqual(1, dropped5);
        }

        [Test]
        public void FilterStones_DropsUnresolved()
        {
            var catalog = StoneCatalog("s_a");

            var kept = PresetApply.FilterStones(new[] { "s_GHOST", "s_a" }, catalog, out int dropped);

            CollectionAssert.AreEqual(new[] { "s_a" }, kept);
            Assert.AreEqual(1, dropped);
        }

        // ---- FilterCards -----------------------------------------------------

        [Test]
        public void FilterCards_CapsAtEffectiveDeckSize()
        {
            var cards = new DreamcatcherCard[12];
            var ids = new List<string>();
            for (int i = 0; i < 12; i++) { cards[i] = Card("c_" + i); ids.Add("c_" + i); }
            var catalog = CardCatalog(cards);   // ruleConfig 없음 → 기본 10

            var kept = PresetApply.FilterCards(ids, catalog, out int dropped);

            Assert.AreEqual(DeckRules.DefaultDeckSize, kept.Count);
            Assert.AreEqual(2, dropped);
        }

        [Test]
        public void FilterCards_AppliesPerTypeCap()
        {
            var catalog = CardCatalog(
                Card("c_sq1", CardType.Squad), Card("c_sq2", CardType.Squad),
                Card("c_sq3", CardType.Squad), Card("c_u", CardType.Unit));

            var kept = PresetApply.FilterCards(
                new[] { "c_sq1", "c_sq2", "c_sq3", "c_u" }, catalog, out int dropped);

            CollectionAssert.AreEqual(new[] { "c_sq1", "c_sq2", "c_u" }, kept,
                "Squad 타입 기본 상한 2 — 페이지 CanAdd 와 같은 규칙");
            Assert.AreEqual(1, dropped);
        }

        [Test]
        public void FilterCards_DropsHidden_Duplicates_Unresolved()
        {
            var catalog = CardCatalog(
                Card("c_ok"),
                Card("c_hidden", visible: 0),
                Card("c_sub", category: CardCategory.Subconscious));

            var kept = PresetApply.FilterCards(
                new[] { "c_ok", "c_hidden", "c_sub", "c_ok", "c_GHOST" }, catalog, out int dropped);

            // gift-phase-removal unit 0 — 무의식은 **더 이상 제외되지 않는다**. 선물 전용
            // 카드였을 땐 여기서 떨어뜨렸지만 이제 일반 덱 카드라, 남는 제외 사유는
            // 숨김·중복·미해석 셋뿐이다. 이 단언이 그 승격의 회귀 가드다.
            CollectionAssert.AreEqual(new[] { "c_ok", "c_sub" }, kept);
            Assert.AreEqual(3, dropped);
        }

        // ---- 공통 경계 -------------------------------------------------------

        [Test]
        public void NullCatalog_DropsEverything_NoThrow()
        {
            var units = PresetApply.FilterUnits(new[] { "u_a", "u_b" }, null, out int du);
            var stones = PresetApply.FilterStones(new[] { "s_a" }, null, out int ds);
            var cards = PresetApply.FilterCards(new[] { "c_a" }, null, out int dc);

            Assert.AreEqual(0, units.Count);
            Assert.AreEqual(2, du, "미배선을 조용한 빈 프리셋으로 위장하지 않는다 — 전량 제외로 집계");
            Assert.AreEqual(0, stones.Count);
            Assert.AreEqual(1, ds);
            Assert.AreEqual(0, cards.Count);
            Assert.AreEqual(1, dc);
        }

        [Test]
        public void NullInput_ReturnsEmpty_ZeroDropped()
        {
            var catalog = UnitCatalog("u_a");

            var kept = PresetApply.FilterUnits(null, catalog, out int dropped);

            Assert.AreEqual(0, kept.Count);
            Assert.AreEqual(0, dropped);
        }
    }
}
