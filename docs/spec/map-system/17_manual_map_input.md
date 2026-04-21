# ManualMapInput Struct (맵툴 예약)

**작업 구분**: Phase 10B

## 목적

Q-K 축소 결정 반영: 사용자 bullet 4 "사용자가 이동타일 수동 지정" 경로의 **내부 data shape 만** 정의. 외부 I/O (JSON / scene asset / 맵툴 UI) 는 맵툴 Phase 로 이관. Phase 10B 에선 `ManualMapInput → GeneratedMap` 변환 경로만 확보.

## 변경 대상

- 새 파일: `Assets/_Project/Scripts/Data/ManualMapInput.cs`
- 새 static: `BattleMapBuilder.BuildFromManual(ManualMapInput)` (task 3 의 Builder 확장)

## 구현

### ManualMapInput struct

```csharp
using Unity.Mathematics;

namespace Wassup.Data
{
    // Phase 10B: 맵툴/외부 I/O 의 입력 data shape.
    // 맵툴 (Phase 11+) 이 이 struct 를 채우면 BattleMapBuilder 가 GeneratedMap 으로 변환.
    // 외부 직렬화 (JSON/SO) 는 맵툴 구현 시 결정.
    [System.Serializable]
    public struct ManualMapInput
    {
        public int2     gridSize;
        public int2[]   walkCells;          // Walk 타일로 지정할 셀 목록
        public int2[]   placeCells;         // 옵션. null/empty 면 walk 인근 자동 채움
        public int2[]   spawns;             // 1~N
        public int2     goal;
        public int2[]   envCells;           // 옵션. 환경 타일 (Phase 10 시각만)
        public int2[]   decoCells;          // 옵션. 배경 오브젝트 타일
    }
}
```

### BattleMapBuilder.BuildFromManual

```csharp
public static GeneratedMap BuildFromManual(ManualMapInput input, int seed = 0, int generatorVersion = 0)
{
    // H-7 fix: 필수 필드 null/empty 체크
    if (input.gridSize.x <= 0 || input.gridSize.y <= 0)
    {
        Debug.LogError("[BuildFromManual] gridSize must be positive.");
        return default;
    }
    if (input.walkCells == null || input.walkCells.Length == 0)
    {
        Debug.LogError("[BuildFromManual] walkCells required.");
        return default;
    }
    if (input.spawns == null || input.spawns.Length == 0)
    {
        Debug.LogError("[BuildFromManual] spawns required.");
        return default;
    }
    // H-4 / N-2: goal 및 **모든 spawn** bounds 체크
    if (!InBounds(input.goal, input.gridSize))
    {
        Debug.LogError($"[BuildFromManual] goal {input.goal} out of gridSize {input.gridSize}.");
        return default;
    }
    for (int i = 0; i < input.spawns.Length; i++)
    {
        if (!InBounds(input.spawns[i], input.gridSize))
        {
            Debug.LogError($"[BuildFromManual] spawns[{i}] {input.spawns[i]} out of gridSize {input.gridSize}.");
            return default;
        }
    }

    int n = input.gridSize.x * input.gridSize.y;
    var tiles = new NativeArray<MapTileType>(n, Allocator.Persistent);

    // 1. 전체 Place 로 초기화 (Walk/Env/Deco 는 명시 지정 셀만)
    for (int i = 0; i < n; i++) tiles[i] = MapTileType.Place;

    // 2. Walk 마킹
    MarkCells(tiles, input.gridSize, input.walkCells, MapTileType.Walk);

    // 3. Env / Deco 명시 지정
    if (input.envCells != null)  MarkCells(tiles, input.gridSize, input.envCells, MapTileType.Env);
    if (input.decoCells != null) MarkCells(tiles, input.gridSize, input.decoCells, MapTileType.Deco);

    // 4. placeCells 가 명시되면 나머지 Place → Deco 전환 (명시 안 하면 모두 Place 유지)
    if (input.placeCells != null && input.placeCells.Length > 0)
    {
        var placeSet = new System.Collections.Generic.HashSet<int>();
        foreach (var c in input.placeCells)
            placeSet.Add(c.y * input.gridSize.x + c.x);

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
    if (cells == null) return;  // H-7: null 허용
    foreach (var c in cells)
    {
        if (c.x < 0 || c.x >= gridSize.x || c.y < 0 || c.y >= gridSize.y) continue;
        tiles[c.y * gridSize.x + c.x] = type;
    }
}

private static bool InBounds(int2 cell, int2 gridSize)
    => cell.x >= 0 && cell.x < gridSize.x && cell.y >= 0 && cell.y < gridSize.y;
```

### BattleBridge 분기 (Phase 10B 흐름)

C-8 fix: 모든 필드 (`useProcedural`, `mapTheme`, `_manualMapInput`, gridSize source, generatorVersion source) 는 **task 19 에서 owner 로 정의**. 이 task 는 `BuildFromManual` API 와 `ManualMapInput` struct 만 담당. 실제 BattleBridge 분기 코드는 task 19 참조.

## Phase 11+ 이관

- 맵툴 UI (scene 에디터 확장, tile painter)
- JSON / ScriptableObject 직렬화 포맷
- 맵툴 → ManualMapInput 로딩 경로

## 완료 기준

- `ManualMapInput.cs` 컴파일.
- `BuildFromManual` 메서드 컴파일.
- EditMode 테스트: 간단한 ManualMapInput (5×5 grid, walk 셀 3개, spawn 1개, goal 1개) → GeneratedMap 의 tiles 에 해당 셀 Walk 마킹 확인.
- 경계 밖 셀은 무시 (warning 아닌 silent skip).
- Phase 10B 현재는 BattleBridge 가 procedural 경로만 사용 (manual 경로는 맵툴 Phase 에서 활성화).
- H-4: goal / spawn 이 gridSize 밖 → LogError + default(GeneratedMap) 반환 (exception 없음).
- H-7: gridSize/walkCells/spawns null/empty → LogError + default 반환.

## Subtask 분할 (OVERRUN 대응, 35분 예상)

- **17A** — `ManualMapInput` struct 정의 + `BuildFromManual` body 구현
- **17B** — null/bounds 검증 (H-4, H-7) + MarkCells / InBounds helper
- **17C** — EditMode 테스트 (정상 케이스 + null/out-of-bounds edge case)
