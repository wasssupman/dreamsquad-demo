using UnityEngine.Networking;

namespace Wassup.Core.Api
{
    // demo-username-recovery Unit 0 — how a signed-in session authenticates one
    // request. Two mutually exclusive modes: firebase (Bearer idToken) and the
    // demo username fallback (X-AUTH-USERNAME). Built from UserSession and
    // applied at the single Send seam (unit 3) so callers never branch on mode.
    public readonly struct AuthCredential
    {
        public readonly string idToken;   // firebase mode
        public readonly string userName;  // demo username mode (X-AUTH-USERNAME)

        private AuthCredential(string idToken, string userName)
        {
            this.idToken = idToken;
            this.userName = userName;
        }

        public static AuthCredential Bearer(string idToken) => new AuthCredential(idToken, null);
        public static AuthCredential Username(string userName) => new AuthCredential(null, userName);
        public static AuthCredential None => default;

        public bool IsValid => !string.IsNullOrEmpty(idToken) || !string.IsNullOrEmpty(userName);

        // Attaches exactly one auth header. username wins when both are set —
        // matches the server (X-AUTH-USERNAME overrides Bearer), though a valid
        // credential only ever carries one.
        public void Apply(UnityWebRequest request)
        {
            if (!string.IsNullOrEmpty(userName))
                request.SetRequestHeader("X-AUTH-USERNAME", userName);
            else if (!string.IsNullOrEmpty(idToken))
                request.SetRequestHeader("Authorization", $"Bearer {idToken}");
        }
    }
}
