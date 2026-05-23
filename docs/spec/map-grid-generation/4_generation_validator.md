# Unit 4 — Validator + Reject 루프

## 목적

`IncrementalPathBuilder.Build` 결과를 종합 검증하고, 실패 시 attempt 카운터 증가 후 re-seed. 전체 outer 루프(`maxMapAttempts`) 안에서 `(goal/spawn placement) → (path build) → (validator)` 가 1 attempt. 모든 attempt 실패 시 명시적 `MapGenerationFailedException` 던짐 (현재 `ProceduralMapGenerator` 의 silent fallback 과 다른 정책).

## 변경 대상

- 신설: `Assets/_Project/Scripts/Data/MapGrid/MapGridValidator.cs`
- 신설: `Assets/_Project/Scripts/Data/MapGrid/MapGridGenerator.cs` (outer 루프 오케스트레이터)
- 신설: `Assets/_Project/Scripts/Data/MapGrid/MapGenerationFailedException.cs`
- 신설: `Assets/_Project/Tests/EditMode/MapGrid/MapGridValidatorTests.cs`
- 신설: `Assets/_Project/Tests/EditMode/MapGrid/MapGridGeneratorTests.cs`

## 구현

### Validator

```csharp
public static class MapGridValidator
{
    public enum FailReason : byte
    {
        Ok = 0,
        Disconnected = 1,
        GoalDegreeNotOne = 2,
        SpawnDegreeNotOne = 3,
        HasTwoByTwoBlock = 4,
        BranchTooShort = 5,
        BranchTooFewTurns = 6,
    }

    public static FailReason Validate(
        in PathBuildResult build,
        int2 gridSize, int2 goal, NativeArray<int2> spawns,
        MapGridGenerationSettings settings)
    {
        // 1. Connectivity: BFS from goal cell → 모든 spawn 도달
        // 2. degree checks:
        //    - goal degree == 1
        //    - 각 spawn degree == 1
        // 3. 2×2 block 검사 (path 셀 4개가 정사각형 차지하는 경우 없음)
        // 4. 각 spawn → goal 의 path 위 거리·꺾임 측정
        //    - 거리(cell 수) ≥ settings.EffectiveMinBranchCellCount(gridSize)
        //    - 꺾임 수 ≥ settings.MinBranchTurnCount
        // 5. 모두 통과 → Ok
    }

    // BFS 로 각 spawn 별 path-along distance 와 turn count 동시 계산
    public static (int cellCount, int turnCount) MeasureBranch(
        int2 spawn, int2 goal, int2 gridSize, in NativeHashSet<int> path) { ... }
}
```

### Outer 오케스트레이터

