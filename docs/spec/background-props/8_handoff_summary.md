# Background Props — Handoff Summary

**주의**: 이 문서는 legacy handoff 이다. 현재 기준 handoff 는 [docs/spec/board-visualization/5_handoff_summary.md](/Users/sy/dev/wassup/docs/spec/board-visualization/5_handoff_summary.md) 를 본다.

**작성일**: 2026-04-23<br>
**상태**: v1 구현 커밋 완료. 커밋 `9318879 Add background prop and styled tile rendering` 기준으로 theme 이미지 매칭, footprint placement, runtime instantiate, styled tile rendering, 캐릭터 0.7배 visual scale 조정까지 반영됐다. 이후 Main Camera 자동 프레이밍, background prop rule 고도화, terrain surface rule 고도화가 미커밋 상태로 추가됐다.

## Prototype Scope

- `PropData` ScriptableObject 초안 (`Wassup/PropData` 메뉴).
- `PropBillboard` 기본 런타임 컴포넌트.
- `PropDataEditor` Inspector 의 `Generate Billboard Prefab` 버튼 prototype.
- 샘플 기본 프리팹: `prop_prototype_1_1.asset` + 동명 prefab.

## Implemented In V1 Pass

- `MapThemeData.tileProps / decorProps` 연동.
- `Data/Theme/{themeName}` SO 와 `Art/Theme/{themeName}` PNG 매칭.
- `BackgroundPropPlacer.Generate` 로 배경 타일 가용 영역 flood fill + footprint 후보 필터 + 중앙 배치 우선 + seeded random placement.
- `PropPlacement` record.
- 1x1 외 2x1, 1x2, 2x2, 3x3 footprint occupancy 검증 테스트.
- `MapView.InstantiateBackgroundProps` 를 통한 runtime instantiate 연결.
- `BattleBridge.BuildMapForBattle` 에서 tileProps 가 있으면 background props 경로 사용, 없으면 기존 obstacle prefab 경로 유지.
- `PropDataEditor.Generate Billboard Prefab` 에서 theme 동명 PNG 자동 매칭, sprite import 설정 정규화, theme별 prefab output 지원.
- Forest theme prototype prop 이미지/PropData/prefab 15종과 이전 dummy 4종 추가.
- `MapThemeData` 에 tile texture/thickness/top scale/side color 설정 추가.
- `MapView` 타일 렌더링을 `SideBlock` cube + textured `Top` quad 구조로 변경.
- `Tile_Unlit` shader 추가.
- `BattleBridge.CharacterVisualScale = 0.7f` 로 적/수비/Spine/drag preview/health bar 비율을 타일 대비 축소.
- Main Camera 자동 프레이밍 추가: 맵 생성 후 `GeneratedMap.gridSize`, `tileSize`, FOV/pitch/padding 기준으로 전체 맵이 한눈에 들어오도록 위치/각도 계산. 이 변경은 현재 미커밋.
- `PropData.placementWeight / minDistanceCells` 추가.
- `MapThemeData` 에 repeat avoidance, spawn/goal density reduction, path-adjacent large prop penalty, large prop outer-region preference 파라미터 추가.
- `BackgroundPropPlacer` 는 후보별 density/weight/minDistance 를 평가하고, 최근 사용 프랍이 아닌 대체 후보가 있으면 반복 프랍을 제외한다.
- Forest theme prototype 값 조정: density `0.88`, max count `28`, repeat window `2`, spawn/goal radius `1`, spawn/goal multiplier `0.55`, large path-adjacent multiplier `0.35`, large inner multiplier `0.55`.
- 20x10 procedural map smoke sample 기준 배경 타일 약 59~62칸에서 프랍 28개, 점유 29칸 수준으로 튜닝했다.
- Tile surface quality pass:
  - `MapThemeData` 에 tile variant 배열과 variant noise 파라미터 추가.
  - Forest background tile variants: `grass1`, `grass2`, `smallgrass1`, `biggrass2`.
  - Volcano prototype variants: `burn_grass1`, `burn_land1` 과 `volcano.asset` 추가.
  - Prop runtime placement 은 tile 표면보다 살짝 위(`y=0.04`)에 배치하고, 화면 아래쪽 프랍이 앞에 오도록 `sortingOrder` 를 보정한다.
