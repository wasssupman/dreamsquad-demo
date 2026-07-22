using System.Text;
using UnityEditor;
using UnityEngine;
using Wassup.Core;
using Wassup.Data;
using Wassup.Data.PresetImport;
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

        // sheet-export-push unit 4 — Apps Script /exec URL. 쓰기 권한 secret 이라
        // 프로젝트에 커밋하지 않고 에디터 로컬(EditorPrefs)에만 둔다.
        private const string ScriptUrlPrefsKey = "Wassup.UnitStatImport.ScriptUrl";

        // preset-sheet-import unit 2 — Presets 탭(신규). 위치 기반 list-SoT + id→SO 참조 해석이라
        // 8탭의 keyed-upsert 와 별개 경로(applier 가 collection.presets 를 통째 재구성).
        private const string PresetTabPrefsKey = "Wassup.UnitStatImport.PresetSheet";
        private const string DefaultPresetTab = "Presets";

        private string _baseUrl = "";
        private string _defenderSheet = "";
        private string _enemySheet = "";
        private string _dcSheets = "";
        private string _scriptUrl = "";
        private string _presetTab = "";
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
            _scriptUrl = EditorPrefs.GetString(ScriptUrlPrefsKey, "");
            _presetTab = EditorPrefs.GetString(PresetTabPrefsKey, DefaultPresetTab);
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
            }
            // review L1 — export is local SO → disk; it needs tab names, not the API URL.
            using (new EditorGUI.DisabledScope(_requestInFlight || dcTabs == null))
            {
                if (GUILayout.Button("Export Dreamcatcher SO → JSON Files"))
                {
                    string folder = EditorUtility.SaveFolderPanel("Export Dreamcatcher JSON", "", "");
                    if (!string.IsNullOrEmpty(folder))
                        _statusLog = DcSheetExporter.ExportToFolder(folder, dcTabs, DcFolder, SkillFolder);
                }
                // unit 8 — 시트 반영용 단일 파일(탭명 키 병합) + 챗봇 프롬프트 동시 출력.
                if (GUILayout.Button("Export Dreamcatcher → 시트 페이로드 (1파일 + 프롬프트)"))
                {
                    string path = EditorUtility.SaveFilePanel(
                        "Export Dreamcatcher 시트 페이로드", "", "dreamcatcher_sheet_payload", "json");
                    if (!string.IsNullOrEmpty(path))
                        _statusLog = DcSheetExporter.ExportCombinedFile(path, dcTabs, DcFolder, SkillFolder);
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Push to Sheet (전 8탭)", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            _scriptUrl = EditorGUILayout.TextField("Apps Script URL", _scriptUrl);
            if (EditorGUI.EndChangeCheck()) EditorPrefs.SetString(ScriptUrlPrefsKey, _scriptUrl);
            EditorGUILayout.LabelField("  쓰기 권한 secret — 커밋 금지", EditorStyles.miniLabel);

            // 유닛 탭(Defenders/Enemies) + DC 6탭을 한 번에 시트로 push. dcTabs 는 위에서
            // 계산된 것을 재사용. URL·탭 입력이 온전할 때만 활성.
            using (new EditorGUI.DisabledScope(_requestInFlight
                || string.IsNullOrWhiteSpace(_scriptUrl)
                || string.IsNullOrWhiteSpace(_defenderSheet) || string.IsNullOrWhiteSpace(_enemySheet)
                || dcTabs == null))
            {
                if (GUILayout.Button(_requestInFlight ? "..." : "Push to Sheet"))
                {
                    if (EditorUtility.DisplayDialog("Push to Sheet",
                        "전 8탭(Defenders/Enemies + DC 6)을 시트에 업서트합니다.\n"
                        + "고아 행(시트엔 있고 SO 엔 없음)은 삭제하지 않고 리포트만 합니다.\n계속할까요?",
                        "Push", "취소"))
                    {
                        StartPush(dcTabs);
                    }
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Preset", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            _presetTab = EditorGUILayout.TextField("Preset Sheet", _presetTab);
            if (EditorGUI.EndChangeCheck()) EditorPrefs.SetString(PresetTabPrefsKey, _presetTab);

            using (new EditorGUI.DisabledScope(_requestInFlight
                || string.IsNullOrWhiteSpace(_baseUrl) || string.IsNullOrWhiteSpace(_presetTab)))
            {
                if (GUILayout.Button(_requestInFlight ? "Importing..." : "Import Preset"))
                {
                    _requestInFlight = true;
                    _statusLog = "Requesting...";
                    RunPresetImport(_baseUrl, _presetTab, result =>
                    {
                        _statusLog = result;
                        _requestInFlight = false;
                        Repaint();
                    });
                }
            }
            // export = 로컬 SO → disk. 현 프리셋을 시트 시드로 뽑는다(API URL 불요).
            using (new EditorGUI.DisabledScope(_requestInFlight || string.IsNullOrWhiteSpace(_presetTab)))
            {
                if (GUILayout.Button("Export Preset SO → JSON (시트 시드)"))
                {
                    string path = EditorUtility.SaveFilePanel("Export Preset 시트 시드", "", _presetTab.Trim(), "json");
                    if (!string.IsNullOrEmpty(path))
                        _statusLog = PresetSheetExporter.ExportToFile(path);
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
            // review H2 — onDone must fire even when apply throws, or the window's
            // _requestInFlight sticks until a domain reload (same guarantee the
            // unit path gets from its try/finally).
            SheetFetcher.FetchAll(urls, results =>
            {
                string result;
                try { result = ApplyDcFetched(results, tabNames); }
                catch (System.Exception e) { result = $"Import failed: {e}"; }
                onDone(result);
            });
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

            // review (architect #3) — surface each tab's SoT mode so "can I delete
            // this row?" never depends on remembering the spec.
            log.AppendLine($"[mode] {tabs[1]}/{tabs[3]}: sheet-SoT (rows rebuild arrays; deleting a row deletes the effect) · {tabs[2]}: Unity-SoT (values only).");

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

        // preset-sheet-import unit 2 — 1탭 fetch → parse → PresetSheetApplier → collection 저장.
        // onDone 은 apply 예외에도 발화(RunDcImport 동일 보장) → _requestInFlight 고착 방지.
        internal static void RunPresetImport(string baseUrl, string tab, System.Action<string> onDone)
        {
            var url = SheetEnvelopeParser.BuildSheetUrl(baseUrl, tab);
            SheetFetcher.Fetch(url, result =>
            {
                string res;
                try { res = ApplyPresetFetched(result, tab); }
                catch (System.Exception e) { res = $"Import failed: {e}"; }
                onDone(res);
            });
        }

        // 시트 = list-SoT: collection.presets 를 통째 재구성. id→SO 는 AssetDatabase 스캔 인덱스.
        // rows=null(fetch 실패/빈) 또는 컬렉션 없으면 no-op(applier 가 리스트 보존).
        internal static string ApplyPresetFetched(SheetFetcher.Result r, string tab)
        {
            var log = new StringBuilder();
            var rows = SheetEnvelopeParser.ParseSheetLogged<PresetDto>(r.body, r.transportError, tab, log);
            if (rows == null) return log.ToString();

            var collection = PresetCollectionAsset.Load(log);
            if (collection == null) return log.ToString();

            var unitsById = UnitStatApplier.BuildIndex(
                UnitAssetScan.Enumerate<DefenderUnitData>(DefenderFolder), so => so.id, log, nameof(DefenderUnitData));
            var cardsById = UnitStatApplier.BuildIndex(
                UnitAssetScan.Enumerate<DreamcatcherCard>(DcFolder), so => so.id, log, nameof(DreamcatcherCard));

            bool changed = PresetSheetApplier.Apply(rows,
                id => unitsById.TryGetValue(id, out var u) ? u : null,
                id => cardsById.TryGetValue(id, out var c) ? c : null,
                SquadSave.SlotCount, collection, log);

            if (changed)
            {
                EditorUtility.SetDirty(collection);
                AssetDatabase.SaveAssets();
            }
            return log.ToString();
        }

        // sheet-export-push unit 4 — 전 8탭 push. payload 조립(동기, 자체 try/catch) 후
        // SheetPushClient.Push(비동기). 콜백은 성공/거부/전송오류/예외 모두에서 발화하므로
        // _requestInFlight 가 물리지 않는다(import 버튼과 동일 보장).
        private void StartPush(string[] dcTabs)
        {
            _requestInFlight = true;
            _statusLog = "Building payload...";
            Repaint();

            string payload;
            try
            {
                payload = SheetPushPayload.BuildCombinedJson(
                    _defenderSheet, _enemySheet, DefenderFolder, EnemyFolder,
                    dcTabs, DcFolder, SkillFolder);
            }
            catch (System.Exception e)
            {
                _statusLog = $"Payload build failed: {e}";
                _requestInFlight = false;
                Repaint();
                return;
            }

            _statusLog = "Pushing...";
            SheetPushClient.Push(_scriptUrl, payload, report =>
            {
                _statusLog = report;
                _requestInFlight = false;
                Repaint();
            });
        }
    }
}
