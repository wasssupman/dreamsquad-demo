# Board Visualization Spec

**작성일**: 2026-04-24 (rev3)
**상태**: design rev3 — critic/architect 리뷰 반영
**목표**: 정형화된 grid 기반 전투 보드를 유지하면서, 화면에서는 타일 경계가 사라지고 바닥·프랍·경계·코너가 Enter the Gungeon 수준으로 자연스럽게 연결되는 시각 파이프라인을 만든다.

## 목표 재정의 (rev2 3축 유지)

1. **8-이웃 + inner corner overlay**. 같은 바닥이 L자/ㄱ자로 맞닿을 때 inner/outer corner 가 다른 sprite 로 분리된다.
2. **프랍 유기 분포**. Poisson-disk + cluster + rotation/scale jitter. 단순 가중치 랜덤이 아니다.
3. **Env 내부 sub-tile variation**. region-uniform 바닥을 폐기하고 noise 기반 multi-texture 로 덮는다.

## rev3 변경 (리뷰 반영)

- Deco 결정을 shape mask 작업 앞으로 이동 (`7`).
- `BoardVisualPlan` 에 `goal`, `spawns` 포함 (placer 가 plan 단일 입력으로 동작 가능하도록).
- Inner corner 는 **overlay-only**. `BoardShapeType` enum 에는 포함하지 않고 `innerCornerMask` + overlay quad 로만 표현.
- Shape 집합은 **16종** (outer corner 만 shape class, inner corner 는 mask).
- `BoardVisualCell` 필드 확장 (`innerCornerMask`, `surfaceNoiseHash`, `decorBudgetBias`) 을 `9` 작업에 명시적으로 귀속.
- `TerrainSurfaceSelector` 의 legacy shape 의존 제거를 `9` 범위에 포함.
- `placeInnerCornerTexture` null fallback 을 `10`, `14` 에 명시.
- prop rewrite (`13`) 는 decor anchor 5종 (`12`) 이후로 배치.

## 유지 원칙

- `Walk`, `Place`, `Env` gameplay zone 유지.
- `GeneratedMap.tiles` 가 gameplay source of truth.
- 시각화는 `BoardVisualPlan` 경유. renderer 와 placer 가 plan 만 소비.
- 테마 교체 가능. 구조는 유지되고 자산만 교체.

## 최상위 구조

```text
GeneratedMap
  -> BoardVisualPlan (cells, regions, anchors, goal, spawns)
  -> MapView                 (plan only)
  -> BackgroundPropPlacer    (plan only)
```

## 문서 목록 (rev3 순서)

| 번호 | 파일 | 역할 |
|---|---|---|
| README | `README.md` | 본 문서 |
| 0 | `0_scope_and_goals.md` | 범위 / 비범위 / Acceptance |
| 1 | `1_board_visual_plan.md` | plan 데이터 구조 (8-bit mask, inner corner mask, 5종 anchor, goal/spawns) |
| 2 | `2_zone_transition_rules.md` | shape 16종 + inner corner overlay taxonomy |
| 3 | `3_decor_placement_rules.md` | Poisson + cluster + jitter 알고리즘 |
| 4 | `4_implementation_review_loop.md` | 구현 단계와 review 절차 |
| 5 | `5_handoff_summary.md` | 직전 세션 인계 (참조용) |
| 6 | `6_leak_investigation.md` | Persistent allocates leak 추적 |
| 7 | `7_deco_resolution.md` | MapTileType.Deco 거취 결정 (rev3: 선행 확정) |
| 8 | `8_placer_plan_consumer.md` | BackgroundPropPlacer → BoardVisualPlan 전환 (spawn/goal via plan) |
| 9 | `9_shape_mask_extension.md` | 8-이웃 mask + cell 필드 추가 + SurfaceSelector 마이그레이션 |
| 10 | `10_place_rendering_finalization.md` | Place edge/corner + inner corner overlay + renderer 해석 단일화 |
| 11 | `11_env_surface_variation.md` | Env region 내부 variation + region 간 blend |
| 12 | `12_decor_anchor_expansion.md` | 5종 anchor 생성 (RegionCenter/Edge/OuterBorder/NearWalkButSafe/Filler) |
| 13 | `13_prop_distribution_rewrite.md` | Poisson + cluster + jitter 구현 |
| 14 | `14_theme_asset_contract.md` | 테마 자산 카테고리 + null fallback |
| 15 | `15_verification_loop.md` | 검증 체크리스트 |
| 16 | `16_visual_audit.md` | rev3 구현 이후 결함 카탈로그 (17~ 분기 근거) |
| 17 | `17_poisson_proper.md` | audit V-001/V-002: prop 분포/marker 정리 |
| 18 | `18_corner_asset_pass.md` | audit V-003: inner/outer corner asset pass |
| 19 | `19_place_edge_finish.md` | audit V-004: Place edge/fringe finish |
| 20 | `20_env_variation_tuning.md` | audit V-006: Env variation/blend tuning |
| 21 | `21_walk_shape_polish.md` | audit V-005: Walk shape polish |
| 22 | `22_theme_palette_pass.md` | audit V-007: 전체 palette/board feel pass |

