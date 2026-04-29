# Kickoff Handoff — Draft Stage Map Prebuild

**Status**: spec 작성 완료, 구현 미착수.
**Spec 폴더**: `docs/spec/draft-stage-map-prebuild/` (README + 0~5 + handoff template).
**작성**: 2026-04-30.
**다음 작업자**: Codex CLI (구현). 사용자 직접 호출.
**전제 spec**: `docs/spec/draft-ux-upgrade/` (DraftView 흐름), `docs/spec/map-system/` (BuildMapForBattle 인프라), `docs/spec/destructible-blocking-hazards/` (BlockingHazard / HazardDestroyedEvent), `docs/spec/cc-pipeline-and-obstacle/` (Obstacle entity).

## 본 spec 의 자리

`BattleBridge.BuildMapForBattle()` 의 호출 시점을 `BeginPlacement` (DraftConfirmed 후) → `PrepareDraftMap` (Draft 진입 전) 으로 앞당긴다. 카드 fan 뒤로 풀스크린 맵 표시 + 옵션 토글 시 즉시 재생성 + placement 트랜지션 무비용. **순수 시점 재배치 + cleanup 책임 보강**. 게임 동작 변화 0.

## 핵심 결정 요약

| 결정 | 채택 | 이유 |
|---|---|---|
| 시각 표시 방식 | A — 풀스크린 배경 + UI 오버레이 | 카메라 변화 없음. 가장 단순 |
| 옵션 변경 갱신 | A — 즉시 재생성 | 토글 빈도 낮고 직관적. hitch 보이면 후속 spec 에서 debounce |
| draft 시각화 범위 | A — 풀 셋 (1~6) | placement 전환 무비용. obstacles + flow field 까지 포함 |
| Redraft 정책 | 같은 옵션 + 새 seed (OnRedraftRequested 가 PrepareDraftMap 재호출) | 카드 reroll = 맵 reroll. TeardownCurrentBattle 이 이미 맵을 destroy 하므로 단순한 재빌드 |
| Restart 정책 | 맵 재빌드 via BeginPlacement 폴백 (OnRestartRequested 흐름 변경 X) | 옵션 동일하므로 결과 같은 맵. fallback 가드가 빌드 책임 |
| EnsureQueriesAndQueues 분리 | BuildMapForBattle 분리 + 멱등화 | PrepareDraftMap / BeginPlacement 모두 안전하게 호출 가능 |

## 절대 보존 (되돌리지 말 것)

- `BuildMapForBattle()` 함수 시그니처 / 내부 로직 — 본 spec 은 시점만 재배치, 함수 자체는 변경 금지.
- `_placementAllowed` 플래그가 PlacementInput 입력을 차단하는 정책 — draft 동안 PlacementInput.Initialize 가 미리 호출돼도 이 플래그가 false 면 입력 무시.
- `BattleBridge.RestartBattle()` 의 entity destroy 블록 (line ~241~301) — 본 spec 의 `CleanupDraftMapBeforeRebuild` 는 이 패턴의 부분 재사용. RestartBattle 자체 변경 X.
- 운영 중 NativeQueue 8개 채널 (GoalReached / DefenderDeath / MeteorBurst / DefenderAttack / ProjectileHit / EnemyCc / HazardRuntime / HazardDestroyed) 의 lifecycle — `EnsureQueriesAndQueues` 멱등화로 draft / placement 양쪽에서 한번씩만 dispose+재생성.
- `cc-pipeline-and-obstacle`, `path-zone-hazards`, `destructible-blocking-hazards`, `draft-ux-upgrade` 의 기존 계약 모두.

## 작업 시 주의

### EnsureQueriesAndQueues 분리

- 현재 line ~683 의 `BuildMapForBattle();` 호출 줄을 **반드시 제거**. 이 줄이 남으면 PrepareDraftMap → BeginPlacement 시점에 BuildMapForBattle 가 두 번 호출되어 GameObject 누적.
- 멱등 가드(`_ecsInfrastructureReady`)는 `StopBattle` / `TeardownCurrentBattle` / `OnDisable` 에서 false 로 reset.

### Cleanup 책임

