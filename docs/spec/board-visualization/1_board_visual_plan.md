# Board Visual Plan (rev3)

## 역할

`BoardVisualPlan` 은 `GeneratedMap + visualSeed + MapThemeData` 를 받아 renderer 와 placer 가 공통으로 소비하는 시각 projection 이다. renderer 는 셀 타입을 직접 해석하지 않고, placer 는 `GeneratedMap.tiles` / `spawns` / `goal` 을 직접 읽지 않는다.

`BoardVisualPlan` 은 immutable runtime plan 이다.

## 책임

- zone 별 region grouping
- 셀별 **8-이웃** zone adjacency 정보
- edge / outer corner / **inner corner mask** / strip / isolated 판정
- zone 별 surface style 선택 입력값 공급
- **5종 decor anchor** 생성
- `pathProximity`, `borderProximity`, `decorBudgetBias`, `surfaceNoiseHash` 파생값
- spawn/goal 좌표 노출 (placer 가 plan 단일 입력으로 동작 가능하도록)
- deterministic 결과

## Builder 입출력

입력:
- `GeneratedMap`
- `MapThemeData`
- `visualSeed`

출력 (필드):
- `cells[x, y]`
- `regions[]`
- `decorAnchors[]`
- `goal: int2`
- `spawns: int2[]`
- `gridSize: int2`, `seed: int`

생성 순서:
1. zone cell 분석 (`MapTileType -> BoardZoneType`, Deco→Env folding, 근거 `7_deco_resolution.md`)
2. cardinal 4-이웃 기반 connected region grouping
3. 8-bit `sameZoneMask` 계산
4. 4-bit `transitionMask` 계산
5. 4-bit `innerCornerMask` 계산 (대각 비트가 0 이고 인접 두 cardinal 비트가 1 인 지점)
6. `shapeClass` 분류 (16종, inner corner 제외)
7. `pathProximity`, `borderProximity` BFS
8. `surfaceNoiseHash`, `decorBudgetBias` 계산
9. 5종 anchor 생성
10. `goal` / `spawns` 복사

셀 필드 추가 / mask 계산은 `9_shape_mask_extension.md` 가 구현 단계에서 owns.

## 셀 단위 필드

| 필드 | 타입 | 의미 |
|---|---|---|
| `sourceTileType` | `MapTileType` | 원본 |
| `zoneType` | `BoardZoneType` | Walk / Place / Env |
| `regionId` | `int` | 연결 영역 식별자 |
| `sameZoneMask` | `byte` | 8-bit (N/NE/E/SE/S/SW/W/NW) |
| `transitionMask` | `byte` | 4-bit cardinal |
| `innerCornerMask` | `byte` | 4-bit (NE=1, SE=2, SW=4, NW=8) |
| `shapeClass` | `BoardShapeType` | 16종 |
| `surfaceNoiseHash` | `uint` | deterministic hash (visualSeed ^ x ^ y) |
| `decorBudgetBias` | `float` | 0~1 장식 밀도 제어 |
| `pathProximity` | `byte` | 255 = Walk 없음 |
| `borderProximity` | `byte` | 맵 외곽까지 거리 |

`sameZoneMask` bit 순서:
```
bit 0: N   bit 1: NE   bit 2: E   bit 3: SE
bit 4: S   bit 5: SW   bit 6: W   bit 7: NW
```

기존 4-bit consumer: `cardinal = mask & 0b01010101`.

## BoardShapeType (16종)

- `Isolated`
- `EndN`, `EndE`, `EndS`, `EndW`
- `StraightNS`, `StraightEW`
- `OuterCornerNE`, `OuterCornerNW`, `OuterCornerSE`, `OuterCornerSW`
- `TJunctionN`, `TJunctionE`, `TJunctionS`, `TJunctionW`
- `Cross`

**Inner corner 는 shape class 에 포함되지 않는다.** 동일 셀의 기본 shape 는 유지되고, `innerCornerMask` 의 비트마다 renderer 가 별도 overlay quad 를 올린다. 한 셀에 최대 4 개 overlay 까지 가능 (케이스 처리는 `10_place_rendering_finalization.md`).

## Region Grouping

- cardinal 4-이웃 연결 (대각 연결 금지).
- Region 필드: `id`, `zoneType`, `cellCount`, `min`, `max`, `anchorCell`.

## Decor Anchor (5종)

`BoardDecorAnchorType`: `RegionCenter`, `RegionEdge`, `OuterBorder`, `NearWalkButSafe`, `Filler`.
생성 규칙은 `12_decor_anchor_expansion.md`.

## Spawn / Goal

- `plan.goal = map.goal`
- `plan.spawns = map.spawns` (read-only copy)

placer 는 이 필드로 spawn/goal 회피 거리를 계산. 그 외 consumer 는 필요 시 참조.

## Renderer Contract

`MapView` 는 plan 만 읽는다.

1. 보드 base = 전체 공통 mesh
2. Env region = sub-tile noise 기반 multi-texture (`11`)
3. Walk = `shapeClass` + yaw 로 shape sprite (`10`)
4. Place = base slab + outer corner sprite + inner corner overlay per innerCornerMask bit (`10`)
5. detail / decor = anchor 를 따름

## Placer Contract

`BackgroundPropPlacer` 는 plan 만 읽는다.

- region iteration → `plan.Regions`
- 셀 정보 → `plan.CellAt`
- anchor 후보 → `plan.DecorAnchors`
- spawn/goal 회피 → `plan.spawns`, `plan.goal`

## Determinism

- `sameZoneMask`, `innerCornerMask`, `regionId` 는 map 구조만으로 결정.
- `surfaceNoiseHash`, `decorBudgetBias` 는 `visualSeed + (x, y)` 로 결정.
- anchor 생성은 map 구조만으로 결정 (random 배제).
- placer 의 Poisson/cluster/jitter 는 별도 `placementSeed` (deterministic 파생).