```csharp
public static class MapGridGenerator
{
    public static GeneratedMap Generate(
        int seed,
        int2 gridSize,
        MapGridGenerationSettings settings,
        Allocator allocator)
    {
        int lastAttempt = -1;
        for (int attempt = 0; attempt < settings.MaxMapAttempts; attempt++)
        {
            lastAttempt = attempt;
            // Random.CreateFromIndex(uint) 는 strong hashed seeding — XOR-MUL HashSeed 보다 collision risk 낮음.
            var rng = Unity.Mathematics.Random.CreateFromIndex(HashSeed(seed, attempt, settings.GeneratorVersion));

            // gs.spawns 는 TempJob (4-프레임 lifetime, leak detector 가 잡음). 항상 finally 에서 Dispose.
            GoalSpawnResult gs = GoalSpawnPlacer.Pick(ref rng, gridSize, settings, Allocator.TempJob);
            if (!gs.IsValid) continue;

            PathBuildResult build = default;
            try
            {
                build = IncrementalPathBuilder.Build(ref rng, gridSize, gs.goal, gs.spawns, settings, Allocator.TempJob);
                if (!build.IsValid) continue;

                var fail = MapGridValidator.Validate(build, gridSize, gs.goal, gs.spawns, settings);
                if (fail != MapGridValidator.FailReason.Ok) continue;

                // success path
                var map = CellClassifier.Bake(seed, gridSize, settings.GeneratorVersion, build, gs.goal, gs.spawns, allocator);
                _lastAttemptCountInternal = attempt + 1;
                return map;
            }
            finally
            {
                build.Dispose();   // PathBuildResult.Dispose 는 IsValid 와 무관하게 안전
                if (gs.spawns.IsCreated) gs.spawns.Dispose();
            }
        }

        _lastAttemptCountInternal = settings.MaxMapAttempts;
        throw new MapGenerationFailedException(seed, gridSize, settings.MaxMapAttempts);
    }

    // Test-only out parameter overload (테스트 전용)
    public static GeneratedMap Generate(int seed, int2 gridSize, MapGridGenerationSettings settings,
                                         Allocator allocator, out int attemptCount)
    {
        var map = Generate(seed, gridSize, settings, allocator);
        attemptCount = _lastAttemptCountInternal;
        return map;
    }

    // 정적 백킹은 마지막 호출의 attempt 만 보관 — 운영 코드는 직접 사용 금지. 테스트 외엔 out param 만.
    private static int _lastAttemptCountInternal;

    static uint HashSeed(int baseSeed, int attempt, int generatorVersion)
    {
        // Random.CreateFromIndex 는 0 도 허용. unchecked XOR 로 모든 비트 활용.
        unchecked
        {
            uint h = (uint)baseSeed;
            h ^= (uint)attempt * 2654435761u;
            h ^= (uint)generatorVersion * 374761393u;
            return h;
        }
    }
}

public sealed class MapGenerationFailedException : System.Exception
{
    public int Seed { get; }
    public int2 GridSize { get; }
    public int Attempts { get; }
    public MapGenerationFailedException(int seed, int2 gridSize, int attempts)
        : base($"Map generation failed after {attempts} attempts (seed={seed}, grid={gridSize.x}x{gridSize.y})")
    { Seed = seed; GridSize = gridSize; Attempts = attempts; }
}
```

## EditMode 테스트

### `MapGridValidatorTests`

- `Validate_HappyPath_ReturnsOk`: 손으로 구성한 작은 path → Ok.
- `Validate_GoalDegreeTwo_FailsGoalDegree`.
- `Validate_DisconnectedSpawn_FailsConnectivity`.
- `Validate_TwoByTwoBlock_Detected`.
- `Validate_BranchTooShort_FailsBranch`: 길이만 부족하고 꺾임은 충분한 path (minBranchCellCount=20, 셀 수=12, 꺾임=5) → BranchTooShort. Validator 가 길이 검사를 꺾임 검사보다 먼저 수행하도록 순서 lock-in.
- `Validate_BranchTooFewTurns_Detected`: 셀 수는 충분하고 꺾임만 부족한 path (minBranchCellCount=5, 셀 수=20, 꺾임=1, minBranchTurnCount=3) → BranchTooFewTurns.

### `MapGridGeneratorTests`

- `Generate_DefaultSettings_Succeeds`: seed=0, Wide30x15 grid → `IsCreated == true`.
- `Generate_Deterministic_AcrossAttempts`: 같은 seed → tiles/spawns/goal 동일 + `attemptCount` 동일.
- `Generate_ThrowsOnImpossibleSettings`: minBranchCellCount=1000 으로 설정 → 600 attempts 내 실패 → `MapGenerationFailedException`.
- `Generate_AcrossPresets_AllSucceed`: 3개 프리셋 × 50 seed = 150 케이스, 실패율 ≤ 5 %.
- `Generate_DisposesOnFailure`: 강제 실패 케이스에서 NativeArray 누수 없음 (Allocator.TempJob leak check 활용).
- `HashSeed_NoCollisionsAcrossAttempts`: seed ∈ {0..99} × attempt ∈ {0..599} × version 1 = 60,000 조합에서 unique uint 비율 ≥ 99.9 %.

## 완료 기준

- [ ] `MapGridValidator`/`MapGridGenerator`/`MapGenerationFailedException` 컴파일.
- [ ] Validator 테스트 6 케이스, Generator 테스트 5 케이스 모두 통과.
- [ ] 3 프리셋 × 50 seed 회귀: 실패율 ≤ 5 %, 평균 attempt ≤ 50.
- [ ] outer 루프가 silent fallback 을 만들지 않는다 (기존 `BattleMapBuilder.BuildFallbackLinear` 호출 금지). 실패는 명시적 throw.
- [ ] 확인 일자 + 커밋 해시 (구현 후 채움):