- `MapView.OnDestroy` 만 정리하던 `_obstaclesRoot`, `_backgroundPropsRoot`, `_goalMarkerRoot` 를 `ResetVisualRoots` 메서드로 분리. RebuildDraftMap 진입 시 호출. 호출 후 `BuildMapForBattle` 안에서 `InstantiateObstacles` / `InstantiateBackgroundProps` 가 새 root 를 만든다.
- ECS entity destroy 시 컴포넌트 타입 (코드 검증 완료):
  - `Wassup.Battle.Effects.Hazard` (path-zone-hazards) — `Hazard.cs:6`
  - `Wassup.Battle.Effects.BlockingHazard` (destructible-blocking-hazards) — `BlockingHazard.cs:6`
  - `Wassup.Battle.Effects.Obstacle` (cc-pipeline-and-obstacle) — `Obstacle.cs:6`
  - **주의 — `HazardTag` / `ObstacleTag` 는 존재하지 않음**. 본 spec 초안에 잘못 적혔다가 수정됨.
- BlockingHazard visual / SO registry cleanup 은 기존 `ClearBlockingHazardVisuals()` + `_blockingHazardSoRegistry.Clear()` + `_blockingHazardSoIndex.Clear()` 패턴 재사용 (`TeardownCurrentBattle` line 236, 315~316 참조).

### Redraft / Restart 분기

- **Redraft**: `BattleBridge.OnRedraftRequested` (line 181~) 안에서 `TeardownCurrentBattle()` 후 **`PrepareDraftMap()` 재호출** 추가. 그 다음 `draftController.BeginDraft()`. `DraftController.BeginDraft` 자체는 변경 X.
- **Restart**: `OnRestartRequested` 흐름 변경 X. `TeardownCurrentBattle` 이 맵을 destroy 하지만 `BeginPlacement` 의 `!_generatedMap.IsCreated` 폴백 가드가 BuildMapForBattle 호출 → 결과 같은 맵.
- 첫 게임 시작은 `GameManager.Start` 가 PrepareDraftMap 을 호출. BeginDraft 자체는 트리거 추가 없음.
- **주의**: 초안에는 BeginDraft 안에서 `HasGeneratedMap` 가드로 RebuildDraftMap 을 호출했으나, TeardownCurrentBattle 이 이미 맵을 destroy 한 후라 이 가드가 절대 발동하지 않는다는 critic 지적으로 OnRedraftRequested 측 호출로 변경됨.

### 회귀 검증 시나리오

`5_playmode_smoke_and_handoff.md` 의 V1~V10 표 참조. 사용자 manual 검증 필수.

### 테스트 인프라

- EditMode 에서 `World.DefaultGameObjectInjectionWorld` 를 못 가져올 수 있음 → `new World("Test")` 명시 생성 + dispose 패턴.
- 기존 `SpawnBlockingHazardTests.cs` / `HazardDestroyedEventTests.cs` 의 fixture 패턴 재사용.

## 사용자 확인 protocol

각 unit commit 후:
- **Unit 0**: 컴파일 + 기존 EditMode 회귀 0 + PlayMode 동일 동작 (BeginPlacement 폴백으로 빌드).
- **Unit 1**: 컴파일 + 기존 회귀 0.
- **Unit 2**: PlayMode 첫 시각 확인 — Play 누르면 카드 fan 과 맵이 동시에 보이는가? (V1)
- **Unit 3**: PlayMode 옵션 토글 즉시 갱신 (V2~V4) + Redraft 시 맵 새로 (V7).
- **Unit 4**: EditMode 통과.
- **Unit 5**: PlayMode V1~V10 manual + handoff 작성.

## 작업 시작점

`docs/spec/draft-stage-map-prebuild/0_bridge_prepare_and_rebuild.md` 를 읽고 그 파일만 가지고 Unit 0 작업 진행. README.md 의 공통 원칙 + 본 handoff 의 "절대 보존" 섹션을 상시 컨텍스트로 유지.

## 참조 spec (의존)

- `docs/spec/draft-ux-upgrade/` — DraftView / MapSettingsPanelView 흐름.
- `docs/spec/map-system/` — BuildMapForBattle / FlowField / ProceduralMapGenerator 인프라.
- `docs/spec/destructible-blocking-hazards/` — BlockingHazard 컴포넌트 + HazardDestroyedEvent 채널.
- `docs/spec/cc-pipeline-and-obstacle/` — Obstacle entity + ObstacleSingleton.
