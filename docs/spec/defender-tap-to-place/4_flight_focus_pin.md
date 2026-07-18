# 4 · 탭 비행 중 포커스 고정 (flight focus pin)

## 목적

탭 배치의 시뮬 비행 중, 타일 포커스(하이라이트)가 **날아가는 유닛 발밑을 실시간 추종**하며
경로를 따라 흐르던 것을 멈추고, **탭한 목표 타일 하나에만 정적으로 고정**한다.
스와이프(진짜 D&D)는 기존대로 발밑 실시간 추종을 유지한다.

검증 질문: 유닛을 탭 선택하고 타일을 탭하면, 비행 내내 **선택한 그 칸에만** 포커스가 떠 있고
경로를 따라 포커스가 흐르지 않는가? 스와이프 배치는 여전히 손가락 아래를 실시간 추종하는가?

## 변경 대상

- `Assets/_Project/Scripts/UI/DefenderDragPlacementController.cs`

## 구현

핵심 관찰: 목표셀(`targetCell`)은 탭 순간 `HandleArmedBoardTap` 에서 이미 확정된다. 비행은 순수 연출.
문제는 `Update()` 가 비행 중에도 매 프레임 `ResolveFocusAndTarget(dt)` 를 호출해 **`_unitTargetWorld`(날아가는 발점)**
로 셀을 재해석하는 것. → 포커스가 경로를 따라 흐른다.

1. **필드**: `private Vector2Int? _simFocusCell;` — 탭 비행 내내 고정할 선택 타일.
2. **`RunSimulatedDrag`**: 렌더 활성 블록(`_onBoard = true; _posInit = true;`)에서 `_simFocusCell = targetCell;`.
3. **`Update()`**: 포커스 해석을 잠금 인자로 전달 —
   `ResolveFocusAndTarget(dt, lockCell: _simulatedDrag ? _simFocusCell : null)`.
   (키링 스프링/트랜스폼/카메라 포커스 피드는 그대로 — 비행 비주얼은 유지, 타일 포커스만 고정.)
4. **`ResolveFocusAndTarget(float dt, bool forceCommit = false, Vector2Int? lockCell = null)`**:
   - `lockCell` 이 있으면 히스테리시스(`PlacementCellSnap.Resolve`)·디바운스(`PlacementSnapDebounce.Step`)를
     건너뛰고 `cell = lockCell.Value`. validity(`CanPlaceDefenderAt`)·`SetHover`·팝은 기존 경로 그대로 재사용.
   - 액체 하이라이트(`stickyLiquidEnabled`)는 잠금이면 `SetPlacementStretch(cell, Vector2.zero, 0f, valid)`
     로 **정적**(손가락 방향 번짐 없음), 아니면 기존 `EvaluateStretch` 번짐.
5. **`CleanupSession`**: `_simFocusCell = null;`(세션 종료 시 정리, `_simulatedDrag` 리셋과 동반).

부수: 탭 순간 목표셀에 확정 팝 1회(기존 `SetHover` change-pulse = "이 타일 선택됨" 피드백). 이후 정적 유지.
범위 프리뷰는 탭 경로에서 계속 억제(`_simulatedDrag`). 스와이프/카메라/범위 프리뷰 동작 불변.

## 완료 기준

- 컴파일 클린.
- Play(에디터): 유닛 탭 선택 → 타일 탭 → **선택 칸에만** 하이라이트 고정, 경로 따라 흐르는 포커스 없음.
- 스와이프 배치는 손가락 아래 실시간 추종 + 액체 번짐 유지(회귀 없음).

> **확인 2026-07-18** (커밋 `95b08252`): 컴파일 클린 · 코드리뷰 clean(0 critical/major) · **사용자 Play 통과 확인**(선택 타일만 포커스, 발밑 추종 제거).
