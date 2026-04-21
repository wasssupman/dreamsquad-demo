# Seed + Generator Version Logging

**작업 구분**: Phase 10B

## 목적

재현성 확보. 버그 리포트 시 `seed + generatorVersion` 이 있으면 동일 맵 재생성 가능. `BattleLogger` 에 필수 필드 2개 추가.

## 변경 대상

- Modify: `Assets/_Project/Scripts/Logging/BattleLogger.cs` (또는 해당 로그 스키마 파일)
- Modify: `Assets/_Project/Scripts/Logging/BattleLogSchema.cs` (schema record)
- Modify: `Assets/_Project/Scripts/Bridge/BattleBridge.cs` (판 시작 시 로그 호출)

## 구현

### Schema 필드

기존 `BattleLogSchema` 판 단위 record 에 추가:
```csharp
public record MapRecord
{
    public int seed;
    public int generatorVersion;
    public int gridWidth;
    public int gridHeight;
    public int spawnCount;
    // (기존 필드)
}
```

### BattleLogger

```csharp
public void LogMap(int seed, int generatorVersion, int2 gridSize, int spawnCount)
{
    var rec = new MapRecord
    {
        seed = seed,
        generatorVersion = generatorVersion,
        gridWidth = gridSize.x,
        gridHeight = gridSize.y,
        spawnCount = spawnCount,
    };
    _currentBattle.map = rec;
}
```

### BattleBridge 호출

`BuildMapForBattle` 끝에서:
```csharp
if (_logger != null)
{
    _logger.LogMap(
        _generatedMap.seed,
        _generatedMap.generatorVersion,
        _generatedMap.gridSize,
        _generatedMap.spawns.Length);
}
```

### 콘솔 로그 (debug)

판 시작 시 1줄 정도 `Debug.Log` 로 "seed/version/size" 출력:
```csharp
Debug.Log($"[BattleBridge] Map: seed={_generatedMap.seed} ver={_generatedMap.generatorVersion} size={_generatedMap.gridSize} spawns={_generatedMap.spawns.Length}");
```

## 재현성 검증

- 동일 seed 로 재진입 → 동일 `seed` 로그 + 동일 맵
- generatorVersion 증가 시 (알고리즘 수정) → 같은 seed 라도 다른 맵 생성. 버그 리포트 재현 불가 → **알고리즘 변경 시 generatorVersion 증가 수동 갱신 규약** 을 `MapGenerationSettings` 주석에 명시 (task 1 에서 이미 명시됨)

## 기존 draft / skill seed 로그와 관계

Phase 7 에서 추가된 `BattleLogSchema.SkillRecord.seed`, `draft.seed` 와 별개. Map seed 는 새 layer.

## 완료 기준

- `BattleLogSchema.MapRecord` 필드 추가 컴파일.
- 판 진입 시 JSON 로그에 `map: { seed, generatorVersion, gridWidth, gridHeight, spawnCount }` 블록 포함.
- 동일 seed 2회 판 진입 시 두 로그의 map.seed 동일 값 확인.
- `Debug.Log` 콘솔 출력 1줄 확인.

## Subtask 분할 (OVERRUN 대응, 25분 예상)

- **16A** — `BattleLogSchema.MapRecord` 필드 + `BattleLogger.LogMap` API
- **16B** — `BattleBridge.BuildMapForBattle` 에서 `_logger.LogMap` + `Debug.Log` 호출
- **16C** — JSON 로그 확인 테스트 + 동일 seed 2회 재현 검증
