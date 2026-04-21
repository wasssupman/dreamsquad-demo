# Path Carve Algorithm v1

**작업 구분**: Phase 10B

## 목적

Q-6 결정 반영: **각 spawn 별 독립 randomized Manhattan walk + BFS post-validation**. Multi-line × single-goal 에서 각 spawn 이 자기 경로를 가지고 goal 근처에서 수렴.

## 변경 대상

- 새 파일: `Assets/_Project/Scripts/Data/PathCarver.cs`

## 알고리즘

1. 각 spawn 에서 goal 까지 **Manhattan walk** 수행
2. 매 스텝 dx/dy 중 남은 거리가 큰 쪽을 우선, bias 완화용 random tie-break
3. 일정 확률 (p = 0.15) 로 **detour** (현재 방향과 수직으로 1 step) — 경로 곡선화
4. 방문 셀을 `MapTileType.Walk` 로 마킹 (중복 허용, 여러 spawn path 교차 가능)
5. 모든 spawn 완료 후 BFS 로 goal 에서 역방향 도달성 재검증 (9_multispawn_connectivity 재사용)

## 구현

```csharp
using Unity.Collections;
using Unity.Mathematics;

namespace Wassup.Data
{
    public static class PathCarver
    {
        public const float DetourProbability = 0.15f;
        public const int MaxStepsMultiplier = 4;  // manhattan(spawn, goal) * 4 까지 허용

        public static bool CarveAllSpawnsToGoal(
            ref Random rng,
            NativeArray<MapTileType> tiles,
            int2 gridSize,
            NativeArray<int2> spawns,
            int2 goal)
        {
            // goal 은 반드시 Walk
            SetTile(tiles, gridSize, goal, MapTileType.Walk);

            for (int s = 0; s < spawns.Length; s++)
            {
                if (!CarveSingleSpawn(ref rng, tiles, gridSize, spawns[s], goal))
                    return false;
            }
            return true;
        }

        private static bool CarveSingleSpawn(
            ref Random rng,
            NativeArray<MapTileType> tiles,
            int2 gridSize,
            int2 spawn,
            int2 goal)
        {
            int2 current = spawn;
            SetTile(tiles, gridSize, current, MapTileType.Walk);

            int manhattan = math.abs(goal.x - spawn.x) + math.abs(goal.y - spawn.y);
            int maxSteps = manhattan * MaxStepsMultiplier;

            for (int step = 0; step < maxSteps; step++)
            {
                if (current.x == goal.x && current.y == goal.y) return true;

                int2 dir = DecideStepDir(ref rng, current, goal);
                int2 next = current + dir;

                if (next.x < 0 || next.x >= gridSize.x || next.y < 0 || next.y >= gridSize.y)
                    continue;  // 경계 밖 skip, 다음 시도

                SetTile(tiles, gridSize, next, MapTileType.Walk);
                current = next;
            }
            return current.x == goal.x && current.y == goal.y;
        }

        private static int2 DecideStepDir(ref Random rng, int2 current, int2 goal)
        {
            int dx = goal.x - current.x;
            int dy = goal.y - current.y;

            // 일정 확률로 detour
            if (rng.NextFloat() < DetourProbability)
            {
                // 현재 경로와 수직 방향
                bool vertDetour = math.abs(dx) >= math.abs(dy);
                int sign = rng.NextBool() ? 1 : -1;
                return vertDetour ? new int2(0, sign) : new int2(sign, 0);
            }

            // goal 방향 (Manhattan)
            if (math.abs(dx) > math.abs(dy))
                return new int2(math.sign(dx), 0);
            if (math.abs(dy) > math.abs(dx))
                return new int2(0, math.sign(dy));
            // 동률이면 random tie-break
            return rng.NextBool()
                ? new int2(math.sign(dx), 0)
                : new int2(0, math.sign(dy));
        }

        private static void SetTile(NativeArray<MapTileType> tiles, int2 gridSize, int2 cell, MapTileType type)
        {
            int idx = cell.y * gridSize.x + cell.x;
            tiles[idx] = type;
        }
    }
}
```

## 불변조건 (Codex H-4 대응)

1. ✅ 모든 path cell 이 그리드 경계 안 — `next.x >= 0 && < gridSize.x` 체크
2. ✅ path 가 4-neighbor 연속 — Manhattan walk 는 항상 dx=±1 또는 dy=±1
3. ✅ ④⑤ 이후 Walk 셀을 덮어쓰지 않음 — ObstaclePlacer 는 `Place` 셀만 수정 (task 14)
4. ✅ BFS 재검증 — ProceduralMapGenerator 가 `MapConnectivity.AllSpawnsReachGoal` 호출

## 완료 기준

- 컴파일 0 errors.
- EditMode 테스트:
  - 동일 seed → 동일 Walk 셀 집합
  - 간단한 20×20 맵에서 2 spawn + 1 goal carve 후 `AllSpawnsReachGoal` == true
  - MaxSteps 초과 시 false 반환 (edge case: spawn 이 가장자리, goal 도 반대편, detour 과도)
- 확률 0.15 detour 가 매 run 다른 형태 생성 (결정성은 seed 당, variation 은 seed 간)
- M-4 note: detour 설명 ("1-2 step") 과 구현 ("1 step") 일치 — 현재 구현은 1 step detour 만. 문서 수정 완료.

## Subtask 분할 (OVERRUN 대응, 35분 예상)

- **12A** — `PathCarver.CarveAllSpawnsToGoal` + `CarveSingleSpawn` core loop
- **12B** — `DecideStepDir` (detour + Manhattan bias + random tie-break)
- **12C** — EditMode 테스트 (직선/장애물/연결성 유지/maxSteps 경계)
