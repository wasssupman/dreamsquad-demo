using System;
using System.Text;
using UnityEngine.Networking;

namespace Wassup.SheetSync
{
    // 이식 가능한 sheet-sync 코어의 POST transport. push payload(JSON) 를 Apps Script
    // 웹앱 /exec (또는 {success, data, errorDetail} 로 응답하는 임의 엔드포인트)로 보낸다.
    // GET 은 이 프로젝트에서 소비처가 없어 넣지 않는다 — import 는 레거시 SheetFetcher 유지.
    // 에디터에서 검증된 SheetFetcher.Fetch 의 비동기 스타일을 그대로 따른다(에디터·런타임 공용).
    public static class SheetHttp
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

        public static void Post(string url, string jsonBody, Action<Result> onDone)
        {
            var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonBody ?? "")),
                downloadHandler = new DownloadHandlerBuffer(),
                // hung connection 이 완료 콜백을 영영 안 부르는 것 방지 (SheetFetcher 규칙).
                timeout = 30,
            };
            request.SetRequestHeader("Content-Type", "application/json");

            var operation = request.SendWebRequest();
            operation.completed += _ =>
            {
                // HTTP 에러여도 바디를 보존한다 — Apps Script 는 에러도 JSON 으로 답한다.
                var result = new Result(
                    request.downloadHandler != null ? request.downloadHandler.text : null,
                    request.result == UnityWebRequest.Result.Success ? null : request.error);
                request.Dispose();
                onDone(result);
            };
        }
    }
}
