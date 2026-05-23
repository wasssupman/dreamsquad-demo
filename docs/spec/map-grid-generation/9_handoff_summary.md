# Unit 9 — Handoff Summary

> 본 spec 의 단위 0~13 구현 + 검증 + 커밋 완료. 2026-05-22 ~ 23 한 세션에서 마감.

## Commits

- `d601e91 feat(map-grid-generation): unit 0~1 — MapDocument SO + GeneratedMap meta + Settings`
- `e30c8a0 feat(map-grid-generation): unit 2~5 — placer + builder + validator + classifier`
- `b96fd8f feat(map-grid-generation): unit 6 + 10 — BattleBridge adapter + MapSettingsPanel toggle`
- `42d0fce feat(map-grid-generation): unit 7~8 — Editor debug window + integration/sweep tests`
- `7397198 docs(map-grid-generation): unit 11~13 spec — 6-section + custom size + adaptive turns`
- (이 커밋) `docs(map-grid-generation): close spec — handoff + status 완료`

## Implemented

- 새 namespace `Wassup.Data.MapGrid` 15+ C# 파일.
- `MapDocument` (authoring SO) + `MapDocumentBuilder` (authoring ↔ runtime 라운드트립).
- `GeneratedMap` 메타 NativeArray 3개 확장 (`mergeDegree`/`chokepoint`/`propLayerId`) + Dispose 5-array 안전.
- `GoalSpawnPlacer`: 6-section adaptive layout (3×2 / 2×3), section anchor zone 기반 seed-random goal + spawn.
- `PathRouter`: L/U/Z (1~3 turn) + 4-turn S + 5-turn W shape.
- `IncrementalPathBuilder`: attach-to-existing-path, isValidRoute, 2×2 block 회피, turn 많은 shape 우선 시도.
- `MapGridValidator`: connectivity / degree / 2×2 / branch length / branch turns + reject reason 6종.
- `MapGridGenerator`: outer 600 attempt 루프 + `Random.CreateFromIndex` 결정성 + `MapGenerationFailedException`.
- `CellClassifier`: Walk + Place 베이크 + mergeDegree/chokepoint emergent.
- `MapGridBattleAdapter`: settings null guard + MapDocument 캐시 우선 + `int2? gridSizeOverride` API + `MinGridDimension = 6` clamp.
- `BattleBridge`: `MapSource` enum (Legacy/Manual/Fixture/Procedural_Legacy/MapGrid) 도입, if/else → switch 마이그레이션. MapGrid 케이스에서 connectivity / prop placer skip + `MapGenerationFailedException` LogError + early return.
- `DraftController`: `SelectedMapSource` / `SelectedMapGridGridSize` + setter forwarding.
- `MapSettingsPanelView`: Map Source 토글 (Legacy/MapGrid) + MapGrid 선택 시 Preset quick-fill (Auto/30x15/20x20/10x20) + W/H 입력 (default 20×10) + min clamp.
- `MapGridDebugWindow`: Window/Wassup/Map Grid Debug — seed/preset 조작, SceneView gizmo, Sweep 통계, Bake to MapDocument.
- BattleScene 의 BattleBridge 에 `mapSource = MapGrid` + `MapGridGenerationSettings_Default` asset wiring.

## Key Files

