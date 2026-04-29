# Draft Stage Map Prebuild — Design

**작성일**: 2026-04-30
**Spec 폴더**: `docs/spec/draft-stage-map-prebuild/`

## 한 줄 요약

드래프트 진입 시점에 맵 빌드를 끝내서 카드 fan 뒤로 풀스크린으로 표시. MAP SETTINGS 옵션 토글 시 즉시 재생성. placement 진입 시 재빌드 skip.

## 아키텍처 요약

- `BattleBridge.PrepareDraftMap()` / `RebuildDraftMap()` 두 진입점을 신설. 둘 다 기존 `BuildMapForBattle()` 재사용.
- 현재 `EnsureQueriesAndQueues()` 마지막에 inline 호출되는 `BuildMapForBattle()` 를 분리해 별도 진입점이 직접 호출하도록 한다.
- `BeginPlacement()` 는 `_generatedMap.IsCreated` 면 build skip — 추가 빌드 0.
- `GameManager.Start()` 가 `BeginDraft()` 직전 `PrepareDraftMap()` 호출.
- `DraftController.SetMapGenerationOptions()` 가 `RebuildDraftMap()` 트리거.
- `BeginDraft` (Redraft path) 도 `RebuildDraftMap()` 호출 — 같은 옵션 + 새 seed.
- Restart 는 맵 유지 (entity 만 destroy).

## 비목표 (현 spec 범위 밖)

- 옵션 변경 시 트랜지션 애니메이션
- 카메라 인터랙션 (회전/줌/팬)
- 맵 빌드 비용 프로파일링 / debounce
- 카드 fan 영역 alpha / blur (UI 가독성)
- placement 슬롯 시각화 변경

## 작업 단위

`docs/spec/draft-stage-map-prebuild/` 의 0~5 파일 순서대로 구현.
