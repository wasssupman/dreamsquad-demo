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

        // multi-goal-map 유닛 3 — 분리 복도 각자 골. 단일 골이면 반대 복도 spawn 이 도달 불가라
        // false 였을 케이스가, 멀티-소스 BFS(goals 전체 시드)로 true 가 된다.
        [Test]
        public void AllSpawnsReachGoal_MultiGoal_SeparateCorridors_EachSpawnReachesOwnGoal_ReturnsTrue()
        {
            int w = 5, h = 3, n = w * h;
            var tiles = new NativeArray<MapTileType>(n, Allocator.Persistent);
            var spawns = new NativeArray<int2>(2, Allocator.Persistent);
            var goals = new NativeArray<int2>(2, Allocator.Persistent);
            try
            {
                for (int i = 0; i < n; i++) tiles[i] = MapTileType.Place;
                for (int x = 0; x < w; x++) { tiles[0 * w + x] = MapTileType.Walk; tiles[2 * w + x] = MapTileType.Walk; }
                spawns[0] = new int2(0, 0); spawns[1] = new int2(0, 2);
                goals[0] = new int2(w - 1, 0); goals[1] = new int2(w - 1, 2);

                var map = new GeneratedMap
                {
                    tiles = tiles, spawns = spawns, goals = goals,
                    gridSize = new int2(w, h), goal = goals[0],
                };
                Assert.IsTrue(MapConnectivity.AllSpawnsReachGoal(map));
            }
            finally
            {
                if (tiles.IsCreated) tiles.Dispose();
                if (spawns.IsCreated) spawns.Dispose();
                if (goals.IsCreated) goals.Dispose();
            }
        }

        [Test]
        public void AllSpawnsReachGoal_MultiGoal_SpawnIsolatedFromAllGoals_ReturnsFalse()
        {
            int w = 5, h = 3, n = w * h;
            var tiles = new NativeArray<MapTileType>(n, Allocator.Persistent);
            var spawns = new NativeArray<int2>(2, Allocator.Persistent);
            var goals = new NativeArray<int2>(1, Allocator.Persistent);
            try
            {
                for (int i = 0; i < n; i++) tiles[i] = MapTileType.Place;
                for (int x = 0; x < w; x++) tiles[0 * w + x] = MapTileType.Walk; // corridor A only
                tiles[2 * w + 0] = MapTileType.Walk;                              // isolated walk cell for spawn 2
                spawns[0] = new int2(0, 0); spawns[1] = new int2(0, 2);
                goals[0] = new int2(w - 1, 0);

                var map = new GeneratedMap
                {
                    tiles = tiles, spawns = spawns, goals = goals,
                    gridSize = new int2(w, h), goal = goals[0],
                };
                Assert.IsFalse(MapConnectivity.AllSpawnsReachGoal(map)); // spawn (0,2) 고립 → 어떤 골도 도달 불가
            }
            finally
            {
                if (tiles.IsCreated) tiles.Dispose();
                if (spawns.IsCreated) spawns.Dispose();
                if (goals.IsCreated) goals.Dispose();
            }
        }
    }
}