### Runtime
- `Assets/_Project/Scripts/AssemblyInfo.cs` (`InternalsVisibleTo` for tests)
- `Assets/_Project/Scripts/Data/MapGrid/` × 15 파일
- `Assets/_Project/Scripts/Bridge/MapSource.cs`
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` (수정: switch + 3 SerializeField + 가드)
- `Assets/_Project/Scripts/Core/DraftController.cs` (수정: 신규 setter)
- `Assets/_Project/Scripts/UI/Draft/MapSettingsPanelView.cs` (수정: 패널 재구성)
- `Assets/_Project/Scripts/Data/GeneratedMap.cs` (수정: 3 NativeArray + Dispose)
- `Assets/_Project/Data/Maps/MapGridGenerationSettings_Default.asset`

### Editor
- `Assets/_Project/Editor/MapGrid/MapGridDebugWindow.cs`

### Tests (EditMode) × 9 파일
- `MapDocumentRoundTripTests`, `MapGridGenerationSettingsTests`, `GoalSpawnPlacerTests`,
  `IncrementalPathBuilderTests`, `MapGridValidatorTests`, `MapGridGeneratorTests`,
  `MapGridBattleAdapterTests`, `MapGridIntegrationTests`, `MapGridSeedSweepTests`.

## Verified

- **EditMode 전체**: 283 total / 281 pass / 0 fail / 2 pre-existing skip (`ModifierFrameworkTests` 2건 — 무관).
- **MapGrid 단독 sweep**: 3 preset × 50 seed 통과율 ≥ 90 %, 평균 attempt ≤ 100.
- **PlayMode smoke** (5 시나리오):
  - Default 20×10 → 정상 생성, S0 코너 goal, 다중 spawn (`Assets/Screenshots/mapgrid_default_20x10.png`).
  - Custom 25×12 → 정확히 25×12 (`mapgrid_custom_25x12.png`).
  - Preset Wide30x15 → S2 코너 goal, 4 spawn (`mapgrid_6section_wide_s2goal.png` / `mapgrid_30x15_4spawn.png`).
  - Preset Tall10x20 → S5 코너 goal (`mapgrid_6section_tall_s5goal.png` / `mapgrid_preset_tall.png`).
  - Square 20×20 → 4-turn S-shape (`mapgrid_4turn_20x20.png`).
- console: 0 ERROR (pre-existing "referenced script (Unknown)" 1건 — 무관).
- mapSource=Legacy 회귀 확인: 기존 ProceduralMapGenerator 경로 + Env/Deco 채움 + BackgroundPropPlacer 정상 동작.

## Notes

- **Env/Deco 셀은 생성하지 않는다.** MapGrid 모드에서 BattleBridge 가 `BackgroundPropPlacer` / `InstantiateObstacles` 호출 자체를 skip. 시각적으로 더 flat. 후속 theming spec 이 overlay 단계로 추가.
- **`MapConnectivity.AllSpawnsReachGoal` 후처리도 MapGrid 에선 skip.** Validator 가 이미 보장.
- `useProcedural` boolean + `MapSource.Procedural_Legacy` 는 살아 있음 — `mapSource=Legacy` 가 기존 동작 보존. cleanup spec 에서 제거 예정.
- `propLayerId` 는 schema slot 만 확보 (항상 0). 후속 theming spec 이 writer.
- `MapDocument.SetFrom` 은 `internal` — `AssemblyInfo.cs` 의 `InternalsVisibleTo` 로 테스트 접근.
- `MapGridGenerator.Generate(... out int attempts)` 오버로드는 테스트 전용.
- **Turn count cap = 4**. `EffectiveMinBranchTurnCount` = `min(4, max(SO, min(W,H)/4))`. 5-turn shape (`TryBuild5Turn`) 은 구현돼 있지만 보수적 cap. 20×20 만 4-turn 사용. 더 큰 grid 에서 5+ turn 활성화하려면 cap 상향 + sweep 재검증 필요.
- Runtime 오버라이드(`_mapGridGridSizeOverride`) 는 SerializeField 아님 — Play 진입마다 리셋. 영구 변경은 inspector 의 `mapGridSettings.AllowedPresets` 로.

## Follow-up

- **Cleanup spec**: 옛 `ProceduralMapGenerator` / `PathCarver` / `ObstaclePlacer` / `MapConnectivity` (generator-side) / `MapData` SO / `useProcedural` SerializeField / `MapSource.Procedural_Legacy` enum 제거.
- **PlayMode scene smoke 테스트** — `Assets/_Project/Tests/PlayMode/MapGrid/MapGridBattleBridgePlayModeTest.cs`. BattleScene 로드 + mapSource=MapGrid + attacker spawn → goal reach 검증. (현재 spec 안에 정의만, 파일 미생성.)
- **핸드크래프트 맵 에디터** — `MapDocument` 를 grid 셀 클릭으로 칠하는 EditorWindow.
- **Env/Deco theming spec** — `propLayerId` writer + `BackgroundPropPlacer` MapGrid 통합.
- **Edge goal 모드 spec** — section anchor 외 edge goal 지원.
- **Builder 7-turn+ shape** — wave/spiral 등 추가 + turn cap 상향. 매우 큰 grid (40×40+) 지원.
- **Burst-compile spec** — 핵심 inner loop (PathRouter, IncrementalPathBuilder, Validator) Burst 호환.
- **카메라 화각 / 타일 world scale 자동화** — preset/custom size 별 framing.
