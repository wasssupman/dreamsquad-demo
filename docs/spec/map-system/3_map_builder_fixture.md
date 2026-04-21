# BattleMapBuilder — PrototypeMap → GeneratedMap

**작업 구분**: Phase 10A

## 목적

Phase 10A 검증용. 기존 `PrototypeMap.asset` (`TileType` byte array) 를 새 `MapTileType` 기반 `GeneratedMap` 으로 변환. procedural 생성 없이 Phase 10A 만으로 검증 가능.

## 변경 대상

- 새 파일: `Assets/_Project/Scripts/Data/BattleMapBuilder.cs`

## 구현

```csharp
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Wassup.Data
{
    public static class BattleMapBuilder
    {
        // Phase 10A: 기존 MapData(PrototypeMap fixture)를 GeneratedMap으로 변환.
        // Phase 10B procedural 도입 후에도 fixture 기반 테스트 경로로 유지.
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

        // Phase 9 -> Phase 10 tile type 매핑. 8_prototype_map_migration.md 참조.
        private static MapTileType MapTile(TileType legacy) => legacy switch
        {
            TileType.Path      => MapTileType.Walk,
            TileType.Buildable => MapTileType.Place,
            TileType.Obstacle  => MapTileType.Deco,   // 배경 오브젝트 타일로 재해석
            _                  => MapTileType.Deco,
        };
    }
}
```

## 완료 기준

- `BattleMapBuilder.cs` 컴파일.
- EditMode 테스트: PrototypeMap 에 대해 `BuildFromFixture` 호출 → tiles 개수 200 (20×10) / spawns 개수 일치 / goal 일치.
- Dispose 후 NativeArray leak 없음 (테스트 `try/finally`).

> **L-2 note**: `BuildFromFixture` 는 legacy adapter only. procedural 경로는 `ProceduralMapGenerator.Generate` 사용. 이 패턴을 procedural 코드에 복사하지 말 것.

## Subtask 분할 (OVERRUN 대응, 25분 예상)

- **3A** — `BattleMapBuilder.BuildFromFixture` + `MapTile(TileType)` 매핑
- **3B** — EditMode 테스트 (tiles 개수, spawn/goal 일치, dispose)
- **3C** — legacy 매핑 주석 + L-2 복사 금지 문구
