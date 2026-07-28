using NUnit.Framework;
using Wassup.Core.Api;

namespace Wassup.Tests.EditMode.Api
{
    // abandoned-match-reconciliation unit 0 — PlayerPrefs roundtrip + single-slot +
    // corrupt/absent guards. Clears its key in setup/teardown so it never leaks into
    // project prefs.
    public class PendingMatchStoreTests
    {
        [SetUp]
        public void SetUp() => PendingMatchStore.Clear();

        [TearDown]
        public void TearDown() => PendingMatchStore.Clear();

        [Test]
        public void SaveThenLoad_RoundTripsAllFields()
        {
            PendingMatchStore.Save("a-1", "u-1", 1_700_000_000L);

            Assert.IsTrue(PendingMatchStore.TryLoad(out var rec));
            Assert.AreEqual("a-1", rec.attemptId);
            Assert.AreEqual("u-1", rec.userId);
            Assert.AreEqual(1_700_000_000L, rec.startedAtUnix);
        }

        [Test]
        public void Load_WhenEmpty_ReturnsFalse()
        {
            Assert.IsFalse(PendingMatchStore.TryLoad(out _));
        }

        [Test]
        public void Clear_RemovesRecord()
        {
            PendingMatchStore.Save("a-1", "u-1", 1L);
            PendingMatchStore.Clear();
            Assert.IsFalse(PendingMatchStore.TryLoad(out _));
        }

        [Test]
        public void Save_Overwrites_SingleSlot()
        {
            PendingMatchStore.Save("a-1", "u-1", 1L);
            PendingMatchStore.Save("a-2", "u-2", 2L);

            Assert.IsTrue(PendingMatchStore.TryLoad(out var rec));
            Assert.AreEqual("a-2", rec.attemptId);
            Assert.AreEqual("u-2", rec.userId);
            Assert.AreEqual(2L, rec.startedAtUnix);
        }

        // ── tournament-flow-guards unit 9 — compare-and-clear ────────────────

        [Test]
        public void ClearIfMatches_SameAttempt_Clears()
        {
            PendingMatchStore.Save("a-1", "u-1", 1L);
            Assert.IsTrue(PendingMatchStore.ClearIfMatches("a-1"));
            Assert.IsFalse(PendingMatchStore.TryLoad(out _));
        }

        // complete 왕복 중에 새 매치가 자기 attempt 를 저장했다면, 늦게 도착한 성공
        // 응답이 **다음 판의 안전망**을 지워서는 안 된다.
        [Test]
        public void ClearIfMatches_DifferentAttempt_KeepsRecord()
        {
            PendingMatchStore.Save("a-2", "u-1", 2L);
            Assert.IsFalse(PendingMatchStore.ClearIfMatches("a-1"));
            Assert.IsTrue(PendingMatchStore.TryLoad(out var rec));
            Assert.AreEqual("a-2", rec.attemptId);
        }

        [Test]
        public void ClearIfMatches_NoRecordOrEmptyId_ReturnsFalse()
        {
            Assert.IsFalse(PendingMatchStore.ClearIfMatches("a-1")); // 레코드 없음
            PendingMatchStore.Save("a-1", "u-1", 1L);
            Assert.IsFalse(PendingMatchStore.ClearIfMatches(null));
            Assert.IsFalse(PendingMatchStore.ClearIfMatches(""));
            Assert.IsTrue(PendingMatchStore.TryLoad(out _)); // 무엇도 지우지 않았다
        }

        [Test]
        public void Load_EmptyAttemptId_ReturnsFalse()
        {
            // a record with no attemptId is nothing the client can complete.
            PendingMatchStore.Save("", "u-1", 1L);
            Assert.IsFalse(PendingMatchStore.TryLoad(out _));
        }
    }
}
