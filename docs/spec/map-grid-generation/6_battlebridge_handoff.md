# Unit 6 — BattleBridge 어댑터

## 목적

새 `MapGridGenerator.Generate` 와 `MapDocument` 를 `BattleBridge.BuildMapForBattle()` 의 if/else 체인에 **새 분기로 합류** 시킨다. 기존 3 경로(`Manual` / `useProcedural` / `Fixture`)는 본 spec 안정화 전까지 살린다. 동시에 `MapSource` enum 을 도입해 if/else 를 switch 로 마이그레이션 — 이후 cleanup spec 이 legacy 경로를 제거할 때 변경점이 축소된다.

## 변경 대상

- 수정: `Assets/_Project/Scripts/Bridge/BattleBridge.cs`
  - `BuildMapForBattle()` (`:455-498` 부근) — if/else → switch 마이그레이션 + 새 `MapGrid` 케이스.
  - 새 SerializeField: `MapSource mapSource`, `MapGridGenerationSettings mapGridSettings`, `MapDocument mapDocument`.
  - `useProcedural` 는 `[Obsolete]` + `[HideInInspector]` 로 유지 (다음 cleanup spec 에서 삭제).
  - `MapConnectivity.AllSpawnsReachGoal` 후처리는 `MapGrid` 케이스에서 **skip** — Validator 가 이미 동일 보장.
  - `BackgroundPropPlacer.Generate` / `InstantiateObstacles` / `InstantiateBackgroundProps` 호출은 `MapGrid` 케이스에서 **skip** — Env/Deco 가 없으므로 prop 풀이 빈손이 됨. 후속 theming spec 이 wire-up.
- 신설: `Assets/_Project/Scripts/Bridge/MapSource.cs` (enum).
- 신설: `Assets/_Project/Scripts/Data/MapGrid/MapGridBattleAdapter.cs` (정적 헬퍼).
- 신설: `Assets/_Project/Tests/PlayMode/MapGrid/MapGridBattleBridgePlayModeTest.cs`.

## 구현

### `MapSource` enum

```csharp
// Assets/_Project/Scripts/Bridge/MapSource.cs
namespace Wassup.Bridge
{
    public enum MapSource : byte
    {
        // 기본값 = 기존 동작 보존 (useProcedural 따라 분기).
        // legacy 가 사라지면 default 는 Fixture 또는 MapGrid 로 변경.
        Legacy = 0,
        Manual = 1,           // _manualMapInput.HasValue 경로
        Fixture = 2,          // BattleMapBuilder.BuildFromFixture(map, ...)
        Procedural_Legacy = 3,// ProceduralMapGenerator.Generate (deprecated, cleanup 예정)
        MapGrid = 4,          // 신: MapGridGenerator.Generate + MapDocument
    }
}
```

### `BattleBridge` 마이그레이션 (`BuildMapForBattle` 내부)

기존 `:466-492` 의 if/else 체인을 다음 switch 로 **교체**. 행동 보존을 위해 `Legacy` 케이스가 옛 if/else 와 동일한 결과를 만든다.

