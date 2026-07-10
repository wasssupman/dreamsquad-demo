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

        // dreamcatcher-sheet-sync unit 3 — DC tab names are contract-fixed
        // (0_json_schema_contract.md); prefs only exist for ad-hoc experiments.
        private const string DcSheetsPrefsKey = "Wassup.UnitStatImport.DcSheets";
        private const string DefaultDcSheets = "DcCards,DcCardEffects,DcMechanics,DcAttackMods,DcSkills,DcConfig";
        private const string DcFolder = "Assets/_Project/Data/Dreamcatcher";
        private const string SkillFolder = "Assets/_Project/Data/Skills";

        private string _baseUrl = "";
        private string _defenderSheet = "";
        private string _enemySheet = "";
        private string _dcSheets = "";
        private string _statusLog = "";
        private bool _requestInFlight;

        [MenuItem("Window/Wassup/Unit Stat Import")]
        public static void Open() => GetWindow<UnitStatImportWindow>("Unit Stat Import");

        private void OnEnable()
        {
            _baseUrl = EditorPrefs.GetString(BaseUrlPrefsKey, DefaultBaseUrl);
            _defenderSheet = EditorPrefs.GetString(DefenderSheetPrefsKey, "Defenders");
            _enemySheet = EditorPrefs.GetString(EnemySheetPrefsKey, "Enemies");
            _dcSheets = EditorPrefs.GetString(DcSheetsPrefsKey, DefaultDcSheets);
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
            EditorGUILayout.LabelField("Dreamcatcher", EditorStyles.boldLabel);
            // unit 3 — six contract tabs edited as one comma list (rarely touched).
            EditorGUI.BeginChangeCheck();
            _dcSheets = EditorGUILayout.TextField("DC Sheets (6)", _dcSheets);
            if (EditorGUI.EndChangeCheck()) EditorPrefs.SetString(DcSheetsPrefsKey, _dcSheets);

            string[] dcTabs = SplitDcSheets(_dcSheets);
            using (new EditorGUI.DisabledScope(_requestInFlight
                || string.IsNullOrWhiteSpace(_baseUrl) || dcTabs == null))
            {
                if (GUILayout.Button(_requestInFlight ? "Importing..." : "Import Dreamcatcher"))
                {
                    _requestInFlight = true;
                    _statusLog = "Requesting...";
                    RunDcImport(_baseUrl, dcTabs, result =>
                    {
                        _statusLog = result;
                        _requestInFlight = false;
                        Repaint();
                    });
                }
                if (GUILayout.Button("Export Dreamcatcher SO → JSON Files"))
                {
                    string folder = EditorUtility.SaveFolderPanel("Export Dreamcatcher JSON", "", "");
                    if (!string.IsNullOrEmpty(folder))
                        _statusLog = DcSheetExporter.ExportToFolder(folder, dcTabs, DcFolder, SkillFolder);
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Result", EditorStyles.boldLabel);
            EditorGUILayout.TextArea(_statusLog, GUILayout.MinHeight(120));
        }

        internal static string[] SplitDcSheets(string commaList)
        {
            var parts = (commaList ?? "").Split(',');
            if (parts.Length != 6) return null;
            for (int i = 0; i < parts.Length; i++)
            {
                parts[i] = parts[i].Trim();
                if (parts[i].Length == 0) return null;
            }
            return parts;
        }

        // unit 3 — fetch 6 tabs → parse → DcSheetApplier, shared by the window
        // button and headless verification (one-shot MenuItem pattern).
        internal static void RunDcImport(string baseUrl, string[] tabNames, System.Action<string> onDone)
        {
            var urls = new string[tabNames.Length];
            for (int i = 0; i < tabNames.Length; i++)
                urls[i] = SheetEnvelopeParser.BuildSheetUrl(baseUrl, tabNames[i]);
            SheetFetcher.FetchAll(urls, results => onDone(ApplyDcFetched(results, tabNames)));
        }

        internal static string ApplyDcFetched(SheetFetcher.Result[] r, string[] tabs)
        {
            var log = new StringBuilder();
            var payload = new DcSheetPayload
            {
                cards = SheetEnvelopeParser.ParseSheetLogged<DcCardDto>(r[0].body, r[0].transportError, tabs[0], log),
                cardEffects = SheetEnvelopeParser.ParseSheetLogged<DcCardEffectDto>(r[1].body, r[1].transportError, tabs[1], log),
                mechanics = SheetEnvelopeParser.ParseSheetLogged<DcMechanicDto>(r[2].body, r[2].transportError, tabs[2], log),
                attackMods = SheetEnvelopeParser.ParseSheetLogged<DcAttackModDto>(r[3].body, r[3].transportError, tabs[3], log),
                skills = SheetEnvelopeParser.ParseSheetLogged<DcSkillDto>(r[4].body, r[4].transportError, tabs[4], log),
                configs = SheetEnvelopeParser.ParseSheetLogged<DcConfigDto>(r[5].body, r[5].transportError, tabs[5], log),
            };
            if (payload.cards == null && payload.cardEffects == null && payload.mechanics == null
                && payload.attackMods == null && payload.skills == null && payload.configs == null)
                return log.ToString();

            var cardsById = UnitStatApplier.BuildIndex(
                UnitAssetScan.Enumerate<DreamcatcherCard>(DcFolder), so => so.id, log, nameof(DreamcatcherCard));
            var skillsById = UnitStatApplier.BuildIndex(
                UnitAssetScan.Enumerate<SkillData>(SkillFolder), so => so.id, log, nameof(SkillData));
            var configsById = new System.Collections.Generic.Dictionary<string, ScriptableObject>();
            foreach (var kv in UnitStatApplier.BuildIndex(
                         UnitAssetScan.Enumerate<AwakeningConfig>(DcFolder), so => so.id, log, nameof(AwakeningConfig)))
                configsById[kv.Key] = kv.Value;
            foreach (var kv in UnitStatApplier.BuildIndex(
                         UnitAssetScan.Enumerate<DeckRuleConfig>(DcFolder), so => so.id, log, nameof(DeckRuleConfig)))
            {
                if (!configsById.TryAdd(kv.Key, kv.Value))
                    log.AppendLine($"[dc-config] id '{kv.Key}' exists on two config types — DeckRuleConfig skipped.");
            }

            return DcSheetApplier.Apply(payload, cardsById, skillsById, configsById, so =>
            {
                EditorUtility.SetDirty(so);
                AssetDatabase.SaveAssetIfDirty(so);
            }, log);
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
