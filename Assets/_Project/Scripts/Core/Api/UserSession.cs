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
        // tournament-play-report Unit 0 — game-server base URL carried out of the
        // login view so battle-scene API callers don't need a scene reference.
        public static string GameServerBaseUrl { get; private set; }

        public static bool IsSignedIn => Current != null;

        // baseUrl is optional so pre-existing callers (tests included) compile
        // unchanged; null/empty leaves the stored value as-is.
        public static void Set(UserSignApi.SignedInUser user, string idToken, string gameServerBaseUrl = null)
        {
            Current = user;
            IdToken = idToken;
            if (!string.IsNullOrEmpty(gameServerBaseUrl)) GameServerBaseUrl = gameServerBaseUrl;
        }

        public static void Clear()
        {
            Current = null;
            IdToken = null;
            GameServerBaseUrl = null;
        }
    }
}
