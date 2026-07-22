using System;
using System.Collections.Generic;
using UnityEngine;

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

        // session-token-refresh Unit 0 — refresh material for reactive re-mint of
        // an expired firebase idToken (TryRefreshBearer). Set only by a firebase
        // login; guest/username sessions leave both null (they never token-expire).
        public static string RefreshToken { get; private set; }
        public static string FirebaseApiKey { get; private set; }

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
        // refreshToken/firebaseApiKey are set only by a firebase login so the
        // session can re-mint its own idToken (session-token-refresh unit 0/2).
        public static void Set(UserSignApi.SignedInUser user, string idToken,
            string gameServerBaseUrl = null, string authUserName = null,
            string refreshToken = null, string firebaseApiKey = null)
        {
            Current = user;
            IdToken = idToken;
            AuthUserName = authUserName;
            RefreshToken = refreshToken;
            FirebaseApiKey = firebaseApiKey;
            if (!string.IsNullOrEmpty(gameServerBaseUrl)) GameServerBaseUrl = gameServerBaseUrl;
        }

        public static void Clear()
        {
            Current = null;
            IdToken = null;
            AuthUserName = null;
            RefreshToken = null;
            FirebaseApiKey = null;
            GameServerBaseUrl = null;
        }

        // ── session-token-refresh Unit 0 — reactive firebase idToken re-mint ─────
        // A firebase idToken lives ~1h and is never refreshed mid-session, so the
        // server starts rejecting it (403 HANDLE_ACCESS_DENIED). The Send seam
        // (unit 1) calls this on a 401/403 to re-mint IN PLACE, then retries once.
        //
        // In-memory only: IdToken/RefreshToken are updated here but NOT persisted —
        // PlayerPrefs stays the login view's job, and firebase refresh tokens are
        // long-lived so the stored one still bootstraps the next launch. Concurrent
        // 401/403s (history + reporter) coalesce onto one in-flight refresh.
        private static bool _refreshing;
        private static readonly List<Action<bool>> _refreshWaiters = new();

        public static void TryRefreshBearer(Action<bool> done)
        {
            // Only a firebase session carries refresh material; guest/username
            // sessions can't token-expire, so there is nothing to re-mint.
            if (string.IsNullOrEmpty(RefreshToken) || string.IsNullOrEmpty(FirebaseApiKey))
            {
                done(false);
                return;
            }

            _refreshWaiters.Add(done);
            if (_refreshing) return; // a refresh is already in flight — coalesce onto it
            _refreshing = true;

            string apiKey = FirebaseApiKey;
            string refreshToken = RefreshToken;
            FirebaseAuthRestClient.RefreshIdToken(apiKey, refreshToken, (tokens, error) =>
            {
                // A Clear() (logout) during the round-trip must not resurrect a
                // dead session — treat a wiped source as failure and apply nothing.
                bool sessionGone = string.IsNullOrEmpty(RefreshToken) || string.IsNullOrEmpty(FirebaseApiKey);
                bool ok = tokens != null && !sessionGone;
                if (ok)
                {
                    IdToken = tokens.Value.idToken;
                    if (!string.IsNullOrEmpty(tokens.Value.refreshToken))
                        RefreshToken = tokens.Value.refreshToken; // firebase may rotate it
                }
                else if (!sessionGone)
                {
                    // Definitive firebase rejection → the stored refresh material is
                    // dead; drop it so later 401/403s don't hammer refresh. Transient
                    // network failures keep it (a later attempt may still succeed).
                    if (FirebaseAuthRestClient.IsDefinitiveAuthError(error))
                    {
                        RefreshToken = null;
                        FirebaseApiKey = null;
                    }
                    Debug.LogWarning($"[UserSession] bearer refresh failed: {error}");
                }

                _refreshing = false;
                var waiters = _refreshWaiters.ToArray();
                _refreshWaiters.Clear();
                for (int i = 0; i < waiters.Length; i++) waiters[i](ok);
            });
        }
    }
}
