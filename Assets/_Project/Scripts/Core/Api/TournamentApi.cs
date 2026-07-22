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
            // tournament-history Unit 0 — 상세 팝업 제목용 토너먼트 이름.
            public string name;
            public int entryCount;
            public int maxEntryCount;
            public List<ResultEntry> entries;
        }

        // tournament-history Unit 0 — 내 (진행 중) 토너먼트 참가 1건. unclaimed
        // 목록 응답의 bare 배열 원소. 소비 필드만 — userId/tournamentTypeId/
        // rewardData 는 파싱하지 않는다 (ResultEntry 선례).
        [Serializable]
        public class UserTournamentResultEntry
        {
            public string tournamentEntryId;  // 상세 조회 경로 파라미터
            public string tournamentName;
            public int score;
            public int rank;
            public string createdTime;         // ISO-8601, 파싱은 뷰에서
            public bool claimed;
        }

        [Serializable]
        internal class ExtraDataBody
        {
            public string debug;
        }

        public static void Play(string baseUrl, AuthCredential credential, Action<PlayState, string> onDone)
        {
            var request = new UnityWebRequest(BuildPlayUrl(baseUrl), UnityWebRequest.kHttpVerbPOST);
            Send(request, credential, (body, transportError) =>
            {
                var state = TryParsePlay(body, out string error);
                if (state == null && transportError != null) error = $"{error} (HTTP: {transportError})";
                onDone(state, state != null ? null : error);
            });
        }

        // onDone(success, error) — the TournamentResult payload is not consumed
        // here; ranking is fetched separately via GetResult (spec decision).
        public static void Complete(string baseUrl, AuthCredential credential, string attemptId, int score,
            string debugJson, Action<bool, string> onDone)
        {
            var request = new UnityWebRequest(BuildCompleteUrl(baseUrl, attemptId, score), UnityWebRequest.kHttpVerbPOST);
            request.uploadHandler = new UploadHandlerRaw(
                System.Text.Encoding.UTF8.GetBytes(BuildCompleteBody(debugJson)));
            request.SetRequestHeader("Content-Type", "application/json");
            Send(request, credential, (body, transportError) =>
            {
                bool ok = ApiEnvelope.TryGetData(body, out _, out string error);
                if (!ok && transportError != null) error = $"{error} (HTTP: {transportError})";
                onDone(ok, ok ? null : error);
            });
        }

        public static void GetResult(string baseUrl, AuthCredential credential, string entryId, Action<ResultData, string> onDone)
        {
            var request = new UnityWebRequest(BuildResultUrl(baseUrl, entryId), UnityWebRequest.kHttpVerbGET);
            Send(request, credential, (body, transportError) =>
            {
                var result = TryParseResult(body, out string error);
                if (result == null && transportError != null) error = $"{error} (HTTP: {transportError})";
                onDone(result, result != null ? null : error);
            });
        }

        // tournament-history Unit 0 — 내 (진행 중) 토너먼트 참가 목록. 응답 data 는
        // UserTournamentResultEntry 의 bare 배열. onDone(list, error) — 정확히 한쪽만 유효.
        public static void GetUnclaimedEntries(string baseUrl, AuthCredential credential,
            Action<List<UserTournamentResultEntry>, string> onDone)
        {
            var request = new UnityWebRequest(BuildUnclaimedUrl(baseUrl), UnityWebRequest.kHttpVerbGET);
            Send(request, credential, (body, transportError) =>
            {
                var list = TryParseUnclaimed(body, out string error);
                if (list == null && transportError != null) error = $"{error} (HTTP: {transportError})";
                onDone(list, list != null ? null : error);
            });
        }

        // demo-username-recovery Unit 3 — the single auth seam. The credential
        // decides Bearer vs X-AUTH-USERNAME; callers never branch on session mode.
        private static void Send(UnityWebRequest request, AuthCredential credential, Action<string, string> onResponse)
        {
            request.downloadHandler = new DownloadHandlerBuffer();
            credential.Apply(request);
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

        internal static string BuildUnclaimedUrl(string baseUrl)
            => $"{TrimBase(baseUrl)}/tournament/result/entry/unclaimed";

        // TournamentResultExtraData — Newtonsoft handles the escaping of the
        // embedded battle-log JSON string.
        internal static string BuildCompleteBody(string debugJson)
            => JsonConvert.SerializeObject(new ExtraDataBody { debug = debugJson ?? string.Empty });

        internal static PlayState TryParsePlay(string body, out string error)
            => ApiEnvelope.Parse<PlayState>(body, out error);

        internal static ResultData TryParseResult(string body, out string error)
            => ApiEnvelope.Parse<ResultData>(body, out error);

        internal static List<UserTournamentResultEntry> TryParseUnclaimed(string body, out string error)
            => ApiEnvelope.ParseList<UserTournamentResultEntry>(body, out error);

        private static string TrimBase(string baseUrl) => baseUrl.Trim().TrimEnd('/');
    }
}
