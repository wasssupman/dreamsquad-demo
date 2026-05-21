# 3. BattleBridge Integration + Scene Wiring

## 목적

`BattleBridge` 의 mapTheme 참조를 `SeasonRuntime.Active.mapTheme` 으로 일원화하고, BackdropMounter 의 Mount/Unmount 를 매치 라이프사이클에 통합한다. BattleScene 에 SeasonRegistry 를 wiring 한다.

## 변경 대상

- `Assets/_Project/Scripts/Bridge/BattleBridge.cs`
- `Assets/_Project/Scenes/BattleScene.unity` (UnityMCP)

## 사전 사실 확인 (2026-05-09 기준)

`mapTheme` 의 모든 read 위치 (BattleBridge.cs):

```
36   [SerializeField] private MapThemeData mapTheme;
459  ProceduralMapGenerator.Generate(seed, gridSize, mapTheme, ...)
485  mapView.Initialize(_generatedMap, tileSize, mapTheme)
491  if (mapView != null && mapTheme != null)
493  if (mapTheme.tileProps != null && ...)
496  BackgroundPropPlacer.Generate(visualPlan, mapTheme, _generatedMap.seed)
497  mapView.InstantiateBackgroundProps(visualPlan, mapTheme, placements)
501  mapView.InstantiateObstacles(_generatedMap, mapTheme)
```

총 사용처는 BuildMapForBattle 안 6 read + 1 SerializedField 선언. 외부 read 없음 → 일원화 안전.

매치 라이프사이클 진입점:

```
PrepareDraftMap()                → BuildMapForBattle()
RebuildDraftMap()                → CleanupDraftMapBeforeRebuild() → BuildMapForBattle()
StopBattle()                     → TeardownCurrentBattle() (entity manager 활성 시) 또는 DisposeEcsInfrastructureNativeContainers/TeardownGeneratedMap
OnDestroy()                      → ECS 정리
```

`TeardownCurrentBattle` 은 StopBattle 외 라인 174 / 211 / 237 / 2865 에서도 직접 호출되는 다중-진입 정리 메서드다. 따라서 `BackdropMounter.Unmount` 는 `TeardownCurrentBattle` 내부에 추가해 모든 경로에서 동작하도록 한다.

## 구현

### Step 1. 필드 정리

```csharp
// 기존
[SerializeField] private MapThemeData mapTheme;

// 변경 후
[Header("Season")]
[SerializeField] private SeasonRegistry seasonRegistry;

private GameObject _backdropRoot;
```

`mapTheme` SerializedField 는 제거. 디자이너가 mapTheme 만 단독으로 갈아끼우던 경로는 더 이상 없다 — `SeasonData.mapTheme` 만이 source of truth.

### Step 2. Awake 보강

```csharp
private void Awake()
{
    // ... 기존 ...
    SeasonRuntime.Bind(seasonRegistry);
    if (seasonRegistry == null || seasonRegistry.activeSeason == null
        || seasonRegistry.activeSeason.mapTheme == null)
    {
        Debug.LogError("[BattleBridge] SeasonRegistry / activeSeason / mapTheme 가 wiring 되지 않았다. BattleScene 에 SeasonRegistry.asset 을 연결하라.", this);
    }
}
```

### Step 3. BuildMapForBattle 본문 정리

진입부에서 시즌 mapTheme 을 한 번만 lookup. 그 다음 6 read 위치는 모두 local `theme` 로 교체.

```csharp
private void BuildMapForBattle()
{
    TeardownGeneratedMap();
    TeardownFlowField();

    var theme = SeasonRuntime.Active?.mapTheme;     // 시즌 source of truth
    var backdrop = SeasonRuntime.Active?.backdrop;  // null 이면 backdrop skip

    // ... 기존 mapPathShape / gridSize / generation 로직 그대로 ...

    if (useProcedural)
    {
        _generatedMap = ProceduralMapGenerator.Generate(seed, gridSize, theme, ...);
    }

    // ... connectivity check ...

    if (mapView != null) mapView.Initialize(_generatedMap, tileSize, theme);
    if (placementInput != null) placementInput.Initialize(_generatedMap, tileSize);
    FrameMainCameraForMap();

    // 백드롭은 카메라 framing 직후에 마운트 (camera pose 가 settle 된 시점)
    BackdropMounter.Unmount(ref _backdropRoot);
    if (enableSeasonBackdrop && backdrop != null)
    {
        _backdropRoot = BackdropMounter.Mount(_generatedMap, Camera.main, backdrop, tileSize);
    }

    BuildFlowField();

    if (mapView != null && theme != null)
    {
        if (theme.tileProps != null && theme.tileProps.Length > 0)
        {
            var visualPlan = mapView.VisualPlan;
            var placements = BackgroundPropPlacer.Generate(visualPlan, theme, _generatedMap.seed);
            mapView.InstantiateBackgroundProps(visualPlan, theme, placements);
        }
        else
        {
            mapView.InstantiateObstacles(_generatedMap, theme);
        }
    }

    // ... 로깅 그대로 ...
}
```

