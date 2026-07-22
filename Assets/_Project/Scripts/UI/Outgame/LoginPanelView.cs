using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wassup.Core.Api;

namespace Wassup.UI
{
    // outgame-login-gate Unit 1 — auth logic + status display for the lobby login
    // panel. Panel GameObject visibility is owned by OutgameMenuController; this
    // view only runs the sign-in flow and reports via onSignedIn.
    //
    // Identity policy (spec README): the stable key is the anonymous account —
    // a stored refresh token is always tried first; only a definitive Firebase
    // rejection discards it and mints a new account. Transient (network) failures
    // never create a new identity.
    public class LoginPanelView : MonoBehaviour
    {
        private const string RefreshTokenPrefsKey = "Wassup.Auth.RefreshToken";
        private const string UserNamePrefsKey = "Wassup.Auth.UserName";
        // demo-username-recovery Unit 2 — marks a session recovered by name
        // (X-AUTH-USERNAME) rather than a firebase token. There is no refresh
        // token to auto-sign-in with, so the next launch re-adopts by name.
        // Mutually exclusive with a stored RefreshToken.
        private const string UsernameModePrefsKey = "Wassup.Auth.UsernameMode";
        private const float SignedInLingerSec = 0.8f;

        [SerializeField] private TMP_InputField nameInput;
        [SerializeField] private Button loginButton;
        // unit 4 — guest escape hatch. Stays interactable while busy so a hung
        // request never locks the game.
        [SerializeField] private Button skipButton;
        [SerializeField] private TMP_Text statusLabel;
        // Firebase web apiKey is a public client identifier by design.
        [SerializeField] private string firebaseApiKey = "AIzaSyBFy7R0JJqLwkEJR7DwKz2da-QgrwU4CdM";
        [SerializeField] private string gameApiBaseUrl = "https://dev-api-somnia.cashroyale.games";

        public event Action onSignedIn;

        private bool _busy;
        // unit 4 — bumped by ResetAccount. Async sign-in chains capture the epoch
        // at start and abort on mismatch, so a late success can't re-persist an
        // account the user just reset.
        private int _authEpoch;

        private void Awake()
        {
            loginButton.onClick.AddListener(OnLoginClicked);
            if (skipButton != null) skipButton.onClick.AddListener(OnSkipClicked);
            if (nameInput != null) nameInput.text = PlayerPrefs.GetString(UserNamePrefsKey, "");
            SetStatus("");
        }

        private void Start()
        {
            if (UserSession.IsSignedIn) return; // scene revisit within the session

            string storedToken = PlayerPrefs.GetString(RefreshTokenPrefsKey, "");
            string storedName = PlayerPrefs.GetString(UserNamePrefsKey, "");

            // firebase silent sign-in (same device): a stored refresh token +
            // name. A stored name implies a completed first login.
            if (!string.IsNullOrEmpty(storedToken) && !string.IsNullOrEmpty(storedName))
            {
                SetBusy(true, "SIGNING IN...");
                int epoch = _authEpoch;
                FirebaseAuthRestClient.RefreshIdToken(firebaseApiKey, storedToken, (tokens, error) =>
                {
                    if (this == null || epoch != _authEpoch) return;
                    if (tokens == null) { HandleFailure(error, clearTokenIfDefinitive: true); return; }
                    SignInToGameServer(tokens.Value, storedName, epoch);
                });
                return;
            }

            // demo-username-recovery Unit 2 — silent re-adopt: a prior name
            // recovery left a marker but no firebase token. Look the name up
            // again (X-AUTH-USERNAME) and re-adopt. Not found / network → drop
            // to the login panel silently; the marker stays so a later manual
            // login still attempts recovery. Never mints on this silent path.
            if (PlayerPrefs.GetInt(UsernameModePrefsKey, 0) == 1 && !string.IsNullOrEmpty(storedName))
            {
                SetBusy(true, "SIGNING IN...");
                int epoch = _authEpoch;
                UserLookupApi.GetUser(gameApiBaseUrl, storedName, result =>
                {
                    if (this == null || epoch != _authEpoch) return;
                    if (result.outcome == UserLookupApi.Outcome.Found)
                        AdoptExistingUser(result.user, storedName, epoch);
                    else
                        SetBusy(false, "");
                });
            }
        }

