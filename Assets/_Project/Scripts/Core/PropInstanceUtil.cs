using UnityEngine;
using Wassup.Data;
using Wassup.Presentation;

namespace Wassup.Core
{
    // 프랍 인스턴스 공용 후처리 헬퍼 (legacy-render-removal unit 0 — 구 MapView 에서 verbatim 추출).
    // TilemapMapView 가 쓰는 렌더 중립 유틸.
    internal static class PropInstanceUtil
    {
        internal static void DisablePropDebugMarkers(GameObject instance)
        {
            var renderers = instance.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                string n = renderers[i].gameObject.name.ToLowerInvariant();
                if (n.Contains("marker") || n.Contains("footprint") || n.Contains("debug") || n.Contains("bounds"))
                    renderers[i].gameObject.SetActive(false);
            }
        }

        internal static void ApplyPropSorting(
            GameObject instance,
            PropData prop,
            Wassup.Data.PropPlacement placement,
            BoardVisualPlan plan)
        {
            if (instance == null || prop == null)
                return;

            int order = prop.sortingOrder + BoardSortOrder.Compute(plan.gridSize, placement.x, placement.y);
            var renderers = instance.GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
                renderers[i].sortingOrder = order;
        }

        internal static void ApplyPropGlobalTint(GameObject instance, Color tint)
        {
            var renderers = instance.GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
                renderers[i].color = renderers[i].color * tint;
        }
    }
}
