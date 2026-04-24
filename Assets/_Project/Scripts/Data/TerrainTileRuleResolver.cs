using Unity.Mathematics;
using UnityEngine;

namespace Wassup.Data
{
    public static class TerrainTileRuleResolver
    {
        public const float WalkOverlayHeight = 0.012f;
        public const float PlaceTopHeight = 0.018f;
        public const float WalkOverlayScale = 1.025f;

        public static TerrainTileRenderInfo Resolve(GeneratedMap map, MapThemeData theme, int x, int y, float placeScale)
        {
            if (!map.IsCreated)
                return TerrainTileRenderInfo.None;

            var type = map.TileAt(new int2(x, y));
            return Resolve(map, theme, type, x, y, placeScale);
        }

        public static TerrainTileRenderInfo Resolve(
            GeneratedMap map,
            MapThemeData theme,
            MapTileType type,
            int x,
            int y,
            float placeScale)
        {
            switch (type)
            {
                case MapTileType.Walk:
                    return new TerrainTileRenderInfo(
                        true,
                        TerrainSurfaceSelector.SelectTexture(map, theme, type, x, y),
                        GetWalkYaw(map, x, y),
                        WalkOverlayHeight,
                        WalkOverlayScale);

                case MapTileType.Place:
                    int backgroundMask = TerrainTileShapeUtility.GetBackgroundNeighborMask(map, x, y);
                    return new TerrainTileRenderInfo(
                        true,
                        TerrainSurfaceSelector.SelectTexture(map, theme, type, x, y),
                        0f,
                        PlaceTopHeight,
                        math.saturate(placeScale),
                        backgroundMask != 0 && theme != null && theme.placeBackgroundEdgeTexture != null,
                        theme != null ? theme.placeBackgroundEdgeTexture : null,
                        backgroundMask);

                default:
                    return TerrainTileRenderInfo.None;
            }
        }

        public static bool IsContinuousTerrainTile(MapTileType type)
        {
            return type == MapTileType.Env || type == MapTileType.Deco;
        }

        public static Texture2D SelectContinuousTerrainTexture(MapThemeData theme)
        {
            if (theme == null)
                return null;

            if (theme.envSurfaceRules != null)
            {
                for (int i = 0; i < theme.envSurfaceRules.Length; i++)
                {
                    if (theme.envSurfaceRules[i] != null && theme.envSurfaceRules[i].texture != null)
                        return theme.envSurfaceRules[i].texture;
                }
            }

            if (theme.envTileTexture != null)
                return theme.envTileTexture;

            if (theme.envTileVariants != null)
            {
                for (int i = 0; i < theme.envTileVariants.Length; i++)
                {
                    if (theme.envTileVariants[i] != null)
                        return theme.envTileVariants[i];
                }
            }

            return null;
        }

        public static float GetWalkYaw(GeneratedMap map, int x, int y)
        {
            var shape = TerrainTileShapeUtility.GetWalkShape(map, x, y);
            return shape switch
            {
                TerrainTileShape.EndN => 90f,
                TerrainTileShape.EndE => 180f,
                TerrainTileShape.EndS => 270f,
                TerrainTileShape.EndW => 0f,
                TerrainTileShape.CornerNE => 0f,
                TerrainTileShape.CornerNW => 270f,
                TerrainTileShape.CornerSE => 90f,
                TerrainTileShape.CornerSW => 180f,
                TerrainTileShape.TJunctionN => 0f,
                TerrainTileShape.TJunctionE => 90f,
                TerrainTileShape.TJunctionS => 180f,
                TerrainTileShape.TJunctionW => 270f,
                _ => 0f,
            };
        }
    }
}
