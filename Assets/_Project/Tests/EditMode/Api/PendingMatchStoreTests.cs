using NUnit.Framework;
using UnityEngine;
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

        // ── tournament-deck-info unit 4 — 덱 스냅샷 ───────────────────────────

        [Test]
        public void SaveDeckInfo_SameAttempt_RoundTrips()
        {
            PendingMatchStore.Save("a-1", "u-1", 1L);

            Assert.IsTrue(PendingMatchStore.SaveDeckInfo("a-1", "{\"v\":1}"));
            Assert.IsTrue(PendingMatchStore.TryLoad(out var rec));
            Assert.AreEqual("{\"v\":1}", rec.deckInfo);
            Assert.AreEqual("a-1", rec.attemptId);   // 다른 필드는 보존된다
            Assert.AreEqual("u-1", rec.userId);
            Assert.AreEqual(1L, rec.startedAtUnix);
        }

        // 이전 판의 늦은 저장이 **다음 판의** 레코드에 덱을 박으면, reconcile 이 남의 덱을
        // 그 attempt 의 덱으로 올린다.
        [Test]
        public void SaveDeckInfo_DifferentAttempt_IsNoOp()
        {
            PendingMatchStore.Save("a-2", "u-1", 2L);

            Assert.IsFalse(PendingMatchStore.SaveDeckInfo("a-1", "{\"v\":1}"));
            Assert.IsTrue(PendingMatchStore.TryLoad(out var rec));
            Assert.IsTrue(string.IsNullOrEmpty(rec.deckInfo));
        }

        [Test]
        public void SaveDeckInfo_NoRecordOrEmptyId_ReturnsFalse()
        {
            Assert.IsFalse(PendingMatchStore.SaveDeckInfo("a-1", "{\"v\":1}")); // 레코드 없음
            PendingMatchStore.Save("a-1", "u-1", 1L);
            Assert.IsFalse(PendingMatchStore.SaveDeckInfo(null, "{\"v\":1}"));
            Assert.IsFalse(PendingMatchStore.SaveDeckInfo("", "{\"v\":1}"));
        }

        // 새 attempt 의 레코드는 덱이 비어 있어야 한다 — 남으면 이전 판의 덱이 이 판의
        // 마감에 실린다.
        [Test]
        public void Save_NewAttempt_ClearsDeckInfo()
        {
            PendingMatchStore.Save("a-1", "u-1", 1L);
            PendingMatchStore.SaveDeckInfo("a-1", "{\"v\":1}");

            PendingMatchStore.Save("a-2", "u-1", 2L);

            Assert.IsTrue(PendingMatchStore.TryLoad(out var rec));
            Assert.IsTrue(string.IsNullOrEmpty(rec.deckInfo));
        }

        // 구 빌드가 남긴 레코드(deckInfo 키 없음)도 그대로 마감할 수 있어야 한다 — 앱
        // 업데이트 직전에 하드킬된 판이 여기 해당한다.
        [Test]
        public void Load_LegacyRecordWithoutDeckInfo_Succeeds()
        {
            PlayerPrefs.SetString("Wassup.PendingMatch",
                "{\"attemptId\":\"a-1\",\"userId\":\"u-1\",\"startedAtUnix\":1}");

            Assert.IsTrue(PendingMatchStore.TryLoad(out var rec));
            Assert.AreEqual("a-1", rec.attemptId);
            Assert.AreEqual(string.Empty, rec.deckInfo);   // TryLoad 가 누락 필드를 정규화한다
        }
    }
}
