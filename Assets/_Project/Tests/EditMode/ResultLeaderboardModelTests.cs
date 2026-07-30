using System.Collections.Generic;
using NUnit.Framework;
using Wassup.Core.Api;
using Wassup.UI;

namespace Wassup.Tests.EditMode
{
    // result-screen-visual-upgrade Unit 2 — pure row-model coverage for the
    // result leaderboard (no UI objects). Pins the tournament-play-report
    // contract: score-desc rank, WAITING fill to maxEntryCount, own-row flag.
    public class ResultLeaderboardModelTests
    {
        private static TournamentApi.ResultEntry Entry(string id, string name, int score, int rank = 0)
            => new TournamentApi.ResultEntry { userId = id, userName = name, score = score, rank = rank };

        [Test]
        public void BuildRows_OrdersByScoreDescending_AndAssignsPositionRank()
        {
            var entries = new List<TournamentApi.ResultEntry>
            {
                Entry("u1", "Alice", 700),
                Entry("u2", "Bob", 1200),
                Entry("u3", "Carol", 900),
            };

            var rows = LeaderboardList.BuildRows(entries, maxEntryCount: 3, ownUserId: "u1");

            Assert.AreEqual(3, rows.Count);
            Assert.AreEqual("Bob", rows[0].Name);
            Assert.AreEqual(1, rows[0].Rank);
            Assert.AreEqual("Carol", rows[1].Name);
            Assert.AreEqual(2, rows[1].Rank);
            Assert.AreEqual("Alice", rows[2].Name);
            Assert.AreEqual(3, rows[2].Rank);
        }

        [Test]
        public void BuildRows_FillsWaitingSlotsUpToMaxEntryCount()
        {
            var entries = new List<TournamentApi.ResultEntry> { Entry("u1", "Alice", 500) };

            var rows = LeaderboardList.BuildRows(entries, maxEntryCount: 10, ownUserId: "u1");

            Assert.AreEqual(10, rows.Count);
            Assert.IsFalse(rows[0].IsWaiting);
            for (int i = 1; i < rows.Count; i++)
            {
                Assert.IsTrue(rows[i].IsWaiting, $"slot {i} should be waiting");
                Assert.AreEqual("대기 중...", rows[i].Name); // battle-ui-korean 한글화 반영
                Assert.AreEqual(i + 1, rows[i].Rank);
            }
        }

        [Test]
        public void BuildRows_FlagsOwnRow()
        {
            var entries = new List<TournamentApi.ResultEntry>
            {
                Entry("me", "Player", 800),
                Entry("other", "Rival", 1000),
            };

            var rows = LeaderboardList.BuildRows(entries, maxEntryCount: 2, ownUserId: "me");

            Assert.IsFalse(rows[0].IsPlayer); // Rival (1000)
            Assert.IsTrue(rows[1].IsPlayer);  // Player (800)
        }

        [Test]
        public void BuildRows_ServerRankWinsWhenPositive()
        {
            var entries = new List<TournamentApi.ResultEntry>
            {
                Entry("u1", "Alice", 700, rank: 5),
                Entry("u2", "Bob", 1200, rank: 2),
            };

            var rows = LeaderboardList.BuildRows(entries, maxEntryCount: 2, ownUserId: "");

            Assert.AreEqual(2, rows[0].Rank); // server rank, not position 1
            Assert.AreEqual(5, rows[1].Rank);
        }

        [Test]
        public void BuildRows_TruncatesLongNames_AndSubstitutesEmpty()
        {
            var entries = new List<TournamentApi.ResultEntry>
            {
                Entry("u1", "ABCDEFGHIJKLMNOP", 900), // 16 chars -> 10
                Entry("u2", "", 800),
            };

            var rows = LeaderboardList.BuildRows(entries, maxEntryCount: 2, ownUserId: "");

            Assert.AreEqual("ABCDEFGHIJ", rows[0].Name);
            Assert.AreEqual("?", rows[1].Name);
        }

        [Test]
        public void BuildRows_NullEntries_ReturnsAllWaiting()
        {
            var rows = LeaderboardList.BuildRows(null, maxEntryCount: 10, ownUserId: "me");

            Assert.AreEqual(10, rows.Count);
            foreach (var row in rows) Assert.IsTrue(row.IsWaiting);
        }

        // tournament-history-deck-view unit 1 — 덱보기 버튼이 쓸 원문 페이로드를 행이
        // 실어 나른다. 파싱은 팝업을 여는 직전에 한 번.
        [Test]
        public void BuildRows_CarriesDeckInfo_AndLeavesWaitingSlotsNull()
        {
            var withDeck = Entry("u1", "Alice", 700);
            withDeck.deckInfo = "{\"v\":1}";

            var rows = LeaderboardList.BuildRows(
                new List<TournamentApi.ResultEntry> { withDeck, Entry("u2", "Bob", 400) },
                maxEntryCount: 3, ownUserId: "u1");

            Assert.AreEqual("{\"v\":1}", rows[0].DeckInfo);
            Assert.IsNull(rows[1].DeckInfo, "기록이 없는 참가(구 엔트리)는 null 이다");
            Assert.IsTrue(rows[2].IsWaiting);
            Assert.IsNull(rows[2].DeckInfo);
        }
    }
}