## 작업 순서

번호 순서대로. 6 → 7 → 8 → 9 → 10 → 11 → 12 → 13 → 14 → 15.

- 6: 반복 검증 안정화 (인프라)
- 7: Deco zone 의미 확정 (mask 이전에 필요)
- 8: placer 를 plan 단일 입력으로 전환
- 9: plan cell 필드 확장 + shape mask 8-이웃화
- 10: Place 렌더 마감 + renderer 해석 단일화
- 11: Env 내부 variation + blend
- 12: anchor 5종 (prop rewrite 의 재료)
- 13: prop distribution rewrite (Poisson + cluster + jitter)
- 14: theme 자산 계약 고정
- 15: verification loop 고정
- 16: visual audit (rev3 결과 결함 카탈로그 → 17~ 분기)
- 17: prop distribution proper pass (audit High)
- 18: corner asset quality pass (audit High)
- 19: place edge/fringe finish (audit High)
- 20: env variation/blend tuning (audit High)
- 21: walk shape polish (audit Mid)
- 22: theme palette pass (audit High)

## 공통 계약 (rev3)

- `BoardVisualPlan` 출력: `cells[x,y]`, `regions[]`, `decorAnchors[]`, `goal: int2`, `spawns: int2[]`.
- `BoardVisualCell` 필드: `sourceTileType`, `zoneType`, `regionId`, `sameZoneMask (8-bit)`, `transitionMask (4-bit cardinal)`, `innerCornerMask (4-bit diagonal)`, `shapeClass`, `surfaceNoiseHash`, `decorBudgetBias`, `pathProximity`, `borderProximity`.
- `BoardShapeType` 집합 **16종**: `Isolated` + `End×4` + `Straight×2` + `OuterCorner×4` + `TJunction×4` + `Cross`. **inner corner 는 enum 에 없음**.
- Inner corner 표현: `innerCornerMask` 의 각 비트(NE/SE/SW/NW) 마다 같은 셀 위에 overlay quad 를 하나씩 올림. 한 셀 최대 4 overlay.
- `BoardDecorAnchorType`: `RegionCenter / RegionEdge / OuterBorder / NearWalkButSafe / Filler`.
- 프랍 분포: Poisson-disk seed → cluster 확장 → rotation/scale jitter → occupancy 검증.
- renderer 는 `BoardVisualPlan` 만 읽는다. `map.TileAt` 직접 호출 금지.
- placer 는 `BoardVisualPlan` 만 읽는다. `GeneratedMap.tiles` / `spawns` / `goal` 직접 참조 금지 (`plan.goal`, `plan.spawns` 사용).
- Deco: rev3 에서 `Env` 로 folding 유지. (자세한 근거 `7_deco_resolution.md`)
- `placeInnerCornerTexture` null 이면 inner corner overlay 를 그리지 않는다. outer corner 로 fallback 하지 않는다.

## 성공 기준 (v1)

- 같은 바닥이 L자로 맞닿을 때 inner corner overlay 가 시각적으로 구분된다.
- Env / Place 내부에서 2 종 이상 surface variation 이 관찰된다.
- 프랍이 cluster 또는 scatter 패턴, rotation/scale jitter 로 복붙 인상 없음.
- 같은 seed 에서 동일한 `BoardVisualPlan` + placement 재현.
- forest ↔ volcano 테마 교체 시 렌더 오류 없이 같은 구조 유지.
- `BattleBridge.StartBattle` 100 회 반복에서 Persistent leak 경고 0.

## legacy 폴더와의 관계

`docs/spec/background-props/` 는 legacy. rev3 기준 문서는 이 폴더만. legacy 폴더의 `9_terrain_surface_rules.md` 는 아카이브로 동결.
