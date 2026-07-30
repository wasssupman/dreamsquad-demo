using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Wassup.Core.Api;
using Wassup.Data;
using Wassup.UI;

namespace Wassup.Tests.EditMode
{
    // tournament-history-deck-view units 2·3 — 팝업 뷰의 견고성 계약. 순수 변환
    // (DeckInfoDisplayTests)이 못 덮는 몫: 탭별 선택 유지, 인덱스 기준 선택(중복 슬롯),
    // 빈 목록/무페이로드 안내, 반복 Show.
    public class DeckInfoPopupTests
    {
        private GameObject _go;
        private DeckInfoPopup _popup;
        private DreamcatcherCardCatalog _cards;

        [SetUp]
        public void SetUp()
        {
            var card = ScriptableObject.CreateInstance<DreamcatcherCard>();
            card.id = "c_known";
            card.displayName = "알려진 카드";
            _cards = ScriptableObject.CreateInstance<DreamcatcherCardCatalog>();
            _cards.cards = new[] { card };

            var unit = ScriptableObject.CreateInstance<DefenderUnitData>();
            unit.id = "u_known";
            unit.displayName = "알려진 유닛";
            var units = ScriptableObject.CreateInstance<DefenderCatalog>();
            units.units = new[] { unit };

            var stone = ScriptableObject.CreateInstance<DreamstoneData>();
            stone.id = "s_known";
            stone.displayName = "알려진 스톤";
            var stones = ScriptableObject.CreateInstance<DreamstoneCatalog>();
            stones.stones = new[] { stone };

            _go = new GameObject("DeckInfoPopupTests", typeof(RectTransform));
            _popup = _go.AddComponent<DeckInfoPopup>();
            _popup.Setup(units, stones, _cards);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_go);

        private static TournamentDeckInfo.Payload Payload(
            IEnumerable<string> units = null, IEnumerable<string> stones = null, IEnumerable<string> cards = null)
            => new TournamentDeckInfo.Payload
            {
                v = TournamentDeckInfo.Version,
                squad = new TournamentDeckInfo.SquadDeck
                {
                    units = units != null ? new List<string>(units) : new List<string>(),
                    stones = stones != null ? new List<string>(stones) : new List<string>(),
                },
                dc = new TournamentDeckInfo.DreamcatcherDeck
                {
                    cards = cards != null ? new List<string>(cards) : new List<string>(),
                },
            };

        private int CountCells() => CountNamed(_go.transform, "Cell");

        private static int CountNamed(Transform root, string name)
        {
            int n = root.name == name ? 1 : 0;
            for (int i = 0; i < root.childCount; i++) n += CountNamed(root.GetChild(i), name);
            return n;
        }

        [Test]
        public void NullPayload_ShowsGuidance_OnBothTabs_WithoutThrowing()
        {
            _popup.Show(null, "someone");

            Assert.IsFalse(_popup.TryGetSelectedItem(out _));
            Assert.AreEqual(0, CountCells());

            _popup.SwitchTab(1);
            Assert.IsFalse(_popup.TryGetSelectedItem(out _));
            Assert.AreEqual(0, CountCells());
        }

        [Test]
        public void EmptyTab_KeepsPopupUsable_AndSelectsNothing()
        {
            // 카드 없이 플레이한 참가자 — 스쿼드 탭은 정상, 드림캐쳐 탭만 비어 있다.
            _popup.Show(Payload(units: new[] { "u_known" }), "someone");

            Assert.IsTrue(_popup.TryGetSelectedItem(out var unit));
            Assert.AreEqual("u_known", unit.Id);

            _popup.SwitchTab(1);
            Assert.IsFalse(_popup.TryGetSelectedItem(out _));
            Assert.AreEqual(0, CountCells());
        }

        [Test]
        public void SelectionIsByIndex_NotById_SoDuplicatesAddressCorrectly()
        {
            // 같은 카드 2장이 설계상 허용된다. id 기준이면 어느 슬롯을 골랐는지 잃는다.
            _popup.Show(Payload(cards: new[] { "c_known", "c_unknown", "c_known" }), "someone");
            _popup.SwitchTab(1);

            Assert.AreEqual(3, CountCells());

            _popup.SelectCell(0, 1);
            Assert.IsTrue(_popup.TryGetSelectedItem(out var second));
            Assert.AreEqual("c_unknown", second.Id);
            Assert.IsFalse(second.Resolved);

            _popup.SelectCell(0, 2);
            Assert.IsTrue(_popup.TryGetSelectedItem(out var third));
            Assert.AreEqual("c_known", third.Id);
            Assert.IsTrue(third.Resolved, "중복의 두 번째 사본도 정상 해석돼야 한다");
        }

        [Test]
        public void TabSelection_IsKeptPerTab()
        {
            _popup.Show(Payload(units: new[] { "u_known" }, stones: new[] { "s_known" },
                cards: new[] { "c_known" }), "someone");

            _popup.SelectCell(1, 0); // 스쿼드 탭의 드림스톤 섹션
            Assert.IsTrue(_popup.TryGetSelectedItem(out var stone));
            Assert.AreEqual("s_known", stone.Id);

            _popup.SwitchTab(1);
            Assert.IsTrue(_popup.TryGetSelectedItem(out var card));
            Assert.AreEqual("c_known", card.Id);

            _popup.SwitchTab(0);
            Assert.IsTrue(_popup.TryGetSelectedItem(out var back));
            Assert.AreEqual("s_known", back.Id, "탭을 돌아오면 그 탭의 선택이 남아 있어야 한다");
        }

