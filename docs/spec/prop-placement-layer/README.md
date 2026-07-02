# prop-placement-layer — 맵 위 구조물/장식 프랍 배치 (View 토대)

> 상태: **완료 2026-07-02** (units 0~1 · Play 검증 PASS). 순수 View · Tilemap-only. 3-렌즈 리뷰 **GO-WITH-CHANGES** 반영. **D1 = KayKit 3D 메쉬**(`platform_arrow`, billboard 아님).

## 배경 / 문제

goal/spawn 이 placeholder 로 표시된다 — Tilemap 모드는 납작한 `PH_Goal`/`PH_Spawn` 타일(`overlayTilemap`), Legacy3D 는 primitive ring+beacon(`MapView.BuildGoalMarker`). 맵 위 **지정 셀에 세우는 3D 구조물 프랍**을 데이터 구동으로 배치할 토대가 없다.

## 검증 질문 (Spec 1)

기존 프랍 인스턴스화 기계를 재사용해, **지정 셀에 3D 메쉬 구조물 프랍**(KayKit)을 똑바로 세워 build-time 에 배치하고, goal/spawn 을 그 프랍으로 렌더하며 placeholder 마커 타일을 억제하는가? (순수 View, sim 무변경.)

## 스코프 결정 (3-렌즈 리뷰 + 구현 발견, 2026-07-02)

Spec 1 은 **순수 View · Tilemap-only · 무추상**. 리뷰 반려 항목 + 구현 중 발견을 반영:

- ❌ `PlaceProp/RemoveProp/PropHandle/dispatch/통합 인터페이스` — Spec-1 클라이언트 0 = 조기추상. Spec-2 로.
- ❌ 단일 "PropSpawner" home — View→`TilemapMapView`, ECS→`BattleBridge` 분리.
- ❌ `BackgroundPropPlacer` 미러 — goal/spawn 은 알려진 셀. `PropPlacement` 인라인.
- ⚠️ **발견**: `billboardMode = None` 은 이미 "바닥에 눕는 sprite" 의미로 쓰인다(`prop_concept_*`·`prop_edge_forest_*` 8개, forest 테마). → None 을 hijack 하면 회귀. **건드리지 않는다.**
- ✅ **메쉬는 빌보드 무관** — 구조물 메쉬는 `PropBillboard` 없이, **역회전(-90°X) 구조물 root** 아래 두면 부모 90° 상쇄로 똑바로 선다. `InstantiateProp`(unit 0) 무변경.

## feature-wide 계약

1. 구조물 프랍은 **순수 View GameObject**(Obstacle/엔티티 미부착) → goal 셀 pathing 봉쇄 없음. `GeneratedMap`/`FlowField`/sim 절대 미변경.
2. goal/spawn 프리팹 매핑 = `MapThemeData`(`goalStructureProp`/`spawnStructureProp : PropData`). 하드코딩 금지, 테마별 swap.
3. 구조물 = **KayKit 3D 메쉬 프리팹**(PropBillboard 없음). `_structurePropsRoot` 를 `localRotation = Euler(-90,0,0)` 로 두어 부모(XZ 바닥 90°X)를 상쇄 → 메쉬 child 는 identity 로 똑바로 선다. 기존 `None`-mode sprite 는 무관(별도 `_backgroundPropsRoot`).
4. 마커 억제 = `InstantiateStructureProps` 가 구조물 놓은 셀의 overlay 타일을 `SetTile(cell, null)`. `PaintMarkers`/`Initialize` 시그니처 무변경.
5. 배치 배선 = `BattleBridge` 에서 `theme.tileProps` 가드와 **독립**한 별도 호출.
6. **Tilemap-only.** Legacy `MapView.BuildGoalMarker` 손대지 않음.

## 작업 단위

| # | 작업 | 문서 | 목적 |
|---|---|---|---|
| 0 | InstantiateProp 추출 | `0_extract_instantiate_prop.md` | per-prop body → resolved `PropData` 받는 메서드. 동작 무변경. ✅ 완료(e42e2c7) |
| 1 | goal/spawn 구조물 | `1_goal_spawn_structures.md` | `MapThemeData` 필드 + 역회전 구조물 root + `InstantiateStructureProps` + 마커 억제 + BattleBridge 배선 + KayKit authoring. ✅ 완료 |

## 후속 후보

- **데코 프랍 authored 배치** [S] · `MapThemeData.decorProps`(예약됨) 지정 셀 배치. Spec 1 토대 위.
- **런타임 blocker 프랍** [M] · 방어유닛 파괴가능 여부(D2 미정). 기존 `destructible-blocking-hazards` + `EffectSpawner.SpawnBlockingHazard`/`SpawnObstacle` 재사용. Obstacle=Effects 유지, BattleBridge 직접 EM(새 큐 금지).
- **적유닛 발원 blocker** [M] · sim 발원 → NativeQueue 채널.
- **구조물 footprint 다중셀 정합** [S] · 2×2+ 구조물의 셀 점유/정렬 정밀화(현재 셀 중앙 배치).
