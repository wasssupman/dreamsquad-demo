using System;
using UnityEngine;
using UnityEngine.Networking;

namespace Wassup.Core.Api
{
    // demo-username-recovery Unit 1 — "does this name already exist on the
    // server?" via GET /user with the X-AUTH-USERNAME header. Three-way result
    // so the login flow (unit 2) can decide: adopt the existing account, mint a
    // fresh firebase one, or stop without minting.
    //
    // Probe (2026-07-22): existing name → 200 + User envelope; absent name → 403
    // HANDLE_ACCESS_DENIED (a failure WITH a body). A connection failure returns
    // no body — that split is what keeps a network blip from minting a duplicate
    // account (spec contract #3).
    public static class UserLookupApi
    {
        private const int TimeoutSeconds = 10;

        public enum Outcome { Found, NotFound, NetworkError }

        public readonly struct Result
        {
            public readonly Outcome outcome;
            public readonly UserSignApi.SignedInUser user;  // Found only
            public readonly string error;                    // NetworkError detail only

            private Result(Outcome outcome, UserSignApi.SignedInUser user, string error)
            {
                this.outcome = outcome;
                this.user = user;
                this.error = error;
            }

            public static Result Found(UserSignApi.SignedInUser user) => new Result(Outcome.Found, user, null);
            public static Result NotFound() => new Result(Outcome.NotFound, null, null);
            public static Result Network(string error) => new Result(Outcome.NetworkError, null, error);
        }

        // Pure classifier — unit-tested without a live request. body is the
        // response text (may be null/empty), transportError is UnityWebRequest's
        // error on a non-success result (null on 2xx).
        internal static Result Classify(string body, string transportError)
        {
            var user = ApiEnvelope.Parse<UserSignApi.SignedInUser>(body, out string parseError);
            if (user != null) return Result.Found(user);

            // A server response WITH a body (e.g. 403 HANDLE_ACCESS_DENIED for an
            // absent name) is a definitive "does not exist" — safe to mint. No
            // body means the request never reached a verdict → network failure,
            // which must NOT mint (contract #3).
            if (!string.IsNullOrWhiteSpace(body)) return Result.NotFound();
            return Result.Network(transportError ?? parseError);
        }

        public static void GetUser(string baseUrl, string userName, Action<Result> onDone)
        {
            string url = $"{baseUrl.Trim().TrimEnd('/')}/user";
            var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbGET)
            {
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = TimeoutSeconds,
            };
            request.SetRequestHeader("X-AUTH-USERNAME", userName);
            request.SetRequestHeader("X-SERVICE-APP-VERSION", Application.version);

            var operation = request.SendWebRequest();
            operation.completed += _ =>
            {
                // keep the body even on HTTP failure — the 403/errorDetail lives there.
                string body = request.downloadHandler != null ? request.downloadHandler.text : null;
                string transportError = request.result == UnityWebRequest.Result.Success ? null : request.error;
                request.Dispose();
                onDone(Classify(body, transportError));
            };
        }
    }
}
