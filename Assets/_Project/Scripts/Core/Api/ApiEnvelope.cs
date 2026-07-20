using System;
using System.Collections.Generic;
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
                root = JObject.Parse(body);
            }
            catch (Exception e)
            {
                error = $"JSON parse failed: {e.Message}";
                return false;
            }

            if (!(root.Value<bool?>("success") ?? false))
            {
                var detail = root["errorDetail"] as JObject;
                error = detail == null
                    ? "success=false (no errorDetail)"
                    : $"{detail.Value<string>("errorCode")} — {detail.Value<string>("errorMessage")} / {detail.Value<string>("detailMessage")}";
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
