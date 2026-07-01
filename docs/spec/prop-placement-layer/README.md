# prop-placement-layer — 맵 위 구조물/장식 프랍 배치 (View 토대)

> 상태: unit 0 완료(2026-07-02, compile). Spec 1 = 데코 토대 · 순수 View · Tilemap-only. 3-렌즈 리뷰 **GO-WITH-CHANGES** 반영. **D1 = KayKit 3D 메쉬**(2026-07-02, billboard 에서 전환).

## 배경 / 문제

goal/spawn 이 placeholder 로 표시된다 — Tilemap 모드는 납작한 `PH_Goal`/`PH_Spawn` 타일(`overlayTilemap`), Legacy3D 는 primitive ring+beacon(`MapView.BuildGoalMarker`). 맵 위 **지정 셀에 의도적으로 놓는 구조물/장식 프랍**을 데이터 구동으로 배치할 토대가 없다. 기존 배경 프랍(`InstantiateBackgroundProps`)은 weighted-scatter 전용이라 그대로는 못 쓴다.

## 검증 질문 (Spec 1)

기존 배경-프랍 인스턴스화 기계를 재사용해, **지정 셀에 3D 메쉬 구조물 프랍**(KayKit)을 똑바로 세워 build-time 에 배치하고, goal/spawn 을 그 프랍으로 렌더하며 placeholder 마커 타일을 억제하는가? (순수 View, sim 무변경.)

## 스코프 결정 (3-렌즈 리뷰, 2026-07-02)

Spec 1 은 **순수 View · Tilemap-only · 무추상**. 리뷰가 반려한 조기추상은 넣지 않는다:

- ❌ `PlaceProp/RemoveProp/PropHandle/dispatch/통합 인터페이스` — Spec-1 클라이언트 0 = 조기추상(규칙 위반). Spec-2 로 미룸.
- ❌ 단일 "PropSpawner" home — View→`TilemapMapView`, ECS→`BattleBridge` 분리. 통합하지 않음(BattleBridge 는 3693줄 god class).
- ❌ `BackgroundPropPlacer` 미러 — goal/spawn 은 알려진 셀. `PropPlacement` 인라인 2~3개면 됨.
- ✅ 재사용: `PropData`, `PropPlacement`, `InstantiateBackgroundProps`(→ `InstantiateProp` 추출, unit 0 완료).
- ✅ **D1: 구조물 = KayKit 3D 메쉬**(`Assets/KayKit/`). 리뷰 지목 실작업 = 프랍이 부모 90°X 회전을 상속해 눕는 문제 → `billboardMode=None` 메쉬용 **똑바로 세우기 처리**를 unit 1 로 추가.

## feature-wide 계약

1. 구조물/데코 프랍은 **순수 View GameObject**(Obstacle/엔티티 미부착) → goal 셀 pathing 봉쇄 없음. `GeneratedMap`/`FlowField`/sim 절대 미변경.
2. goal/spawn 프리팹 매핑 = `MapThemeData`(`goalStructureProp`/`spawnStructureProp : PropData`). 하드코딩 금지, 테마별 swap.
3. 배치 배선 = `TilemapMapView` 의 **별도 sibling 메서드**(`InstantiateRingProps` 패턴). BattleBridge 에서 자체 가드로 호출 — `theme.tileProps != null` 가드 안에 중첩 금지.
4. 마커 억제 = `PaintMarkers` 에서 구조물 프랍 있으면 goalTile/spawnTile skip.
5. **Tilemap-only.** Legacy `MapView.BuildGoalMarker` 손대지 않음(별도 backend 의무 생성 회피).
6. 구조물 프랍 = **3D 메쉬**(`billboardMode = None`). `InstantiateProp` 이 None-mode 를 감지해 월드 업라이트 회전(+yaw) 및 월드-공간 visualOffset 적용 — 기존 billboard 경로(부모회전 상속)는 보존. 메쉬는 depth-buffer 정렬(SpriteRenderer 정렬/틴트는 no-op).

## 작업 단위

| # | 작업 | 문서 | 목적 |
|---|---|---|---|
| 0 | InstantiateProp 추출 | `0_extract_instantiate_prop.md` | `InstantiateBackgroundProps` per-prop body → resolved `PropData` 받는 `InstantiateProp(...)`. **동작 무변경.** ✅ 완료 |
| 1 | 메쉬 프랍 업라이트 지원 | `1_mesh_prop_support.md` | `InstantiateProp` 에 `billboardMode==None` 분기 — 월드 업라이트 회전 + 월드 visualOffset. billboard 경로 보존. compile + 테스트 메쉬 육안 |
| 2 | goal/spawn 구조물 | `2_goal_spawn_structures.md` | `MapThemeData` 프랍 필드 + KayKit 프리팹 + goal/spawn 셀 인라인 배치 + sibling 호출 + 마커 억제. Play 시각 검증 |

## 후속 후보

- **데코 프랍 authored 배치** [S] · `MapThemeData.decorProps`(이미 예약됨) 지정 셀 배치. Spec 1 토대 위. 고정맵 저작 수요 시.
- **런타임 blocker 프랍** [M] · 방어유닛 파괴가능 여부(D2 미정). 기존 `destructible-blocking-hazards` spec + `EffectSpawner.SpawnBlockingHazard`(파괴가능) / `SpawnObstacle`(plain) 재사용. **Obstacle=Effects 유지**, BattleBridge 직접 EM(새 NativeQueue 금지). "prop 수렴"은 실측 후 결정(전제 아님).
- **적유닛 발원 blocker** [M] · sim 발원 → `HazardSpawnRequestsSingleton` 류 NativeQueue 채널.
- **billboard 구조물 변형** [S] · 3D 메쉬가 룩에 안 맞으면 `billboardMode=Tilted` sprite/spine 구조물로 대체(부모회전 상속 경로, 코드 무변경).
