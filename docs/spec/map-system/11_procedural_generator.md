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
// C-5 fix: Random 모호성 제거 — Unity.Mathematics.Random 만 사용
using Random = Unity.Mathematics.Random;

namespace Wassup.Data
{
    public static class ProceduralMapGenerator
    {
        public const int MaxAttempts = 3;
        // C-4 fix: CurrentGeneratorVersion 상수 삭제.
        // generatorVersion 은 caller (BattleBridge via MapGenerationSettings) 가 파라미터로 전달.

        // seed: EffectiveSeed from MapGenerationSettings
        // gridSize: X×Y 가변 (기본 20×20)
        // theme: MapThemeData (Phase 10B task 13)
        // generatorVersion: MapGenerationSettings.generatorVersion 단일 소스
        // returns: GeneratedMap (caller owns dispose). 실패 시 fallback 직선 맵.
        public static GeneratedMap Generate(int seed, int2 gridSize, MapThemeData theme, int generatorVersion)
        {
            for (int attempt = 0; attempt < MaxAttempts; attempt++)
            {
                uint rngSeed = HashSeed(seed, attempt, generatorVersion);
                var rng = new Random(rngSeed);

                var map = TryGenerate(ref rng, gridSize, theme, seed, generatorVersion);

                if (!map.IsCreated) continue;
                if (!MapConnectivity.AllSpawnsReachGoal(map))
                {
                    map.Dispose();
                    continue;
                }
                return map;
            }

            Debug.LogWarning($"[ProceduralMapGenerator] 3 attempts failed (seed={seed}). Falling back to linear.");
            return BattleMapBuilder.BuildFallbackLinear(gridSize, seed, generatorVersion);
        }

        private static uint HashSeed(int baseSeed, int attempt, int generatorVersion)
        {
            unchecked
            {
                uint h = (uint)baseSeed;
                h ^= (uint)attempt * 2654435761u;
                h ^= (uint)generatorVersion * 374761393u;
                return h == 0 ? 1u : h;  // Unity.Mathematics.Random 은 seed=0 금지
            }
        }

        private static GeneratedMap TryGenerate(ref Random rng, int2 gridSize, MapThemeData theme, int seed, int version)
        {
            int n = gridSize.x * gridSize.y;
            NativeArray<MapTileType> tiles = default;
            NativeArray<int2>        spawns = default;
            try
            {
                tiles = new NativeArray<MapTileType>(n, Allocator.Persistent);

                // 1. 초기화: 모두 Place
                for (int i = 0; i < n; i++) tiles[i] = MapTileType.Place;

                // 2. spawn N개 + goal 1개 결정 (theme 기본 규칙 또는 hardcoded)
                var spawnList = DecideSpawnsAndGoal(ref rng, gridSize, out int2 goal);
                spawns = new NativeArray<int2>(spawnList.Length, Allocator.Persistent);
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
            catch
            {
                // M-2 fix: exception 경로에서 Persistent array leak 방지
                if (tiles.IsCreated)  tiles.Dispose();
                if (spawns.IsCreated) spawns.Dispose();
                throw;
            }
        }

        // H-3 fix: spawn 중복 방지 — 최소 수직 간격 유지
        private static int2[] DecideSpawnsAndGoal(ref Random rng, int2 gridSize, out int2 goal)
        {
            // v1 heuristic: goal = 우측 가장자리 중앙 ± 2
            int goalY = gridSize.y / 2 + rng.NextInt(-2, 3);
            goal = new int2(gridSize.x - 1, math.clamp(goalY, 0, gridSize.y - 1));

            // 2~3 spawns, 좌측 가장자리 (x=0), 최소 수직 간격 2 유지
            int spawnCount = rng.NextInt(2, 4);
            int minGap = math.max(2, gridSize.y / (spawnCount * 2));
            var used = new System.Collections.Generic.HashSet<int>();
            var result = new System.Collections.Generic.List<int2>();

            int maxTries = spawnCount * 10;
            while (result.Count < spawnCount && maxTries-- > 0)
            {
                int y = rng.NextInt(0, gridSize.y);
                bool conflict = false;
                foreach (int uy in used)
                    if (math.abs(uy - y) < minGap) { conflict = true; break; }
                if (!conflict)
                {
                    used.Add(y);
                    result.Add(new int2(0, y));
                }
            }

            // 실패 시 균등 분포 fallback (결정적)
            if (result.Count < spawnCount)
            {
                result.Clear();
                int step = gridSize.y / spawnCount;
                for (int i = 0; i < spawnCount; i++)
                    result.Add(new int2(0, i * step + step / 2));
            }

            return result.ToArray();
        }
    }
}
```

## 결정성

- 동일 `seed + gridSize + theme.obstaclePrefabs.Length + generatorVersion` 파라미터 → 동일 `GeneratedMap`
- `Unity.Mathematics.Random(uint)` 은 Xorshift128 결정적
- `HashSeed` 는 attempt 간 독립 RNG 분기

## 완료 기준

- 컴파일 0 errors.
- EditMode 테스트: 동일 seed + theme + generatorVersion 으로 2회 Generate → tiles/spawns/goal 전부 바이트 레벨 동일.
- 3회 재시도 후 실패 시 fallback 맵 반환 확인 (tiles.Length == gridSize.x * gridSize.y).
- PathCarver / ObstaclePlacer 는 별도 task (12/14) — 이 task 는 entry point + 흐름만.
- spawn 중복 없음 (H-3): `result[i].y != result[j].y` for i != j, 최소 간격 2 유지.
- TryGenerate exception 경로에서 Persistent array leak 없음 (M-2).

## Subtask 분할 (OVERRUN 대응, 45분 예상)

- **11A** — `Generate(seed, gridSize, theme, generatorVersion)` 시그니처 + `HashSeed` + Random alias using
- **11B** — `TryGenerate` try/catch leak 방지 + `DecideSpawnsAndGoal` 중복 방지
- **11C** — 결정성 EditMode 테스트 (same seed + version → same map)
