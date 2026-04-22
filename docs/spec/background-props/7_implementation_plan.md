# Implementation Plan

**작업 구분**: 7 / Plan

## 현재 상태

구현된 것은 기본 프리팹 prototype 이다.

- `PropData`
- `PropBillboard`
- `PropDataEditor.Generate Billboard Prefab`
- `prop_prototype_1_1.asset`
- `prop_prototype_1_1.prefab`

미구현:

- Theme 경로 매칭.
- `MapThemeData.tileProps / decorProps`.
- footprint placement.
- runtime instantiate.
- test coverage.

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

## 완료 기준

- Unity compile 0 errors.
- EditMode tests 추가 및 통과.
- Play smoke: procedural map 생성 후 배경 타일 영역에 prop 자동 배치.
- 같은 seed 재생성 시 placement 동일.
