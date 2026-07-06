using System;
using UnityEngine.Networking;

namespace Wassup.Data.StatImport
{
    // simplify pass (2026-07-06) — the one fetch wrapper shared by the editor
    // importer and the runtime refresher. Keeps the body even on HTTP failure:
    // the API returns 500 with a JSON errorDetail the parser surfaces.
    public static class SheetFetcher
    {
        public readonly struct Result
        {
            public readonly string body;
            public readonly string transportError; // null when HTTP succeeded

            public Result(string body, string transportError)
            {
                this.body = body;
                this.transportError = transportError;
            }
        }

        // Both requests run concurrently (they are independent); onDone fires on
        // the main thread once both complete, so the join counter needs no lock.
        public static void FetchBoth(string urlA, string urlB, Action<Result, Result> onDone)
        {
            var results = new Result[2];
            int remaining = 2;
            Fetch(urlA, r => { results[0] = r; if (--remaining == 0) onDone(results[0], results[1]); });
            Fetch(urlB, r => { results[1] = r; if (--remaining == 0) onDone(results[0], results[1]); });
        }

        public static void Fetch(string url, Action<Result> onDone)
        {
            var request = UnityWebRequest.Get(url);
            var operation = request.SendWebRequest();
            operation.completed += _ =>
            {
                var result = new Result(
                    request.downloadHandler != null ? request.downloadHandler.text : null,
                    request.result == UnityWebRequest.Result.Success ? null : request.error);
                request.Dispose();
                onDone(result);
            };
        }
    }
}
