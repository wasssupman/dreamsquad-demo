# Unit 5 — 셀 분류 베이크

## 목적

`PathBuildResult` (path 셀 집합) + goal + spawns 를 받아 `GeneratedMap` 의 셀 타입과 메타데이터(`mergeDegree`, `chokepoint`, `propLayerId`) 를 채운다.

**스코프 명시 (Interpretation A 채택)**: 본 spec 의 절차적 생성기는 셀 타입을 `Walk` 와 `Place` 2종만 사용한다. path 셀 = `Walk`, 그 외 = `Place`. `Env`/`Deco` 는 베이크하지 않는다. 후속 theming spec (`seasonal-map-backdrop` 또는 신규 spec) 이 별도 overlay 단계로 Env/Deco 를 칠한다. 본 spec 의 생성기 결과는 시각적으로 더 "flat" 하다 — 이는 의도된 상태이며, 그 위에 theming spec 이 덧칠된다.

`propLayerId` 는 schema slot 만 확보 (항상 0 으로 베이크). 후속 theming spec 이 채움.

## 변경 대상

- 신설: `Assets/_Project/Scripts/Data/MapGrid/CellClassifier.cs`
- 수정: `Assets/_Project/Scripts/Data/MapGrid/MapGridGenerator.cs` — `PackageRawResult` 대신 `CellClassifier.Bake` 호출
- 신설: `Assets/_Project/Tests/EditMode/MapGrid/CellClassifierTests.cs`

## 구현

```csharp
public static class CellClassifier
{
    public static GeneratedMap Bake(
        int seed,
        int2 gridSize,
        int generatorVersion,
        in PathBuildResult build,
        int2 goal,
        NativeArray<int2> spawns,
        Allocator allocator)
    {
        int n = gridSize.x * gridSize.y;
        var tiles       = new NativeArray<MapTileType>(n, allocator);
        var mergeDegree = new NativeArray<byte>(n, allocator);
        var chokepoint  = new NativeArray<byte>(n, allocator);
        var propLayerId = new NativeArray<byte>(n, allocator);

        // 1. 모든 셀 = Place
        for (int i = 0; i < n; i++) tiles[i] = MapTileType.Place;

        // 2. path 셀 = Walk + degree 계산
        var pathEnum = build.pathCells.GetEnumerator();
        while (pathEnum.MoveNext())
        {
            int idx = pathEnum.Current;
            tiles[idx] = MapTileType.Walk;
            int2 c = IndexToCell(idx, gridSize);
            byte deg = CountPathNeighbors(c, gridSize, build.pathCells);
            mergeDegree[idx] = deg;
            chokepoint[idx]  = (byte)(deg >= 3 ? 1 : 0);
            // propLayerId 는 0 유지 (Walk 셀은 prop 안 깔림)
        }

        // 3. spawn/goal 의 mergeDegree 는 항상 1 (validator 가 보장). 그대로 둔다.

        // 4. NativeArray<int2> spawns 복사
        var outSpawns = new NativeArray<int2>(spawns.Length, allocator);
        outSpawns.CopyFrom(spawns);

        return new GeneratedMap {
            tiles = tiles, mergeDegree = mergeDegree,
            chokepoint = chokepoint, propLayerId = propLayerId,
            gridSize = gridSize, spawns = outSpawns,
            goal = goal, seed = seed, generatorVersion = generatorVersion,
        };
    }

    static byte CountPathNeighbors(int2 c, int2 gridSize, in NativeHashSet<int> path)
    {
        byte deg = 0;
        if (c.x + 1 < gridSize.x && path.Contains(CellIndex(new int2(c.x + 1, c.y), gridSize))) deg++;
        if (c.x - 1 >= 0          && path.Contains(CellIndex(new int2(c.x - 1, c.y), gridSize))) deg++;
        if (c.y + 1 < gridSize.y && path.Contains(CellIndex(new int2(c.x, c.y + 1), gridSize))) deg++;
        if (c.y - 1 >= 0          && path.Contains(CellIndex(new int2(c.x, c.y - 1), gridSize))) deg++;
        return deg;
    }
}
```

### `MapGridGenerator.Generate` 수정

```csharp
// 기존: PackageRawResult(...)
// 신: CellClassifier.Bake(seed, gridSize, settings.GeneratorVersion, build, gs.goal, gs.spawns, allocator)
```

호출 후 `build.Dispose()` 와 `gs.spawns.Dispose()` (CellClassifier 가 복사본 만듦) 정상 동작 확인.

## EditMode 테스트

- `Bake_TilesAndMeta_LengthMatchGrid`: 모든 NativeArray length = W*H.
- `Bake_PathCellsAreWalk`: build.pathCells 의 모든 인덱스가 tiles == Walk.
- `Bake_NonPathCellsArePlace`: 나머지 셀 tiles == Place.
- `Bake_GoalDegree_IsOne`: validator 통과한 build → `mergeDegree[goalIndex] == 1`.
- `Bake_SpawnDegree_IsOne`: 모든 spawn 의 mergeDegree == 1.
- `Bake_MergeCells_FlaggedChokepoint`: degree ≥ 3 셀의 `chokepoint == 1`.
- `Bake_PropLayerId_AllZero`: 모든 셀 propLayerId == 0.
- `Bake_DisposesInputs_OutputOwnsCopies`: 입력 spawns 를 Dispose 해도 outSpawns 정상 동작.

## 완료 기준

- [ ] `CellClassifier.Bake` 컴파일.
- [ ] `MapGridGenerator.Generate` 가 `CellClassifier.Bake` 사용으로 변경, 단위 4 테스트 모두 여전히 통과.
- [ ] CellClassifier 테스트 8 케이스 통과.
- [ ] 3 프리셋 × 50 seed 회귀에서 chokepoint 셀이 평균 ≥ 1 개 (single-spawn 케이스 제외) — 합류 emergence 검증.
- [ ] 확인 일자 + 커밋 해시 (구현 후 채움):
