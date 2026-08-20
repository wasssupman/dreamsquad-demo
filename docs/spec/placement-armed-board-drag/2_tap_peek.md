# 2 — 탭 배치 + 비행 중 공격범위 노출

> **⚠ unit 4 가 이 계약을 뒤집었다 (2026-08-20).** 무효셀/맵 밖 릴리즈는 이제 **arm 을 해제**한다
> (릴리즈는 언제나 제스처를 끝낸다). 아래 «arm 유지» 서술은 당시 계약의 기록이다 —
> 현재 계약은 `4_invalid_release_disarm.md` 와 README 를 본다.

**작업 구분**: feature (의존: unit 0·1)

## 목적

armed 유닛으로 보드를 **짧게 탭**(이동 임계 미만으로 다운→업)하면, **기존 클릭 배치와 동일하게 그 칸에
즉시 배치**하되, 공격범위를 **비행 중에만** 노출한다("클릭 배치를 범위 표시로 확장"). 배치되면(착지)
범위는 곧바로 사라진다 — 다른 배치 동작과 동일하게 잔상(linger) 없음. 드래그(이동) 경로는 unit 0 그대로
릴리즈 셀에 커밋한다.

> 정정 이력: ① 초기 "탭=범위 피크만, 배치 X" → 사용자 정정(탭도 배치). ② "착지 후 linger" → 사용자 정정
> (2026-07-20) — 다른 동작처럼 **날아가는 동안만** 범위 노출, 배치되면 소거. linger·`tapPeekDuration` 폐기.

## 변경 대상

- `Assets/_Project/Scripts/UI/DefenderDragPlacementController.cs`
  - `_tapPlaceRangeRoutine`(Coroutine) 필드.
  - `HandleBoardTap(screen)` — 유효/무효 분기.
  - `StartTapPlaceRangePeek/RunTapPlaceRangePeek/CancelTapPlaceRangePeek`.
  - press 개시 + `BeginDrag(!simulated)` 에서 `CancelTapPlaceRangePeek()`.

## 구현

탭 릴리즈 → `_boardGestureActive=false; _boardDragging=false;` → `HandleBoardTap(cur)`:

- **유효셀**: `SimulateDragTo(unit, _armedFromScreen, cell)`(즉시 비행 배치 — 내부 `BeginDrag→Disarm` 이
  스카우트/arm 정리) 후 `StartTapPlaceRangePeek(cell, unit)`.
  - `RunTapPlaceRangePeek`: **비행 세션이 사는 동안만**(`_session.active && _simulatedDrag`) 매 프레임
    `SetPlacementRange(cell)` 재확인(비행은 sim 경로라 범위를 스스로 안 그리고 `CleanupSession` 으로 clear 만
    하므로, 재확인이 그 clear 를 덮어써 비행 내내 범위가 보인다). 착지(커밋)로 세션이 끝나면 **곧바로**
    `ClearPlacementRange` — linger 없음.
  - **자기 flight 의 `Disarm`/`ResetBoardGesture` 에 죽으면 안 되므로** 별도 코루틴(`_tapPlaceRangeRoutine`).
    취소 지점은 오직 **새 보드 press** 와 **새 트레이 드래그**(`BeginDrag(!simulated)`) — sim 경로(자기 flight) 제외.
- **무효셀**: `FlashPlacementReject(cell)` + `ResetBoardGesture()`(스카우트 범위 즉시 소거). 배치 없음, arm 유지(재시도).
- **보드 밖 탭**: `ResetBoardGesture()`(취소).

**주의**: 범위 노출 길이는 별도 상수가 아니라 **비행 수명**에 묶인다 — 비행 시간 튜닝(`DragSwaySettings` 그룹 ⑦
`tapTravel*`)이 곧 노출 길이. per-range alpha 페이드는 `TilemapMapView` 펄스가 단독 소유라 미지원(후속).

## 완료 기준

- 컴파일 통과.
- Play: arm 후 보드를 **짧게 탭**하면 기존 클릭처럼 그 칸에 **즉시 배치**되고(트레이→셀 비행), 공격범위가
  **비행 중에만** 보이다가 **배치되는 순간 사라진다**(착지 후 잔상 없음).
- **무효셀 탭**: reject 플래시 + 범위 즉시 소거, 배치 안 됨, arm 유지.
- 탭 배치 직후(비행 중) **트레이에서 새 유닛을 드래그**하면 이전 탭의 범위가 즉시 사라지고 새 드래그 범위만 보인다.
- 드래그(이동) 릴리즈 배치는 unit 0 그대로.
- unit 0·1 동작(드래그 배치 / 스카우트 / 인스펙트 양보) 회귀 없음.

사용자 Play 확인: **통과 2026-07-20** · 구현 커밋 `5b1c575f`
