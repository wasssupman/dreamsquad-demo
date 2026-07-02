using System;
using System.Text;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
using Wassup.Data;

namespace Wassup.Editor.UnitStatImport
{
    // unit-stat-spreadsheet-schema Unit 1 — fetches the spreadsheet-authored stat
    // payload from the REST API and applies it to existing Defender/Enemy SO assets.
    // Update-only by id match; never creates new .asset files.
    public class UnitStatImportWindow : EditorWindow
    {
        private const string UrlPrefsKey = "Wassup.UnitStatImport.Url";
        private const string DefenderFolder = "Assets/_Project/Data/Defenders";
        private const string EnemyFolder = "Assets/_Project/Data/Enemies";

        private string _url = "";
        private string _statusLog = "";
        private bool _requestInFlight;

        [MenuItem("Window/Wassup/Unit Stat Import")]
        public static void Open() => GetWindow<UnitStatImportWindow>("Unit Stat Import");

        private void OnEnable()
        {
            _url = EditorPrefs.GetString(UrlPrefsKey, "");
            // hotfix ③ — serialized true survives a domain reload while the
            // completed callback does not; reset so the Import button never sticks.
            _requestInFlight = false;
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Unit Stat Import", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            _url = EditorGUILayout.TextField("API URL", _url);
            if (EditorGUI.EndChangeCheck())
            {
                EditorPrefs.SetString(UrlPrefsKey, _url);
            }

            using (new EditorGUI.DisabledScope(_requestInFlight || string.IsNullOrWhiteSpace(_url)))
            {
                if (GUILayout.Button(_requestInFlight ? "Importing..." : "Import"))
                {
                    StartImport(_url);
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Result", EditorStyles.boldLabel);
            EditorGUILayout.TextArea(_statusLog, GUILayout.MinHeight(120));
        }

        private void StartImport(string url)
        {
            _requestInFlight = true;
            _statusLog = "Requesting...";

            var request = UnityWebRequest.Get(url);
            var operation = request.SendWebRequest();
            operation.completed += _ => OnRequestComplete(request);
        }

        private void OnRequestComplete(UnityWebRequest request)
        {
            try
            {
                if (request.result != UnityWebRequest.Result.Success)
                {
                    _statusLog = $"Request failed: {request.error}";
                    return;
                }

                UnitStatImportPayload payload;
                try
                {
                    payload = JsonConvert.DeserializeObject<UnitStatImportPayload>(request.downloadHandler.text);
                }
                catch (Exception e)
                {
                    _statusLog = $"JSON parse failed: {e.Message}";
                    return;
                }

                _statusLog = ApplyPayload(payload);
            }
            finally
            {
                request.Dispose();
                _requestInFlight = false;
                Repaint();
            }
        }

        internal static string ApplyPayload(UnitStatImportPayload payload)
        {
            var log = new StringBuilder();
            int matched = 0, unmatched = 0, fieldsApplied = 0, projected = 0, skipped = 0;

            var defendersById = BuildAssetIndex<DefenderUnitData>(DefenderFolder, so => so.id, log);
            var enemiesById = BuildAssetIndex<AttackUnitData>(EnemyFolder, so => so.id, log);

            var seenIds = new System.Collections.Generic.HashSet<string>();
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
        private static System.Collections.Generic.Dictionary<string, T> BuildAssetIndex<T>(
            string folder, Func<T, string> idSelector, StringBuilder log) where T : ScriptableObject
        {
            var byId = new System.Collections.Generic.Dictionary<string, T>();
            var ambiguous = new System.Collections.Generic.HashSet<string>();
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
