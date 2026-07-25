using NUnit.Framework;
using Wassup.Core.Api;

namespace Wassup.Tests.EditMode.Api
{
    // abandoned-match-reconciliation unit 1 (+ tournament-flow-guards unit 5·6) —
    // ReconcilePending's EditMode-safe client-side branches (no-record / no-account /
    // account-mismatch). Each returns WITHOUT reaching the Complete0 network call. The
    // complete(0) path (나이 무관 항상 시도, 성공 시에만 pending clear) fires a real server
    // request and is verified live (throwaway-account probe), not here.
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
    }
}
