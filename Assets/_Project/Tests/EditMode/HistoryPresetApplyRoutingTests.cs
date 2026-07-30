using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Wassup.Core;
using Wassup.Core.Api;
using Wassup.UI;

namespace Wassup.Tests.EditMode
{
    // deck-info-preset-apply unit 2 — 히스토리 패널의 예약 스테이징. 패널은 프로필을
    // 모르고 예약(Stage) + 이동 요청(onPresetApply)까지만 한다 — 그 계약을 고정한다.
    //
    // 호스트를 비활성으로 두는 이유: OnEnable 이 LoadEntries(네트워크/UserSession)로
    // 가므로 EditMode 에서 돌릴 수 없다. RequestPresetApply 는 UI 를 건드리지 않는다.
    public class HistoryPresetApplyRoutingTests
    {
        private GameObject _go;
        private TournamentHistoryPanel _panel;

        [SetUp]
        public void SetUp()
        {
            PresetApply.Clear();
            _go = new GameObject("HistoryRoutingHost");
            _go.SetActive(false);
            _panel = _go.AddComponent<TournamentHistoryPanel>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
            PresetApply.Clear();
        }

        // OpenDeckPopup 이 하는 두 줄(_deckPayload/_deckOwnerName 기억)을 재현한다 —
        // 실제 경로는 LeaderboardList.Row + 팝업 빌드가 필요해 EditMode 범위 밖이다.
        private void SetOpenedDeck(TournamentDeckInfo.Payload payload, string owner)
        {
            typeof(TournamentHistoryPanel)
                .GetField("_deckPayload", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(_panel, payload);
            typeof(TournamentHistoryPanel)
                .GetField("_deckOwnerName", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(_panel, owner);
        }

        private void Request(PresetApply.Target target)
            => typeof(TournamentHistoryPanel)
                .GetMethod("RequestPresetApply", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(_panel, new object[] { target });

        private static TournamentDeckInfo.Payload Payload(
            string[] units = null, string[] stones = null, string[] cards = null)
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

        [Test]
        public void SquadRequest_StagesSquadFieldsOnly_AndRaisesEvent()
        {
            var received = new List<PresetApply.Target>();
            _panel.onPresetApply += t => received.Add(t);
            SetOpenedDeck(Payload(units: new[] { "u_a" }, stones: new[] { "s_a" },
                cards: new[] { "c_a" }), "wassup");

            Request(PresetApply.Target.Squad);

            CollectionAssert.AreEqual(new[] { PresetApply.Target.Squad }, received);
            Assert.IsTrue(PresetApply.TryConsume(PresetApply.Target.Squad, out var req));
            Assert.AreEqual("wassup의 덱", req.presetName);
            CollectionAssert.AreEqual(new[] { "u_a" }, req.unitIds);
            CollectionAssert.AreEqual(new[] { "s_a" }, req.stoneIds);
            Assert.IsNull(req.cardIds, "탭이 곧 대상 — 스쿼드 적용에 카드를 끌고 가지 않는다");
        }

        [Test]
        public void DreamcatcherRequest_StagesCardsOnly()
        {
            SetOpenedDeck(Payload(units: new[] { "u_a" }, cards: new[] { "c_a", "c_b" }), "rival");

            Request(PresetApply.Target.Dreamcatcher);

            Assert.IsTrue(PresetApply.TryConsume(PresetApply.Target.Dreamcatcher, out var req));
            Assert.AreEqual("rival의 덱", req.presetName);
            CollectionAssert.AreEqual(new[] { "c_a", "c_b" }, req.cardIds);
            Assert.IsNull(req.unitIds);
            Assert.IsNull(req.stoneIds);
        }

        [Test]
        public void LastOpenedDeckWins()
        {
            SetOpenedDeck(Payload(units: new[] { "u_first" }), "first");
            Request(PresetApply.Target.Squad);
            Assert.IsTrue(PresetApply.TryConsume(PresetApply.Target.Squad, out var a));
            CollectionAssert.AreEqual(new[] { "u_first" }, a.unitIds);

            SetOpenedDeck(Payload(units: new[] { "u_second" }), "second");
            Request(PresetApply.Target.Squad);
            Assert.IsTrue(PresetApply.TryConsume(PresetApply.Target.Squad, out var b));
            CollectionAssert.AreEqual(new[] { "u_second" }, b.unitIds);
            Assert.AreEqual("second의 덱", b.presetName);
        }

        [Test]
        public void NullPayload_StagesEmptyRequest_WithFallbackName_NoThrow()
        {
            // 버튼 비활성(항목 0)이 정상 방어선이고, 여기는 그 뒤의 안전망이다.
            SetOpenedDeck(null, null);

            Request(PresetApply.Target.Squad);

            Assert.IsTrue(PresetApply.TryConsume(PresetApply.Target.Squad, out var req));
            Assert.AreEqual("불러온 덱", req.presetName);
            Assert.IsNull(req.unitIds);
        }
    }
}