- Terrain surface rule pass:
  - `TerrainSurfaceSelector` 추가. `MapView` 내부 variant index 선택을 분리하고, 테마 데이터 기반 surface rule 평가 결과를 렌더링한다.
  - `MapThemeData` 에 `place/walk/env/decoSurfaceRules`, `pathSurfaceInfluence`, `edgeSurfaceInfluence` 추가.
  - Surface rule 은 base weight, low-frequency noise range, moisture/detail range, path proximity multiplier, edge multiplier 를 평가한다.
  - Forest theme 은 grass/smallgrass/biggrass 가 noise/moisture/path/edge 영향으로 군집되도록 env/deco rules 를 구성했다.
  - Volcano theme 은 burn grass 와 burn land 가 경로 근처/외곽/노이즈 기준으로 나뉘도록 env/deco rules 를 구성했다.
  - 새 타일 텍스처 import 설정을 3D 카메라용으로 `mipmap on`, `trilinear`, `aniso 4`, `uncompressed` 로 조정했다.
- Terrain transition foundation:
  - `TerrainTileShape` / `TerrainTileShapeUtility` 추가.
  - `Walk` 셀의 4방향 이웃을 기반으로 single/end/straight/corner/T/cross shape 를 판별한다.
  - `MapThemeData` 에 walk shape texture slot 을 추가했다.
  - `TerrainSurfaceSelector` 는 walk shape texture 가 있으면 surface rule/variant fallback 보다 우선 사용한다.
  - Forest/Volcano walk shape texture 6종씩 생성 후 theme 에 연결했다.
  - corner/end/T-junction 은 기준 방향 texture 를 사용하고, `MapView` 가 shape 에 맞춰 top quad 를 회전한다.
- Terrain continuity pass:
  - `Env/Deco` 를 셀별 top quad 로 렌더링하지 않는다.
  - 맵 전체에 `BoardBase` cube 와 `ContinuousTerrainTop` quad 를 만든다.
  - `Walk` 는 연속 경로 overlay, `Place` 는 개별 배치석 top 으로 분리했다.
  - Forest visual tuning: `tileTopScale 0.86`, `tileThickness 0.42`, side color 를 더 어둡고 중립적으로 조정했다.
- RuleTile-like resolver pass:
  - critic review 결과 `Env/Deco` surface rule 이 렌더 경로에서 우회되는 문제를 문서에 명시했다.
  - `TerrainTileRenderInfo` / `TerrainTileRuleResolver` 추가.
  - `MapView` 의 Walk/Place 렌더 결정과 continuous terrain texture 선택을 resolver 경유로 변경했다.
  - resolver 는 Unity RuleTile 을 직접 쓰지 않고 `GeneratedMap` 이웃 관계를 렌더 정보로 바꾸는 프로젝트 전용 경로다.
- Terrain decal prototype:
  - `MapThemeData.terrainDetailTextures / terrainDetailDensity / terrainDetailScale / placeBackgroundEdgeTexture` 추가.
  - `MapView` 가 Env/Deco 위에 seeded terrain detail overlay 를 생성한다.
  - Place 와 background 가 맞닿는 방향에 grass fringe edge overlay 를 생성한다.
  - 현재 grass fringe 는 절차적 prototype 이며, 화면 확인 결과 사각 패치 문제는 줄었지만 고품질 전용 decal asset 이 더 필요하다.

## Not Implemented Yet

- 디자이너용 batch generator / footprint gizmo / naming validation.
- 실제 Play mode 장시간 smoke 와 모바일 실기 프레임 확인.
- 경로 corner/edge 전용 타일.
- 더 정교한 biome/prop category 기반 배치 룰.

## Key Files

