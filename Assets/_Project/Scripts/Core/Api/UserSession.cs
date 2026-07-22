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

        // demo-username-recovery Unit 0 — the demo fallback identity. Non-empty
        // when this session was recovered by name (X-AUTH-USERNAME) instead of a
        // firebase token. Mutually exclusive with a non-empty IdToken.
        public static string AuthUserName { get; private set; }

        public static bool IsSignedIn => Current != null;

        // "real (non-guest) server account" predicate — a firebase session (has
        // IdToken) or a demo username-recovered session (has AuthUserName). The
        // guest SKIP path sets neither, so it stays false. Distinct from
        // IsSignedIn, which is true for guests too.
        public static bool HasAccount =>
            !string.IsNullOrEmpty(IdToken) || !string.IsNullOrEmpty(AuthUserName);

        // How to authenticate a request for the current session. Username mode
        // takes precedence; the single Send seam (unit 3) applies it.
        public static AuthCredential Credential =>
            !string.IsNullOrEmpty(AuthUserName) ? AuthCredential.Username(AuthUserName)
            : !string.IsNullOrEmpty(IdToken) ? AuthCredential.Bearer(IdToken)
            : AuthCredential.None;

        // baseUrl is optional so pre-existing callers (tests included) compile
        // unchanged; null/empty leaves the stored value as-is. authUserName is
        // set only by the demo recovery path (unit 2); firebase/guest leave it null.
        public static void Set(UserSignApi.SignedInUser user, string idToken,
            string gameServerBaseUrl = null, string authUserName = null)
        {
            Current = user;
            IdToken = idToken;
            AuthUserName = authUserName;
            if (!string.IsNullOrEmpty(gameServerBaseUrl)) GameServerBaseUrl = gameServerBaseUrl;
        }

        public static void Clear()
        {
            Current = null;
            IdToken = null;
            AuthUserName = null;
            GameServerBaseUrl = null;
        }
    }
}