        private void OnDestroy()
        {
            if (loginButton != null) loginButton.onClick.RemoveListener(OnLoginClicked);
            if (skipButton != null) skipButton.onClick.RemoveListener(OnSkipClicked);
        }

        // unit 4 — enter without auth. Session-only guest identity: nothing is
        // persisted, so the next app launch shows the login panel again. If an
        // in-flight sign-in later succeeds, the real session overwrites the
        // guest one (a promotion, not a conflict).
        private void OnSkipClicked()
        {
            // an established real session (e.g. skip clicked during the
            // post-success linger) must never be demoted to guest — just close
            // the gate immediately.
            if (UserSession.IsSignedIn) { onSignedIn?.Invoke(); return; }

            string userName = nameInput != null ? nameInput.text.Trim() : "";
            if (userName.Length == 0) userName = "GUEST";
            UserSession.Set(new UserSignApi.SignedInUser
            {
                userId = "",
                userName = userName,
                provider = "guest",
            }, idToken: "", gameApiBaseUrl);
            Debug.Log($"[LoginPanel] entered without login as guest '{userName}'.");
            onSignedIn?.Invoke();
        }

        private void OnLoginClicked()
        {
            if (_busy) return;
            string userName = nameInput != null ? nameInput.text.Trim() : "";
            if (userName.Length == 0)
            {
                SetStatus("ENTER YOUR NAME");
                return;
            }

            SetBusy(true, "SIGNING IN...");
            int epoch = _authEpoch;
            string storedToken = PlayerPrefs.GetString(RefreshTokenPrefsKey, "");
            if (!string.IsNullOrEmpty(storedToken))
            {
                FirebaseAuthRestClient.RefreshIdToken(firebaseApiKey, storedToken, (tokens, error) =>
                {
                    if (this == null || epoch != _authEpoch) return;
                    if (tokens != null) { SignInToGameServer(tokens.Value, userName, epoch); return; }
                    if (FirebaseAuthRestClient.IsDefinitiveAuthError(error))
                    {
                        PlayerPrefs.DeleteKey(RefreshTokenPrefsKey);
                        MintOrAdopt(userName, epoch);
                        return;
                    }
                    HandleFailure(error, clearTokenIfDefinitive: false);
                });
            }
            else
            {
                MintOrAdopt(userName, epoch);
            }
        }

        // demo-username-recovery Unit 2 — before minting a NEW firebase account,
        // ask the server whether this name already exists. If so, adopt it
        // (X-AUTH-USERNAME mode) instead of creating a duplicate. Only a
        // definitive not-found mints; a network failure stops without minting so
        // a blip can't spawn a second account (contract #3).
        private void MintOrAdopt(string userName, int epoch)
        {
            UserLookupApi.GetUser(gameApiBaseUrl, userName, result =>
            {
                if (this == null || epoch != _authEpoch) return;
                switch (result.outcome)
                {
                    case UserLookupApi.Outcome.Found:
                        AdoptExistingUser(result.user, userName, epoch);
                        break;
                    case UserLookupApi.Outcome.NotFound:
                        SignUpFresh(userName, epoch);
                        break;
                    default: // NetworkError — do not mint
                        HandleFailure($"network: {result.error}", clearTokenIfDefinitive: false);
                        break;
                }
            });
        }

        // demo-username-recovery Unit 2 — adopt an existing server account by
        // name. No firebase token: the session authenticates via X-AUTH-USERNAME
        // (UserSession.AuthUserName), and the marker lets the next launch
        // re-adopt silently. The header value is the exact input that just
        // produced a 200 (proven-good); Current holds the server user object.
        private void AdoptExistingUser(UserSignApi.SignedInUser user, string userName, int epoch)
        {
            if (this == null || epoch != _authEpoch) return;
            PlayerPrefs.SetString(UserNamePrefsKey, userName);
            PlayerPrefs.SetInt(UsernameModePrefsKey, 1);
            PlayerPrefs.DeleteKey(RefreshTokenPrefsKey); // no stale firebase token in username mode
            PlayerPrefs.Save();
            UserSession.Set(user, idToken: "", gameApiBaseUrl, authUserName: userName);
            SetBusy(false, $"SIGNED IN AS {user.userName}".ToUpperInvariant());
            if (isActiveAndEnabled) StartCoroutine(NotifySignedInAfterLinger());
        }