```csharp
// 기존 변수들 (theme, seed, version, options, gridSize) 는 그대로 사용.
switch (mapSource)
{
    case MapSource.Manual:
        if (_manualMapInput.HasValue)
            _generatedMap = BattleMapBuilder.BuildFromManual(_manualMapInput.Value, seed, version);
        else
            goto case MapSource.Fixture;   // graceful fallback — 옛 코드와 동일 의미
        break;

    case MapSource.Procedural_Legacy:
        _generatedMap = ProceduralMapGenerator.Generate(
            seed, gridSize, theme, version,
            options.pathShape, options.spawnLaneCount, options.MinPlaceableRatio);
        break;

    case MapSource.Fixture:
        if (map == null)
        {
            Debug.LogError("[BattleBridge] map reference missing — cannot build fixture GeneratedMap.", this);
            _generatedMap = BattleMapBuilder.BuildFallbackLinear(gridSize, seed, version, options.spawnLaneCount);
        }
        else
            _generatedMap = BattleMapBuilder.BuildFromFixture(map, seed, version);
        break;

    case MapSource.MapGrid:
        try
        {
            _generatedMap = MapGridBattleAdapter.Build(seed, mapGridSettings, mapDocument);
        }
        catch (MapGenerationFailedException ex)
        {
            Debug.LogError($"[BattleBridge] {ex.Message}", this);
            _generatedMap = default;   // silent fallback 금지. UI 가 실패 인지 후 재시도/재시드 유도.
            return;                     // BuildMapForBattle 조기 종료 — 후속 단계(FlowField/MapView) skip.
        }
        break;

    case MapSource.Legacy:
    default:
        // 행동 보존: 기존 if/else 와 동일
        if (_manualMapInput.HasValue)
            _generatedMap = BattleMapBuilder.BuildFromManual(_manualMapInput.Value, seed, version);
        else if (useProcedural)
            _generatedMap = ProceduralMapGenerator.Generate(
                seed, gridSize, theme, version, options.pathShape, options.spawnLaneCount, options.MinPlaceableRatio);
        else if (map == null)
        {
            Debug.LogError("[BattleBridge] map reference missing — cannot build fixture GeneratedMap.", this);
            _generatedMap = BattleMapBuilder.BuildFallbackLinear(gridSize, seed, version, options.spawnLaneCount);
        }
        else
            _generatedMap = BattleMapBuilder.BuildFromFixture(map, seed, version);
        break;
}

// connectivity 후처리 — MapGrid 는 Validator 가 이미 보장하므로 skip
if (mapSource != MapSource.MapGrid)
{
    if (!MapConnectivity.AllSpawnsReachGoal(_generatedMap))
    {
        Debug.LogWarning("[BattleBridge] GeneratedMap connectivity failed; using fallback linear map.", this);
        TeardownGeneratedMap();
        _generatedMap = BattleMapBuilder.BuildFallbackLinear(gridSize, seed, version, options.spawnLaneCount);
    }
}
```

기존 `mapView.Initialize` / `placementInput.Initialize` / `BuildFlowField` 호출은 변경 없음. **단, BackgroundPropPlacer / InstantiateObstacles / InstantiateBackgroundProps 분기에 `mapSource != MapSource.MapGrid` 가드 추가** — Env/Deco 셀이 없으므로 prop 풀 placement 가 무의미.

```csharp
if (mapView != null && theme != null && mapSource != MapSource.MapGrid)
{
    // 기존 InstantiateBackgroundProps / InstantiateObstacles 블록 유지
}
```

### `MapGridBattleAdapter`

```csharp
namespace Wassup.Data.MapGrid
{
    public static class MapGridBattleAdapter
    {
        public static GeneratedMap Build(int seed, MapGridGenerationSettings settings, MapDocument cacheDocOrNull)
        {
            if (settings == null)
                throw new InvalidOperationException(
                    "[MapGridBattleAdapter] MapGridGenerationSettings 가 null — BattleBridge inspector 에 할당하라.");

            // 1. cache 문서가 있으면 그것을 사용 (절차적 결과 캐시 또는 손수 작성)
            if (cacheDocOrNull != null && cacheDocOrNull.Width > 0 && cacheDocOrNull.Tiles?.Count > 0)
                return MapDocumentBuilder.ToGeneratedMap(cacheDocOrNull, Allocator.Persistent);

            // 2. 절차적 생성
            int2 gridSize = PickGridSize(settings, seed);
            return MapGridGenerator.Generate(seed, gridSize, settings, Allocator.Persistent);
        }

        static int2 PickGridSize(MapGridGenerationSettings settings, int seed)
        {
            var presets = settings.AllowedPresets;
            if (presets == null || presets.Count == 0) return new int2(20, 10);
            int idx = math.abs(seed) % presets.Count;
            return MapGridGenerationSettings.PresetToGridSize(presets[idx]);
        }
    }
}
```

