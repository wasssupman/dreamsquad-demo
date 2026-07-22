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
        // 서버가 2026-07 스키마 변경으로 attempt 필드를 data.userTournamentState 아래로
        // 중첩했다(이전엔 data 직속). 그래서 PlayState 는 data 를, 소비 필드는
        // userTournamentState 를 미러링한다.
        [Serializable]
        public class PlayState
        {
            public UserTournamentState userTournamentState;
        }

        [Serializable]
        public class UserTournamentState
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
            Send(() => new UnityWebRequest(BuildPlayUrl(baseUrl), UnityWebRequest.kHttpVerbPOST), credential,
                (body, transportError) =>
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
            Send(() =>
            {
                var request = new UnityWebRequest(BuildCompleteUrl(baseUrl, attemptId, score), UnityWebRequest.kHttpVerbPOST);
                request.uploadHandler = new UploadHandlerRaw(
                    System.Text.Encoding.UTF8.GetBytes(BuildCompleteBody(debugJson)));
                request.SetRequestHeader("Content-Type", "application/json");
                return request;
            }, credential, (body, transportError) =>
            {
                bool ok = ApiEnvelope.TryGetData(body, out _, out string error);
                if (!ok && transportError != null) error = $"{error} (HTTP: {transportError})";
                onDone(ok, ok ? null : error);
            });
        }

        public static void GetResult(string baseUrl, AuthCredential credential, string entryId, Action<ResultData, string> onDone)
        {
            Send(() => new UnityWebRequest(BuildResultUrl(baseUrl, entryId), UnityWebRequest.kHttpVerbGET), credential,
                (body, transportError) =>
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
            Send(() => new UnityWebRequest(BuildUnclaimedUrl(baseUrl), UnityWebRequest.kHttpVerbGET), credential,
                (body, transportError) =>
            {
                var list = TryParseUnclaimed(body, out string error);
                if (list == null && transportError != null) error = $"{error} (HTTP: {transportError})";
                onDone(list, list != null ? null : error);
            });
        }

        // demo-username-recovery Unit 3 / session-token-refresh Unit 1 — the single
        // auth seam. The credential decides Bearer vs X-AUTH-USERNAME; callers never
        // branch on session mode. requestFactory builds a fresh UnityWebRequest per
        // attempt because an expired firebase idToken (403/401) triggers a token
        // refresh + one replay, and a UnityWebRequest is single-use.
        private static void Send(Func<UnityWebRequest> requestFactory, AuthCredential credential,
            Action<string, string> onResponse)
            => Attempt(requestFactory, credential, allowRefresh: true, onResponse);

        private static void Attempt(Func<UnityWebRequest> requestFactory, AuthCredential credential,
            bool allowRefresh, Action<string, string> onResponse)
        {
            var request = requestFactory();
            request.downloadHandler = new DownloadHandlerBuffer();
            credential.Apply(request);
            request.SetRequestHeader("X-SERVICE-APP-VERSION", Application.version);
            request.timeout = TimeoutSeconds;

            var operation = request.SendWebRequest();
            operation.completed += _ =>
            {
                // keep the body even on HTTP failure — errorDetail lives there.
                long code = request.responseCode;
                string body = request.downloadHandler != null ? request.downloadHandler.text : null;
                string transportError = request.result == UnityWebRequest.Result.Success ? null : request.error;
                request.Dispose();

                // Expired firebase idToken → this server rejects with 403
                // HANDLE_ACCESS_DENIED (401 on standard servers). Re-mint the idToken
                // once via the session's refresh token, then replay this request with
                // the new Bearer. Only Bearer sessions refresh; username/guest have an
                // empty idToken and fall through. A timeout is responseCode 0, so it
                // never triggers here (refresh can't fix a network failure). Single
                // replay (allowRefresh:false) — a second failure surfaces as-is.
                if ((code == 403 || code == 401) && allowRefresh && !string.IsNullOrEmpty(credential.idToken))
                {
                    UserSession.TryRefreshBearer(refreshed =>
                    {
                        if (refreshed)
                            Attempt(requestFactory, UserSession.Credential, allowRefresh: false, onResponse);
                        else
                            onResponse(body, transportError); // refresh unavailable/failed → surface original
                    });
                    return;
                }

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
