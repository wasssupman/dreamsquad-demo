using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Wassup.Data.StatImport
{
    // runtime-stat-refresh Unit 0 — extracted from UnitStatImportWindow so the
    // build-side refresher shares the exact same envelope/row parsing rules.
    // Real API contract: per-sheet envelope {success, data:[row], errorDetail}.
    // Empty-string cells are stripped before binding: the sheet contract reads a
    // blank cell as "keep the existing SO value", identical to an omitted key
    // (and float?/enum? fields would otherwise choke on "").
    public static class SheetEnvelopeParser
    {
        public static string BuildSheetUrl(string baseUrl, string sheetName)
            => $"{baseUrl.Trim().TrimEnd('/')}/{Uri.EscapeDataString(sheetName.Trim())}";

        public static T[] ParseSheetRows<T>(string body, out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(body))
            {
                error = "empty response body";
                return null;
            }

            JObject root;
            try
            {
                root = JObject.Parse(body);
            }
            catch (Exception e)
            {
                error = $"JSON parse failed: {e.Message}";
                return null;
            }

            if (!(root.Value<bool?>("success") ?? false))
            {
                var detail = root["errorDetail"] as JObject;
                error = detail == null
                    ? "success=false (no errorDetail)"
                    : $"{detail.Value<string>("errorCode")} — {detail.Value<string>("errorMessage")} / {detail.Value<string>("detailMessage")}";
                return null;
            }

            var rows = root["data"] as JArray;
            if (rows == null)
            {
                error = "success=true but 'data' is not an array";
                return null;
            }

            foreach (var row in rows)
            {
                var obj = row as JObject;
                if (obj == null) continue;
                var emptyProps = new List<string>();
                foreach (var prop in obj.Properties())
                {
                    if (prop.Value.Type == JTokenType.String && string.IsNullOrWhiteSpace((string)prop.Value))
                        emptyProps.Add(prop.Name);
                }
                foreach (var name in emptyProps) obj.Remove(name);
            }

            try
            {
                return rows.ToObject<T[]>();
            }
            catch (Exception e)
            {
                error = $"row binding failed: {e.Message}";
                return null;
            }
        }
    }
}
