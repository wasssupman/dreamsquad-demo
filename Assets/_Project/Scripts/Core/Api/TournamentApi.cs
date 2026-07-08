using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace Wassup.Core.Api
{
    // tournament-play-report Unit 0 — the three tournament endpoints this demo
    // consumes: play (attempt issue), complete (score + battle log), and result
    // (competitor ranking). Same conventions as UserSignApi: envelope via
    // ApiEnvelope, onDone(value, error) with exactly one side meaningful.
    public static class TournamentApi
    {
        private const int TimeoutSeconds = 10;

        // only the fields we consume — the server schemas are much larger.
        [Serializable]
        public class PlayState
        {
            public string status;
            public string tournamentEntryId;
            public string tournamentEntryAttemptId;
        }

        [Serializable]
        public class ResultEntry
        {
            public string userId;
            public string userName;
            public int score;
            public int rank;
        }

        [Serializable]
        public class ResultData
        {
            public int entryCount;
            public int maxEntryCount;
            public List<ResultEntry> entries;
        }

        [Serializable]
        internal class ExtraDataBody
        {
            public string debug;
        }

        public static void Play(string baseUrl, string idToken, Action<PlayState, string> onDone)
        {
            var request = new UnityWebRequest(BuildPlayUrl(baseUrl), UnityWebRequest.kHttpVerbPOST);
            Send(request, idToken, (body, transportError) =>
            {
                var state = TryParsePlay(body, out string error);
                if (state == null && transportError != null) error = $"{error} (HTTP: {transportError})";
                onDone(state, state != null ? null : error);
            });
        }

        // onDone(success, error) — the TournamentResult payload is not consumed
        // here; ranking is fetched separately via GetResult (spec decision).
        public static void Complete(string baseUrl, string idToken, string attemptId, int score,
            string debugJson, Action<bool, string> onDone)
        {
            var request = new UnityWebRequest(BuildCompleteUrl(baseUrl, attemptId, score), UnityWebRequest.kHttpVerbPOST);
            request.uploadHandler = new UploadHandlerRaw(
                System.Text.Encoding.UTF8.GetBytes(BuildCompleteBody(debugJson)));
            request.SetRequestHeader("Content-Type", "application/json");
            Send(request, idToken, (body, transportError) =>
            {
                bool ok = ApiEnvelope.TryGetData(body, out _, out string error);
                if (!ok && transportError != null) error = $"{error} (HTTP: {transportError})";
                onDone(ok, ok ? null : error);
            });
        }

        public static void GetResult(string baseUrl, string idToken, string entryId, Action<ResultData, string> onDone)
        {
            var request = new UnityWebRequest(BuildResultUrl(baseUrl, entryId), UnityWebRequest.kHttpVerbGET);
            Send(request, idToken, (body, transportError) =>
            {
                var result = TryParseResult(body, out string error);
                if (result == null && transportError != null) error = $"{error} (HTTP: {transportError})";
                onDone(result, result != null ? null : error);
            });
        }

        private static void Send(UnityWebRequest request, string idToken, Action<string, string> onResponse)
        {
            request.downloadHandler = new DownloadHandlerBuffer();
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
                onResponse(body, transportError);
            };
        }

        internal static string BuildPlayUrl(string baseUrl)
            => $"{TrimBase(baseUrl)}/tournament/play";

        internal static string BuildCompleteUrl(string baseUrl, string attemptId, int score)
            => $"{TrimBase(baseUrl)}/tournament/complete/{attemptId}/{score}";

        internal static string BuildResultUrl(string baseUrl, string entryId)
            => $"{TrimBase(baseUrl)}/tournament/result/tournament/{entryId}";

        // TournamentResultExtraData — Newtonsoft handles the escaping of the
        // embedded battle-log JSON string.
        internal static string BuildCompleteBody(string debugJson)
            => JsonConvert.SerializeObject(new ExtraDataBody { debug = debugJson ?? string.Empty });

        internal static PlayState TryParsePlay(string body, out string error)
            => ApiEnvelope.Parse<PlayState>(body, out error);

        internal static ResultData TryParseResult(string body, out string error)
            => ApiEnvelope.Parse<ResultData>(body, out error);

        private static string TrimBase(string baseUrl) => baseUrl.Trim().TrimEnd('/');
    }
}
