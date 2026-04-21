using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using Wassup.Data;
using Random = Unity.Mathematics.Random;

namespace Wassup.Tests.EditMode
{
    public class ObstaclePlacerTests
    {
        [Test]
        public void Place_PreservesWalkAndMinimumPlaceRatio()
        {
            var theme = ScriptableObject.CreateInstance<MapThemeData>();
            var prefab = new GameObject("TestObstacle");
            theme.obstaclePrefabs = new[] { prefab };
            theme.minPlaceableRatio = 0.4f;

            var gridSize = new int2(10, 10);
            var tiles = new NativeArray<MapTileType>(100, Allocator.Temp);
            try
            {
                for (int i = 0; i < tiles.Length; i++) tiles[i] = MapTileType.Place;
                for (int x = 0; x < gridSize.x; x++) tiles[5 * gridSize.x + x] = MapTileType.Walk;

                int originalWalk = Count(tiles, MapTileType.Walk);
                int originalPlace = Count(tiles, MapTileType.Place);
                var rng = new Random(1u);

                ObstaclePlacer.Place(ref rng, tiles, gridSize, theme);

                Assert.AreEqual(originalWalk, Count(tiles, MapTileType.Walk));
                Assert.GreaterOrEqual(Count(tiles, MapTileType.Place), (int)math.ceil(originalPlace * theme.minPlaceableRatio));
            }
            finally
            {
                tiles.Dispose();
                Object.DestroyImmediate(prefab);
                Object.DestroyImmediate(theme);
            }
        }

        private static int Count(NativeArray<MapTileType> tiles, MapTileType type)
        {
            int count = 0;
            for (int i = 0; i < tiles.Length; i++)
                if (tiles[i] == type) count++;
            return count;
        }
    }
}
