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
        // _completesInFlight 는 static 이라 테스트 간에 샌다 — 양쪽에서 되돌린다.
        [SetUp]
        public void SetUp() { PendingMatchStore.Clear(); UserSession.Clear(); SetPrivateStatic("_completesInFlight", 0); }

        [TearDown]
        public void TearDown() { PendingMatchStore.Clear(); UserSession.Clear(); SetPrivateStatic("_completesInFlight", 0); }

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

        // ── tournament-flow-guards unit 9 — in-flight complete 중엔 재마감 금지 ────

        // clear-on-success 로 바뀌면서 응답 전까지 레코드가 남는다. 나가기(abandon)
        // 직후 로비 진입처럼 두 경로가 겹칠 때, 남아 있는 레코드를 보고 같은 attempt 를
        // 두 번 마감하지 않아야 한다(레코드는 그대로 유지 — 실패 시 재시도 근거).
        [Test]
        public void ReconcilePending_CompleteInFlight_SkipsAndKeepsRecord()
        {
            PendingMatchStore.Save("a-1", "u-1", Now());
            SignInAs("u-1");
            SetPrivateStatic("_completesInFlight", 1);

            bool? released = null;
            TournamentMatchReporter.ReconcilePending(r => released = r);

            Assert.IsFalse(released.Value);
            Assert.IsTrue(PendingMatchStore.TryLoad(out var rec));
            Assert.AreEqual("a-1", rec.attemptId);
        }

        // 카운터인 이유(unit 9 리뷰): 서로 다른 attempt 의 complete 두 개가 겹칠 때
        // (지연된 reconcile(A) + 새 매치의 abandon(B)), 먼저 돌아온 응답이 가드를
        // 조기 해제하면 안 된다 — 하나라도 in-flight 면 reconcile 은 계속 skip.
        [Test]
        public void ReconcilePending_TwoInFlight_OneReturns_StillSkips()
        {
            PendingMatchStore.Save("b-2", "u-1", Now());
            SignInAs("u-1");
            SetPrivateStatic("_completesInFlight", 2);
            InvokePrivateStatic("CompleteReturned"); // 첫 응답 도착 — 아직 하나 남음

            bool? released = null;
            TournamentMatchReporter.ReconcilePending(r => released = r);

            Assert.IsFalse(released.Value);
            Assert.IsTrue(PendingMatchStore.TryLoad(out _)); // 재마감 발신 없음
        }

        // Play 진입 리셋 + 0 클램프 — 이전 세션의 고아 콜백이 뒤늦게 감소시켜도
        // 카운터가 음수로 내려가 가드가 망가지지 않는다.
        [Test]
        public void CompleteReturned_AtZero_ClampsNotNegative()
        {
            SetPrivateStatic("_completesInFlight", 0);
            InvokePrivateStatic("CompleteReturned"); // 고아 콜백
            Assert.AreEqual(0, (int)GetPrivateStatic("_completesInFlight"));
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

        private static void InvokePrivateStatic(string method)
            => typeof(TournamentMatchReporter)
                .GetMethod(method, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                .Invoke(null, null);

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
