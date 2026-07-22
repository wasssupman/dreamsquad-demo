using NUnit.Framework;
using UnityEngine.Networking;
using Wassup.Core.Api;

namespace Wassup.Tests.EditMode.Api
{
    // outgame-login-gate Unit 0 — pure parse/session coverage (no live network).
    public class UserAuthApiTests
    {
        // ── ApiEnvelope ──────────────────────────────────────────────────────────

        [Test]
        public void ApiEnvelope_Parse_SuccessObject_Binds()
        {
            const string body = @"{ ""success"": true, ""data"": { ""userId"": ""u-1"", ""userName"": ""sj"", ""provider"": ""GUEST"" } }";

            var user = ApiEnvelope.Parse<UserSignApi.SignedInUser>(body, out string error);

            Assert.IsNull(error);
            Assert.AreEqual("u-1", user.userId);
            Assert.AreEqual("sj", user.userName);
            Assert.AreEqual("GUEST", user.provider);
        }

        [Test]
        public void ApiEnvelope_Parse_SuccessFalse_ReportsErrorDetail()
        {
            const string body = @"{ ""success"": false, ""errorDetail"": {
                ""errorCode"": ""AUTHENTICATION_FAIL"", ""errorMessage"": ""인증 실패"" } }";

            var user = ApiEnvelope.Parse<UserSignApi.SignedInUser>(body, out string error);

