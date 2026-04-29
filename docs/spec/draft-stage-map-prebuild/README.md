# Draft Stage Map Prebuild Spec

**작성일**: 2026-04-30
**상태**: 완료 2026-04-30 (코드 + EditMode 170/170 + PlayMode V1~V10 사용자 확인 통과).
**연결 문서**: `docs/plans/2026-04-30-draft-stage-map-prebuild-design.md`, `6_handoff_summary.md`

## 목표

드래프트 진입 시점에 `BuildMapForBattle()` 를 미리 호출해 맵을 풀스크린 배경으로 표시한다. 카드 fan / wave strip / MAP SETTINGS 패널은 그대로 위에 얹힌다 (오버레이). MAP SETTINGS 옵션 토글 시 즉시 재생성. placement 진입 시 재빌드 skip — 트랜지션 무비용.

## 작업 단위 목록

| # | 문서 | 목적 |
|---|---|---|
| 0 | `0_bridge_prepare_and_rebuild.md` | `EnsureQueriesAndQueues` 에서 `BuildMapForBattle` 분리 + `PrepareDraftMap` / `RebuildDraftMap` 신설 + `BeginPlacement` skip 가드 |
| 1 | `1_rebuild_cleanup_responsibilities.md` | RebuildDraftMap 시 mapView 의 obstacles/background props GameObject root 재생성 + ECS hazard / blockingHazard / placed obstacle entity destroy |
| 2 | `2_gamemanager_start_order.md` | `GameManager.Start` 가 `BeginDraft` 직전 `PrepareDraftMap` 호출 |
| 3 | `3_draft_controller_rebuild_triggers.md` | `DraftController.SetMapGenerationOptions` rebuild 트리거 + `BeginDraft` (Redraft) 의 새 seed 빌드 |
| 4 | `4_editmode_tests.md` | EditMode 단위 — `BattleBridgeDraftMapTests`, `DraftControllerMapRebuildTests` |
| 5 | `5_playmode_smoke_and_handoff.md` | PlayMode smoke + `6_handoff_summary.md` 작성 |

## 공통 원칙 (feature-wide 계약)

- **단일 build 함수**: `BattleBridge.BuildMapForBattle()` 가 유일한 맵 빌드 진입점. PrepareDraftMap / RebuildDraftMap / BeginPlacement(폴백) 모두 이걸 호출. 본 spec 은 시점만 재배치.
- **EnsureQueriesAndQueues / BuildMapForBattle 분리**: ECS 인프라 초기화(queues, singletons, queries) 는 PrepareDraftMap 진입 시 1회. BuildMapForBattle 은 분리되어 별도 호출.
- **idempotent map view (부분)**: MapView 의 tile/material 빌드는 이미 `BuildSharedMaterials` / `BuildTiles` 가 자체 cleanup. obstacles 와 background props root 는 OnDestroy 에서만 정리되므로 RebuildDraftMap 이 명시적으로 root 재생성 책임.
- **ECS entity 누적 0**: Rebuild 시 hazard / blockingHazard / placed obstacle entity 를 query 로 destroy. RestartBattle 의 destroy 블록(현재 line ~241~301) 의 부분 패턴 재사용.
- **placement 입력 차단**: `_placementAllowed = false` 가 PlacementInput 입력을 차단. draft 동안 PlacementInput.Initialize 가 미리 호출돼도 안전.
- **카메라 framing 즉시 갱신**: 옵션으로 grid size 가 바뀌면 `FrameMainCameraForMap` 이 다시 적용. 트랜지션 없이 점프 (현 spec 범위 밖).
- **테스트 범위**: EditMode 단위 + PlayMode smoke 1개. PlayMode 는 Start 직후 `_generatedMap.IsCreated == true`, 옵션 토글 후 새 seed, Confirm 후 mapView 재인스턴스 0 (자식 카운트 변화 0).
- **타 spec 계약 보존**: cc-pipeline-and-obstacle / path-zone-hazards / destructible-blocking-hazards / draft-ux-upgrade 의 기존 계약 모두 유지. 본 spec 은 시점 재배치 + cleanup 책임 보강만.
- **Redraft = 새 맵**: `OnRedraftRequested` 가 TeardownCurrentBattle 후 PrepareDraftMap 재호출. 같은 옵션 + 새 seed.
- **Restart = 맵 재빌드 (BeginPlacement 폴백)**: `OnRestartRequested` 흐름은 변경 X. `TeardownCurrentBattle` 이 맵을 destroy 하고, `BeginPlacementPhase` → `BeginPlacement` 의 `!_generatedMap.IsCreated` 폴백 가드가 BuildMapForBattle 을 호출. 시각적으로 "같은 판" 이지만 내부적으로는 재빌드 (옵션 그대로니 결과 동일).

## 검증 질문 (= 종료 조건)

1. **시각**: 게임 시작 시 카드 fan 뒤로 맵이 보이는가? MAP SETTINGS 토글 시 맵이 즉시 갱신되는가?
2. **placement 트랜지션**: Confirm → BeginPlacement 동안 mapView 자식 GameObject 카운트가 변하지 않는가?
3. **회귀 0**: 기존 placement / battle / Restart / Redraft 흐름이 동일하게 동작하는가? hazard / obstacle 시각/충돌이 정상인가?
4. **Rebuild 안정성**: 옵션 토글 50회 반복 시 ECS entity / NativeArray / GameObject 누적이 없는가?

## 후속 후보 (현 spec 범위 밖)

- 옵션 토글 시 grid size 변경 트랜지션 (카메라 / 카드 fan 위치 보정)
- 맵 빌드 비용 프로파일링 + debounce
- 카드 fan 영역 alpha / blur 처리 (UI 가독성)
- draft 단계 카메라 회전/줌 인터랙션
- Redraft 시 옵션 자동 reroll (현재는 옵션 유지)
