using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    public class MapConnectivityTests
    {
        [Test]
        public void AllSpawnsReachGoal_ReturnsTrueForConnectedWalkPath()
        {
            var map = BattleMapBuilder.BuildFallbackLinear(new int2(8, 6), 1, 1);
            try
            {
                Assert.IsTrue(MapConnectivity.AllSpawnsReachGoal(map));
            }
            finally { map.Dispose(); }
        }

        [Test]
        public void AllSpawnsReachGoal_ReturnsFalseForDisconnectedWalkPath()
        {
            var tiles = new NativeArray<MapTileType>(9, Allocator.Persistent);
            var spawns = new NativeArray<int2>(2, Allocator.Persistent);
            try
            {
                for (int i = 0; i < tiles.Length; i++) tiles[i] = MapTileType.Place;
                tiles[1 * 3 + 0] = MapTileType.Walk;
                tiles[0 * 3 + 0] = MapTileType.Walk;
                tiles[1 * 3 + 2] = MapTileType.Walk;
                spawns[0] = new int2(0, 1);
                spawns[1] = new int2(0, 0);

                var map = new GeneratedMap
                {
                    tiles = tiles,
                    spawns = spawns,
                    gridSize = new int2(3, 3),
                    goal = new int2(2, 1),
                };

                Assert.IsFalse(MapConnectivity.AllSpawnsReachGoal(map));
            }
            finally
            {
                if (tiles.IsCreated) tiles.Dispose();
                if (spawns.IsCreated) spawns.Dispose();
            }
        }
    }
}
