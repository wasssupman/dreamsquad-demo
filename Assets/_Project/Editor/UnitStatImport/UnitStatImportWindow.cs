using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
using Wassup.Data;

namespace Wassup.Editor.UnitStatImport
{
    // unit-stat-spreadsheet-schema Unit 1 — fetches the spreadsheet-authored stat
    // payload from the REST API and applies it to existing Defender/Enemy SO assets.
    // Update-only by id match; never creates new .asset files.
    // Unit 4 — real API contract: GET {baseUrl}/{sheetName}, one call per tab,
    // per-sheet envelope {success, data:[row], errorDetail}.
    public class UnitStatImportWindow : EditorWindow
    {
        private const string BaseUrlPrefsKey = "Wassup.UnitStatImport.BaseUrl";
        private const string DefenderSheetPrefsKey = "Wassup.UnitStatImport.DefenderSheet";
        private const string EnemySheetPrefsKey = "Wassup.UnitStatImport.EnemySheet";
        private const string DefaultBaseUrl = "https://dev-api-somnia.cashroyale.games/demo/google/sheet";
        private const string DefenderFolder = "Assets/_Project/Data/Defenders";
        private const string EnemyFolder = "Assets/_Project/Data/Enemies";

        private string _baseUrl = "";
        private string _defenderSheet = "";
        private string _enemySheet = "";
        private string _statusLog = "";
        private bool _requestInFlight;

        [MenuItem("Window/Wassup/Unit Stat Import")]
        public static void Open() => GetWindow<UnitStatImportWindow>("Unit Stat Import");

