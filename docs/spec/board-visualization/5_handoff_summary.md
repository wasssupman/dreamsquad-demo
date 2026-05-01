# Board Visualization — Handoff Summary

**작성일**: 2026-04-24  
**상태**: in progress  
**기준 문서**: `docs/spec/board-visualization/`

## 한 줄 요약

보드 시각화 리셋은 문서 기준 재정의, `BoardVisualPlan` 도입, `MapView` 부분 이관, forest 테마 자산 재생성까지 진행됐다.  
현재 `Env` 쪽은 이전보다 확실히 나아졌고, 남은 핵심 문제는 `Place` 구역의 경계감과 내부 반복감이다.

## 현재 기준

이 작업은 더 이상 오픈월드형 terrain generation 이 아니다.

- `Walk`, `Place`, `Env` 의 gameplay zone 은 유지
- 시각적으로는 셀 단위가 아니라 연결된 보드 단위로 읽히게 구성
- 참고 방향은 `Enter the Gungeon` 류의 room/board 기반 시각화

즉, 핵심은 `terrain simulation` 이 아니라 `board visualization pipeline` 이다.

## 활성 문서

- [README.md](/Users/sy/dev/wassup/docs/spec/board-visualization/README.md)
- [0_scope_and_goals.md](/Users/sy/dev/wassup/docs/spec/board-visualization/0_scope_and_goals.md)
- [1_board_visual_plan.md](/Users/sy/dev/wassup/docs/spec/board-visualization/1_board_visual_plan.md)
- [2_zone_transition_rules.md](/Users/sy/dev/wassup/docs/spec/board-visualization/2_zone_transition_rules.md)
- [3_decor_placement_rules.md](/Users/sy/dev/wassup/docs/spec/board-visualization/3_decor_placement_rules.md)
- [4_implementation_review_loop.md](/Users/sy/dev/wassup/docs/spec/board-visualization/4_implementation_review_loop.md)
- [5_handoff_summary.md](/Users/sy/dev/wassup/docs/spec/board-visualization/5_handoff_summary.md)

기존 `docs/spec/background-props/` 문서군은 legacy 로 본다.  
legacy handoff 는 [8_handoff_summary.md](/Users/sy/dev/wassup/docs/spec/background-props/8_handoff_summary.md) 에 남아 있다.

## 구현 완료 범위

### 1. `BoardVisualPlan` 기반 도입

추가된 핵심 타입:

- [BoardZoneType.cs](/Users/sy/dev/wassup/Assets/_Project/Scripts/Data/BoardZoneType.cs)
- [BoardShapeType.cs](/Users/sy/dev/wassup/Assets/_Project/Scripts/Data/BoardShapeType.cs)
- [BoardShapeUtility.cs](/Users/sy/dev/wassup/Assets/_Project/Scripts/Data/BoardShapeUtility.cs)
- [BoardDecorAnchorType.cs](/Users/sy/dev/wassup/Assets/_Project/Scripts/Data/BoardDecorAnchorType.cs)
- [BoardDecorAnchor.cs](/Users/sy/dev/wassup/Assets/_Project/Scripts/Data/BoardDecorAnchor.cs)
- [BoardVisualCell.cs](/Users/sy/dev/wassup/Assets/_Project/Scripts/Data/BoardVisualCell.cs)
- [BoardVisualRegion.cs](/Users/sy/dev/wassup/Assets/_Project/Scripts/Data/BoardVisualRegion.cs)
- [BoardVisualPlan.cs](/Users/sy/dev/wassup/Assets/_Project/Scripts/Data/BoardVisualPlan.cs)
- [BoardVisualPlanBuilder.cs](/Users/sy/dev/wassup/Assets/_Project/Scripts/Data/BoardVisualPlanBuilder.cs)

현재 builder 산출물:

- `Deco -> Env` folding
- connected region grouping
- `sameZoneMask`
- `transitionMask`
- `envNeighborMask`
- generic `shapeClass`
- `pathProximity`
- `borderProximity`
- basic decor anchor (`RegionCenter`, `RegionEdge`)

유지한 원칙:

- `sourceTileType` 보존
- board edge 는 transition 으로 취급하지 않음
- renderer 가 필요한 최소 시각화 정보만 plan 에서 공급

### 2. `MapView` 의 부분 이관

핵심 파일:

- [MapView.cs](/Users/sy/dev/wassup/Assets/_Project/Scripts/Core/MapView.cs)

현재 반영된 내용:

- 초기화 시 `BoardVisualPlanBuilder.Build(map, map.seed)` 호출
- `Walk` yaw 는 `BoardShapeType` 기반
- `Place` edge overlay 는 `visualCell.envNeighborMask` 기반
- `BuildTerrainDetails()` 는 `_visualPlan.DecorAnchors` 기반
- 기존 `ContinuousTerrainTop` 제거
- `Env` 는 보드 전체 한 장이 아니라 region/run surface 로 렌더
- `Place` edge 는 2-layer overlay 로 톤다운

