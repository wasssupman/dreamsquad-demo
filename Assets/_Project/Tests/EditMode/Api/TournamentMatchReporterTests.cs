using NUnit.Framework;
using Wassup.Core.Api;

namespace Wassup.Tests.EditMode.Api
{
    // abandoned-match-reconciliation unit 1 — ReconcilePending's client-side branches
    // (no-record / no-account / account-mismatch / over-window discard). Each of these
    // clears the store and returns WITHOUT reaching the Complete0 network call, so they
    // are EditMode-safe. The within-window Complete0 path fires a real server request
    // and is verified live (throwaway-account probe), not here.
    public class TournamentMatchReporterTests
    {
        [SetUp]
        public void SetUp() { PendingMatchStore.Clear(); UserSession.Clear(); }

        [TearDown]
        public void TearDown() { PendingMatchStore.Clear(); UserSession.Clear(); }

        private static void SignInAs(string userId)
            => UserSession.Set(new UserSignApi.SignedInUser { userId = userId }, "id-token",
                "https://dev-api-somnia.cashroyale.games");

        private static long Now() => System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        [Test]
        public void ReconcilePending_NoRecord_NoOp()
        {
            SignInAs("u-1");
            Assert.DoesNotThrow(TournamentMatchReporter.ReconcilePending);
            Assert.IsFalse(PendingMatchStore.TryLoad(out _));
        }

        [Test]
        public void ReconcilePending_NoAccount_ClearsRecord()
        {
            PendingMatchStore.Save("a-1", "u-1", Now());
            // UserSession cleared in SetUp → HasAccount false: drop the record, no send.
            TournamentMatchReporter.ReconcilePending();
            Assert.IsFalse(PendingMatchStore.TryLoad(out _));
        }

        [Test]
        public void ReconcilePending_AccountMismatch_ClearsWithoutComplete()
        {
            // record belongs to a different user → must NOT be completed under this session.
            PendingMatchStore.Save("a-1", "other-user", Now());
            SignInAs("u-1");
            TournamentMatchReporter.ReconcilePending();
            Assert.IsFalse(PendingMatchStore.TryLoad(out _));
        }

        [Test]
        public void ReconcilePending_OverWindow_DiscardsWithoutComplete()
        {
            long oldUnix = Now() - (PendingMatchPolicy.DefaultTtlSeconds + 3600); // TTL + 1h past
            PendingMatchStore.Save("a-1", "u-1", oldUnix);
            SignInAs("u-1");
            TournamentMatchReporter.ReconcilePending();
            Assert.IsFalse(PendingMatchStore.TryLoad(out _));
        }
    }
}
