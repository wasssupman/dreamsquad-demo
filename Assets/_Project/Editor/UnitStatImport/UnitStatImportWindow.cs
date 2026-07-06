using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
using Wassup.Data;
using Wassup.Data.StatImport;

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

        // The parse/apply core lives in the runtime assembly (SheetEnvelopeParser /
        // UnitStatApplier) since runtime-stat-refresh units 0-1, shared with the
        // in-build refresher. These forwards keep the window API (and its tests)
        // stable.
        internal static T[] ParseSheetRows<T>(string body, out string error)
            => SheetEnvelopeParser.ParseSheetRows<T>(body, out error);

        internal static string BuildSheetUrl(string baseUrl, string sheetName)
            => SheetEnvelopeParser.BuildSheetUrl(baseUrl, sheetName);

        internal static void ProjectMagnitude(AttackOutput[] outputs, AttackOutputKind kind, float? value,
            string field, string label, StringBuilder log, ref int projected, ref int skipped)
            => UnitStatApplier.ProjectMagnitude(outputs, kind, value, field, label, log, ref projected, ref skipped);

        internal static void WarnDeprecatedAttackDamage(float? attackDamage, string label, StringBuilder log)
            => UnitStatApplier.WarnDeprecatedAttackDamage(attackDamage, label, log);

        // Editor apply = shared core + AssetDatabase scan + per-asset disk save.
        internal static string ApplyPayload(UnitStatImportPayload payload)
        {
            var log = new StringBuilder();
            var defendersById = UnitStatApplier.BuildIndex(
                LoadAssets<DefenderUnitData>(DefenderFolder), so => so.id, log, nameof(DefenderUnitData));
            var enemiesById = UnitStatApplier.BuildIndex(
                LoadAssets<AttackUnitData>(EnemyFolder), so => so.id, log, nameof(AttackUnitData));

            return UnitStatApplier.Apply(payload, defendersById, enemiesById, so =>
            {
                EditorUtility.SetDirty(so);
                AssetDatabase.SaveAssetIfDirty(so);
            }, log);
        }

        private static IEnumerable<T> LoadAssets<T>(string folder) where T : ScriptableObject
        {
            foreach (var guid in AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { folder }))
            {
                var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null) yield return asset;
            }
        }
    }
}
