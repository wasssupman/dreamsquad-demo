# Phase 10 — 맵 시스템 재설계 (Seed Procedural + Branch/Trunk/Root + Theme)

> Phase 10 은 Phase 9 의 flow field 엔진 위에 (1) 타일 4종 enum (Walk/Place/Env/Deco), (2) seed 기반 procedural 맵 생성, (3) branch/trunk/root 다중 spawn 레인 알고리즘, (4) 테마 오브젝트 배치, (5) 판 시작 시 맵 설정 UI 를 쌓아 올린 단계다. 맵은 매 판 seed 로 재생성되며 같은 seed 는 동일 맵을 보장한다. 상세 구현 스펙은 `docs/spec/map-system/` 에 작업 단위(0~20)로 분산되어 있다.

---

## 1. 목표

- 4-타입 enum 으로 타일 역할 분리 (Walk / Place / Env / Deco).
- `GeneratedMap` runtime struct + `Dispose` 로 판 단위 맵 소유.
- Seed-procedural 생성 + fallback 직선 맵.
- Branch/trunk/root 알고리즘으로 다중 spawn → shared trunk → single goal 구조.
- 테마 SO (`MapThemeData`) + forest 테마 에셋 + `ObstaclePlacer` 로 Walk/Place 비침범 배치.
- 판 시작 브리핑에서 path shape / map size / obstacle density / spawn lane count 설정 UI.
- Seed + generatorVersion + grid + spawnCount + pathShape 를 battle log 에 기록.
- `MapData.paths` `[Obsolete]` 필드 완전 제거.

### 비목표

- Env 타일의 환경 효과 동작 (Phase 11+).
- `ManualMapInput` 의 authoring UI 및 직렬화 (Phase 11+).
- Multi-cell obstacle, multi-goal, theme obstacle footprint 확장 (Phase 11+).
- generated map seed/version → QA 재현 플로우 자동화 (Phase 11+).

---

## 2. 확정 결정

| 항목 | 구현 결과 |
|---|---|
| 타일 enum | `MapTileType` byte 4종: Walk(0) / Place(1) / Env(2) / Deco(3). mutually exclusive |
| Runtime 모델 | `GeneratedMap` struct (IDisposable). `tiles: NativeArray<MapTileType>`, `gridSize`, `goal`, `spawns: NativeArray<int2>`, `generatorVersion` |
| RNG | `Unity.Mathematics.Random(seed)` (Burst-safe). `UnityEngine.Random` 금지 |
| Flow field walkable | Walk 타일 only (P10A-05) |
| Placement 판정 | Place 타일 only (P10A-06) |
| Spawn 레인 구조 | branch node → shared trunk → root(goal). branch 간 최소 y 간격 2, 기본 20×10 에서 최대 5 레인 |
| Lane 수 clamp | `MapGenerationOptions.Normalized()` 가 높이에 따라 최대치로 clamp |
| Connectivity 검증 | `MapConnectivity.AllSpawnsReachGoal` BFS. 실패 시 `BuildFallbackLinear` 로 직선 맵 |
| 테마 에셋 | `MapThemeData` SO (obstaclePrefab + obstacleMaterial 참조). forest 1종 (rock/tree/bush/flower) |
| Obstacle 배치 | `ObstaclePlacer` 단일 셀, density Low/Mid/High 에 따른 비율. Walk/Place 비침범 보장 |
| AttackDeck | `SpawnEntry.pathId: string` → `spawnIndex: int` 마이그레이션. out-of-range 는 index 0 fallback |
| 브리핑 UI | `TimelineBriefingView.BuildMapSettingsPanel` 에서 path shape / map size (W×H) / obstacle density / spawn lane count |
| 로그 | `BattleLogSchema.MapRecord` + `BattleLogger.LogMap` (seed/version/grid/spawnCount/pathShape) |
| Legacy 제거 | `MapData.paths` / `Paths` property / `PathDefinition` 클래스 + `PrototypeMap.asset` paths 블록 완전 삭제 |

---

## 3. 신규 / 수정 주요 파일

### 3.1 Data 모델 (신규)

