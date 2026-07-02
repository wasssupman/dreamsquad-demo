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

        private static string ApplyPayload(UnitStatImportPayload payload)
        {
            var log = new StringBuilder();
            int matched = 0, unmatched = 0, fieldsApplied = 0;

            foreach (var dto in payload?.defenders ?? Array.Empty<DefenderStatDto>())
            {
                var so = FindDefenderById(dto.id);
                if (so == null) { unmatched++; log.AppendLine($"[defender] no match for id='{dto.id}'"); continue; }
                fieldsApplied += UnitStatFieldMapper.ApplyNonNullFields(dto, so);
                EditorUtility.SetDirty(so);
                matched++;
            }

            foreach (var dto in payload?.enemies ?? Array.Empty<EnemyStatDto>())
            {
                var so = FindEnemyById(dto.id);
                if (so == null) { unmatched++; log.AppendLine($"[enemy] no match for id='{dto.id}'"); continue; }
                fieldsApplied += UnitStatFieldMapper.ApplyNonNullFields(dto, so);
                EditorUtility.SetDirty(so);
                matched++;
            }

            AssetDatabase.SaveAssets();
            log.Insert(0, $"Matched {matched}, unmatched {unmatched}, fields applied {fieldsApplied}.\n");
            return log.ToString();
        }

        private static DefenderUnitData FindDefenderById(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var guid in AssetDatabase.FindAssets("t:DefenderUnitData", new[] { DefenderFolder }))
            {
                var asset = AssetDatabase.LoadAssetAtPath<DefenderUnitData>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null && asset.id == id) return asset;
            }
            return null;
        }

        private static AttackUnitData FindEnemyById(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var guid in AssetDatabase.FindAssets("t:AttackUnitData", new[] { EnemyFolder }))
            {
                var asset = AssetDatabase.LoadAssetAtPath<AttackUnitData>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null && asset.id == id) return asset;
            }
            return null;
        }
    }
}