`Allocator.Persistent` 채택 이유: 기존 `BattleMapBuilder.BuildFromFixture` 도 Persistent 를 쓰며, `BattleBridge.TeardownGeneratedMap()` 이 `_generatedMap.Dispose()` 로 정리하는 패턴과 일치.

### Inspector wiring (UnityMCP 자동화)

본 unit 구현 PR 의 일부로 `Assets/_Project/Scenes/BattleScene.unity` 의 `BattleBridge` GameObject 에 다음 SerializeField 를 wire-up 한다:
- `mapSource = Legacy` (기본값, 기존 동작 100% 보존)
- `mapGridSettings = Assets/_Project/Data/Maps/MapGridGenerationSettings_Default.asset`
- `mapDocument = null` (절차적 생성 사용)

UnityMCP 호출 시퀀스 예시 (implementer 가 직접 실행):
1. `manage_scene(load, "Assets/_Project/Scenes/BattleScene.unity")`
2. `find_gameobjects(name: "BattleBridge")` → instanceId 획득
3. `manage_components(action: "set_field", target: instanceId, component: "BattleBridge", field: "mapGridSettings", value: "Assets/_Project/Data/Maps/MapGridGenerationSettings_Default.asset")` × 3
4. `manage_scene(save, ...)`

수동 wiring 으로 대체 가능하나 CLAUDE.md 의 "Unity 씬 wiring 을 사용자 수작업으로 미루지 않는다" 원칙에 따라 UnityMCP 우선.

## PlayMode 테스트

`MapGridBattleBridgePlayModeTest`:

- `Play_MapSource_MapGrid_GeneratesAndStartsBattle`:
  1. BattleScene 로드, BattleBridge.mapSource = MapGrid + mapGridSettings 주입.
  2. PrepareDraftMap → BeginPlacement → BeginBattle.
  3. attacker 1 spawn → goal reach event 발화 확인 (`GoalReachedEventsSingleton` 큐 drain).
  4. `mapView.transform.childCount > 0` 으로 타일 렌더링 확인 (visual smoke).
- `Play_MapSource_Legacy_StillWorks`: 기존 동작 100% 보존 — mapSource=Legacy 로 두고 fixture 시나리오 정상.
- `Play_MapGrid_FailureRaisesError`: 의도적으로 settings 를 빡빡하게 (minBranchCellCount=1000) → BuildMapForBattle 이 LogError 발화 + `_generatedMap.IsCreated == false` + FlowField/MapView 빌드 skip 확인.
- `Play_MapGrid_TeardownRebuildCycle_NoLeak`: BeginBattle ↔ RedraftRequested 30 cycle 반복 후 `_generatedMap.IsCreated` 정상 + NativeArray leak detector 0 경고. (100 cycle 은 EditMode 가 더 적합 — PlayMode 30 으로 축소.)

## 완료 기준

- [ ] `MapSource.cs`, `MapGridBattleAdapter.cs` 컴파일.
- [ ] `BattleBridge.cs` 마이그레이션 후 기존 PlayMode/EditMode 테스트 0 실패. `useProcedural=true` + `mapSource=Legacy` 조합이 기존 동작과 비트 단위 동일 결과 (같은 seed/version).
- [ ] 신규 PlayMode 테스트 4 케이스 통과.
- [ ] BattleScene 의 `BattleBridge` 컴포넌트 inspector 에 3 신규 필드 노출 + UnityMCP wiring 으로 자산 할당.
- [ ] mapSource=MapGrid 로 PlayMode 1판 진입 후 스크린샷 `Assets/Screenshots/mapgrid_smoke.png` 첨부 (path/spawn/goal 식별 가능).
- [ ] console: 0 ERROR / 0 unexpected WARN (단, `MapGrid` 실패 강제 케이스의 LogError 는 의도적).
- [ ] 확인 일자 + 커밋 해시 (구현 후 채움):
