# Unit 10 — MapSettingsPanelView 를 MapGrid 모드에 맞춤

## 목적

현재 `MapSettingsPanelView` (dev-only 패널) 의 4 컨트롤 (Path Type / Map Size / Object Density / Spawn Lanes) 은 모두 legacy ProceduralMapGenerator 전용이라 `MapSource.MapGrid` 모드에선 의미 없다. 패널을 양쪽 모드 모두 지원하도록 확장한다.

## 변경 대상

- 수정: `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `SetMapSource(MapSource)`, `SetMapGridPresetOverride(MapGridPreset?)` 추가, `BuildMapForBattle` 의 MapGrid 경로에서 override 사용.
- 수정: `Assets/_Project/Scripts/Data/MapGrid/MapGridBattleAdapter.cs` — `Build` 에 `MapGridPreset? presetOverride` 인자 추가.
- 수정: `Assets/_Project/Scripts/Core/DraftController.cs` — `SelectedMapSource`, `SelectedMapGridPreset` + setter forwarding.
- 수정: `Assets/_Project/Scripts/UI/Draft/MapSettingsPanelView.cs` — 상단 Map Source 토글 + MapGrid 선택 시 Preset 4 버튼 행 + legacy 4 row 가시성 토글.

## 구현 (요약)

### `MapGridBattleAdapter.Build`
```csharp
public static GeneratedMap Build(
    int seed,
    MapGridGenerationSettings settings,
    MapDocument cacheDocOrNull,
    MapGridPreset? presetOverride = null)
{
    ...
    int2 gridSize = presetOverride.HasValue
        ? MapGridGenerationSettings.PresetToGridSize(presetOverride.Value)
        : PickGridSize(settings, seed);
    return MapGridGenerator.Generate(seed, gridSize, settings, Allocator.Persistent);
}
```

### `BattleBridge`
```csharp
private MapGridPreset? _mapGridPresetOverride;
public void SetMapSource(MapSource src) { mapSource = src; }
public void SetMapGridPresetOverride(MapGridPreset? p) { _mapGridPresetOverride = p; }
// BuildMapForBattle:
case MapSource.MapGrid:
    _generatedMap = MapGridBattleAdapter.Build(seed, mapGridSettings, mapDocument, _mapGridPresetOverride);
```

### `DraftController`
```csharp
public MapSource SelectedMapSource { get; private set; } = MapSource.Legacy;
public MapGridPreset? SelectedMapGridPreset { get; private set; }
public void SetMapSource(MapSource src) { SelectedMapSource = src; battleBridge?.SetMapSource(src); }
public void SetMapGridPreset(MapGridPreset? p) { SelectedMapGridPreset = p; battleBridge?.SetMapGridPresetOverride(p); }
```

### `MapSettingsPanelView`
- 패널 최상단 "Map Source" row: Legacy / MapGrid 2 버튼.
- MapGrid 선택 시: Preset row (Auto / Wide30x15 / Square20x20 / Tall10x20 4 버튼). 그 외 4 row (Path/Size/Density/SpawnLanes) 는 `SetActive(false)`.
- Legacy 선택 시: 기존 4 row 복원, Preset row 숨김.

## 완료 기준

- [ ] 컴파일 0 ERROR.
- [ ] 기존 EditMode 테스트 0 회귀.
- [ ] PlayMode: Legacy 토글 → 기존 procedural 동작. MapGrid 토글 → 새 generator 동작.
- [ ] MapGrid + Preset=Wide30x15 → 항상 30×15 맵 생성 (seed 와 무관).
- [ ] MapGrid + Preset=Auto → seed 에 따라 3 preset 순환.
- [x] 2026-05-23 · b96fd8f
