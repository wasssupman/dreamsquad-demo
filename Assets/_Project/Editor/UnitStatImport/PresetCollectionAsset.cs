using System.Text;
using UnityEditor;
using Wassup.Data;

namespace Wassup.Editor.UnitStatImport
{
    // preset-sheet-import unit 2 — 단일 SquadPresetCollection.asset 로더(import·export 공용).
    // 없거나 복수면 로그로 고지하고 첫 번째를 쓴다(update-only 가 아니라 이 에셋 하나가 대상).
    internal static class PresetCollectionAsset
    {
        public static SquadPresetCollection Load(StringBuilder log)
        {
            var guids = AssetDatabase.FindAssets("t:SquadPresetCollection");
            if (guids.Length == 0) { log.AppendLine("[preset] SquadPresetCollection 에셋 없음."); return null; }
            if (guids.Length > 1) log.AppendLine($"[preset] SquadPresetCollection 복수({guids.Length}) — 첫 번째 사용.");
            return AssetDatabase.LoadAssetAtPath<SquadPresetCollection>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }
    }
}
