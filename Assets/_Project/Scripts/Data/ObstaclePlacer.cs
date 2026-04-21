using Unity.Collections;
using Unity.Mathematics;
using Random = Unity.Mathematics.Random;

namespace Wassup.Data
{
    public static class ObstaclePlacer
    {
        public static void Place(
            ref Random rng,
            NativeArray<MapTileType> tiles,
            int2 gridSize,
            MapThemeData theme,
            float minPlaceableRatio = -1f)
        {
            if (theme == null || theme.obstaclePrefabs == null || theme.obstaclePrefabs.Length == 0) return;

            int n = gridSize.x * gridSize.y;
            int placeCount = 0;
            for (int i = 0; i < n; i++)
                if (tiles[i] == MapTileType.Place) placeCount++;

            float ratio = minPlaceableRatio >= 0f ? minPlaceableRatio : theme.minPlaceableRatio;
            int minPlace = (int)math.ceil(placeCount * math.clamp(ratio, 0.2f, 0.8f));
            int convertCount = placeCount - minPlace;
            if (convertCount <= 0) return;

            var placeIndices = new NativeList<int>(placeCount, Allocator.Temp);
            try
            {
                for (int i = 0; i < n; i++)
                    if (tiles[i] == MapTileType.Place) placeIndices.Add(i);

                for (int i = 0; i < convertCount; i++)
                {
                    int j = rng.NextInt(i, placeIndices.Length);
                    int tmp = placeIndices[i];
                    placeIndices[i] = placeIndices[j];
                    placeIndices[j] = tmp;
                    tiles[placeIndices[i]] = MapTileType.Deco;
                }
            }
            finally
            {
                if (placeIndices.IsCreated) placeIndices.Dispose();
            }
        }
    }
}