        [Test]
        public void RepeatedShow_RebuildsWithoutAccumulating()
        {
            _popup.Show(Payload(cards: new[] { "c_known", "c_known" }), "a");
            _popup.SwitchTab(1);
            Assert.AreEqual(2, CountCells());

            _popup.Show(Payload(cards: new[] { "c_known" }), "b");
            _popup.SwitchTab(1);
            Assert.AreEqual(1, CountCells(), "이전 Show 의 셀이 남으면 안 된다");

            _popup.Show(null, "c");
            Assert.AreEqual(0, CountCells());
        }

        [Test]
        public void NullCatalogs_StillRender()
        {
            var go = new GameObject("NoCatalogs", typeof(RectTransform));
            try
            {
                var popup = go.AddComponent<DeckInfoPopup>();
                popup.Setup(null, null, null);
                popup.Show(Payload(units: new[] { "a", "b" }, cards: new[] { "c" }), "someone");

                Assert.AreEqual(2, CountNamed(go.transform, "Cell"));
                Assert.IsTrue(popup.TryGetSelectedItem(out var item));
                Assert.IsFalse(item.Resolved);
                Assert.AreEqual("a", item.Name);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void PresetButton_HiddenForOwnDeck_ShownForOthers()
        {
            // 내가 그때 쓴 덱을 내 프로필에 다시 쓰는 건 no-op 이다.
            _popup.Show(Payload(cards: new[] { "c_known" }), "wassup", allowPresetApply: false);
            Assert.IsFalse(_popup.IsPresetButtonVisible);

            _popup.Show(Payload(cards: new[] { "c_known" }), "someone-else");
            Assert.IsTrue(_popup.IsPresetButtonVisible, "남의 덱에는 자리가 있어야 한다");
        }

        [Test]
        public void OverCount_RendersEveryItem()
        {
            var many = new List<string>();
            for (int i = 0; i < 14; i++) many.Add($"c_{i}");

            _popup.Show(Payload(cards: many), "someone");
            _popup.SwitchTab(1);

            Assert.AreEqual(14, CountCells(), "고정 슬롯이 아니다 — 온 만큼 전부 그린다");
        }

        // ---- 프리셋 적용 버튼 (deck-info-preset-apply unit 1) -------------------

        [Test]
        public void ApplyButton_RaisesPerTabEvent_AndCloses()
        {
            int squad = 0, dc = 0;
            _popup.SquadApplyRequested += () => squad++;
            _popup.DreamcatcherApplyRequested += () => dc++;

            _popup.Show(Payload(units: new[] { "u_known" }, cards: new[] { "c_known" }), "someone");
            _popup.ClickPresetApply();

            Assert.AreEqual(1, squad, "스쿼드 탭 → 스쿼드 이벤트");
            Assert.AreEqual(0, dc);
            Assert.IsFalse(_popup.gameObject.activeSelf, "적용은 완료된 동작 — 팝업이 닫힌다");

            _popup.Show(Payload(units: new[] { "u_known" }, cards: new[] { "c_known" }), "someone");
            _popup.SwitchTab(1);
            _popup.ClickPresetApply();

            Assert.AreEqual(1, squad);
            Assert.AreEqual(1, dc, "드림캐쳐 탭 → 드림캐쳐 이벤트");
        }

        [Test]
        public void ApplyButton_DisabledWhenTabHasNothingToApply()
        {
            int raised = 0;
            _popup.SquadApplyRequested += () => raised++;
            _popup.DreamcatcherApplyRequested += () => raised++;

            _popup.Show(null, "someone");   // 덱 정보 없음
            Assert.IsFalse(_popup.IsPresetButtonInteractable);
            _popup.ClickPresetApply();
            Assert.AreEqual(0, raised, "비활성이면 이벤트도 없다");

            _popup.SwitchTab(1);
            Assert.IsFalse(_popup.IsPresetButtonInteractable);

            // 혼합 케이스 — 스쿼드는 비었지만 카드는 있다.
            _popup.Show(Payload(cards: new[] { "c_known" }), "someone");
            Assert.IsFalse(_popup.IsPresetButtonInteractable, "빈 스쿼드 탭은 적용할 것이 없다");
            _popup.SwitchTab(1);
            Assert.IsTrue(_popup.IsPresetButtonInteractable, "카드 탭은 적용 가능");
        }

        [Test]
        public void ApplyButton_HiddenForOwnDeck_NeverRaises()
        {
            int raised = 0;
            _popup.SquadApplyRequested += () => raised++;

            _popup.Show(Payload(units: new[] { "u_known" }), "me", allowPresetApply: false);
            _popup.ClickPresetApply();

            Assert.AreEqual(0, raised, "내 덱에는 버튼이 없다 — 숨김 상태에서 발화 금지");
        }
    }
}