        private void SignUpFresh(string userName, int epoch)
        {
            FirebaseAuthRestClient.SignUpAnonymous(firebaseApiKey, (tokens, error) =>
            {
                if (this == null || epoch != _authEpoch) return;
                if (tokens == null) { HandleFailure(error, clearTokenIfDefinitive: false); return; }
                SignInToGameServer(tokens.Value, userName, epoch);
            });
        }

        private void SignInToGameServer(FirebaseAuthRestClient.AuthTokens tokens, string userName, int epoch)
        {
            UserSignApi.SignIn(gameApiBaseUrl, tokens.idToken, userName, (user, error) =>
            {
                if (this == null || epoch != _authEpoch) return;
                if (user == null) { HandleFailure(error, clearTokenIfDefinitive: false); return; }

                PlayerPrefs.SetString(RefreshTokenPrefsKey, tokens.refreshToken);
                PlayerPrefs.SetString(UserNamePrefsKey, userName);
                PlayerPrefs.DeleteKey(UsernameModePrefsKey); // firebase mode, not username mode
                // session-token-refresh unit 2 — carry the refresh material so the
                // session can re-mint its own idToken on a mid-session 403/401.
                UserSession.Set(user, tokens.idToken, gameApiBaseUrl,
                    refreshToken: tokens.refreshToken, firebaseApiKey: firebaseApiKey);
                SetBusy(false, $"SIGNED IN AS {user.userName}".ToUpperInvariant());
                // panel may have been deactivated by a skip (unit 4) while this
                // request was in flight — StartCoroutine would throw there, and
                // no notify is needed: the gate is already open and the
                // UserSession.Set above promoted the guest to the real account.
                if (isActiveAndEnabled) StartCoroutine(NotifySignedInAfterLinger());
            });
        }

        private IEnumerator NotifySignedInAfterLinger()
        {
            yield return new WaitForSeconds(SignedInLingerSec);
            onSignedIn?.Invoke();
        }

        // outgame-login-gate unit 3 — dev "RESET ACCOUNT": forget the stored
        // account entirely. The next login mints a new anonymous identity.
        public void ResetAccount()
        {
            _authEpoch++; // abort in-flight sign-in chains (unit 4)
            PlayerPrefs.DeleteKey(RefreshTokenPrefsKey);
            PlayerPrefs.DeleteKey(UserNamePrefsKey);
            PlayerPrefs.DeleteKey(UsernameModePrefsKey);
            PlayerPrefs.Save();
            UserSession.Clear();
            if (nameInput != null) nameInput.text = "";
            SetBusy(false, "");
            Debug.Log("[LoginPanel] account reset.");
        }

        private void HandleFailure(string error, bool clearTokenIfDefinitive)
        {
            if (clearTokenIfDefinitive && FirebaseAuthRestClient.IsDefinitiveAuthError(error))
                PlayerPrefs.DeleteKey(RefreshTokenPrefsKey);
            SetBusy(false, DisplayableError(error));
            Debug.Log($"[LoginPanel] sign-in failed: {error}");
        }

        // status label has no Korean glyphs — show only the leading errorCode part
        // of server errors ("CODE — 한글 메시지" → "CODE"). Firebase/network errors
        // are already English.
        internal static string DisplayableError(string error)
        {
            if (string.IsNullOrEmpty(error)) return "SIGN-IN FAILED";
            int dash = error.IndexOf('—');
            string display = dash > 0 ? error.Substring(0, dash).Trim() : error;
            return display.Length == 0 ? "SIGN-IN FAILED" : display;
        }

        private void SetBusy(bool busy, string status)
        {
            _busy = busy;
            if (loginButton != null) loginButton.interactable = !busy;
            if (nameInput != null) nameInput.interactable = !busy;
            SetStatus(status);
        }

        private void SetStatus(string text)
        {
            if (statusLabel != null) statusLabel.text = text;
        }
    }
}
