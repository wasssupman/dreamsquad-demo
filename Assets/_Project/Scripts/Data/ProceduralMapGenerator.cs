using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using Random = Unity.Mathematics.Random;

namespace Wassup.Data
{
    public static class ProceduralMapGenerator
    {
        public const int MaxAttempts = 3;

        public static GeneratedMap Generate(
            int seed,
            int2 gridSize,
            MapThemeData theme,
            int generatorVersion,
            MapPathShape pathShape = MapPathShape.Free,
            int spawnLaneCount = 2,
            float minPlaceableRatio = -1f)
        {
            gridSize = new int2(math.max(4, gridSize.x), math.max(4, gridSize.y));
            spawnLaneCount = math.clamp(spawnLaneCount, 2, gridSize.y);

            for (int attempt = 0; attempt < MaxAttempts; attempt++)
            {
                var rng = new Random(HashSeed(seed, attempt, generatorVersion));
                var map = TryGenerate(ref rng, gridSize, theme, seed, generatorVersion, pathShape, spawnLaneCount, minPlaceableRatio);

                if (!map.IsCreated) continue;
                if (!MapConnectivity.AllSpawnsReachGoal(map))
                {
                    map.Dispose();
                    continue;
                }

                return map;
            }

            Debug.LogWarning($"[ProceduralMapGenerator] {MaxAttempts} attempts failed (seed={seed}). Falling back to linear.");
            return BattleMapBuilder.BuildFallbackLinear(gridSize, seed, generatorVersion, spawnLaneCount);
        }

        private static uint HashSeed(int baseSeed, int attempt, int generatorVersion)
        {
            unchecked
            {
                uint h = (uint)baseSeed;
                h ^= (uint)attempt * 2654435761u;
                h ^= (uint)generatorVersion * 374761393u;
                return h == 0 ? 1u : h;
            }
        }

        private static GeneratedMap TryGenerate(
            ref Random rng,
            int2 gridSize,
            MapThemeData theme,
            int seed,
            int version,
            MapPathShape pathShape,
            int spawnLaneCount,
            float minPlaceableRatio)
        {
            int n = gridSize.x * gridSize.y;
            NativeArray<MapTileType> tiles = default;
            NativeArray<int2> spawns = default;

            try
            {
                tiles = new NativeArray<MapTileType>(n, Allocator.Persistent);
                for (int i = 0; i < n; i++) tiles[i] = MapTileType.Place;

                var spawnList = DecideSpawnsAndGoal(ref rng, gridSize, spawnLaneCount, out int2 goal);
                spawns = new NativeArray<int2>(spawnList.Length, Allocator.Persistent);
                for (int i = 0; i < spawnList.Length; i++) spawns[i] = spawnList[i];

                if (!PathCarver.CarveAllSpawnsToGoal(ref rng, tiles, gridSize, spawns, goal, pathShape))
                {
                    tiles.Dispose();
                    spawns.Dispose();
                    return default;
                }

                ObstaclePlacer.Place(ref rng, tiles, gridSize, theme, minPlaceableRatio);

                return new GeneratedMap
                {
                    tiles = tiles,
                    gridSize = gridSize,
                    spawns = spawns,
                    goal = goal,
                    seed = seed,
                    generatorVersion = version,
                };
            }
            catch
            {
                if (tiles.IsCreated) tiles.Dispose();
                if (spawns.IsCreated) spawns.Dispose();
                throw;
            }
        }

        private static int2[] DecideSpawnsAndGoal(ref Random rng, int2 gridSize, int spawnCount, out int2 goal)
        {
            int goalY = gridSize.y / 2 + rng.NextInt(-2, 3);
            goal = new int2(gridSize.x - 1, math.clamp(goalY, 0, gridSize.y - 1));

            spawnCount = math.clamp(spawnCount, 2, math.max(2, (gridSize.y + 1) / 2));
            var result = new List<int2>();
            int span = math.max(1, gridSize.y - 2);
            for (int i = 0; i < spawnCount; i++)
            {
                int y = spawnCount == 1
                    ? gridSize.y / 2
                    : 1 + (int)math.round(i * (span / (float)(spawnCount - 1)));
                y = math.clamp(y, 0, gridSize.y - 1);
                if (i > 0) y = math.max(y, result[i - 1].y + 2);
                y = math.min(y, gridSize.y - 1);
                result.Add(new int2(0, y));
            }

            for (int i = result.Count - 2; i >= 0; i--)
            {
                int maxY = result[i + 1].y - 2;
                if (result[i].y <= maxY) continue;
                result[i] = new int2(0, math.max(0, maxY));
            }

            return result.ToArray();
        }
    }
}