아직 남은 점:

- `MapView` 가 여전히 일부 규칙을 직접 해석한다
- 최종적으로는 `BoardVisualPlan -> render descriptor -> renderer` 형태가 더 적합하다

### 3. `BackgroundPropPlacer` 는 아직 plan 미소비

핵심 파일:

- [BackgroundPropPlacer.cs](/Users/sy/dev/wassup/Assets/_Project/Scripts/Data/BackgroundPropPlacer.cs)

현재 상태:

- 입력은 여전히 `GeneratedMap + MapThemeData + seed`
- 내부에서 직접 flood fill region 계산
- `BoardVisualPlan` 을 아직 읽지 않는다

즉, renderer 만 부분 이관됐고 placer 는 아직 legacy 쪽에 더 가깝다.  
`renderer 와 placer 가 plan 을 공통 소비` 한다는 목표는 아직 반만 달성된 상태다.

### 4. forest 자산 교체 및 퀄업

새 atlas:

- [forest_env_surface_atlas_v2.png](/Users/sy/dev/wassup/Assets/_Project/Art/Theme/forest/forest_env_surface_atlas_v2.png)
- [forest_transition_decal_atlas_v2.png](/Users/sy/dev/wassup/Assets/_Project/Art/Theme/forest/forest_transition_decal_atlas_v2.png)
- [forest_place_surface_atlas_v2.png](/Users/sy/dev/wassup/Assets/_Project/Art/Theme/forest/forest_place_surface_atlas_v2.png)

실사용 텍스처:

- [tile_forest_grass1.png](/Users/sy/dev/wassup/Assets/_Project/Art/Theme/forest/tile_forest_grass1.png)
- [tile_forest_grass2.png](/Users/sy/dev/wassup/Assets/_Project/Art/Theme/forest/tile_forest_grass2.png)
- [tile_forest_smallgrass1.png](/Users/sy/dev/wassup/Assets/_Project/Art/Theme/forest/tile_forest_smallgrass1.png)
- [tile_forest_biggrass2.png](/Users/sy/dev/wassup/Assets/_Project/Art/Theme/forest/tile_forest_biggrass2.png)
- [tile_forest_place_grass_edge.png](/Users/sy/dev/wassup/Assets/_Project/Art/Theme/forest/tile_forest_place_grass_edge.png)
- [tile_forest_env_detail_patch_v2.png](/Users/sy/dev/wassup/Assets/_Project/Art/Theme/forest/tile_forest_env_detail_patch_v2.png)
- [tile_place.png](/Users/sy/dev/wassup/Assets/_Project/Art/Theme/forest/tile_place.png)
- [tile_place_variant_a.png](/Users/sy/dev/wassup/Assets/_Project/Art/Theme/forest/tile_place_variant_a.png)
- [tile_place_variant_b.png](/Users/sy/dev/wassup/Assets/_Project/Art/Theme/forest/tile_place_variant_b.png)
- [tile_place_variant_c.png](/Users/sy/dev/wassup/Assets/_Project/Art/Theme/forest/tile_place_variant_c.png)
- [tile_place_variant_d.png](/Users/sy/dev/wassup/Assets/_Project/Art/Theme/forest/tile_place_variant_d.png)

theme 반영:

- [forest.asset](/Users/sy/dev/wassup/Assets/_Project/Map/Theme/forest/forest.asset)
  - `terrainDetailTextures` 를 `tile_forest_env_detail_patch_v2` 로 교체
  - `placeTileTexture` / `placeTileVariants` 를 새 slab variant 세트로 교체

## 현재 화면 판단

검증 스크린샷:

- [board_visual_battle_gameview_clean.png](/Users/sy/dev/wassup/Assets/Screenshots/board_visual_battle_gameview_clean.png)
- [board_visual_battle_gameview_clean_v2.png](/Users/sy/dev/wassup/Assets/Screenshots/board_visual_battle_gameview_clean_v2.png)
- [board_visual_battle_sceneview_clean.png](/Users/sy/dev/wassup/Assets/Screenshots/board_visual_battle_sceneview_clean.png)
- [board_visual_battle_sceneview_clean_v2.png](/Users/sy/dev/wassup/Assets/Screenshots/board_visual_battle_sceneview_clean_v2.png)

현재 판단:

- `Env` 배경은 이전의 “보드 전체 한 장 반복 텍스처” 단계에서 벗어났다
- 새 forest surface 자산 덕분에 잔디 면은 이전보다 훨씬 자연스럽다
- `Place` 는 완전 흰 판처럼 뜨던 상태에서 벗어났다
- `Place` variant 추가로 내부 반복감은 일부 줄었다
- `Place` edge 는 2-layer overlay 로 이전보다 덜 튄다

아직 부족한 점:

- `Place` 외곽이 여전히 “배치 구역”처럼 분리되어 읽힌다
- `Place` 내부 seam 반복이 남아 있다
- 완성도 기준으로는 `Env` 보다 `Place` 가 더 큰 병목이다

