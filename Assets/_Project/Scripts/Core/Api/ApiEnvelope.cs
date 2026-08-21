using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Wassup.Core.Api
{
    // outgame-login-gate Unit 0 — the game server's common response format
    // {success, data, errorDetail}, one definition for every consumer
    // (sheet importer, sign-in, future content APIs). Firebase REST responses
    // are NOT this envelope — do not route them through here.
    public static class ApiEnvelope
    {
        // Validates the envelope and hands back the raw data token. Shared seam:
        // SheetEnvelopeParser binds it as a row array, Parse<T> as a single object.
        // allowNullData: list endpoints pass true so a success envelope whose data
        // is null/missing (some servers omit []) is a valid empty result, not an
        // error. Default false keeps the single-object/sheet callers strict.
        public static bool TryGetData(string body, out JToken data, out string error, bool allowNullData = false)
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
                // DateParseHandling.None: keep ISO date strings verbatim. Newtonsoft's
                // default coerces date-like strings to DateTime, which then reads back
                // as a locale-dependent string (e.g. createdTime). Every field we bind
                // is plain string/number — none wants auto-DateTime.
                using (var reader = new JsonTextReader(new StringReader(body))
                       { DateParseHandling = DateParseHandling.None })
                {
                    root = JObject.Load(reader);
                }
            }
            catch (Exception e)
            {
                error = $"JSON parse failed: {e.Message}";
                return false;
            }

            if (!ReadSuccess(root))
            {
                error = DescribeFailure(root);
                return false;
            }

            data = root["data"];
            if (data == null || data.Type == JTokenType.Null)
            {
                data = null;
                if (allowNullData) return true; // empty list case
                error = "success=true but 'data' is missing";
                return false;
            }
            return true;
        }

        // 아래 세 헬퍼는 "서버가 보낸 모양"을 신뢰하지 않는다. 2026-08-18 실기기에서
        // 판 종료 → complete 응답을 읽다가 InvalidCastException 이 났다: 실패 응답의
        // errorDetail 안 문자열 칸에 객체가 들어 있었고, Value<string> 이 스칼라가 아닌
        // 토큰에 던진다. 그 예외는 UnityWebRequest 완료 콜백 안에서 삼켜져 onDone 이
        // 영영 안 불리고, TournamentMatchReporter 의 in-flight 카운터가 박혀 이후 로비
        // reconcile 이 통째로 skip 됐다. 실패를 **설명하다가** 실패 처리를 죽인 셈이다.
        // 그래서 이 seam 의 계약은 "어떤 바디가 와도 던지지 않는다" 이다.

        // 스칼라가 아니면 원문(JSON)을 그대로 돌려준다 — 내용을 버리면 진단이 불가능해진다.
        static string Scalar(JToken parent, string name)
        {
            var token = parent?[name];
            if (token == null || token.Type == JTokenType.Null) return null;
            return token is JValue value ? value.Value?.ToString() : token.ToString(Formatting.None);
        }

        // 없거나 스칼라가 아니면 성공으로 치지 않는다. 문자열 "true" 는 이전 구현
        // (Value<bool?>)이 받아주던 모양이라 계속 받는다.
        static bool ReadSuccess(JObject root)
        {
            if (!(root["success"] is JValue value)) return false;
            if (value.Value is bool flag) return flag;
            return bool.TryParse(value.Value?.ToString(), out bool parsed) && parsed;
        }

        static string DescribeFailure(JObject root)
        {
            var detail = root["errorDetail"];
            if (detail == null || detail.Type == JTokenType.Null) return "success=false (no errorDetail)";
            if (!(detail is JObject))
                return $"success=false — errorDetail={detail.ToString(Formatting.None)}";
            return $"{Scalar(detail, "errorCode")} — {Scalar(detail, "errorMessage")} / {Scalar(detail, "detailMessage")}";
        }

        public static T Parse<T>(string body, out string error) where T : class
        {
            if (!TryGetData(body, out var data, out error)) return null;
            try
            {
                return data.ToObject<T>();
            }
            catch (Exception e)
            {
                error = $"data binding failed: {e.Message}";
                return null;
            }
        }

        // List endpoints: a success envelope with null/missing data binds to an
        // empty list rather than an error (see allowNullData). Real failures
        // (success=false, parse errors) still return null with a message.
        public static List<T> ParseList<T>(string body, out string error)
        {
            if (!TryGetData(body, out var data, out error, allowNullData: true)) return null;
            if (data == null) return new List<T>();
            try
            {
                return data.ToObject<List<T>>();
            }
            catch (Exception e)
            {
                error = $"data binding failed: {e.Message}";
                return null;
            }
        }
    }
}