`enableSeasonBackdrop` 은 `[SerializeField] private bool enableSeasonBackdrop = true;` 로 추가. 디버그 OFF 스위치.

### Step 4. 라이프사이클 정리

`TeardownCurrentBattle` 진입부에 추가 (다중-진입 정리 메서드 — 모든 경로 cover):

```csharp
private void TeardownCurrentBattle()
{
    BackdropMounter.Unmount(ref _backdropRoot);
    // ... 기존 ECS 엔티티 정리 / native container dispose / generated map teardown ...
}
```

`CleanupDraftMapBeforeRebuild` 진입부에 추가 (RebuildDraftMap 경로):

```csharp
private void CleanupDraftMapBeforeRebuild()
{
    BackdropMounter.Unmount(ref _backdropRoot);
    // ... 기존 entity destroy / mapView reset / hazard registry clear ...
}
```

`StopBattle` 의 ECS 정리-없는 경로에도 명시:

```csharp
public void StopBattle()
{
    if (HasLiveEntityManager())
    {
        TeardownCurrentBattle();   // 위 Unmount 가 여기서도 동작
        return;
    }

    BackdropMounter.Unmount(ref _backdropRoot);
    // ... 기존 정리 (DisposeEcsInfrastructureNativeContainers / TeardownGeneratedMap) ...
    _ecsInfrastructureReady = false;
}
```

`OnDestroy` 에 추가:

```csharp
private void OnDestroy()
{
    BackdropMounter.Unmount(ref _backdropRoot);
    // ... 기존 ECS 정리 ...
}
```

`Mount` 호출 직전에도 항상 `Unmount` 가 한 번 더 호출되므로 (Step 3) RebuildDraftMap 경로도 안전.

### Step 5. 씬 wiring (UnityMCP)

1. `manage_scene` 으로 `BattleScene.unity` 열기.
2. `find_gameobjects` 로 BattleBridge 가 붙은 GameObject 찾기.
3. `manage_components` 로 `seasonRegistry` 필드에 `Assets/_Project/Data/Season/SeasonRegistry.asset` 의 GUID 할당.
4. 기존 `mapTheme` SerializedField 가 사라졌으므로 자동으로 inspector 에서 빠짐. 누락 reference 경고 없는지 확인.
5. `enableSeasonBackdrop = true` 확인.
6. 씬 저장.

## 완료 기준

- BattleBridge.cs 컴파일 clean (`mcp__UnityMCP__read_console`).
- `grep "mapTheme " BattleBridge.cs` 결과 = SerializedField 선언 0 + 직접 read 0. 모두 `theme` 로 교체.
- BattleScene.unity 에 BattleBridge.seasonRegistry 가 SeasonRegistry.asset 으로 채워진 상태로 저장.
- 본 단위 자체에서 Play 진입 X (5/6 단위에서 검증).

## 의존

- 선행: 1번 (데이터 모델), 2번 (Mounter)
- 후행: 5번 (SO 채움) → 6번 (Play 검증)

## 주의

- ECS 코드 / SystemGroup / EntityManager 호출은 추가하지 않는다. 모두 MonoBehaviour 계층.
- `BackdropMounter` 호출은 BattleBridge 외 다른 곳에서 절대 추가 금지.
- `enableSeasonBackdrop = false` 는 디버그용 OFF 스위치. 정상 운영은 항상 ON 이고 SeasonRegistry 가 wiring 돼있어야 한다.
- `Camera.main` 이 Awake 시점엔 null 일 수 있으나, `BuildMapForBattle` 은 PrepareDraftMap → DeferredPrepareDraftMap (yield return null) 경로를 거치므로 Camera 가 활성화된 후 호출된다. Mount 진입 시점엔 valid 하다고 가정.

확인 일자: 2026-05-10 / 커밋: 84a6103 (씬 wiring은 unit 5에서 SeasonRegistry.asset 생성 후 진행)
