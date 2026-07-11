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

        // simplify pass (2026-07-06) — the per-sheet "N rows / fetch failed" log
        // policy, shared so the editor and runtime result texts never drift.
        public static T[] ParseSheetLogged<T>(string body, string transportError,
            string sheetLabel, System.Text.StringBuilder log)
        {
            var unknownHeaders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var rows = ParseSheetRows<T>(body, out string error, unknownHeaders);
            if (rows != null)
            {
                log.AppendLine($"[{sheetLabel}] {rows.Length} rows received.");
                // review 3b — a renamed/removed column otherwise fails SILENTLY:
                // the DTO field stays null and "blank cell = keep" swallows every
                // edit in that column while the import still reports success.
                if (unknownHeaders.Count > 0)
                    log.AppendLine($"[{sheetLabel}] headers not in contract (edits in them are IGNORED): {string.Join(", ", unknownHeaders)} — check for renamed columns.");
                return rows;
            }
            string http = transportError != null ? $" (HTTP: {transportError})" : "";
            log.AppendLine($"[{sheetLabel}] fetch failed: {error}{http}");
            return null;
        }

        // unknownHeaders (optional): collects sheet columns that bind to no DTO
        // field. `_`-prefixed columns are out of contract by design and excluded.
        public static T[] ParseSheetRows<T>(string body, out string error,
            HashSet<string> unknownHeaders = null)
        {
            // envelope validation + errorDetail wording live in ApiEnvelope
            // (outgame-login-gate unit 0) — one definition for every consumer.
            if (!Wassup.Core.Api.ApiEnvelope.TryGetData(body, out var data, out error)) return null;

            var rows = data as JArray;
            if (rows == null)
            {
                error = "success=true but 'data' is not an array";
                return null;
            }

            HashSet<string> knownFields = null;
            if (unknownHeaders != null)
            {
                knownFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var f in typeof(T).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
                    knownFields.Add(f.Name);
            }

            foreach (var row in rows)
            {
                var obj = row as JObject;
                if (obj == null) continue;
                var emptyProps = new List<string>();
                foreach (var prop in obj.Properties())
                {
                    if (knownFields != null && !prop.Name.StartsWith("_") && !knownFields.Contains(prop.Name))
                        unknownHeaders.Add(prop.Name);
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
