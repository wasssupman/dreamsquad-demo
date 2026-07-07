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
        private const float SignedInLingerSec = 0.8f;

        [SerializeField] private TMP_InputField nameInput;
        [SerializeField] private Button loginButton;
        [SerializeField] private TMP_Text statusLabel;
        // Firebase web apiKey is a public client identifier by design.
        [SerializeField] private string firebaseApiKey = "AIzaSyBFy7R0JJqLwkEJR7DwKz2da-QgrwU4CdM";
        [SerializeField] private string gameApiBaseUrl = "https://dev-api-somnia.cashroyale.games";

        public event Action onSignedIn;

        private bool _busy;

        private void Awake()
        {
            loginButton.onClick.AddListener(OnLoginClicked);
            if (nameInput != null) nameInput.text = PlayerPrefs.GetString(UserNamePrefsKey, "");
            SetStatus("");
        }

        private void Start()
        {
            if (UserSession.IsSignedIn) return; // scene revisit within the session

            // auto sign-in: both pieces must exist (a stored name implies a
            // completed first login).
            string storedToken = PlayerPrefs.GetString(RefreshTokenPrefsKey, "");
            string storedName = PlayerPrefs.GetString(UserNamePrefsKey, "");
            if (string.IsNullOrEmpty(storedToken) || string.IsNullOrEmpty(storedName)) return;

            SetBusy(true, "SIGNING IN...");
            FirebaseAuthRestClient.RefreshIdToken(firebaseApiKey, storedToken, (tokens, error) =>
            {
                if (this == null) return;
                if (tokens == null) { HandleFailure(error, clearTokenIfDefinitive: true); return; }
                SignInToGameServer(tokens.Value, storedName);
            });
        }

        private void OnDestroy()
        {
            if (loginButton != null) loginButton.onClick.RemoveListener(OnLoginClicked);
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
            string storedToken = PlayerPrefs.GetString(RefreshTokenPrefsKey, "");
            if (!string.IsNullOrEmpty(storedToken))
            {
                FirebaseAuthRestClient.RefreshIdToken(firebaseApiKey, storedToken, (tokens, error) =>
                {
                    if (this == null) return;
                    if (tokens != null) { SignInToGameServer(tokens.Value, userName); return; }
                    if (FirebaseAuthRestClient.IsDefinitiveAuthError(error))
                    {
                        PlayerPrefs.DeleteKey(RefreshTokenPrefsKey);
                        SignUpFresh(userName);
                        return;
                    }
                    HandleFailure(error, clearTokenIfDefinitive: false);
                });
            }
            else
            {
                SignUpFresh(userName);
            }
        }

        private void SignUpFresh(string userName)
        {
            FirebaseAuthRestClient.SignUpAnonymous(firebaseApiKey, (tokens, error) =>
            {
                if (this == null) return;
                if (tokens == null) { HandleFailure(error, clearTokenIfDefinitive: false); return; }
                SignInToGameServer(tokens.Value, userName);
            });
        }

        private void SignInToGameServer(FirebaseAuthRestClient.AuthTokens tokens, string userName)
        {
            UserSignApi.SignIn(gameApiBaseUrl, tokens.idToken, userName, (user, error) =>
            {
                if (this == null) return;
                if (user == null) { HandleFailure(error, clearTokenIfDefinitive: false); return; }

                PlayerPrefs.SetString(RefreshTokenPrefsKey, tokens.refreshToken);
                PlayerPrefs.SetString(UserNamePrefsKey, userName);
                UserSession.Set(user, tokens.idToken);
                SetBusy(false, $"SIGNED IN AS {user.userName}".ToUpperInvariant());
                StartCoroutine(NotifySignedInAfterLinger());
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
            PlayerPrefs.DeleteKey(RefreshTokenPrefsKey);
            PlayerPrefs.DeleteKey(UserNamePrefsKey);
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
