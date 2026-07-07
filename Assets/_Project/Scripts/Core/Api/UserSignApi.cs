using System;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace Wassup.Core.Api
{
    // outgame-login-gate Unit 0 — game-server sign-in with the Firebase idToken.
    // Creates/loads the server-side user; the returned userId is what test
    // records will be attributed to.
    public static class UserSignApi
    {
        private const int TimeoutSeconds = 10;

        // only the fields we consume — the server User schema is much larger.
        [Serializable]
        public class SignedInUser
        {
            public string userId;
            public string userName;
            public string provider;
        }

        [Serializable]
        internal class MetadataBody
        {
            public Metadata metadata;
        }

        [Serializable]
        internal class Metadata
        {
            public string userName;
            public string appVersion;
            public string osType;
            public string lang;
        }

        // onDone(user, error) — exactly one of the two is meaningful.
        public static void SignIn(string baseUrl, string idToken, string userName, Action<SignedInUser, string> onDone)
        {
            string url = $"{baseUrl.Trim().TrimEnd('/')}/user/sign/in";
            var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
            request.uploadHandler = new UploadHandlerRaw(
                System.Text.Encoding.UTF8.GetBytes(BuildMetadataBody(userName)));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", $"Bearer {idToken}");
            request.SetRequestHeader("X-SERVICE-APP-VERSION", Application.version);
            request.timeout = TimeoutSeconds;

            var operation = request.SendWebRequest();
            operation.completed += _ =>
            {
                // keep the body even on HTTP failure — errorDetail lives there.
                string body = request.downloadHandler != null ? request.downloadHandler.text : null;
                string transportError = request.result == UnityWebRequest.Result.Success ? null : request.error;
                request.Dispose();

                var user = TryParseSignIn(body, out string error);
                if (user == null && transportError != null) error = $"{error} (HTTP: {transportError})";
                onDone(user, user != null ? null : error);
            };
        }

        internal static SignedInUser TryParseSignIn(string body, out string error)
            => ApiEnvelope.Parse<SignedInUser>(body, out error);

        internal static string BuildMetadataBody(string userName)
        {
            return JsonConvert.SerializeObject(new MetadataBody
            {
                metadata = new Metadata
                {
                    userName = userName,
                    appVersion = Application.version,
                    osType = CurrentOsType(),
                    lang = Application.systemLanguage.ToString(),
                },
            });
        }

        private static string CurrentOsType()
        {
            switch (Application.platform)
            {
                case RuntimePlatform.Android: return "ANDROID";
                case RuntimePlatform.IPhonePlayer: return "IOS";
                default: return "WEB";
            }
        }
    }
}
