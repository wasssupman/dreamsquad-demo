# Unit 3 — Incremental Path Builder

## 목적

`GoalSpawnResult` 를 입력받아 각 spawn 을 **기존 path 셀에 attach** 하는 incremental 방식으로 직선 경로를 그린다. 합류 셀(degree ≥ 3) 은 attach 선택의 결과로 **emergent** 발생. 곡선 없음, 90° 꺾임만, 2×2 path block 금지. 참고 구현은 `_reference_algorithm.md` 의 TS 코드.

## 변경 대상

- 신설: `Assets/_Project/Scripts/Data/MapGrid/PathRouter.cs`
- 신설: `Assets/_Project/Scripts/Data/MapGrid/IncrementalPathBuilder.cs`
- 신설: `Assets/_Project/Tests/EditMode/MapGrid/IncrementalPathBuilderTests.cs`

## 구현

### 좌표 키 표현

TS 구현의 `Set<string>` 은 C# 에서 `NativeHashSet<int>` 로. `cellIndex(int2 c) = c.y * gridSize.x + c.x`. 인덱스 ↔ int2 변환 helper 는 `MapGridIndex` static class 에 둔다.

### `PathRouter` (후보 경로 생성기)

```csharp
public static class PathRouter
{
    // L-shape(2 corner) + U-shape(3 corner) 후보 점열을 생성.
    // 반환은 NativeList<NativeList<int2>> 대신, 미드포인트 (mx,my) 와 shape tag 를 가진 struct 배열로 표현해 alloc 절감.
    public struct RouteCandidate
    {
        public byte shape;          // 0:L_via_attachX, 1:L_via_attachY, 2:U_h, 3:U_v, 4:Z_h, 5:Z_v
        public int2 mid;            // shape 별 mx/my 의미 다름
    }

    public static void EnumerateCandidates(
        ref Unity.Mathematics.Random rng,
        int2 start, int2 attach, int2 gridSize, int midpointSamples,
        NativeList<RouteCandidate> outCandidates) { /* shape 0/1 1회 + shape 2~5 midpointSamples 회 */ }

    public static bool TryExpandCandidate(
        in RouteCandidate cand, int2 start, int2 attach, int2 gridSize,
        NativeList<int> outRouteCellIndices) { /* 점열 → lineKeys 연결, 인접 중복 셀 제거 */ }
}
```

### `IncrementalPathBuilder`

```csharp
public struct PathBuildResult : IDisposable
{
    public NativeHashSet<int> pathCells;    // index = y*W + x
    public NativeArray<int>   spawnOrder;   // findRoute 순서 (디버그용)
    public bool IsValid;

    // Dispose 계약: IsValid 와 무관하게 항상 안전. 미할당 NativeHashSet/Array 도 IsCreated 체크 후 skip.
    public void Dispose()
    {
        if (pathCells.IsCreated) pathCells.Dispose();
        if (spawnOrder.IsCreated) spawnOrder.Dispose();
    }
}

public static class IncrementalPathBuilder
{
    public static PathBuildResult Build(
        ref Unity.Mathematics.Random rng,
        int2 gridSize,
        int2 goal,
        NativeArray<int2> spawns,            // GoalSpawnPlacer 결과
        MapGridGenerationSettings settings,
        Allocator allocator)
    {
        var path = new NativeHashSet<int>(gridSize.x * gridSize.y / 4, allocator);
        path.Add(CellIndex(goal, gridSize));

        // 첫 spawn → goal 직접
        // 이후 spawn → attach 후보(path 셀 중 spawn/goal 제외 + degree ≤ 2) 중 셔플 선택
        for (int i = 0; i < spawns.Length; i++)
        {
            bool ok = TryFindRoute(ref rng, spawns[i], firstRoute: i == 0,
                                    gridSize, goal, spawns, path, settings);
            if (!ok) { /* path.Dispose(); return invalid; */ }
        }

        return new PathBuildResult { pathCells = path, IsValid = true, ... };
    }

    static bool TryFindRoute(
        ref Unity.Mathematics.Random rng,
        int2 start, bool firstRoute,
        int2 gridSize, int2 goal, NativeArray<int2> spawns,
        NativeHashSet<int> path, MapGridGenerationSettings settings)
    {
        // attach 후보 enumerate
        // for attempt in [0, settings.MaxRouteAttempts):
        //   pick attach 셀
        //   PathRouter.EnumerateCandidates(...)
        //   for cand in candidates:
        //     expand → routeCells
        //     IsValidRoute(routeCells, attach, path, spawns, ownSpawn) ?
        //       → path 에 routeCells[0 .. n-2] 추가 (마지막 셀=attach 는 이미 path 에 있음)
        //       → return true
        // return false
    }

    static bool IsValidRoute(...)
    {
        // 1. routeCells.Length >= 2
        // 2. routeCells[0] == start, routeCells[last] == attach
        // 3. path 가 attach 셀 포함
        // 4. routeCells 내부 중복 없음 (HashSet 으로 확인)
        // 5. for k in routeCells[0 .. n-2]:
        //      - !path.Contains(k)
        //      - k 가 다른 spawn 셀이면 false
        //      - k 의 4방향 이웃 중 path 와 닿는 셀은 (k == lastNew && neighbor == attach) 한 경우만 허용
        // 6. (path ∪ routeCells[0 .. n-2]) 에 2×2 block 없음
    }
}
```

### 핵심 invariant (테스트로 lock-in)

- 모든 path 셀의 degree (이웃 path 셀 수) 는 ∈ {1, 2, 3, 4}.
- goal 의 degree = 1 (직접 라우팅 후 다른 spawn 이 goal 에 attach 안 함 → `getAttachCandidates` 에서 goal 제외).
- 각 spawn 의 degree = 1.
- 합류 셀 = degree ≥ 3 인 비 spawn/goal path 셀.

## EditMode 테스트

- `Build_TwoSpawn_ProducesTree`: spawn 2개, goal 1개 → path 가 단일 연결성분, goal/spawn degree=1.
- `Build_FourSpawn_HasAtLeastOneMerge`: spawn 4개 → degree ≥ 3 셀이 ≥ 1.
- `Build_NoTwoByTwoBlock`: 1000 seed 결과 path 에 2×2 정사각형 없음.
- `Build_AllCellsAxisAligned`: 경로 segment 가 항상 dx=0 or dy=0.
- `Build_Deterministic`: 같은 seed + 같은 spawns 입력 → 같은 path 셀 집합.
- `Build_SmallGrid10x20_Succeeds`: 100 seed 중 ≥ 90 % 성공.
- `Build_FailsGracefullyWhenBoxed`: 의도적으로 spawn 4개를 corner zone 좁게 + minBranchCellCount 과대 → `IsValid=false`, NativeHashSet 누수 없음.

## 완료 기준

- [ ] `PathRouter`/`IncrementalPathBuilder` 컴파일 (Burst 호환은 후속 task — 본 unit 은 Managed 허용).
- [ ] EditMode 테스트 7 케이스 모두 통과.
- [ ] 1000 seed 무작위 실행에서 평균 attempt 수와 실패율 로깅 (성공률 ≥ 90 % 목표).
- [ ] `_reference_algorithm.md` 의 TS 코드와 알고리즘 형태 동치 (RNG 알고리즘이 다르므로 출력 셀 집합 동치는 불가). 대신 **결정성 snapshot 테스트**: `(seed=0, gridSize=Wide30x15, 임의의 고정 spawns)` 입력에 대한 path 셀 집합 해시를 EditMode 테스트가 hard-coded 기댓값으로 검증.
- [ ] 확인 일자 + 커밋 해시 (구현 후 채움):
