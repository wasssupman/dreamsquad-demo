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

        [Test]
        public void Load_EmptyAttemptId_ReturnsFalse()
        {
            // a record with no attemptId is nothing the client can complete.
            PendingMatchStore.Save("", "u-1", 1L);
            Assert.IsFalse(PendingMatchStore.TryLoad(out _));
        }
    }
}
