using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    public class TerrainTileShapeUtilityTests
    {
        [Test]
        public void GetWalkShape_ReturnsStraightForOppositeNeighbors()
        {
            var map = CreateMap(3, 3);
            try
            {
                SetWalk(map, 1, 0);
                SetWalk(map, 1, 1);
                SetWalk(map, 1, 2);

                Assert.AreEqual(TerrainTileShape.StraightNS, TerrainTileShapeUtility.GetWalkShape(map, 1, 1));
            }
            finally
            {
                map.Dispose();
            }
        }

        [Test]
        public void GetWalkShape_ReturnsCornerForAdjacentNeighbors()
        {
            var map = CreateMap(3, 3);
            try
            {
                SetWalk(map, 1, 1);
                SetWalk(map, 2, 1);
                SetWalk(map, 1, 2);

                Assert.AreEqual(TerrainTileShape.CornerNE, TerrainTileShapeUtility.GetWalkShape(map, 1, 1));
            }
            finally
            {
                map.Dispose();
            }
        }

        [Test]
        public void GetWalkShape_ReturnsTJunctionForThreeNeighbors()
        {
            var map = CreateMap(3, 3);
            try
            {
                SetWalk(map, 1, 1);
                SetWalk(map, 0, 1);
                SetWalk(map, 2, 1);
                SetWalk(map, 1, 2);

                Assert.AreEqual(TerrainTileShape.TJunctionN, TerrainTileShapeUtility.GetWalkShape(map, 1, 1));
            }
            finally
            {
                map.Dispose();
            }
        }

        [Test]
        public void GetCardinalNeighborMask_EncodesNeighborTypes()
        {
            var map = CreateMap(3, 3);
            try
            {
                map.tiles[2 * map.gridSize.x + 1] = MapTileType.Env;
                map.tiles[1 * map.gridSize.x + 2] = MapTileType.Env;

                Assert.AreEqual(3, TerrainTileShapeUtility.GetCardinalNeighborMask(map, 1, 1, MapTileType.Env));
            }
            finally
            {
                map.Dispose();
            }
        }

        private static GeneratedMap CreateMap(int width, int height)
        {
            var gridSize = new int2(width, height);
            var tiles = new NativeArray<MapTileType>(width * height, Allocator.Persistent);
            for (int i = 0; i < tiles.Length; i++)
                tiles[i] = MapTileType.Place;

            return new GeneratedMap
            {
                tiles = tiles,
                spawns = new NativeArray<int2>(new[] { new int2(0, 0) }, Allocator.Persistent),
                gridSize = gridSize,
                goal = new int2(width - 1, height - 1),
                seed = 99,
            };
        }

        private static void SetWalk(GeneratedMap map, int x, int y)
        {
            map.tiles[y * map.gridSize.x + x] = MapTileType.Walk;
        }
    }
}
