# Draft Stage Map Prebuild — Handoff Summary

**완료일**: 2026-04-30
**상태**: 구현 완료 + EditMode 170/170 + critic 1회 ACCEPT WITH MINOR FIXES + PlayMode V1~V10 사용자 확인 통과.

## Commit

| 범위 | 해시 | 설명 |
|---|---|---|
| spec docs | `259e900` | spec 작성 (README + Units 0~5 + handoff template + _session_handoff). Critic 1회 REVISE → CRITICAL/MAJOR 6건 반영 후 commit |
| Unit 0 | `1c6fa1e` | EnsureQueriesAndQueues 분리 + PrepareDraftMap / RebuildDraftMap / HasGeneratedMap + BeginPlacement 폴백 가드 |
| Unit 1 | `3833c8a` | CleanupDraftMapBeforeRebuild + DestroyEntitiesByType + MapView.ResetVisualRoots |
| Unit 2 | `3d3cb28` | GameManager.Start 가 BeginDraft 직전 PrepareDraftMap 호출 |
| Unit 3 | `af12211` | DraftController.SetMapGenerationOptions / SetMapPathShape rebuild 트리거 + BattleBridge.OnRedraftRequested 가 PrepareDraftMap 재호출 |
| Unit 4 | `19bc188` | EditMode 7 테스트 + RebuildDraftMapCallCount (UNITY_INCLUDE_TESTS 가드) |

## Implemented

- `BattleBridge.PrepareDraftMap()` — World ready 체크 + 1 frame deferred coroutine + EnsureQueriesAndQueues + BuildMapForBattle
- `BattleBridge.RebuildDraftMap()` — CleanupDraftMapBeforeRebuild + BuildMapForBattle
- `BattleBridge.CleanupDraftMapBeforeRebuild()` — Hazard / BlockingHazard / Obstacle entity destroy + MapView.ResetVisualRoots + ClearBlockingHazardVisuals + SO registry clear + map/flow teardown
- `BattleBridge.HasGeneratedMap` getter
- `BattleBridge.EnsureQueriesAndQueues()` 멱등화 (`_ecsInfrastructureReady`); reset 책임은 `TeardownCurrentBattle` + `OnDestroy` 만
- `BattleBridge.BeginPlacement` — `!_generatedMap.IsCreated` 폴백으로 기존 직접 진입 경로 보존
- `BattleBridge.OnRedraftRequested` — TeardownCurrentBattle 후 PrepareDraftMap 재호출 (Redraft 시 새 맵)
- `MapView.ResetVisualRoots` — obstacles / background props / goal marker root 방어용 teardown
- `GameManager.Start` — BeginDraft 직전 PrepareDraftMap
- `DraftController.SetMapGenerationOptions / SetMapPathShape` — 옵션 변경 시 즉시 RebuildDraftMap

## Key Files

Bridge: `Assets/_Project/Scripts/Bridge/BattleBridge.cs` (Unit 0/1/3/4)
Core: `Assets/_Project/Scripts/Core/GameManager.cs` (Unit 2), `DraftController.cs` (Unit 3), `MapView.cs` (Unit 1)
Tests: `Assets/_Project/Tests/EditMode/BattleBridgeDraftMapTests.cs` (4), `DraftControllerMapRebuildTests.cs` (3)

## Verified

- 컴파일 + Burst 활성
- EditMode 170/170 통과 (회귀 0; 신규 7 + 기존 163)
- Critic 1회 (Opus) — ACCEPT WITH MINOR FIXES. 자세한 사항은 본 문서 "Notes" 참조
- PlayMode V1~V10 사용자 manual 통과 — 카드 fan 뒤 맵 시각, MAP SETTINGS 즉시 갱신, Confirm/Placement/Battle/Redraft/Restart 회귀 0.

## Notes

