using UnityEditor;
using UnityEngine;
using Wassup.Data;

namespace Wassup.Editor
{
    // prop-area-pools unit 0 — 일회성 마이그레이션. 기존 단일 tileProps 풀을
    // 근경(playAreaProps)/원경(distantRingProps) 두 WeightedProp 리스트로 복사한다.
    // 기존 opt-out 규칙(excludeFromDistantRing / distantRingWeight) 을 계승해 초기 값 생성.
    // 멱등: 이미 채워진 리스트는 건드리지 않는다. unit 3 에서 이 파일과 소스 필드를 제거한다.
    public static class ThemePropPoolMigration
    {
        [MenuItem("Wassup/Dev/Migrate Theme Prop Pools")]
        public static void Migrate()
        {
            var guids = AssetDatabase.FindAssets("t:MapThemeData");
            int migrated = 0;
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var theme = AssetDatabase.LoadAssetAtPath<MapThemeData>(path);
                if (theme == null) continue;

                if (!MigrateTheme(theme, path)) continue;
                EditorUtility.SetDirty(theme);
                migrated++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[ThemePropPoolMigration] migrated {migrated}/{guids.Length} MapThemeData assets.");
        }

        private static bool MigrateTheme(MapThemeData theme, string path)
        {
            bool hasNear = theme.playAreaProps != null && theme.playAreaProps.Length > 0;
            bool hasRing = theme.distantRingProps != null && theme.distantRingProps.Length > 0;
            if (hasNear && hasRing)
            {
                Debug.Log($"[ThemePropPoolMigration] skip (already populated): {path}");
                return false;
            }
            if (theme.tileProps == null || theme.tileProps.Length == 0)
                return false;

            if (!hasNear)
            {
                var near = new System.Collections.Generic.List<WeightedProp>();
                foreach (var prop in theme.tileProps)
                {
                    if (prop == null) continue;
                    near.Add(new WeightedProp { prop = prop, weight = Mathf.Max(0, prop.placementWeight) });
                }
                theme.playAreaProps = near.ToArray();
            }

            if (!hasRing)
            {
                var ring = new System.Collections.Generic.List<WeightedProp>();
                foreach (var prop in theme.tileProps)
                {
                    if (prop == null || prop.excludeFromDistantRing) continue;
                    float w = prop.distantRingWeight >= 0f ? prop.distantRingWeight : Mathf.Max(0, prop.placementWeight);
                    ring.Add(new WeightedProp { prop = prop, weight = w });
                }
                theme.distantRingProps = ring.ToArray();
            }

            Debug.Log($"[ThemePropPoolMigration] {path}: play={theme.playAreaProps.Length}, ring={theme.distantRingProps.Length}");
            return true;
        }
    }
}
