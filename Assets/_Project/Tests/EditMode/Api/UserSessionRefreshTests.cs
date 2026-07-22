using NUnit.Framework;
using Wassup.Core.Api;

namespace Wassup.Tests.EditMode.Api
{
    // session-token-refresh Unit 0 — refresh material plumbing + the no-network
    // guard of TryRefreshBearer. The actual firebase re-mint round-trip is covered
    // by unit 2 live verification (needs a real refresh token + server), so every
    // test here exercises only sessions WITHOUT refresh material, which resolve
    // synchronously without touching FirebaseAuthRestClient.
    public class UserSessionRefreshTests
    {
        [SetUp]
        public void SetUp() => UserSession.Clear();

        [TearDown]
        public void TearDown() => UserSession.Clear();

        // ── Set/Clear plumbing ───────────────────────────────────────────────────

        [Test]
        public void Set_Firebase_StoresRefreshMaterial_ClearWipesIt()
        {
            UserSession.Set(new UserSignApi.SignedInUser { userId = "u-1" }, "id-token",
                gameServerBaseUrl: "https://x", refreshToken: "rt-1", firebaseApiKey: "key-1");

            Assert.AreEqual("rt-1", UserSession.RefreshToken);
            Assert.AreEqual("key-1", UserSession.FirebaseApiKey);
            Assert.AreEqual("id-token", UserSession.Credential.idToken, "firebase session authenticates as Bearer");

            UserSession.Clear();
            Assert.IsNull(UserSession.RefreshToken);
            Assert.IsNull(UserSession.FirebaseApiKey);
        }

        [Test]
        public void Set_Guest_LeavesRefreshMaterialNull()
        {
            // LoginPanelView SKIP: no refresh material passed.
            UserSession.Set(new UserSignApi.SignedInUser { userId = "", provider = "guest" }, idToken: "");

            Assert.IsNull(UserSession.RefreshToken);
            Assert.IsNull(UserSession.FirebaseApiKey);
        }

        [Test]
        public void Set_Username_LeavesRefreshMaterialNull()
        {
            UserSession.Set(new UserSignApi.SignedInUser { userName = "wassup" },
                idToken: "", gameServerBaseUrl: null, authUserName: "wassup");

            Assert.IsNull(UserSession.RefreshToken);
            Assert.IsNull(UserSession.FirebaseApiKey);
            Assert.AreEqual("wassup", UserSession.Credential.userName, "username session authenticates via X-AUTH-USERNAME");
        }

        // ── TryRefreshBearer guard (no network) ──────────────────────────────────
        // A session with no refresh material must resolve done(false) SYNCHRONOUSLY
        // — i.e. before TryRefreshBearer returns — so FirebaseAuthRestClient (and
        // thus the network) is never touched.

        [Test]
        public void TryRefreshBearer_Guest_SynchronousFalse()
        {
            UserSession.Set(new UserSignApi.SignedInUser { userId = "" }, idToken: "");
            AssertRefreshResolvesSyncTo(false);
        }

        [Test]
        public void TryRefreshBearer_Username_SynchronousFalse()
        {
            UserSession.Set(new UserSignApi.SignedInUser { userName = "wassup" },
                idToken: "", gameServerBaseUrl: null, authUserName: "wassup");
            AssertRefreshResolvesSyncTo(false);
        }

        [Test]
        public void TryRefreshBearer_FirebaseWithoutRefreshMaterial_SynchronousFalse()
        {
            // firebase idToken but no refresh material (legacy Set overload) — cannot
            // re-mint, so it must bail synchronously rather than hit the network.
            UserSession.Set(new UserSignApi.SignedInUser { userId = "u-1" }, "id-token");
            AssertRefreshResolvesSyncTo(false);
        }

        // Calls TryRefreshBearer and asserts the callback fired synchronously with
        // the expected value (proving no async/network path was entered).
        private static void AssertRefreshResolvesSyncTo(bool expected)
        {
            bool fired = false;
            bool result = !expected;
            UserSession.TryRefreshBearer(ok => { fired = true; result = ok; });
            Assert.IsTrue(fired, "callback must fire synchronously (no network path)");
            Assert.AreEqual(expected, result);
        }
    }
}
