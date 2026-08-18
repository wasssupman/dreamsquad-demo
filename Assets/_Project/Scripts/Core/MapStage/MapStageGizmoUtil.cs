#if UNITY_EDITOR
using UnityEngine;
using Wassup.Data;

namespace Wassup.Core
{
    // map-diorama-stage unit 0 — 마커/footprint 기즈모 공용 헬퍼. 에디터 전용(빌드에 미포함).
    // 양자화는 반드시 MapStageMath 를 경유한다 — 기즈모가 보여주는 셀과 빌더가 굽는 셀이
    // 같은 산식이어야 "놓는 순간 차지 셀이 보인다"는 저작 계약이 성립한다.
    internal static class MapStageGizmoUtil
    {
        internal static bool TryGetStage(Component c, out MapStage stage)
        {
            stage = c.GetComponentInParent<MapStage>();
            return stage != null;
        }

        internal static Vector2Int CellOf(MapStage stage, Component c)
        {
            Vector3 local = stage.transform.InverseTransformPoint(c.transform.position);
            return MapStageMath.LocalToCell(local, stage.gridOriginLocal, stage.previewTileSize);
        }

        // fill 색으로 셀 바닥 쿼드 하나. playArea 밖 셀은 무채색으로 강등해 "논리에 안 들어감"을 보여준다.
        internal static void DrawCell(MapStage stage, Vector2Int cell, Color fill)
        {
            bool inside = MapStageMath.InPlayArea(cell, stage.playAreaCells);
            Gizmos.matrix = stage.transform.localToWorldMatrix;
            Gizmos.color = inside ? fill : new Color(0.5f, 0.5f, 0.5f, 0.35f);
            float t = stage.previewTileSize;
            Vector3 center = MapStageMath.CellCenterLocal(cell, stage.gridOriginLocal, t) + new Vector3(0f, 0.02f, 0f);
            Gizmos.DrawCube(center, new Vector3(t * 0.95f, 0.02f, t * 0.95f));
        }

        internal static void Label(MapStage stage, Vector2Int cell, string text)
        {
            Vector3 world = stage.transform.TransformPoint(
                MapStageMath.CellCenterLocal(cell, stage.gridOriginLocal, stage.previewTileSize));
            UnityEditor.Handles.Label(world + Vector3.up * 0.1f, text);
        }
    }
}
#endif
