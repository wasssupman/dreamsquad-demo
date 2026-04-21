# BattleBridge Integration

**작업 구분**: Phase 10A

## 목적

`BattleBridge` 를 GeneratedMap 단일 owner 로 지정. `MapView` / `PlacementInput` 에 GeneratedMap 주입. Phase 9 `FlowFieldSingleton` 수명 관리 패턴 재사용.

## 변경 대상

- Modify: `Assets/_Project/Scripts/Bridge/BattleBridge.cs`

## 구현

### 필드

```csharp
[SerializeField] private MapGenerationSettings mapSettings;
// 기존 [SerializeField] private MapData map; 는 Phase 10A 동안 유지 (fixture 경로)

private GeneratedMap _generatedMap;
```

### 판 시작 흐름

`EnsureQueriesAndQueues()` 또는 `BeginPlacement` 안에서:

```csharp
private void BuildMapForBattle()
{
    // Phase 10A: fixture 경로만. Phase 10B 에서 procedural/manual 분기 추가.
    TeardownGeneratedMap();

    int seed = mapSettings != null ? mapSettings.EffectiveSeed : 0;
    int ver  = mapSettings != null ? mapSettings.generatorVersion : 0;
    _generatedMap = BattleMapBuilder.BuildFromFixture(map, seed, ver);

    // 소비자 주입
    if (mapView != null)         mapView.Initialize(_generatedMap);
    if (placementInput != null)  placementInput.Initialize(_generatedMap);

    // FlowField 는 GeneratedMap 기반으로 재계산
    BuildFlowField();  // 시그니처 변경: map 대신 _generatedMap 사용
}

private void TeardownGeneratedMap()
{
    if (_generatedMap.IsCreated) _generatedMap.Dispose();
    _generatedMap = default;
}
```

### tileSize 단일 소스

Phase 9 에서 `tileSize` 는 `BattleBridge` 필드로 유지. `_generatedMap.gridSize` 와 함께 MapView/PlacementInput 로 전달.

### 판 종료

`TeardownCurrentBattle()` 에서:

```csharp
TeardownFlowField();        // 기존
TeardownGeneratedMap();     // 신규
```

`OnDestroy` 에서도 동일.

## 흐름 diagram

```
StartBattle()
  → EnsureQueriesAndQueues()
      → BuildMapForBattle()
          1. TeardownGeneratedMap (멱등)
          2. BattleMapBuilder.BuildFromFixture → GeneratedMap
          3. MapView.Initialize(GeneratedMap)
          4. PlacementInput.Initialize(GeneratedMap)
          5. BuildFlowField (GeneratedMap walkmask 생성 — task 5)

TeardownCurrentBattle()
  → TeardownFlowField
  → TeardownGeneratedMap
```

## 완료 기준

- BattleBridge.cs 컴파일, 0 errors.
- Phase 9 EditMode 52/52 여전히 pass (기능 회귀 없음).
- PlayMode smoke: 판 진입 → Entity Inspector 에서 FlowFieldSingleton 존재 + tiles/spawns 값이 PrototypeMap 과 일치 확인.
- 판 재시작 시 GeneratedMap/FlowFieldSingleton 모두 재생성 (NativeArray leak 없음).
