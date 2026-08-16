using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Wassup.Core.Api;

namespace Wassup.Tests.PlayMode
{
    // outgame-login-gate Unit 3 — live end-to-end auth chain against the real
    // Firebase + game-server endpoints. Network-dependent: a dev-server outage
    // fails this test legitimately. Side effect: one throwaway anonymous account
    // per run (internal dev backend — acceptable).
    public class AuthE2ETest
    {
        private const string ApiKey = "AIzaSyBFy7R0JJqLwkEJR7DwKz2da-QgrwU4CdM";
        private const string BaseUrl = "https://dev-api-somnia.cashroyale.games";
        private const float StepTimeoutSec = 20f;

        // fast-lane unit 2 — dev 서버 상태(계정 중복·응답 스키마 변동)에 좌우되는
        // 환경 의존 테스트라 기본 실행에서 제외한다. Test Runner 에서 직접 선택하면 돈다.
        [Explicit("live dev-server dependent — run by explicit selection only")]
        [UnityTest]
        public IEnumerator AuthChain_SignUp_SignIn_Refresh_KeepsIdentity()
        {
            // 1) anonymous sign-up
            FirebaseAuthRestClient.AuthTokens? tokens = null;
            string error = null;
            bool done = false;
            FirebaseAuthRestClient.SignUpAnonymous(ApiKey, (t, e) => { tokens = t; error = e; done = true; });
            yield return WaitFor(() => done);
            Assert.IsTrue(done, "sign-up timed out");
            Assert.IsNotNull(tokens, $"sign-up failed: {error}");

            // 2) game-server sign-in
            UserSignApi.SignedInUser user = null;
            done = false;
            UserSignApi.SignIn(BaseUrl, tokens.Value.idToken, "e2e-test", (u, e) => { user = u; error = e; done = true; });
            yield return WaitFor(() => done);
            Assert.IsTrue(done, "sign-in timed out");
            Assert.IsNotNull(user, $"sign-in failed: {error}");
            Assert.IsNotEmpty(user.userId, "server must return a userId");
            Assert.AreEqual("e2e-test", user.userName);

            // 3) token refresh keeps the same Firebase account
            FirebaseAuthRestClient.AuthTokens? refreshed = null;
            done = false;
            FirebaseAuthRestClient.RefreshIdToken(ApiKey, tokens.Value.refreshToken, (t, e) => { refreshed = t; error = e; done = true; });
            yield return WaitFor(() => done);
            Assert.IsTrue(done, "refresh timed out");
            Assert.IsNotNull(refreshed, $"refresh failed: {error}");
            Assert.AreEqual(tokens.Value.localId, refreshed.Value.localId, "refresh must keep the same Firebase account");

            // 4) refreshed token maps to the SAME server user — the identity
            //    stability the login flow's refresh-first policy relies on.
            UserSignApi.SignedInUser secondUser = null;
            done = false;
            UserSignApi.SignIn(BaseUrl, refreshed.Value.idToken, "e2e-test", (u, e) => { secondUser = u; error = e; done = true; });
            yield return WaitFor(() => done);
            Assert.IsNotNull(secondUser, $"second sign-in failed: {error}");
            Assert.AreEqual(user.userId, secondUser.userId, "same firebase account must map to the same server user");

            // 5) session set/clear semantics (account reset path)
            UserSession.Set(secondUser, refreshed.Value.idToken);
            Assert.IsTrue(UserSession.IsSignedIn);
            UserSession.Clear();
            Assert.IsFalse(UserSession.IsSignedIn);
            Assert.IsNull(UserSession.IdToken);
        }

        private static IEnumerator WaitFor(System.Func<bool> condition)
        {
            float deadline = Time.realtimeSinceStartup + StepTimeoutSec;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
        }
    }
}
