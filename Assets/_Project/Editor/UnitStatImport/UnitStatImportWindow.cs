using System.Text;
using UnityEditor;
using UnityEngine;
using Wassup.Data;
using Wassup.Data.StatImport;

namespace Wassup.Editor.UnitStatImport
{
    // unit-stat-spreadsheet-schema Unit 1 — fetches the spreadsheet-authored stat
    // payload from the REST API and applies it to existing Defender/Enemy SO assets.
    // Update-only by id match; never creates new .asset files.
    // Unit 4 — real API contract: GET {baseUrl}/{sheetName}, one call per tab,
    // per-sheet envelope {success, data:[row], errorDetail}. The parse/fetch/apply
    // core lives in the runtime assembly (SheetEnvelopeParser / SheetFetcher /
    // UnitStatApplier), shared with the in-build refresher.
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

        private void StartImport()
        {
            _requestInFlight = true;
            _statusLog = "Requesting...";

            SheetFetcher.FetchBoth(
                SheetEnvelopeParser.BuildSheetUrl(_baseUrl, _defenderSheet),
                SheetEnvelopeParser.BuildSheetUrl(_baseUrl, _enemySheet),
                (defenderFetch, enemyFetch) =>
                {
                    try
                    {
                        _statusLog = ApplyFetched(defenderFetch, enemyFetch);
                    }
                    finally
                    {
                        _requestInFlight = false;
                        Repaint();
                    }
                });
        }

        private string ApplyFetched(SheetFetcher.Result defenderFetch, SheetFetcher.Result enemyFetch)
        {
            var log = new StringBuilder();
            var defenders = SheetEnvelopeParser.ParseSheetLogged<DefenderStatDto>(
                defenderFetch.body, defenderFetch.transportError, _defenderSheet, log);
            var enemies = SheetEnvelopeParser.ParseSheetLogged<EnemyStatDto>(
                enemyFetch.body, enemyFetch.transportError, _enemySheet, log);

            var payload = UnitStatApplier.BuildPayload(defenders, enemies);
            if (payload == null) return log.ToString();
            return ApplyPayload(payload, log);
        }

        // Editor apply = shared core + AssetDatabase scan + per-asset disk save.
        internal static string ApplyPayload(UnitStatImportPayload payload, StringBuilder log = null)
        {
            log ??= new StringBuilder();
            var defendersById = UnitStatApplier.BuildIndex(
                UnitAssetScan.Enumerate<DefenderUnitData>(DefenderFolder), so => so.id, log, nameof(DefenderUnitData));
            var enemiesById = UnitStatApplier.BuildIndex(
                UnitAssetScan.Enumerate<AttackUnitData>(EnemyFolder), so => so.id, log, nameof(AttackUnitData));

            return UnitStatApplier.Apply(payload, defendersById, enemiesById, so =>
            {
                EditorUtility.SetDirty(so);
                AssetDatabase.SaveAssetIfDirty(so);
            }, log);
        }
    }
}
