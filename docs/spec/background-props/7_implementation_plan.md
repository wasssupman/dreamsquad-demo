# Implementation Plan

**작업 구분**: 7 / Plan

## 현재 상태

초기 prototype 은 완료됐고, 현재는 background prop 배치와 terrain surface rule prototype 까지 구현됐다.

- `PropData`
- `PropBillboard`
- `PropDataEditor.Generate Billboard Prefab`
- `prop_prototype_1_1.asset`
- `prop_prototype_1_1.prefab`

미구현:

- Walk shape/corner/edge transition texture.
- Place/background edge mask.
- 디자이너용 batch validation/generator.
- Play mode 장시간 smoke.

## 1단계 — 문서/계약 정리

- README 를 배치 알고리즘 중심으로 재정의.
- Data/Theme 와 Art/Theme 경로 규칙 확정.
- footprint 기준점 좌하단 셀로 확정.

완료 기준:

- `4_theme_asset_layout.md`
- `5_footprint_placement_algorithm.md`
- `6_runtime_instantiation.md`

## 2단계 — Data 확장

- `MapThemeData` 에 `PropData[] tileProps`, `PropData[] decorProps` 추가.
- `PropData` 에 v1 placement 필드 추가 여부 결정:
  - `weight`
  - `allowedTiles`
  - `blocksPlacement`
  - `placementSurface`

초기 구현은 필드를 최소화한다.

- `tileProps` 안의 프랍은 `Deco/Env` 에만 배치.
- random weight 없음.
- Place 소모 없음.

## 3단계 — Editor Generator 보강

- theme 경로에서 Art PNG 매칭.
- prefab 출력 경로를 `Assets/_Project/Prefabs/Props/{themeName}/` 로 분기.
- filename footprint validation.

## 4단계 — Placement Algorithm

- `BackgroundPropPlacer` 추가.
- `CanFit` 구현.
- occupancy grid 구현.
- seeded random 선택 구현.
- `PropPlacement` record 구현.

테스트:

- 1x1 배치.
- 2x1 배치.
- 1x2 배치.
- 2x2 배치.
- bounds 초과 거부.
- Walk/Place 타일 거부.
- occupancy 중복 거부.
- seed determinism.

## 5단계 — Runtime Instantiation

- `BackgroundPropSpawner` 추가.
- BattleBridge 또는 map orchestration 에 연결.
- Play smoke 로 실제 맵 위 표시 확인.

## 6단계 — Decor Props

- 우선 prefab 수동 배치 workflow 문서화.
- 자동 외곽 배치는 tile prop 안정화 후 진행.

## 7단계 — Terrain Surface Rules

- `MapThemeData` 에 tile type 별 surface rule 배열 추가.
- `TerrainSurfaceSelector` 로 surface rule 평가를 분리.
- `MapView` 는 selector 결과 texture 를 material cache 로 렌더링.
- Forest/Volcano prototype texture 와 rule 값 구성.
- 3D 카메라용 타일 텍스처 import 설정 정규화.

완료 기준:

- [x] `Env/Deco` background tile 이 테마별 surface rule 로 texture 를 선택한다.
- [x] Forest 는 grass/smallgrass/biggrass 군집을 표현한다.
- [x] Volcano 는 burn grass/burn land 분포를 표현한다.
- [x] rule/fallback 동작에 대한 EditMode test 가 있다.

## 8단계 — Terrain Transitions

- RuleTile-like resolver 추가.
- `Walk` 셀의 4방향 이웃 기반 shape 판별.
- straight/corner/end/T/cross 전용 path texture 선택.
- shape 방향에 맞춘 tile top 회전.
- `Place` 와 background tile 사이 edge mask 계산.
- Forest/Volcano transition texture 추가.
- 경로 주변 1칸의 눌린 풀/탄 자국 같은 surface rule 보정.

완료 기준:

- [x] `TerrainTileRuleResolver` 가 cell 별 render info 를 반환한다.
- [x] 경로 straight/corner/end 가 서로 다른 texture 로 렌더링된다.
- [ ] `Place` 와 `Env/Deco` 경계가 갑작스럽게 끊기지 않는다.
- [ ] transition texture 가 seed 재현성을 유지한다.
- [ ] Play mode screenshot 으로 전체 맵 지형 연속성을 확인한다.

## 완료 기준

- Unity compile 0 errors.
- EditMode tests 추가 및 통과.
- Play smoke: procedural map 생성 후 배경 타일 영역에 prop 자동 배치.
- 같은 seed 재생성 시 placement 동일.
