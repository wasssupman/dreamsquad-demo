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
            => FetchAll(new[] { urlA, urlB }, r => onDone(r[0], r[1]));

        // dreamcatcher-sheet-sync unit 3 — N-tab generalization of FetchBoth
        // (dreamcatcher pulls 6 tabs). Same main-thread join, no lock needed.
        public static void FetchAll(string[] urls, Action<Result[]> onDone)
        {
            var results = new Result[urls.Length];
            int remaining = urls.Length;
            if (remaining == 0) { onDone(results); return; } // review M2 — join would never fire
            for (int i = 0; i < urls.Length; i++)
            {
                int slot = i;
                Fetch(urls[slot], r => { results[slot] = r; if (--remaining == 0) onDone(results); });
            }
        }

        public static void Fetch(string url, Action<Result> onDone)
        {
            var request = UnityWebRequest.Get(url);
            // review M2 — without a timeout a hung connection never completes the
            // FetchAll join, freezing the import UI until domain reload.
            request.timeout = 30;
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