            Assert.IsNull(user);
            StringAssert.Contains("AUTHENTICATION_FAIL", error);
        }

        [Test]
        public void ApiEnvelope_Parse_MalformedOrEmpty_ReturnsError()
        {
            Assert.IsNull(ApiEnvelope.Parse<UserSignApi.SignedInUser>("<html>", out string e1));
            StringAssert.Contains("JSON parse failed", e1);
            Assert.IsNull(ApiEnvelope.Parse<UserSignApi.SignedInUser>(null, out string e2));
            StringAssert.Contains("empty response body", e2);
        }

        // ── FirebaseAuthRestClient parsers ───────────────────────────────────────

        [Test]
        public void TryParseSignUp_CamelCase_Succeeds()
        {
            const string body = @"{ ""idToken"": ""id-1"", ""refreshToken"": ""rt-1"", ""localId"": ""uid-1"", ""expiresIn"": ""3600"" }";

            Assert.IsTrue(FirebaseAuthRestClient.TryParseSignUp(body, out var tokens, out string error));
            Assert.IsNull(error);
            Assert.AreEqual("id-1", tokens.idToken);
            Assert.AreEqual("rt-1", tokens.refreshToken);
            Assert.AreEqual("uid-1", tokens.localId);
        }

        [Test]
        public void TryParseRefresh_SnakeCase_Succeeds()
        {
            const string body = @"{ ""id_token"": ""id-2"", ""refresh_token"": ""rt-2"", ""user_id"": ""uid-2"", ""expires_in"": ""3600"" }";

            Assert.IsTrue(FirebaseAuthRestClient.TryParseRefresh(body, out var tokens, out string error));
            Assert.IsNull(error);
            Assert.AreEqual("id-2", tokens.idToken);
            Assert.AreEqual("rt-2", tokens.refreshToken);
            Assert.AreEqual("uid-2", tokens.localId);
        }

        [Test]
        public void TryParseRefresh_FirebaseError_IsDefinitive()
        {
            const string body = @"{ ""error"": { ""code"": 400, ""message"": ""TOKEN_EXPIRED"" } }";

            Assert.IsFalse(FirebaseAuthRestClient.TryParseRefresh(body, out _, out string error));
            StringAssert.Contains("TOKEN_EXPIRED", error);
            Assert.IsTrue(FirebaseAuthRestClient.IsDefinitiveAuthError(error),
                "a Firebase-rejected token must be classified definitive (stored token may be discarded)");
        }

        [Test]
        public void IsDefinitiveAuthError_NetworkOrParseErrors_AreTransient()
        {
            Assert.IsFalse(FirebaseAuthRestClient.IsDefinitiveAuthError("network: Cannot connect to destination host"));
            Assert.IsFalse(FirebaseAuthRestClient.IsDefinitiveAuthError("JSON parse failed: x"));
            Assert.IsFalse(FirebaseAuthRestClient.IsDefinitiveAuthError(null));
        }

        // ── UserSignApi ──────────────────────────────────────────────────────────

        [Test]
        public void BuildMetadataBody_ContainsUserNameAndVersion()
        {
            string json = UserSignApi.BuildMetadataBody("tester");

            StringAssert.Contains("\"userName\":\"tester\"", json);
            StringAssert.Contains("\"appVersion\":", json);
            StringAssert.Contains("\"osType\":", json);
        }

        // ── UserSession ──────────────────────────────────────────────────────────

        [Test]
        public void UserSession_SetAndClear()
        {
            UserSession.Clear();
            Assert.IsFalse(UserSession.IsSignedIn);

            UserSession.Set(new UserSignApi.SignedInUser { userId = "u-1", userName = "sj" }, "id-token");
            Assert.IsTrue(UserSession.IsSignedIn);
            Assert.AreEqual("u-1", UserSession.Current.userId);
            Assert.AreEqual("id-token", UserSession.IdToken);

            UserSession.Clear();
            Assert.IsFalse(UserSession.IsSignedIn);
            Assert.IsNull(UserSession.IdToken);
        }

        // ── demo-username-recovery Unit 0: auth mode + credential ────────────────

        [Test]
        public void UserSession_FirebaseMode_HasAccountBearerCredential()
        {
            UserSession.Set(new UserSignApi.SignedInUser { userId = "u-1" }, "id-token");

            Assert.IsTrue(UserSession.HasAccount);
            Assert.IsNull(UserSession.AuthUserName);
            var cred = UserSession.Credential;
            Assert.IsTrue(cred.IsValid);
            Assert.AreEqual("id-token", cred.idToken);
            Assert.IsNull(cred.userName);
            UserSession.Clear();
        }

        [Test]
        public void UserSession_UsernameMode_HasAccountUsernameCredential()
        {
            UserSession.Set(new UserSignApi.SignedInUser { userId = "u-2", userName = "wassup" },
                idToken: "", gameServerBaseUrl: null, authUserName: "wassup");

            Assert.IsTrue(UserSession.HasAccount);
            Assert.AreEqual("wassup", UserSession.AuthUserName);
            var cred = UserSession.Credential;
            Assert.IsTrue(cred.IsValid);
            Assert.AreEqual("wassup", cred.userName);
            Assert.IsNull(cred.idToken);
            UserSession.Clear();
        }

        [Test]
        public void UserSession_Guest_SignedInButNoAccount()
        {
            // LoginPanelView SKIP: Current set, empty token, no authUserName.
            UserSession.Set(new UserSignApi.SignedInUser { userId = "", provider = "guest" }, idToken: "");

            Assert.IsTrue(UserSession.IsSignedIn, "guest is signed in for gate purposes");
            Assert.IsFalse(UserSession.HasAccount, "but guest is not a real account");
            Assert.IsFalse(UserSession.Credential.IsValid);
            UserSession.Clear();
        }

        [Test]
        public void UserSession_Clear_ResetsAuthUserName()
        {
            UserSession.Set(new UserSignApi.SignedInUser { userName = "wassup" },
                idToken: "", gameServerBaseUrl: null, authUserName: "wassup");
            UserSession.Clear();

            Assert.IsFalse(UserSession.HasAccount);
            Assert.IsFalse(UserSession.IsSignedIn);
            Assert.IsNull(UserSession.AuthUserName);
        }

        [Test]
        public void AuthCredential_Apply_UsernameMode_SetsHeader()
        {
            using var req = UnityWebRequest.Get("http://localhost/user");
            AuthCredential.Username("wassup").Apply(req);

            Assert.AreEqual("wassup", req.GetRequestHeader("X-AUTH-USERNAME"));
            Assert.IsNull(req.GetRequestHeader("Authorization"));
        }

        [Test]
        public void AuthCredential_Apply_BearerMode_SetsHeader()
        {
            using var req = UnityWebRequest.Get("http://localhost/user");
            AuthCredential.Bearer("id-token").Apply(req);

            Assert.AreEqual("Bearer id-token", req.GetRequestHeader("Authorization"));
            Assert.IsNull(req.GetRequestHeader("X-AUTH-USERNAME"));
        }

        // ── demo-username-recovery Unit 1: GET /user classification ──────────────

        [Test]
        public void UserLookup_Classify_SuccessEnvelope_IsFound()
        {
            const string body = @"{ ""success"": true, ""data"": {
                ""userId"": ""u-9"", ""userName"": ""wassup"", ""provider"": ""GUEST"" } }";

            var result = UserLookupApi.Classify(body, null);

            Assert.AreEqual(UserLookupApi.Outcome.Found, result.outcome);
            Assert.AreEqual("u-9", result.user.userId);
            Assert.AreEqual("wassup", result.user.userName);
        }

        [Test]
        public void UserLookup_Classify_AccessDeniedBody_IsNotFound()
        {
            // absent name → 403 HANDLE_ACCESS_DENIED (failure WITH a body).
            const string body = @"{ ""success"": false, ""errorDetail"": {
                ""errorCode"": ""HANDLE_ACCESS_DENIED"", ""errorMessage"": ""권한이 없습니다."" } }";

            var result = UserLookupApi.Classify(body, "HTTP/1.1 403 Forbidden");

            Assert.AreEqual(UserLookupApi.Outcome.NotFound, result.outcome);
            Assert.IsNull(result.user);
        }

        [Test]
        public void UserLookup_Classify_EmptyBodyWithTransportError_IsNetwork()
        {
            var result = UserLookupApi.Classify("", "Cannot connect to destination host");

            Assert.AreEqual(UserLookupApi.Outcome.NetworkError, result.outcome);
            StringAssert.Contains("Cannot connect", result.error);
        }

        [Test]
        public void UserLookup_Classify_NullBodyWithTransportError_IsNetwork()
        {
            var result = UserLookupApi.Classify(null, "Request timeout");

            Assert.AreEqual(UserLookupApi.Outcome.NetworkError, result.outcome);
        }
    }
}