        private void OnEnable()
        {
            _baseUrl = EditorPrefs.GetString(BaseUrlPrefsKey, DefaultBaseUrl);
            _defenderSheet = EditorPrefs.GetString(DefenderSheetPrefsKey, "Defenders");
            _enemySheet = EditorPrefs.GetString(EnemySheetPrefsKey, "Enemies");
            // hotfix ③ — serialized true survives a domain reload while the
            // completed callback does not; reset so the Import button never sticks.
            _requestInFlight = false;
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Unit Stat Import", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            _baseUrl = EditorGUILayout.TextField("API Base URL", _baseUrl);
            _defenderSheet = EditorGUILayout.TextField("Defender Sheet", _defenderSheet);
            _enemySheet = EditorGUILayout.TextField("Enemy Sheet", _enemySheet);
            if (EditorGUI.EndChangeCheck())
            {
                EditorPrefs.SetString(BaseUrlPrefsKey, _baseUrl);
                EditorPrefs.SetString(DefenderSheetPrefsKey, _defenderSheet);
                EditorPrefs.SetString(EnemySheetPrefsKey, _enemySheet);
            }

            bool inputsMissing = string.IsNullOrWhiteSpace(_baseUrl)
                || string.IsNullOrWhiteSpace(_defenderSheet)
                || string.IsNullOrWhiteSpace(_enemySheet);
            using (new EditorGUI.DisabledScope(_requestInFlight || inputsMissing))
            {
                if (GUILayout.Button(_requestInFlight ? "Importing..." : "Import"))
                {
                    StartImport();
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Export", EditorStyles.boldLabel);
            // unit 5 — SO → one row-array JSON file per sheet tab, named after the
            // sheet name fields above so file ↔ tab mapping is unambiguous.
            using (new EditorGUI.DisabledScope(_requestInFlight
                || string.IsNullOrWhiteSpace(_defenderSheet) || string.IsNullOrWhiteSpace(_enemySheet)))
            {
                if (GUILayout.Button("Export SO → JSON Files"))
                {
                    string folder = EditorUtility.SaveFolderPanel("Export Unit Stat JSON", "", "");
                    if (!string.IsNullOrEmpty(folder))
                    {
                        _statusLog = UnitStatExporter.ExportToFolder(
                            folder, _defenderSheet, _enemySheet, DefenderFolder, EnemyFolder);
                    }
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Result", EditorStyles.boldLabel);
            EditorGUILayout.TextArea(_statusLog, GUILayout.MinHeight(120));
        }

        // unit 4 — one GET per sheet tab; sequential so the result log reads in
        // contract order (defenders, then enemies).
        private void StartImport()
        {
            _requestInFlight = true;
            _statusLog = "Requesting...";

            FetchSheet(BuildSheetUrl(_baseUrl, _defenderSheet), defenderFetch =>
                FetchSheet(BuildSheetUrl(_baseUrl, _enemySheet), enemyFetch =>
                    OnBothSheetsFetched(defenderFetch, enemyFetch)));
        }

        private readonly struct SheetFetch
        {
            public readonly string body;
            public readonly string transportError; // null when HTTP succeeded

            public SheetFetch(string body, string transportError)
            {
                this.body = body;
                this.transportError = transportError;
            }
        }

        private static void FetchSheet(string url, Action<SheetFetch> onDone)
        {
            var request = UnityWebRequest.Get(url);
            var operation = request.SendWebRequest();
            operation.completed += _ =>
            {
                // keep the body even on HTTP failure — the API returns 500 with a JSON
                // errorDetail we can surface (e.g. "구글 시트 연동 실패").
                var fetch = new SheetFetch(
                    request.downloadHandler != null ? request.downloadHandler.text : null,
                    request.result == UnityWebRequest.Result.Success ? null : request.error);
                request.Dispose();
                onDone(fetch);
            };
        }

        private void OnBothSheetsFetched(SheetFetch defenderFetch, SheetFetch enemyFetch)
        {
            try
            {
                var log = new StringBuilder();
                var defenders = ParseSheet<DefenderStatDto>(defenderFetch, _defenderSheet, log);
                var enemies = ParseSheet<EnemyStatDto>(enemyFetch, _enemySheet, log);

                if (defenders == null && enemies == null)
                {
                    _statusLog = log.ToString();
                    return;
                }

                // partial-update philosophy: a sheet that failed to fetch contributes
                // no rows, so the healthy sheet still applies.
                var payload = new UnitStatImportPayload
                {
                    defenders = defenders ?? Array.Empty<DefenderStatDto>(),
                    enemies = enemies ?? Array.Empty<EnemyStatDto>(),
                };
                _statusLog = log.ToString() + ApplyPayload(payload);
            }
            finally
            {
                _requestInFlight = false;
                Repaint();
            }
        }

        private static T[] ParseSheet<T>(SheetFetch fetch, string sheetLabel, StringBuilder log)
        {
            var rows = ParseSheetRows<T>(fetch.body, out string error);
            if (rows != null)
            {
                log.AppendLine($"[{sheetLabel}] {rows.Length} rows received.");
                return rows;
            }
            string http = fetch.transportError != null ? $" (HTTP: {fetch.transportError})" : "";
            log.AppendLine($"[{sheetLabel}] fetch failed: {error}{http}");
            return null;
        }

        // unit 4 — parses the per-sheet envelope and binds data rows to DTOs.
        // Empty-string cells are stripped before binding: the sheet contract reads a
        // blank cell as "keep the existing SO value", identical to an omitted key
        // (and float?/enum? fields would otherwise choke on "").
        internal static T[] ParseSheetRows<T>(string body, out string error)
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

        internal static string BuildSheetUrl(string baseUrl, string sheetName)
            => $"{baseUrl.Trim().TrimEnd('/')}/{Uri.EscapeDataString(sheetName.Trim())}";

        internal static string ApplyPayload(UnitStatImportPayload payload)
        {
            var log = new StringBuilder();
            int matched = 0, unmatched = 0, fieldsApplied = 0, projected = 0, skipped = 0;

            var defendersById = BuildAssetIndex<DefenderUnitData>(DefenderFolder, so => so.id, log);
            var enemiesById = BuildAssetIndex<AttackUnitData>(EnemyFolder, so => so.id, log);

            var seenIds = new HashSet<string>();
            foreach (var dto in payload?.defenders ?? Array.Empty<DefenderStatDto>())
            {
                if (!seenIds.Add($"defender:{dto.id}")) { log.AppendLine($"[defender] duplicate row for id='{dto.id}' — skipped."); continue; }
                if (string.IsNullOrEmpty(dto.id) || !defendersById.TryGetValue(dto.id, out var so))
                { unmatched++; log.AppendLine($"[defender] no match for id='{dto.id}'"); continue; }
                fieldsApplied += UnitStatFieldMapper.ApplyNonNullFields(dto, so);
                ProjectMagnitude(so.outputs, AttackOutputKind.Damage, dto.atk, "atk", $"defender '{dto.id}'", log, ref projected, ref skipped);
                ProjectMagnitude(so.outputs, AttackOutputKind.Heal, dto.heal, "heal", $"defender '{dto.id}'", log, ref projected, ref skipped);
                WarnDeprecatedAttackDamage(dto.attackDamage, $"defender '{dto.id}'", log);
                EditorUtility.SetDirty(so);
                AssetDatabase.SaveAssetIfDirty(so);
                matched++;
            }

            foreach (var dto in payload?.enemies ?? Array.Empty<EnemyStatDto>())
            {
                if (!seenIds.Add($"enemy:{dto.id}")) { log.AppendLine($"[enemy] duplicate row for id='{dto.id}' — skipped."); continue; }
                if (string.IsNullOrEmpty(dto.id) || !enemiesById.TryGetValue(dto.id, out var so))
                { unmatched++; log.AppendLine($"[enemy] no match for id='{dto.id}'"); continue; }
                fieldsApplied += UnitStatFieldMapper.ApplyNonNullFields(dto, so);
                ProjectMagnitude(so.outputs, AttackOutputKind.Damage, dto.atk, "atk", $"enemy '{dto.id}'", log, ref projected, ref skipped);
                WarnDeprecatedAttackDamage(dto.attackDamage, $"enemy '{dto.id}'", log);
                EditorUtility.SetDirty(so);
                AssetDatabase.SaveAssetIfDirty(so);
                matched++;
            }

            log.Insert(0, $"Matched {matched}, unmatched {unmatched}, fields applied {fieldsApplied}, projected {projected}, skipped {skipped}.\n");
            return log.ToString();
        }

        // unit-stat-projection Unit 3 — a planner-facing scalar (atk/heal) writes the
        // unique output of its kind. 0 or 2+ matches is ambiguous: skip + report the
        // reason so the miss is visible, never guess a target.
        internal static void ProjectMagnitude(AttackOutput[] outputs, AttackOutputKind kind, float? value,
            string field, string label, StringBuilder log, ref int projected, ref int skipped)
        {
            if (value == null) return; // omitted -> keep existing magnitude
            if (AttackOutputStats.TrySetUniqueMagnitude(outputs, kind, value.Value))
            {
                projected++;
                return;
            }
            skipped++;
            int count = CountOfKind(outputs, kind);
            string reason = count == 0
                ? $"no {kind} output"
                : $"{count} {kind} outputs (need exactly 1)";
            log.AppendLine($"[{label}] {field}={value.Value} skipped — {reason}.");
        }

        internal static void WarnDeprecatedAttackDamage(float? attackDamage, string label, StringBuilder log)
        {
            if (attackDamage == null) return;
            log.AppendLine($"[{label}] 'attackDamage' is deprecated (renamed to 'atk') and was NOT applied — update the sheet column.");
        }

        private static int CountOfKind(AttackOutput[] outputs, AttackOutputKind kind)
        {
            if (outputs == null) return 0;
            int n = 0;
            foreach (var o in outputs) if (o.kind == kind) n++;
            return n;
        }

        // hotfix ⑥ — one scan per import. An id shared by 2+ assets is an ambiguous
        // write target: drop it from the index entirely and report, never guess.
        private static Dictionary<string, T> BuildAssetIndex<T>(
            string folder, Func<T, string> idSelector, StringBuilder log) where T : ScriptableObject
        {
            var byId = new Dictionary<string, T>();
            var ambiguous = new HashSet<string>();
            foreach (var guid in AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { folder }))
            {
                var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset == null) continue;
                string id = idSelector(asset);
                if (string.IsNullOrEmpty(id)) continue;
                if (!byId.TryAdd(id, asset) && ambiguous.Add(id))
                {
                    byId.Remove(id);
                    log.AppendLine($"[{typeof(T).Name}] duplicate asset id '{id}' — all assets with this id skipped.");
                }
            }
            return byId;
        }
    }
}
