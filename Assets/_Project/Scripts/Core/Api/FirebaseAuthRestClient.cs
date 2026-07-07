using System;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace Wassup.Core.Api
{
    // outgame-login-gate Unit 0 — Firebase Auth over plain REST (no SDK).
    // Anonymous sign-up mints a per-device account; the refresh token keeps it
    // across app restarts. The web apiKey is a public client identifier by
    // Firebase's design — safe to ship.
    public static class FirebaseAuthRestClient
    {
        private const string SignUpUrl = "https://identitytoolkit.googleapis.com/v1/accounts:signUp?key=";
        private const string RefreshUrl = "https://securetoken.googleapis.com/v1/token?key=";
        private const int TimeoutSeconds = 10;

        public readonly struct AuthTokens
        {
            public readonly string idToken;
            public readonly string refreshToken;
            public readonly string localId;

            public AuthTokens(string idToken, string refreshToken, string localId)
            {
                this.idToken = idToken;
                this.refreshToken = refreshToken;
                this.localId = localId;
            }
        }

        // onDone(tokens, error) — exactly one of the two is meaningful.
        // Error contract (identity policy): "firebase: ..." = definitive rejection
        // (safe to discard the stored token), "network: ..." = transient (keep it).
        public static void SignUpAnonymous(string apiKey, Action<AuthTokens?, string> onDone)
        {
            var request = new UnityWebRequest(SignUpUrl + apiKey, UnityWebRequest.kHttpVerbPOST);
            request.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes("{}"));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            Send(request, (body, transportError) =>
            {
                bool ok = TryParseSignUp(body, out var tokens, out string error);
                if (!ok && string.IsNullOrWhiteSpace(body) && transportError != null) error = $"network: {transportError}";
                onDone(ok ? tokens : (AuthTokens?)null, error);
            });
        }

        public static void RefreshIdToken(string apiKey, string refreshToken, Action<AuthTokens?, string> onDone)
        {
            var form = new WWWForm();
            form.AddField("grant_type", "refresh_token");
            form.AddField("refresh_token", refreshToken);
            var request = UnityWebRequest.Post(RefreshUrl + apiKey, form);
            Send(request, (body, transportError) =>
            {
                bool ok = TryParseRefresh(body, out var tokens, out string error);
                if (!ok && string.IsNullOrWhiteSpace(body) && transportError != null) error = $"network: {transportError}";
                onDone(ok ? tokens : (AuthTokens?)null, error);
            });
        }

        // Definitive Firebase rejection (vs transient network failure) — the login
        // flow uses this to decide whether a stored refresh token is dead.
        public static bool IsDefinitiveAuthError(string error)
            => error != null && error.StartsWith("firebase: ");

        // sign-up response is camelCase.
        internal static bool TryParseSignUp(string body, out AuthTokens tokens, out string error)
            => TryParseTokens(body, "idToken", "refreshToken", "localId", out tokens, out error);

        // token-refresh response is snake_case (OAuth2 style) — different casing
        // from sign-up, hence two parsers.
        internal static bool TryParseRefresh(string body, out AuthTokens tokens, out string error)
            => TryParseTokens(body, "id_token", "refresh_token", "user_id", out tokens, out error);

        private static bool TryParseTokens(string body, string idKey, string refreshKey, string uidKey,
            out AuthTokens tokens, out string error)
        {
            tokens = default;
            error = null;
            if (string.IsNullOrWhiteSpace(body))
            {
                error = "empty response body";
                return false;
            }

            JObject root;
            try
            {
                root = JObject.Parse(body);
            }
            catch (Exception e)
            {
                error = $"JSON parse failed: {e.Message}";
                return false;
            }

            string firebaseError = (root["error"] as JObject)?.Value<string>("message");
            if (firebaseError != null)
            {
                error = $"firebase: {firebaseError}";
                return false;
            }

            string idToken = root.Value<string>(idKey);
            string refreshToken = root.Value<string>(refreshKey);
            if (string.IsNullOrEmpty(idToken) || string.IsNullOrEmpty(refreshToken))
            {
                error = $"response missing {idKey}/{refreshKey}";
                return false;
            }
            tokens = new AuthTokens(idToken, refreshToken, root.Value<string>(uidKey));
            return true;
        }

        private static void Send(UnityWebRequest request, Action<string, string> onDone)
        {
            request.timeout = TimeoutSeconds;
            var operation = request.SendWebRequest();
            operation.completed += _ =>
            {
                // keep the body even on HTTP failure — Firebase puts the error
                // JSON there (same rule as the game-server APIs).
                string body = request.downloadHandler != null ? request.downloadHandler.text : null;
                string transportError = request.result == UnityWebRequest.Result.Success ? null : request.error;
                request.Dispose();
                onDone(body, transportError);
            };
        }
    }
}
