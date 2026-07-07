using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

namespace LayerLab.ArtMaker
{
#if UNITY_EDITOR
    /// <summary>
    /// Project 창에서 캐릭터 프리팹의 미리보기를 저장된 썸네일로 표시
    /// Display saved thumbnail as character prefab preview in Project window
    /// </summary>
    [InitializeOnLoad]
    public static class CharacterPrefabPreview
    {
        private static readonly Dictionary<string, Texture2D> ThumbnailCache = new();

        static CharacterPrefabPreview()
        {
            EditorApplication.projectWindowItemOnGUI -= OnProjectWindowItemGUI;
            EditorApplication.projectWindowItemOnGUI += OnProjectWindowItemGUI;
        }

        private static void OnProjectWindowItemGUI(string guid, Rect selectionRect)
        {
            // 리스트 뷰(작은 아이콘)는 건너뜀
            // Skip list view (small icons)
            if (selectionRect.height <= 20) return;

            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.StartsWith("Assets/CharacterPrefabs/") || !path.EndsWith(".prefab")) return;

            var thumbnail = GetThumbnail(path);
            if (thumbnail == null) return;

            // 아이콘 영역 계산 (그리드 뷰에서 상단 정사각형 영역)
            // Calculate icon area (square area at top in grid view)
            float iconSize = selectionRect.width;
            var iconRect = new Rect(selectionRect.x, selectionRect.y, iconSize, iconSize);

            // 기본 프리뷰를 가리기 위해 배경을 먼저 채움
            // Fill background first to cover default preview
            EditorGUI.DrawRect(iconRect, new Color(0.2f, 0.2f, 0.2f, 1f));
            GUI.DrawTexture(iconRect, thumbnail, ScaleMode.ScaleToFit);
        }

        private static Texture2D GetThumbnail(string prefabPath)
        {
            var thumbnailPath = prefabPath.Replace(".prefab", "_Thumbnail.png");

            if (ThumbnailCache.TryGetValue(thumbnailPath, out var cached))
            {
                if (cached != null) return cached;
                ThumbnailCache.Remove(thumbnailPath);
            }

            var thumbnail = AssetDatabase.LoadAssetAtPath<Texture2D>(thumbnailPath);
            if (thumbnail != null)
            {
                ThumbnailCache[thumbnailPath] = thumbnail;
            }

            return thumbnail;
        }

        /// <summary>
        /// 썸네일 캐시 초기화
        /// Clear thumbnail cache
        /// </summary>
        public static void ClearCache()
        {
            ThumbnailCache.Clear();
        }
    }

    /// <summary>
    /// 새 썸네일 PNG가 임포트될 때 캐시를 자동 갱신
    /// Auto-refresh cache when new thumbnail PNGs are imported
    /// </summary>
    public class CharacterPrefabPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets, string[] deletedAssets,
            string[] movedAssets, string[] movedFromAssetPaths)
        {
            foreach (var path in importedAssets)
            {
                if (path.StartsWith("Assets/CharacterPrefabs/") && path.EndsWith("_Thumbnail.png"))
                {
                    CharacterPrefabPreview.ClearCache();
                    return;
                }
            }

            foreach (var path in deletedAssets)
            {
                if (path.StartsWith("Assets/CharacterPrefabs/"))
                {
                    CharacterPrefabPreview.ClearCache();
                    return;
                }
            }
        }
    }
#endif
}
