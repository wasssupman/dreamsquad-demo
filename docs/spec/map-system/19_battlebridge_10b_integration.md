# BattleBridge Phase 10B Integration

**작업 구분**: Phase 10B (procedural + 테마 wiring 의 최종 owner)

## 목적

Phase 10B 의 procedural / manual / fixture 분기를 `BattleBridge` 에 통합. Codex 2차 리뷰 C-8 대응: 여러 task (11/14/17) 에서 암묵적으로 참조하던 BattleBridge 필드들을 이 task 에서 **명시적으로 정의 + owner 지정**. BuildMapForBattle 최종 orchestration.

## 변경 대상

- Modify: `Assets/_Project/Scripts/Bridge/BattleBridge.cs`

## BattleBridge Phase 10B 필드 명세 (owner = 이 task)

```csharp
// ---- Phase 10A 에서 이미 추가됨 (task 4) ----
[SerializeField] private MapGenerationSettings mapSettings;
[SerializeField] private MapData map;                 // legacy fixture (Phase 10B 에서 useProcedural=false 일 때만 사용)

// ---- Phase 10B 추가 (이 task 에서 정의) ----
[Header("Phase 10B — Procedural")]
[SerializeField] private bool useProcedural = true;   // true = ProceduralMapGenerator, false = fixture
[SerializeField] private MapThemeData mapTheme;       // procedural 에 넘기는 테마 (Phase 10B v1 은 forest)

// 맵툴 (Phase 11+) 예약 slot. Phase 10B 에선 null 기본, 맵툴 Phase 에서 활성화.
// Nullable struct: UnityEngine SerializeReference 대신 runtime 주입.
private ManualMapInput? _manualMapInput = null;

// GeneratedMap owner
private GeneratedMap _generatedMap;
```

## gridSize 단일 소스

`mapSettings` 가 단일 source of truth:

```csharp
private int2 GridSize =>
    mapSettings != null
        ? new int2(mapSettings.gridWidth, mapSettings.gridHeight)
        : new int2(20, 20);  // defensive fallback
```

## generatorVersion 단일 소스

```csharp
private int GeneratorVersion => mapSettings != null ? mapSettings.generatorVersion : 1;
```

이 값만 사용. task 11 의 `ProceduralMapGenerator.CurrentGeneratorVersion` 상수는 **삭제** (task 11 patch 참조). `ProceduralMapGenerator.Generate(seed, gridSize, theme, generatorVersion)` 시그니처 확정.

## BuildMapForBattle 최종 orchestration

```csharp
private void BuildMapForBattle()
{
    // 1. 멱등성: 기존 GeneratedMap / FlowFieldSingleton / MapView / PlacementInput 전부 clear
    TeardownGeneratedMap();
    TeardownFlowField();

    int seed = mapSettings != null ? mapSettings.EffectiveSeed : 0;
    int ver  = GeneratorVersion;
    int2 gs  = GridSize;

    // 2. 분기: manual > procedural > fixture
    if (_manualMapInput.HasValue)
    {
        _generatedMap = BattleMapBuilder.BuildFromManual(_manualMapInput.Value, seed, ver);
    }
    else if (useProcedural)
    {
        _generatedMap = ProceduralMapGenerator.Generate(seed, gs, mapTheme, ver);
    }
    else
    {
        // Fixture 경로 (Phase 10A 호환, 테스트 용)
        _generatedMap = BattleMapBuilder.BuildFromFixture(map, seed, ver);
    }

    // 3. 연결성 검증 + fallback
    if (!MapConnectivity.AllSpawnsReachGoal(_generatedMap))
    {
        Debug.LogError("[BattleBridge] Map connectivity check failed. Fallback to linear.");
        _generatedMap.Dispose();
        _generatedMap = BattleMapBuilder.BuildFallbackLinear(gs, seed, ver);
    }

    // 4. 주입
    if (mapView != null)         mapView.Initialize(_generatedMap, tileSize);
    if (placementInput != null)  placementInput.Initialize(_generatedMap, tileSize);

    // 5. FlowField 빌드 (task 5 교체된 시그니처 — _generatedMap 소비)
    BuildFlowField();

    // 6. MapView obstacle prefab Instantiate (task 14)
    if (mapView != null && mapTheme != null)
        mapView.InstantiateObstacles(_generatedMap, mapTheme);

    // 7. 로그 (task 16)
    if (_logger != null)
    {
        _logger.LogMap(
            _generatedMap.seed,
            _generatedMap.generatorVersion,
            _generatedMap.gridSize,
            _generatedMap.spawns.Length);
    }

    Debug.Log($"[BattleBridge] Map: seed={_generatedMap.seed} ver={_generatedMap.generatorVersion} size={_generatedMap.gridSize} spawns={_generatedMap.spawns.Length}");
}

private void TeardownGeneratedMap()
{
    if (_generatedMap.IsCreated) _generatedMap.Dispose();
    _generatedMap = default;
}
```

## Awake legacy init 제거 (C-3)

Phase 9 에서 `BattleBridge.Awake()` 가 `mapView.Initialize(map, tileSize)` + `placementInput.Initialize(tileSize)` 호출. Phase 10B 에선 **`BuildMapForBattle` 이 유일한 주입 지점**. Awake 에서 이 호출 제거:

```csharp
private void Awake()
{
    _em = World.DefaultGameObjectInjectionWorld.EntityManager;
    // Phase 10B: mapView/placementInput.Initialize 호출 제거.
    // BuildMapForBattle 에서만 주입한다 (GeneratedMap 필요).
}
```

## 판 종료

기존 `TeardownCurrentBattle()` + `OnDestroy` 에 추가:

```csharp
TeardownFlowField();      // 기존
TeardownGeneratedMap();   // 신규 (task 4 에서 초안, 이 task 에서 최종)
```

## 맵툴 연동 (Phase 11+)

`_manualMapInput` 설정 경로는 **Phase 11 맵툴 Phase 에서 public setter / scene API 로 확장**. Phase 10B 에선 private null 상태 유지 (procedural 만 활성).

## 완료 기준

- 컴파일 0 errors.
- `useProcedural = true` 기본값 — Play 진입 시 `ProceduralMapGenerator.Generate` 호출.
- `useProcedural = false` 로 전환 → `BattleMapBuilder.BuildFromFixture(map)` 경로 (10A 회귀).
- `mapSettings.defaultSeed = 12345` 고정 → 같은 맵 재현.
- Restart/Redraft 시 GeneratedMap 완전 dispose + 재생성 (NativeArray leak 없음).
- Awake 중복 init 제거 후 scene warning/log 없음.

## Subtask 분할 (OVERRUN 대응)

이 task 는 약 45분 예상 — 3 분할:

- **19A** — 필드 정의 + `GridSize` / `GeneratorVersion` property + Awake legacy init 제거
- **19B** — `BuildMapForBattle` 3분기 orchestration + 연결성 fallback
- **19C** — 주입 순서 + obstacle Instantiate + logging + teardown
