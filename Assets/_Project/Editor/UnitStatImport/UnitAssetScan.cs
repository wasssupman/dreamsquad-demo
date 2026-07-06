using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Wassup.Editor.UnitStatImport
{
    // simplify pass (2026-07-06) — the one AssetDatabase scan shared by the
    // importer (index build) and the exporter (row dump).
    internal static class UnitAssetScan
    {
        internal static IEnumerable<T> Enumerate<T>(string folder) where T : ScriptableObject
        {
            foreach (var guid in AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { folder }))
            {
                var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null) yield return asset;
            }
        }
    }
}
