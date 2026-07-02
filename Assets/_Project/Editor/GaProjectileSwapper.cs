using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Wassup.Data;

namespace Wassup.Editor
{
    // projectile-ga-reskin unit 3 helper.
    // Defender_Archer.projectile 를 GA 변종(Projectile_*_GA) 사이로 순환시켜
    // 플레이하며 빠르게 비교("바꿔가보자")할 수 있게 한다.
    public static class GaProjectileSwapper
    {
        private const string ArcherPath = "Assets/_Project/Data/Defenders/Defender_Archer.asset";
        private const string PixArrowPath = "Assets/_Project/Data/Projectiles/Projectile_Arrow.asset";
        private const string ProjectilesDir = "Assets/_Project/Data/Projectiles";

        [MenuItem("Wassup/VFX/Cycle Archer GA Projectile")]
        public static void CycleArcher()
        {
            var archer = AssetDatabase.LoadAssetAtPath<DefenderUnitData>(ArcherPath);
            if (archer == null) { Debug.LogWarning("[GaSwap] Defender_Archer 없음"); return; }

            var variants = GaVariants();
            if (variants.Count == 0) { Debug.LogWarning("[GaSwap] Projectile_*_GA 없음"); return; }

            int cur = archer.projectile != null ? variants.IndexOf(archer.projectile) : -1;
            var next = variants[(cur + 1) % variants.Count];
            archer.projectile = next;
            EditorUtility.SetDirty(archer);
            AssetDatabase.SaveAssets();
            Debug.Log($"[GaSwap] Archer → {next.name} " +
                      $"(scale={next.visualScale}, facing={next.facing}) [{variants.IndexOf(next) + 1}/{variants.Count}]");
        }

        [MenuItem("Wassup/VFX/Reset Archer to PixPlays Arrow")]
        public static void ResetArcher()
        {
            var archer = AssetDatabase.LoadAssetAtPath<DefenderUnitData>(ArcherPath);
            var pix = AssetDatabase.LoadAssetAtPath<ProjectileData>(PixArrowPath);
            if (archer == null || pix == null) { Debug.LogWarning("[GaSwap] asset 없음"); return; }
            archer.projectile = pix;
            EditorUtility.SetDirty(archer);
            AssetDatabase.SaveAssets();
            Debug.Log("[GaSwap] Archer → Projectile_Arrow (PixPlays 원복)");
        }

        private static List<ProjectileData> GaVariants()
        {
            return AssetDatabase.FindAssets("t:ProjectileData", new[] { ProjectilesDir })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => Path.GetFileNameWithoutExtension(p).EndsWith("_GA"))
                .OrderBy(p => p)
                .Select(AssetDatabase.LoadAssetAtPath<ProjectileData>)
                .Where(pd => pd != null)
                .ToList();
        }
    }
}
