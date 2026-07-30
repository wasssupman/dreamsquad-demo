using System.Collections.Generic;
using NUnit.Framework;
using Wassup.Core.Api;
using Wassup.UI;

namespace Wassup.Tests.EditMode
{
    // tournament-history-deck-view unit 0 — 목록 정렬/일시 표기. 진입 시 "가장 최근"
    // 을 자동 포커스하므로 정렬이 틀리면 엉뚱한 토너먼트가 열린다.
    public class TournamentHistoryPanelSortTests
    {
        private static TournamentApi.UserTournamentResultEntry Entry(string id, string createdTime)
            => new TournamentApi.UserTournamentResultEntry
            {
                tournamentEntryId = id,
                createdTime = createdTime,
            };

        private static List<string> IdsOf(List<TournamentApi.UserTournamentResultEntry> list)
        {
            var ids = new List<string>(list.Count);
            for (int i = 0; i < list.Count; i++) ids.Add(list[i].tournamentEntryId);
            return ids;
        }

        [Test]
        public void SortRecentFirst_NewestFirst()
        {
            var sorted = TournamentHistoryPanel.SortRecentFirst(new[]
            {
                Entry("old", "2026-07-01T10:00:00Z"),
                Entry("new", "2026-07-30T10:00:00Z"),
                Entry("mid", "2026-07-15T10:00:00Z"),
            });

            CollectionAssert.AreEqual(new[] { "new", "mid", "old" }, IdsOf(sorted));
        }

        [Test]
        public void SortRecentFirst_UndatedGoesLast_ButIsNeverDropped()
        {
            // createdTime 은 표시 전용 필드다 — 그거 하나 때문에 참가 기록이 목록에서
            // 사라지면 그 토너먼트는 영영 열 수 없다.
            var sorted = TournamentHistoryPanel.SortRecentFirst(new[]
            {
                Entry("blank", ""),
                Entry("dated", "2026-07-15T10:00:00Z"),
                Entry("garbage", "언젠가"),
            });

            CollectionAssert.AreEqual(new[] { "dated", "blank", "garbage" }, IdsOf(sorted));
        }

        [Test]
        public void SortRecentFirst_EqualTimes_KeepOriginalOrder()
        {
            // List.Sort 는 불안정 정렬이라 인덱스를 최종 타이브레이커로 쓴다. 없으면
            // 같은 시각 참가들의 순서가 실행마다 흔들리고 자동 포커스 대상도 흔들린다.
            var sorted = TournamentHistoryPanel.SortRecentFirst(new[]
            {
                Entry("a", "2026-07-15T10:00:00Z"),
                Entry("b", "2026-07-15T10:00:00Z"),
                Entry("c", "2026-07-15T10:00:00Z"),
            });

            CollectionAssert.AreEqual(new[] { "a", "b", "c" }, IdsOf(sorted));
        }

        [Test]
        public void SortRecentFirst_NullInputs_AreTolerated()
        {
            Assert.IsEmpty(TournamentHistoryPanel.SortRecentFirst(null));

            var sorted = TournamentHistoryPanel.SortRecentFirst(new[]
            {
                null,
                Entry("real", "2026-07-15T10:00:00Z"),
                null,
            });
            CollectionAssert.AreEqual(new[] { "real" }, IdsOf(sorted));
        }

        [Test]
        public void FormatDate_IncludesTimeOfDay()
        {
            // 계약 4 — 목록이 선택 UI 가 되면서 같은 날 참가가 구분돼야 한다.
            // 로컬 변환을 테스트에서 다시 계산하면 동어반복이므로 모양만 고정한다.
            string text = TournamentHistoryPanel.FormatDate("2026-07-15T10:34:00Z");

            Assert.AreEqual(16, text.Length, $"expected yyyy.MM.dd HH:mm, got '{text}'");
            StringAssert.Contains(":", text);
            Assert.AreEqual('.', text[4]);
        }

        [Test]
        public void FormatDate_AcceptsEpochMillis_TheShapeTheServerActuallySends()
        {
            // swagger 는 format: date-time 이라고 하지만 dev 서버는 epoch 밀리초 문자열을
            // 준다. ISO 만 파싱하던 시절엔 날짜 칸이 항상 비어 있었다.
            string text = TournamentHistoryPanel.FormatDate("1785419835370");

            Assert.AreEqual(16, text.Length, $"expected yyyy.MM.dd HH:mm, got '{text}'");
            StringAssert.StartsWith("2026.", text);
        }

        [Test]
        public void SortRecentFirst_OrdersEpochMillis_NewestFirst()
        {
            var sorted = TournamentHistoryPanel.SortRecentFirst(new[]
            {
                Entry("old", "1785310995396"),
                Entry("new", "1785419835370"),
                Entry("mid", "1785406023853"),
            });

            CollectionAssert.AreEqual(new[] { "new", "mid", "old" }, IdsOf(sorted));
        }

        [Test]
        public void FormatDate_AcceptsEpochSeconds_Too()
        {
            // 초 단위로 바뀌어도 견디게 — 경계(1e12)는 실사용 구간에서 안전하다.
            string millis = TournamentHistoryPanel.FormatDate("1785419835370");
            string seconds = TournamentHistoryPanel.FormatDate("1785419835");

            Assert.AreEqual(millis, seconds);
        }

        [Test]
        public void FormatDate_Unparseable_IsBlankNotThrown()
        {
            Assert.AreEqual("", TournamentHistoryPanel.FormatDate(null));
            Assert.AreEqual("", TournamentHistoryPanel.FormatDate(""));
            Assert.AreEqual("", TournamentHistoryPanel.FormatDate("언젠가"));
        }
    }
}
