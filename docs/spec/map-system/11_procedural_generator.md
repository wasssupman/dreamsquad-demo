# ProceduralMapGenerator

**작업 구분**: Phase 10B

## 목적

Seed + gridSize + theme 을 받아 `GeneratedMap` 을 생성. 사용자 bullet 1 "매 판 seed 기반 랜덤 절차적 생성" 의 entry point.

## 변경 대상

- 새 파일: `Assets/_Project/Scripts/Data/ProceduralMapGenerator.cs`

## 구현

```csharp
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Wassup.Data
{
    public static class ProceduralMapGenerator
    {
        public const int MaxAttempts = 3;
        public const int CurrentGeneratorVersion = 1;

        // seed: EffectiveSeed from MapGenerationSettings
        // gridSize: X×Y 가변 (기본 20×20)
        // theme: MapThemeData (Phase 10B task 13)
        // returns: GeneratedMap (caller owns dispose). 실패 시 fallback 직선 맵.
        public static GeneratedMap Generate(int seed, int2 gridSize, MapThemeData theme)
        {
            for (int attempt = 0; attempt < MaxAttempts; attempt++)
            {
                uint rngSeed = HashSeed(seed, attempt);
                var rng = new Random(rngSeed);

                var map = TryGenerate(ref rng, gridSize, theme, seed, CurrentGeneratorVersion);

                if (!map.IsCreated) continue;
                if (!MapConnectivity.AllSpawnsReachGoal(map))
                {
                    map.Dispose();
                    continue;
                }
                return map;
            }

            Debug.LogWarning($"[ProceduralMapGenerator] 3 attempts failed (seed={seed}). Falling back to linear.");
            return BattleMapBuilder.BuildFallbackLinear(gridSize, seed, CurrentGeneratorVersion);
        }

        private static uint HashSeed(int baseSeed, int attempt)
        {
            unchecked
            {
                uint h = (uint)baseSeed;
                h ^= (uint)attempt * 2654435761u;
                h ^= (uint)CurrentGeneratorVersion * 374761393u;
                return h == 0 ? 1u : h;  // Unity.Mathematics.Random 은 seed=0 금지
            }
        }

        private static GeneratedMap TryGenerate(ref Random rng, int2 gridSize, MapThemeData theme, int seed, int version)
        {
            int n = gridSize.x * gridSize.y;
            var tiles = new NativeArray<MapTileType>(n, Allocator.Persistent);

            // 1. 초기화: 모두 Place
            for (int i = 0; i < n; i++) tiles[i] = MapTileType.Place;

            // 2. spawn N개 + goal 1개 결정 (theme 기본 규칙 또는 hardcoded)
            var spawnList = DecideSpawnsAndGoal(ref rng, gridSize, out int2 goal);
            var spawns = new NativeArray<int2>(spawnList.Length, Allocator.Persistent);
            for (int i = 0; i < spawnList.Length; i++) spawns[i] = spawnList[i];

            // 3. Path carve (task 12): 각 spawn 별 독립 Manhattan walk → Walk 마킹
            if (!PathCarver.CarveAllSpawnsToGoal(ref rng, tiles, gridSize, spawns, goal))
            {
                tiles.Dispose();
                spawns.Dispose();
                return default;
            }

            // 4. Obstacle placer (task 14): Walk/Place 비침범 셀에 Env/Deco 타일 배치
            ObstaclePlacer.Place(ref rng, tiles, gridSize, theme);

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

        private static int2[] DecideSpawnsAndGoal(ref Random rng, int2 gridSize, out int2 goal)
        {
            // v1 heuristic: goal = 우측 가장자리 중앙 ± 2, spawns = 좌측 가장자리 2~3개
            int goalY = gridSize.y / 2 + rng.NextInt(-2, 3);
            goal = new int2(gridSize.x - 1, math.clamp(goalY, 0, gridSize.y - 1));

            int spawnCount = rng.NextInt(2, 4);  // 2~3 spawns
            var spawns = new int2[spawnCount];
            for (int i = 0; i < spawnCount; i++)
            {
                int y = rng.NextInt(0, gridSize.y);
                spawns[i] = new int2(0, y);
            }
            return spawns;
        }
    }
}
```

## 결정성

- 동일 `seed + gridSize + theme.obstaclePrefabs.Length + CurrentGeneratorVersion` → 동일 `GeneratedMap`
- `Unity.Mathematics.Random(uint)` 은 Xorshift128 결정적
- `HashSeed` 는 attempt 간 독립 RNG 분기

## 완료 기준

- 컴파일 0 errors.
- EditMode 테스트: 동일 seed + theme 으로 2회 Generate → tiles/spawns/goal 전부 바이트 레벨 동일.
- 3회 재시도 후 실패 시 fallback 맵 반환 확인 (tiles.Length == gridSize.x * gridSize.y).
- PathCarver / ObstaclePlacer 는 별도 task (12/14) — 이 task 는 entry point + 흐름만.
