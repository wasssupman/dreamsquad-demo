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

        // ── tournament-flow-guards unit 7 — 락 판정 + 복구 게이트(onDone) ────────

        // 서버 계약(unit 4 라이브 프로브): play-while-locked 는 "cannot wait" 를 실어
        // 거부한다. 필드 위치가 아니라 구문 포함으로 판정하는 계약을 고정한다.
        [Test]
        public void IsLockError_CannotWaitPhrase_AnyCasing_True()
        {
            Assert.IsTrue(TournamentMatchReporter.IsLockError(
                "TOURNAMENT_ERROR — cannot wait / user already has an open attempt (HTTP: HTTP/1.1 500)"));
            Assert.IsTrue(TournamentMatchReporter.IsLockError("Cannot Wait"));
        }

        [Test]
        public void IsLockError_OtherOrEmpty_False()
        {
            Assert.IsFalse(TournamentMatchReporter.IsLockError(null));
            Assert.IsFalse(TournamentMatchReporter.IsLockError(""));
            Assert.IsFalse(TournamentMatchReporter.IsLockError("empty response body (HTTP: Cannot connect)"));
        }

        // 복구 재시도는 released=true 에만 게이트된다 — 클라측 조기 분기는 전부 false.
        [Test]
        public void ReconcilePending_NoRecord_ReportsNotReleased()
        {
            SignInAs("u-1");
            bool? released = null;
            TournamentMatchReporter.ReconcilePending(r => released = r);
            Assert.IsFalse(released.Value);
        }

        [Test]
        public void ReconcilePending_AccountMismatch_ReportsNotReleased()
        {
            PendingMatchStore.Save("a-1", "other-user", Now());
            SignInAs("u-1");
            bool? released = null;
            TournamentMatchReporter.ReconcilePending(r => released = r);
            Assert.IsFalse(released.Value);
            Assert.IsFalse(PendingMatchStore.TryLoad(out _));
        }

        [Test]
        public void ReconcilePending_NoAccount_ReportsNotReleased()
        {
            PendingMatchStore.Save("a-1", "u-1", Now());
            bool? released = null;
            TournamentMatchReporter.ReconcilePending(r => released = r);
            Assert.IsFalse(released.Value);
        }

        // ── tournament-flow-guards unit 8 — 비게이트 BeginMatch 는 발행하지 않는다 ──

        private static void SetPrivateStatic(string field, object value)
            => typeof(TournamentMatchReporter)
                .GetField(field, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                .SetValue(null, value);

        private static object GetPrivateStatic(string field)
            => typeof(TournamentMatchReporter)
                .GetField(field, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                .GetValue(null);

        // 로그인 상태 + stale attempt 가 있어도, 비게이트 진입은 리셋만 하고 play 를
        // 발행하지 않는다(pending 미생성). 결함 A(TestMode/직접 Play 의 엔트리 오염) 방지.
        [Test]
        public void BeginMatch_NonGated_ResetsStateWithoutIssuing()
        {
            SignInAs("u-1"); // account + baseUrl — 예전 코드라면 여기서 play 가 나갔다
            SetPrivateStatic("_lobbyIssued", false);
            SetPrivateStatic("_attemptId", "stale-attempt");
            TournamentMatchReporter.BeginMatch();
            Assert.IsNull(GetPrivateStatic("_attemptId"));
            Assert.IsFalse(TournamentMatchReporter.HasTournamentSeed);
            Assert.IsFalse(PendingMatchStore.TryLoad(out _)); // 발행 없음 = pending 없음
        }

        // adopt 경로: 로비 발행분은 상태를 보존한 채 플래그만 소거한다.
        [Test]
        public void BeginMatch_LobbyIssued_AdoptsWithoutReset()
        {
            SetPrivateStatic("_lobbyIssued", true);
            SetPrivateStatic("_attemptId", "lobby-attempt");
            TournamentMatchReporter.BeginMatch();
            Assert.AreEqual("lobby-attempt", (string)GetPrivateStatic("_attemptId"));
            Assert.IsFalse((bool)GetPrivateStatic("_lobbyIssued"));
            SetPrivateStatic("_attemptId", null); // static — 다른 테스트로 새지 않게
        }
    }
}