## 검증 및 테스트

최근 확인 기준:

- `BoardVisualPlanBuilderTests` 통과
- `TerrainTileShapeUtilityTests` 통과
- `TerrainSurfaceSelectorTests` 통과

최근 묶음 기준으로:

- board/shape/proximity 관련 9개 테스트 통과
- terrain surface selector 4개 테스트 통과

에디터 확인:

- Unity console 기준 새 렌더 에러는 없었다
- game view 검증은 플레이 중 UI canvas 를 임시로 비활성화한 뒤 캡처하는 방식으로 진행했다

## critic review 요약

반복적으로 나온 지적:

1. `MapView` 는 아직 완전한 consumer 가 아니고 일부 규칙 재해석이 남아 있다
2. `Place` / `Env` transition 의 source of truth 를 더 좁혀야 한다
3. `ContinuousTerrainTop` 한 장 구조는 버리는 게 맞았고, 실제로 region/run surface 로 전환했다
4. 이후에는 renderer 가 `GeneratedMap + theme` 를 직접 읽기보다 `BoardVisualPlan` 또는 그 파생 descriptor 를 읽는 쪽이 맞다

## 남은 리스크

### 1. `Place` 표현

현재 가장 눈에 띄는 문제다.

- edge 가 아직 조금 artificial 하다
- 내부 slab seam 반복이 있다

다음 단계는 둘 다 필요하다.

- edge 를 decal 의존에서 더 줄이고 `inner rim tint + soft fringe` 쪽으로 이동
- seam 강도가 약한 variant 세트로 재교체하거나, variant 분산 방식을 더 조정

### 2. leak warning

플레이 중 `BattleBridge.StartBattle()` 을 반복 호출하며 검증할 때 다음 경고가 간헐적으로 보였다.

- `Leak Detected : Persistent allocates ...`

이건 본선 시각화와 분리해서 볼 수는 있지만, 누적시키면 안 된다.  
`GeneratedMap`, battle restart, Native 리소스 정리 경로를 별도로 추적할 필요가 있다.

우선 확인 대상:

- `BattleBridge.BuildMapForBattle` 전후의 이전 `GeneratedMap.Dispose()` 여부
- battle restart 시 Native owner 정리 순서
- `BackgroundPropPlacer` 의 `NativeArray<bool>` 예외 경로 누락 여부

### 3. render descriptor 부재

지금은 `BoardVisualPlan` 까지는 들어왔지만, `MapView` 전용 render descriptor 계층은 아직 없다.

예:

- zone base style
- transition style
- detail placement plan
- prop/decor placement plan

## 작업 트리 주의사항

현재 워크트리에는 이번 보드 시각화 작업 외에도 background props / theme / battle bridge 관련 변경이 섞여 있다.  
특히 아래 파일은 본 작업과 무관한 dirty 상태이므로 건드리지 말 것.

- `Assets/PixPlays/ElementalAOE/WindAOE/Version_BuiltIn/Materials/WindAoeSmokeMat.mat`

또한 `docs/spec/background-props/8_handoff_summary.md` 는 legacy 문서다.  
신규 handoff 는 이 문서를 기준으로 이어간다.

`docs/spec/background-props/9_terrain_surface_rules.md` 역시 archive 로 동결한다.  
surface rule 관련 현재 기준은 `docs/spec/board-visualization/` 에서만 갱신한다.

## 다음 우선순위

1. `Place` edge 를 더 얇고 덜 인공적으로 조정
2. `Place` seam 반복감 추가 완화
3. `Leak Detected : Persistent allocates` 원인 추적
4. `BackgroundPropPlacer` 입력을 `BoardVisualPlan` 으로 전환하고 테스트 재작성
5. `MapView` 의 남은 direct rule 해석 경로 축소
6. `transitionMask` / `envNeighborMask` / resolver edge mask 단일 소스화
7. anchor 확장과 anchor 계약 테스트 보강

## 빠른 진입 파일

- [MapView.cs](/Users/sy/dev/wassup/Assets/_Project/Scripts/Core/MapView.cs)
- [BoardVisualPlanBuilder.cs](/Users/sy/dev/wassup/Assets/_Project/Scripts/Data/BoardVisualPlanBuilder.cs)
- [BoardVisualPlan.cs](/Users/sy/dev/wassup/Assets/_Project/Scripts/Data/BoardVisualPlan.cs)
- [BoardVisualCell.cs](/Users/sy/dev/wassup/Assets/_Project/Scripts/Data/BoardVisualCell.cs)
- [BoardShapeUtility.cs](/Users/sy/dev/wassup/Assets/_Project/Scripts/Data/BoardShapeUtility.cs)
- [forest.asset](/Users/sy/dev/wassup/Assets/_Project/Map/Theme/forest/forest.asset)
- [board_visual_battle_gameview_clean_v2.png](/Users/sy/dev/wassup/Assets/Screenshots/board_visual_battle_gameview_clean_v2.png)
