using System;
using System.Text;
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
                // ⚠ `downloadHandler.text` 를 쓰지 않는다. 시트 API 응답은
                // `content-type: application/json` 에 **charset 이 없어서** 디코딩이 핸들러의
                // 추론에 맡겨진다. 실제로 그 추론이 어긋난 환경에서 한글 desc 가 UTF-8 바이트
                // 하나하나가 개별 문자가 된 채(ê¸°ë³¸…) SO 에 저장돼 커밋까지 됐다
                // (2026-07-29, 커밋 616e3584 — 에셋 24개 · 문자열 352개 손상).
                // 서버가 charset 을 안 보내는 이상 여기서 못을 박는 것이 유일한 확실한 방어다.
                string body = null;
                if (request.downloadHandler != null)
                {
                    var bytes = request.downloadHandler.data;
                    body = bytes != null && bytes.Length > 0
                        ? new UTF8Encoding(false).GetString(bytes)
                        : string.Empty;
                }
                var result = new Result(
                    body,
                    request.result == UnityWebRequest.Result.Success ? null : request.error);
                request.Dispose();
                onDone(result);
            };
        }
    }
}
