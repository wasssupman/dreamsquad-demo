using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Wassup.SheetSync
{
    // 이식 가능한 sheet-sync 코어. 게임의 Core/Api/ApiEnvelope 와 동일 wire shape
    // ({success, data, errorDetail}) 를 읽지만 그 타입에 의존하지 않는다 — ApiEnvelope 는
    // Firebase/Tournament/UserLookup 이 공유하는 인프라라, 끌어오면 이 모듈이 게임 전체에
    // 묶여 이식이 불가능해진다. 이 모듈의 의존은 UnityEngine + Newtonsoft 뿐이라 그대로
    // 다른 프로젝트로 복사된다. 같은 wire shape 를 두 모듈이 독립적으로 읽는 건 의도된 경계.
    public static class SheetEnvelope
    {
        // 성공 시 data(JToken — 배열/객체 무관, push 응답은 객체)를 반환한다.
        // 실패(빈 바디·비 JSON·success!=true·data 없음)는 data=null + 사람이 읽는 error.
        public static bool TryGetData(string body, out JToken data, out string error)
        {
            data = null;
            error = null;

            if (string.IsNullOrWhiteSpace(body))
            {
                error = "empty response body";
                return false;
            }

            JObject root;
            try
            {
                // DateParseHandling.None — ISO 날짜 문자열을 DateTime 으로 변형하지 않고
                // 원문 유지 (ApiEnvelope 와 동일 정책).
                using var reader = new JsonTextReader(new StringReader(body))
                {
                    DateParseHandling = DateParseHandling.None,
                };
                root = JObject.Load(reader);
            }
            catch (JsonException e)
            {
                error = $"response is not valid JSON: {e.Message}";
                return false;
            }

            var success = root["success"];
            if (success == null || success.Type != JTokenType.Boolean || !success.Value<bool>())
            {
                error = ComposeError(root["errorDetail"]);
                return false;
            }

            data = root["data"];
            if (data == null || data.Type == JTokenType.Null)
            {
                error = "response missing 'data'";
                data = null;
                return false;
            }

            return true;
        }

        private static string ComposeError(JToken errorDetail)
        {
            if (errorDetail == null || errorDetail.Type == JTokenType.Null)
                return "request failed (success != true, no errorDetail)";

            var parts = new List<string>();
            var code = errorDetail["errorCode"]?.ToString();
            var msg = errorDetail["errorMessage"]?.ToString();
            var detail = errorDetail["detailMessage"]?.ToString();
            if (!string.IsNullOrEmpty(code)) parts.Add($"[{code}]");
            if (!string.IsNullOrEmpty(msg)) parts.Add(msg);
            if (!string.IsNullOrEmpty(detail)) parts.Add($"({detail})");
            return parts.Count > 0 ? string.Join(" ", parts) : errorDetail.ToString();
        }
    }
}
