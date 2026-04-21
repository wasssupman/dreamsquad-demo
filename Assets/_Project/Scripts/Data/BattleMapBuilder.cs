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

            var spawns = new NativeArray<int2>(source.SpawnCells.Count, Allocator.Persistent);
            for (int i = 0; i < source.SpawnCells.Count; i++)
            {
                var s = source.SpawnCells[i];
                spawns[i] = new int2(s.x, s.y);
            }

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
