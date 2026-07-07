namespace Wassup.Core.Api
{
    // outgame-login-gate Unit 0 — in-memory auth state. Not a MonoBehaviour
    // manager (GameManager stays the only one); plain data holder, gone on app
    // restart. Persistence (refresh token / user name) lives in PlayerPrefs on
    // the login view side.
    public static class UserSession
    {
        public static UserSignApi.SignedInUser Current { get; private set; }
        public static string IdToken { get; private set; }

        public static bool IsSignedIn => Current != null;

        public static void Set(UserSignApi.SignedInUser user, string idToken)
        {
            Current = user;
            IdToken = idToken;
        }

        public static void Clear()
        {
            Current = null;
            IdToken = null;
        }
    }
}
