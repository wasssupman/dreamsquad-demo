using Unity.Mathematics;
using UnityEngine;

namespace Wassup.Data
{
    public static class TerrainTileRuleResolver
    {
        public const float WalkOverlayHeight = 0.012f;
        public const float PlaceTopHeight = 0.018f;
        public const float WalkOverlayScale = 1.025f;

        public static TerrainTileRenderInfo Resolve(BoardVisualPlan plan, MapThemeData theme, BoardVisualCell cell, int x, int y, float placeScale)
        {
            if (plan == null)
                return TerrainTileRenderInfo.None;

            switch (cell.zoneType)
            {
                case BoardZoneType.Walk:
                    return new TerrainTileRenderInfo(
                        true,
                        TerrainSurfaceSelector.SelectTexture(plan, theme, cell, x, y),
                        GetBoardShapeYaw(cell.shapeClass),
                        WalkOverlayHeight,
                        WalkOverlayScale);

                case BoardZoneType.Place:
                    return new TerrainTileRenderInfo(
                        true,
                        TerrainSurfaceSelector.SelectTexture(plan, theme, cell, x, y),
                        0f,
                        PlaceTopHeight,
                        math.saturate(placeScale),
                        cell.envNeighborMask != 0 && theme != null && theme.placeEdgeTexture != null,
                        theme != null ? theme.placeEdgeTexture : null,
                        cell.envNeighborMask);

                default:
                    return TerrainTileRenderInfo.None;
            }
        }

        public static float GetBoardShapeYaw(BoardShapeType shape)
        {
            return shape switch
            {
                BoardShapeType.EndN => 90f,
                BoardShapeType.EndE => 180f,
                BoardShapeType.EndS => 270f,
                BoardShapeType.EndW => 0f,
                BoardShapeType.OuterCornerNE => 0f,
                BoardShapeType.OuterCornerNW => 270f,
                BoardShapeType.OuterCornerSE => 90f,
                BoardShapeType.OuterCornerSW => 180f,
                BoardShapeType.TJunctionN => 0f,
                BoardShapeType.TJunctionE => 90f,
                BoardShapeType.TJunctionS => 180f,
                BoardShapeType.TJunctionW => 270f,
                _ => 0f,
            };
        }
    }
}
