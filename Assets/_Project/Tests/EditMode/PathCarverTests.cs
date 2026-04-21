using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using Wassup.Data;
using Random = Unity.Mathematics.Random;

namespace Wassup.Tests.EditMode
{
    public class PathCarverTests
    {
        [Test]
        public void CarveAllSpawnsToGoal_CreatesReachableWalkCells()
        {
            var gridSize = new int2(20, 20);
            var tiles = new NativeArray<MapTileType>(gridSize.x * gridSize.y, Allocator.Temp);
            var spawns = new NativeArray<int2>(2, Allocator.Temp);
            try
            {
                for (int i = 0; i < tiles.Length; i++) tiles[i] = MapTileType.Place;
                spawns[0] = new int2(0, 4);
                spawns[1] = new int2(0, 14);
                var rng = new Random(123u);
                var goal = new int2(19, 10);

                Assert.IsTrue(PathCarver.CarveAllSpawnsToGoal(ref rng, tiles, gridSize, spawns, goal));

                var map = new GeneratedMap
                {
                    tiles = tiles,
                    gridSize = gridSize,
                    spawns = spawns,
                    goal = goal,
                };
                Assert.IsTrue(MapConnectivity.AllSpawnsReachGoal(map));
            }
            finally
            {
                tiles.Dispose();
                spawns.Dispose();
            }
        }

        [Test]
        public void StraightShape_CarvesOnlyManhattanSegmentCells()
        {
            var gridSize = new int2(8, 8);
            var tiles = new NativeArray<MapTileType>(gridSize.x * gridSize.y, Allocator.Temp);
            var spawns = new NativeArray<int2>(1, Allocator.Temp);
            try
            {
                for (int i = 0; i < tiles.Length; i++) tiles[i] = MapTileType.Place;
                spawns[0] = new int2(0, 1);
                var rng = new Random(456u);
                var goal = new int2(5, 4);

                Assert.IsTrue(PathCarver.CarveAllSpawnsToGoal(ref rng, tiles, gridSize, spawns, goal, MapPathShape.Straight));

                int walkCount = 0;
                for (int i = 0; i < tiles.Length; i++)
                    if (tiles[i] == MapTileType.Walk) walkCount++;

                int manhattan = math.abs(goal.x - spawns[0].x) + math.abs(goal.y - spawns[0].y);
                Assert.AreEqual(manhattan + 1, walkCount);
            }
            finally
            {
                tiles.Dispose();
                spawns.Dispose();
            }
        }

        [Test]
        public void StraightShape_CarvesSeparatedBranchNodesIntoSharedRoot()
        {
            var gridSize = new int2(12, 10);
            var tiles = new NativeArray<MapTileType>(gridSize.x * gridSize.y, Allocator.Temp);
            var spawns = new NativeArray<int2>(3, Allocator.Temp);
            try
            {
                for (int i = 0; i < tiles.Length; i++) tiles[i] = MapTileType.Place;
                spawns[0] = new int2(0, 1);
                spawns[1] = new int2(0, 4);
                spawns[2] = new int2(0, 7);
                var rng = new Random(789u);
                var goal = new int2(11, 4);

                Assert.IsTrue(PathCarver.CarveAllSpawnsToGoal(ref rng, tiles, gridSize, spawns, goal, MapPathShape.Straight));

                int trunkX = (gridSize.x * 2) / 3;
                Assert.AreEqual(MapTileType.Walk, tiles[1 * gridSize.x + trunkX], "branch node 0");
                Assert.AreEqual(MapTileType.Walk, tiles[4 * gridSize.x + trunkX], "branch node 1 / merge root");
                Assert.AreEqual(MapTileType.Walk, tiles[7 * gridSize.x + trunkX], "branch node 2");
                Assert.AreEqual(MapTileType.Walk, tiles[4 * gridSize.x + goal.x], "goal root");
                Assert.GreaterOrEqual(math.abs(spawns[1].y - spawns[0].y), 2);
                Assert.GreaterOrEqual(math.abs(spawns[2].y - spawns[1].y), 2);
            }
            finally
            {
                tiles.Dispose();
                spawns.Dispose();
            }
        }
    }
}