- `MapTileType.cs` — enum 4종 (P10A-00)
- `MapGenerationSettings.cs` — gridWidth/Height/seed/generatorVersion SO (P10A-01)
- `GeneratedMap.cs` — runtime struct + `Dispose` + `CellIndex` / `TileAt` helpers (P10A-02)
- `MapGenerationOptions.cs` + `MapPathShape.cs` + `MapObstacleDensity.cs` — options + enum (P10B-11)
- `ManualMapInput.cs` — data shape only (P10B-17)

### 3.2 생성 / 검증 (신규)

- `BattleMapBuilder.cs` — `BuildFromFixture` / `BuildFromManual` / `BuildFallbackLinear` (P10A-03 / -03E)
- `ProceduralMapGenerator.cs` — `Generate(seed, gridSize, theme, version, pathShape, spawnLaneCount, minPlaceableRatio)` (P10B-11)
- `PathCarver.cs` — branch/trunk/root path carving (P10B-12)
- `MapConnectivity.cs` — BFS 다중 spawn → goal 도달 검증 (P10A-09)
- `ObstaclePlacer.cs` — 단일 셀 obstacle 배치 (P10B-14)
- `MapThemeData.cs` — 테마 SO (P10B-13)

### 3.3 통합 / 프레젠테이션 (수정)

- `BattleBridge.cs` — Phase 10 map orchestration owner: `mapSettings` / `useProcedural` / `mapTheme` / `mapPathShape` / `mapGenerationOptions` + `_generatedMap` 수명 + `BuildMapForBattle` + `BuildFlowField` 가 `_generatedMap.tiles` 참조 (P10A-04 / P10B-19)
- `MapView.cs` — 4-tile material + goal marker + theme obstacles 렌더 (P10A-07)
- `PlacementInput.cs` — Place-only 판정 (P10A-06)
- `DraftController.cs` — `SelectedMapGenerationOptions` + `SetMapGenerationOptions` + 판 시작 시 BattleBridge 로 전달
- `TimelineBriefingView.cs` — `BuildMapSettingsPanel` (Path Type / Map Size / Density / Spawn Lanes) + spawn-lane 기반 preview
- `BattleLogSchema.cs` / `BattleLogger.cs` — `MapRecord` + `LogMap` (P10B-16)
- `AttackDeck.cs` + `Decks/WaveA.asset` — `pathId` → `spawnIndex` 마이그레이션 (P10B-15)

### 3.4 테마 에셋 (신규)

- `Assets/_Project/Map/Theme/forest/` — forest 1종: rock/tree/bush/flower prefab + Materials 5종 + `forest.asset`

### 3.5 Legacy 제거

- `MapData.cs` — `paths` 필드 / `Paths` property / `PathDefinition` 클래스 삭제
- `PrototypeMap.asset` — paths 블록 제거

---

## 4. 테스트

EditMode 신규/수정:
- `MapTileTypeTests.cs`
- `GeneratedMapTests.cs`
- `BattleMapBuilderTests.cs`
- `MapConnectivityTests.cs`
- `ManualMapInputTests.cs`
- `ProceduralMapGeneratorTests.cs`
- `PathCarverTests.cs`
- `ObstaclePlacerTests.cs`
- `FlowFieldBuilderTests.cs` — walk-only 기준으로 수정

전체 EditMode 69/69 pass (2026-04-21 기준). Play smoke 는 pathShape=Straight, gridSize=20×10, spawnLaneCount=5 로 spawns=5 로그 + console error 0 확인.

---

## 5. 맥락 / 경계 준수

- ECS 맥락: `GeneratedMap` 은 MonoBehaviour (BattleBridge) 소유. ECS Component 는 기존 `FlowFieldSingleton` 만 사용 (Effects).
- 맥락 간 쓰기 위반 없음. 맵 변경 사항은 BattleBridge 가 BuildFlowField 를 통해 Effects 맥락으로 내려보냄.
- `BattleBridge` 가 MonoBehaviour ↔ ECS 유일 창구. 맵 스택(Data 레이어)은 전부 MonoBehaviour 측.

---

## 6. 종료 근거 / 인계

- 종료 시점: 2026-04-21
- 인계 요약: `docs/spec/map-system/20_claude_handoff_summary.md`
- Spec index: `docs/spec/map-system/README.md` (0~20 파일)
- Residual: `docs/residual-issues.md` — Phase 10 시점 미체크 항목 전부 drop. 클린 슬레이트.

**Phase 10 종료. Phase 11 범위 미정 → `docs/phase11-prep.md` 에서 결정.**