- `Assets/_Project/Scripts/Data/PropData.cs`
- `Assets/_Project/Scripts/Data/PropPlacement.cs`
- `Assets/_Project/Scripts/Data/BackgroundPropPlacer.cs`
- `Assets/_Project/Scripts/Data/MapThemeData.cs`
- `Assets/_Project/Scripts/Data/TerrainSurfaceSelector.cs`
- `Assets/_Project/Scripts/Data/TerrainTileRenderInfo.cs`
- `Assets/_Project/Scripts/Data/TerrainTileRuleResolver.cs`
- `Assets/_Project/Scripts/Data/TerrainTileShape.cs`
- `Assets/_Project/Scripts/Data/TerrainTileShapeUtility.cs`
- `Assets/_Project/Scripts/Core/MapView.cs`
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs`
- `Assets/_Project/Scripts/Presentation/PropBillboard.cs`
- `Assets/_Project/Editor/PropDataEditor.cs`
- `Assets/_Project/Shaders/Tile_Unlit.shader`
- `Assets/_Project/Tests/EditMode/BackgroundPropPlacerTests.cs`
- `Assets/_Project/Tests/EditMode/TerrainSurfaceSelectorTests.cs`
- `Assets/_Project/Tests/EditMode/TerrainTileRuleResolverTests.cs`
- `Assets/_Project/Tests/EditMode/TerrainTileShapeUtilityTests.cs`
- `Assets/_Project/Art/Theme/forest/`
- `Assets/_Project/Art/Theme/forest/tile_forest_walk_*.png`
- `Assets/_Project/Art/Theme/volcano/tile_volcano_walk_*.png`
- `Assets/_Project/Data/Theme/forest/`
- `Assets/_Project/Prefabs/Props/forest/`
- `Assets/_Project/Map/Theme/forest/forest.asset`
- `Assets/_Project/Map/Theme/volcano/volcano.asset`
- `docs/spec/background-props/`
- `docs/spec/background-props/9_terrain_surface_rules.md`

## Verification

- EditMode tests: `96/96` passed after character scale, camera framing, prop rule changes, tile variant pass, terrain surface rule pass, walk shape foundation, continuous terrain rendering pass, RuleTile-like resolver pass, and terrain decal prototype.
- `git diff --check`: passed.
- Unity console: cleared after the successful test run.

## Workspace Notes

- Latest committed implementation: `9318879 Add background prop and styled tile rendering`.
- Current uncommitted relevant changes:
  - `Assets/_Project/Scripts/Bridge/BattleBridge.cs` camera auto-framing.
  - `Assets/_Project/Scripts/Data/PropData.cs` prop placement weight/min distance.
  - `Assets/_Project/Scripts/Data/MapThemeData.cs` theme placement rules.
  - `Assets/_Project/Scripts/Data/TerrainSurfaceSelector.cs` terrain surface rule evaluation.
  - `Assets/_Project/Scripts/Data/TerrainTileShape*.cs` walk shape classification.
  - `Assets/_Project/Scripts/Data/BackgroundPropPlacer.cs` rule-based weighted placement.
  - `Assets/_Project/Tests/EditMode/BackgroundPropPlacerTests.cs` rule coverage.
  - `Assets/_Project/Tests/EditMode/TerrainSurfaceSelectorTests.cs` terrain surface rule coverage.
  - `Assets/_Project/Tests/EditMode/TerrainTileShapeUtilityTests.cs` walk shape coverage.
  - `Assets/_Project/Map/Theme/forest/forest.asset` prototype tuning.
  - `Assets/_Project/Art/Theme/forest/tile_forest_*.png` tile variants.
  - `Assets/_Project/Art/Theme/volcano/tile_volcano_*.png` tile variants.
  - `Assets/_Project/Map/Theme/volcano/volcano.asset` volcano prototype theme.
  - `Assets/_Project/Data/Theme/forest/*.asset` prototype prop rule values.
- Current unrelated dirty file left untouched: `Assets/PixPlays/ElementalAOE/WindAOE/Version_BuiltIn/Materials/WindAoeSmokeMat.mat`.

## Next Step

다음 구현은 카메라 자동 프레이밍 커밋 후 Play smoke 와 디자이너 도구 보강이다.

권장 순서:

1. Play mode 에서 실제 UI 포함 화면 프레이밍 확인.
2. `prop_{name}_{x}_{y}` filename validation 추가.
3. Theme 폴더 batch generator 추가.
4. footprint gizmo 추가.
5. Forest/Volcano walk shape 전용 texture 생성 및 theme 연결.
6. Battle start 이후 Play mode screenshot 으로 실제 지형 연속성 확인.
7. Place/background edge mask 와 더 정교한 biome transition 추가.

추가 spec:

- `9_terrain_surface_rules.md` 에 현재 surface rule 구조와 다음 terrain transition 구현 범위를 정리했다.
