# Unit 2 — Goal/Spawn 배치기

## 목적

`(seed, gridSize, settings)` 입력으로 도착 셀 1개 + 활성 분면의 N(2~4) 개 스폰 셀을 결정한다. 결정성 보장 (동일 입력 → 동일 출력). Path 생성은 본 unit 범위 밖.

## 변경 대상

- 신설: `Assets/_Project/Scripts/Data/MapGrid/GoalSpawnPlacer.cs`
- 신설: `Assets/_Project/Tests/EditMode/MapGrid/GoalSpawnPlacerTests.cs`

## 구현

### 분면(Quadrant) 정의

W×H grid 를 중심 기준 4분면으로 분할:
- Q0 = top-left:     `x ∈ [0, W/2),     y ∈ [0, H/2)`
- Q1 = top-right:    `x ∈ [W/2, W),     y ∈ [0, H/2)`
- Q2 = bottom-left:  `x ∈ [0, W/2),     y ∈ [H/2, H)`
- Q3 = bottom-right: `x ∈ [W/2, W),     y ∈ [H/2, H)`

각 분면의 "외곽 코너 zone" = 분면의 그리드 바깥 코너 셀 기준 `cornerZoneRadius` 체비셰프 거리 이내의 셀 집합.
- Q0 코너 = (0, 0), zone = `{ (x, y) | 0 ≤ x < r, 0 ≤ y < r }` (r = cornerZoneRadius)
- Q1 코너 = (W-1, 0), zone = `{ (x, y) | W-r ≤ x < W, 0 ≤ y < r }`
- Q2 코너 = (0, H-1)
- Q3 코너 = (W-1, H-1)

### API

```csharp
namespace Wassup.Data.MapGrid
{
    public struct GoalSpawnResult
    {
        public int2 goal;
        public NativeArray<int2> spawns;  // length = N
        public int activeQuadrantMask;    // bit0=Q0 bit1=Q1 ...
        public bool IsValid;
    }

    public static class GoalSpawnPlacer
    {
        public static GoalSpawnResult Pick(
            ref Unity.Mathematics.Random rng,
            int2 gridSize,
            MapGridGenerationSettings settings,
            Allocator allocator)
        {
            // 1. goal: 중앙 체비셰프 ≤ 2 안에서 1셀.
            // rng.NextInt(min, max) 는 max-exclusive → [-2, 3) = {-2,-1,0,1,2}.
            // 모든 허용 프리셋(10×20, 20×20, 30×15) 에서 W,H ≥ 10 이므로 clamp 불필요 — 균등 분포 보존.
            int cx = gridSize.x / 2;
            int cy = gridSize.y / 2;
            int2 goal = new int2(cx + rng.NextInt(-2, 3), cy + rng.NextInt(-2, 3));

            // 2. spawnCount: [min, max] 안에서
            int spawnCount = rng.NextInt(settings.MinSpawnCount, settings.MaxSpawnCount + 1);

            // 3. 활성 분면 선택: 4개 중 spawnCount 개를 셔플 선택
            int activeMask = PickActiveQuadrants(ref rng, spawnCount); // 1~4 bit set

            // 4. N=1 명시 차단 (settings 가 OnValidate 로 Range(2,4) 강제하지만 reflection-set 방어).
            if (spawnCount < 2 || spawnCount > 4) return default;

            // 5. 각 활성 분면에서 corner zone 후보 셔플 후 distance 룰로 1셀
            var picked = new NativeList<int2>(spawnCount, Allocator.Temp);
            int goalDist = settings.EffectiveSpawnToGoalMinManhattan(gridSize);
            int sSDist = settings.SpawnToSpawnMinManhattan;
            for (int q = 0; q < 4; q++)
            {
                if (((activeMask >> q) & 1) == 0) continue;
                if (!TryPickFromQuadrant(ref rng, q, gridSize, settings.CornerZoneRadius,
                                         goal, goalDist, picked, sSDist, out int2 cell))
                {
                    picked.Dispose();
                    return default; // 실패 → outer 재시도
                }
                picked.Add(cell);
            }

            var spawns = new NativeArray<int2>(picked.Length, allocator);
            for (int i = 0; i < picked.Length; i++) spawns[i] = picked[i];
            picked.Dispose();

            return new GoalSpawnResult {
                goal = goal, spawns = spawns,
                activeQuadrantMask = activeMask, IsValid = true,
            };
        }

        // PickActiveQuadrants: Fisher-Yates 부분 셔플로 {0..3} 중 spawnCount 개
        // TryPickFromQuadrant: corner zone 셀 enumerate → 셔플 → goal/이미 뽑은 spawn 과 distance 룰 만족 첫 셀
        // (실패 시 다음 후보, zone 다 소진 시 false 반환)
    }
}
```

### EditMode 테스트

- `Pick_Deterministic_SameSeedSameResult`: 같은 seed/grid/settings → goal·spawns·mask 동일.
- `Pick_GoalWithinChebyshev2OfCenter`: 1000 seed 샘플, goal 의 `max(|gx-cx|, |gy-cy|) ≤ 2`.
- `Pick_SpawnCountWithinRange`: 1000 seed 샘플, spawnCount ∈ [min, max].
- `Pick_SpawnsInCornerZones`: 각 spawn 이 4 코너 중 하나의 zone(반경 cornerZoneRadius) 안.
- `Pick_DistanceRulesSatisfied`: spawn↔goal Manhattan ≥ effective threshold, spawn↔spawn Manhattan ≥ SpawnToSpawnMinManhattan.
- `Pick_SmallGrid10x20_StillProducesValid`: 작은 그리드(10×20)에서 100 seed 중 ≥ 95% 성공. `EffectiveSpawnToGoalMinManhattan(10,20) = max(6, 6) = 6` 으로 Tall preset 의 corner-zone vs 중앙 goal 기하 만족 가능.
- `Pick_RejectsSpawnCountOutsideRange`: `minSpawnCount=1` 을 reflection 으로 주입 후 Pick → `default` 반환.
- `Pick_DisposesSpawnsOnFailure`: 의도적으로 settings 를 빡빡하게 설정해 실패 강제 → `default` 반환, NativeArray 누수 없음.

## 완료 기준

- [ ] `GoalSpawnPlacer.Pick` 컴파일 + Burst 호환 (`[BurstCompile]` 어트리뷰트는 후속 unit 에서 라우터/validator 통합 시 검토).
- [ ] EditMode 테스트 7 케이스 모두 통과.
- [ ] 1000 seed 무작위 샘플에서 결과 정상 (logging 으로 분포 확인 — corner zone 4개에 spawn 이 균등 분포해야 함).
- [ ] 확인 일자 + 커밋 해시 (구현 후 채움):
