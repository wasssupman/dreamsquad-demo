using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Wassup.Data
{
    public static class BattleMapBuilder
    {
        // Phase 10A: 기존 MapData(PrototypeMap fixture)를 GeneratedMap으로 변환.
        // Phase 10B procedural 도입 후에도 fixture 기반 테스트 경로로 유지.
        // legacy adapter only — procedural 코드에 이 패턴 복사 금지 (L-2 note).
        public static GeneratedMap BuildFromFixture(MapData source, int seed = 0, int generatorVersion = 0)
        {
            int w = MapData.Width;   // 현재 20
            int h = MapData.Height;  // 현재 10
            int n = w * h;

            var tiles = new NativeArray<MapTileType>(n, Allocator.Persistent);
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                var legacy = source.GetTile(x, y);
                tiles[y * w + x] = MapTile(legacy);
            }

            int spawnCount = math.max(2, source.SpawnCells.Count);
            var spawns = new NativeArray<int2>(spawnCount, Allocator.Persistent);
            for (int i = 0; i < source.SpawnCells.Count; i++)
            {
                var s = source.SpawnCells[i];
                spawns[i] = new int2(s.x, s.y);
            }
            for (int i = source.SpawnCells.Count; i < spawnCount; i++)
                spawns[i] = FindAdditionalWalkSpawn(tiles, new int2(w, h), spawns, i, new int2(source.GoalCell.x, source.GoalCell.y));

            return new GeneratedMap
            {
                tiles = tiles,
                gridSize = new int2(w, h),
                spawns = spawns,
                goal = new int2(source.GoalCell.x, source.GoalCell.y),
                seed = seed,
                generatorVersion = generatorVersion,
            };
        }

        public static GeneratedMap BuildFallbackLinear(int2 gridSize, int seed = 0, int generatorVersion = 0, int spawnLaneCount = 2)
        {
            int w = math.max(4, gridSize.x);
            int h = math.max(4, gridSize.y);
            int n = w * h;
            spawnLaneCount = math.clamp(spawnLaneCount, 2, math.max(2, (h + 1) / 2));
            int goalY = h / 2;

            var tiles = new NativeArray<MapTileType>(n, Allocator.Persistent);
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                tiles[y * w + x] = MapTileType.Place;
            }

            var laneYs = new NativeArray<int>(spawnLaneCount, Allocator.Temp);
            int minY = h - 1;
            int maxY = 0;
            int span = math.max(1, h - 2);
            for (int i = 0; i < spawnLaneCount; i++)
            {
                int y = 1 + (int)math.round(i * (span / (float)(spawnLaneCount - 1)));
                y = math.clamp(y, 0, h - 1);
                if (i > 0) y = math.max(y, laneYs[i - 1] + 2);
                y = math.min(y, h - 1);
                laneYs[i] = y;
                minY = math.min(minY, y);
                maxY = math.max(maxY, y);
                for (int x = 0; x < w; x++)
                    tiles[y * w + x] = MapTileType.Walk;
            }
            for (int y = minY; y <= maxY; y++)
                tiles[y * w + (w - 1)] = MapTileType.Walk;

            var spawns = new NativeArray<int2>(spawnLaneCount, Allocator.Persistent);
            for (int i = 0; i < spawnLaneCount; i++)
                spawns[i] = new int2(0, laneYs[i]);
            laneYs.Dispose();

            // multi-goal-map 유닛 0: 라이브 안전망이라 goals 를 명시 세팅([goal]).
            var goals = new NativeArray<int2>(1, Allocator.Persistent);
            goals[0] = new int2(w - 1, goalY);

            return new GeneratedMap
            {
                tiles = tiles,
                gridSize = new int2(w, h),
                spawns = spawns,
                goal = new int2(w - 1, goalY),
                goals = goals,
                seed = seed,
                generatorVersion = generatorVersion,
            };
        }

        public static GeneratedMap BuildFromManual(ManualMapInput input, int seed = 0, int generatorVersion = 0)
        {
            if (input.gridSize.x <= 0 || input.gridSize.y <= 0)
            {
                Debug.LogError("[BattleMapBuilder] BuildFromManual requires positive gridSize.");
                return default;
            }
            if (input.walkCells == null || input.walkCells.Length == 0)
            {
                Debug.LogError("[BattleMapBuilder] BuildFromManual requires at least one walk cell.");
                return default;
            }
            if (input.spawns == null || input.spawns.Length < 2)
            {
                Debug.LogError("[BattleMapBuilder] BuildFromManual requires at least two spawns.");
                return default;
            }
            if (!MapConnectivity.InBounds(input.goal, input.gridSize))
            {
                Debug.LogError($"[BattleMapBuilder] BuildFromManual goal {input.goal} outside gridSize {input.gridSize}.");
                return default;
            }
            for (int i = 0; i < input.spawns.Length; i++)
            {
                if (MapConnectivity.InBounds(input.spawns[i], input.gridSize)) continue;
                Debug.LogError($"[BattleMapBuilder] BuildFromManual spawn[{i}] {input.spawns[i]} outside gridSize {input.gridSize}.");
                return default;
            }

            int n = input.gridSize.x * input.gridSize.y;
            var tiles = new NativeArray<MapTileType>(n, Allocator.Persistent);
            for (int i = 0; i < n; i++) tiles[i] = MapTileType.Place;

            MarkCells(tiles, input.gridSize, input.walkCells, MapTileType.Walk);
            MarkCells(tiles, input.gridSize, input.envCells, MapTileType.Env);
            MarkCells(tiles, input.gridSize, input.decoCells, MapTileType.Deco);

            if (input.placeCells != null && input.placeCells.Length > 0)
            {
                var placeSet = new System.Collections.Generic.HashSet<int>();
                foreach (var cell in input.placeCells)
                {
                    if (!MapConnectivity.InBounds(cell, input.gridSize)) continue;
                    placeSet.Add(cell.y * input.gridSize.x + cell.x);
                }

                for (int i = 0; i < n; i++)
                    if (tiles[i] == MapTileType.Place && !placeSet.Contains(i))
                        tiles[i] = MapTileType.Deco;
            }

            var spawns = new NativeArray<int2>(input.spawns.Length, Allocator.Persistent);
            for (int i = 0; i < input.spawns.Length; i++) spawns[i] = input.spawns[i];

            return new GeneratedMap
            {
                tiles = tiles,
                gridSize = input.gridSize,
                spawns = spawns,
                goal = input.goal,
                seed = seed,
                generatorVersion = generatorVersion,
            };
        }

        private static void MarkCells(NativeArray<MapTileType> tiles, int2 gridSize, int2[] cells, MapTileType type)
        {
            if (cells == null) return;
            foreach (var cell in cells)
            {
                if (!MapConnectivity.InBounds(cell, gridSize)) continue;
                tiles[cell.y * gridSize.x + cell.x] = type;
            }
        }

        private static int2 FindAdditionalWalkSpawn(
            NativeArray<MapTileType> tiles,
            int2 gridSize,
            NativeArray<int2> existing,
            int existingCount,
            int2 goal)
        {
            for (int y = 0; y < gridSize.y; y++)
            for (int x = 0; x < gridSize.x; x++)
            {
                var candidate = new int2(x, y);
                if (math.all(candidate == goal)) continue;
                if (tiles[y * gridSize.x + x] != MapTileType.Walk) continue;

                bool duplicate = false;
                for (int i = 0; i < existingCount; i++)
                {
                    if (!math.all(existing[i] == candidate)) continue;
                    duplicate = true;
                    break;
                }
                if (!duplicate) return candidate;
            }

            return existingCount > 0 ? existing[0] : goal;
        }

        // Phase 9 TileType -> Phase 10 MapTileType 매핑.
        // Buildable → Place (defender 배치)
        // Path      → Walk  (적 이동)
        // Obstacle  → Deco  (배경 오브젝트, flow 차단)
        // 참조: docs/spec/map-system/8_prototype_map_migration.md
        private static MapTileType MapTile(TileType legacy) => legacy switch
        {
            TileType.Path      => MapTileType.Walk,
            TileType.Buildable => MapTileType.Place,
            TileType.Obstacle  => MapTileType.Deco,
            _                  => MapTileType.Deco,
        };
    }
}