### 비기능 변경 0
`BuildMapForBattle()` 함수 자체는 변경 0. `RestartBattle()` / `TeardownCurrentBattle()` 의 entity destroy 블록 변경 0. 8개 NativeQueue + 2개 NativeContainer 의 lifecycle 변경 0. 본 spec 은 시점 재배치 + cleanup 책임 보강만.

### Critic 지적 — 응답

- **MAJOR-1 (테스트 누락 3건)**: `RebuildDraftMap_50Iterations_NoEntityLeak`, `_NoMapViewChildLeak`, `OnRedraftRequested_RebuildsMap` 미작성. 사유: ECS World fixture 의 NativeContainer 누수 + EditMode 에서 mapView 가 null + OnRedraftRequested 가 private 이고 ResultScreen 이벤트 구독이 Awake 시점에 발생. PlayMode V7 (Redraft) / V10 (50회 토글) 이 같은 계약을 검증함. **Follow-up 후보**.
- **MINOR-1 (DeferredPrepareDraftMap 중복 발화)**: World 미준비 상태에서 1 frame 안에 옵션 토글 시 deferred coroutine 2회 큐잉 가능. `BuildMapForBattle` 자체가 dispose-then-build 라 누수 없음. PlayMode 시작 시점 race 한정. **Follow-up 후보**.
- **MINOR-2 (Redraft 시 ECS infra 재생성 비용)**: TeardownCurrentBattle 이 8 queue + singleton entity 전부 dispose 하므로 PrepareDraftMap 이 EnsureQueriesAndQueues 를 다시 돌림. 사용자 발생 빈도가 낮아 무시 가능. 빈도 증가 시 lighter teardown 경로 검토. **Follow-up 후보**.
- **NIT-1 (테스트의 seed 안정성 가정)**: 현재 fixture 의 `MapData` 기본 seed 가 0 으로 결정적이라 안전. 향후 fixture 변경 시 주의.

### Restart 동작 명시

`OnRestartRequested` 자체는 변경 X. `TeardownCurrentBattle` 이 맵을 dispose 하고, 이후 `BeginPlacement` 의 `!_generatedMap.IsCreated` 폴백 가드가 BuildMapForBattle 호출. 옵션 동일하므로 시각적으로 같은 맵. "맵 유지" 가 아니라 "재빌드 via 폴백" 이며 결과만 동일.

### MapView.ResetVisualRoots 의 위치

`MapView.Initialize` / `BuildTiles` / `BuildGoalMarker` / `InstantiateObstacles` / `InstantiateBackgroundProps` 는 모두 자체 root SafeDestroy + 재생성. `ResetVisualRoots` 는 cleanup-before-rebuild 사이의 방어 코드. 현재 코드 경로에서는 redundant 하지만 명시적으로 둠.

### Test 추가의 Production 영향

`RebuildDraftMapCallCount` (BattleBridge.cs:783~786) 만 추가. `#if UNITY_INCLUDE_TESTS` 가드로 비테스트 빌드에서 stripping.

## Follow-up

- EditMode `RebuildDraftMap_50Iterations_NoEntityLeak` — fixture 의 NativeContainer cleanup 패턴 정리 후 추가
- EditMode `RebuildDraftMap_50Iterations_NoMapViewChildLeak` — PlayMode 우선; EditMode 추가는 mapView mock 비용 측정 후
- `OnRedraftRequested_RebuildsMap` PlayMode 검증으로 충분; EditMode 추가는 ResultScreen 의존성 해소 후
- `DeferredPrepareDraftMap` 중복 발화 가드 (사실상 trigger 어려운 race)
- 옵션 토글 grid size 변경 트랜지션 (카메라 / 카드 fan 위치 보정) — 별도 spec
- 맵 빌드 비용 프로파일링 + debounce — 측정 후
- 카드 fan 영역 alpha / blur — UI 가독성
- draft 카메라 회전/줌 인터랙션 — 별도 spec
- Redraft 시 lighter teardown (ECS infra 보존) — 빈도 증가 시
