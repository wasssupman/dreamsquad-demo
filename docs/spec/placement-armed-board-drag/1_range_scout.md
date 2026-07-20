# 1 — 범위 스카우트 (프레스·드래그 중 공격범위 노출)

**작업 구분**: feature (의존: unit 0)

## 목적

armed 유닛의 보드 제스처가 진행되는 동안(**프레스다운부터 릴리즈 직전까지**), 손가락이 가리키는 셀의
**공격범위 + hover 하이라이트**를 노출한다. 키링 유닛 실루엣은 띄우지 않는다(range-only — 사용자 결정
2026-07-20). 유닛은 트레이에 남아있고, 커밋(릴리즈) 시 unit 0 의 시뮬 비행으로 날아온다.

## 변경 대상

- `Assets/_Project/Scripts/UI/DefenderDragPlacementController.cs`
  - `_boardScoutCell`(Vector2Int?) 필드 추가 — 스카우트가 표시 중인 셀(변경 감지·소거용).
  - `UpdateBoardScout(Vector2 screen)` / `ClearBoardScout()` 신설.
  - `UpdateBoardGesture` 에서 매 프레임(릴리즈 제외) `UpdateBoardScout(cur)` 호출.
  - `ResetBoardGesture` 에서 `ClearBoardScout()` 호출.

## 구현

세션(`_session`)이 없는 스카우트 전용 경로다. `SetHover`(세션 바인딩) 대신 bridge 를 직접 호출하되,
드래그 세션과 **같은 표시 계약**을 미러한다:

- 셀 판정: `bridge.TryScreenToCell` 단일 소스. 실패(보드 밖) → `ClearBoardScout()`.
- 유효성: `bridge.CanPlaceDefenderAt(cell, _armedUnit, out _)`.
- 범위/팝: 셀이 바뀐 프레임에만 `SetPlacementRange(cell, _armedUnit)` + `PulsePlacementHover(cell, valid)`
  (세션 `SetHover` 의 `changed` 게이트 미러 — 매 프레임 재페인트 방지).
- hover: `Cfg.stickyLiquidEnabled` 면 `SetPlacementStretch(cell, Vector2.zero, 0f, valid)`(정적 — 탭 비행 unit 4
  와 동일하게 손가락 방향 번짐 없음), 아니면 `SetPlacementHover(cell, valid)`. 셀 변경 시 이전 셀 hover 정리.
- 소거(`ClearBoardScout`): 마지막 셀 hover clear + `ClearPlacementRange()` + `ClearPlacementStretch()`, `_boardScoutCell=null`.

스카우트 소거는 unit 0 의 `ResetBoardGesture`(릴리즈·arm 해제 경유)에 얹는다. 릴리즈 시엔
UpdateBoardScout 를 부르지 않고 곧장 커밋/리셋으로 간다(릴리즈 셀에서 다시 그리지 않음).

**탭과의 관계**: 무이동 탭도 프레스 동안 범위가 잠깐 보였다가 릴리즈에서 즉시 소거된다(현 동작).
릴리즈 후 짧게 유지되다 페이드하는 "피크"는 **unit 2** 가 탭 릴리즈 경로를 바꿔 얹는다.

## 완료 기준

- 컴파일 통과.
- Play: 유닛 arm 후 보드를 **눌러 드래그**하면 손가락을 따라 **공격범위 격자 + hover 칸**이 실시간으로
  따라온다. 유효/무효 칸에서 hover 색이 갈린다(초록·시안 / 빨강). **키링 유닛은 안 뜬다**.
- 릴리즈(커밋/취소) 후 스카우트 범위·hover 가 남지 않는다.
- 보드 밖으로 끌면 범위가 사라지고, 다시 보드로 들어오면 재노출된다.
- unit 0 의 배치/탭/인스펙트-양보 동작이 회귀 없이 유지된다.

사용자 Play 확인: **통과 2026-07-20** · 구현 커밋 `e88fb071`
