# 2 — 보드 타일 탭 → 시뮬레이션 발화

**작업 구분**: feature · 의존: unit 0·1

## 목적

유닛이 arm 된 상태에서 보드 타일을 **탭**하면 그 칸으로 `SimulateDragTo` 를 발화해 D&D 시뮬 배치. 무효 칸은 reject.

## 변경 대상

- `Assets/_Project/Scripts/UI/DefenderDragPlacementController.cs` — `HandleArmedBoardTap` / `PointerOverUi`
  (+ `using UnityEngine.InputSystem` — 프로젝트는 Input System 전용, 레거시 `UnityEngine.Input` 비활성)

## 구현

- **호출 지점**: 컨트롤러 `Update()` 최상단, `_armedUnit != null && !_session.active` 일 때만 `HandleArmedBoardTap()`.
- **`HandleArmedBoardTap` 순서 (가드가 계약)**:
  1. **stale 슬롯 자가치유**: `if (_armedSlot == null) { Disarm(); return; }` — Unity `==` 라 트레이 리빌드로 슬롯이
     파괴됐으면 여기서 arm 을 정리(파괴된 참조로 진행 금지).
  2. `Pointer.current` null / `!press.wasPressedThisFrame` → return. (arm 은 pointer-UP(OnPointerClick)에서 서고
     이 체크는 DOWN 이라, 슬롯 arm 탭이 같은 프레임에 보드 탭으로 새는 일은 구조적으로 없음.)
  3. `mainCamera`/`bridge` null → return.
  4. **`GameManager.IsAiming` 이면 return** — 드림캐쳐 카드(포탈 2-tap 등) 조준 탭이 배치로 이중 소비되는
     aim-mode race 방지. PlacementInput.Update 의 동일 가드와 같은 이유(리뷰 확정).
  5. **`PointerOverUi()` 면 return** — UI 탭(슬롯 재선택 등) 제외. no-arg `IsPointerOverGameObject()` 는
     마우스 pointerId(-1)만 조회해 **터치에서 무력**(Android 에서 UI 위 탭이 보드로 관통). 터치 중이면
     `Touchscreen.current.primaryTouch.touchId` 로 판정하는 정적 헬퍼 `PointerOverUi()` 사용.
  6. **셀 변환 = `bridge.TryScreenToCell(mainCamera, pointer.position, out cell)`** — 단일 소스 재사용.
     ray→RaycastPlane→ToSim→DebugWorldToCell 수동 복제 금지(bridge 주석 "new call sites MUST use this").
  7. `bridge.CanPlaceDefenderAt(cell, _armedUnit)` 유효 → `SimulateDragTo(_armedUnit, _armedFromScreen, cell)`
     (내부 BeginDrag 가 Disarm). 무효 → `FlashPlacementReject(cell)`, **arm 유지**(재시도 가능).
     - **⚠ 뒤집힘**: `placement-armed-board-drag` unit 4(2026-08-20) 이후 무효셀은 **arm 해제**다.
       보드 탭 경로 자체도 그 spec 의 프레스-드래그 상태기계로 대체됐다(unit 0).
- **공격 범위 억제**: 탭 시뮬 경로는 범위 프리뷰 미노출. `_simulatedDrag` 는 `BeginDrag(unit, screen, simulated:true)`
  가 **CleanupSession 직후·첫 내부 UpdateDrag 전에** 세팅(이후 세팅하면 첫 프레임에 범위가 켜져 안 꺼지는 버그 —
  리뷰로 확정·수정). `SetHover` 는 이 플래그면 `SetPlacementRange` 만 스킵(hover/팝/키링 유지). 실제 D&D 는 범위 노출.

## 완료 기준

- Play(배치 페이즈): 슬롯 탭(arm) → 유효 타일 탭 → 유닛이 슬롯에서 그 타일로 비행·배치(탭한 칸 정확 안착).
- 무효 타일 탭 → 빨강 reject + arm 유지(**unit 4 이후: reject + arm 해제**). 드래그 배치 공존.
  탭 경로에서 공격 범위 안 뜸(드래그는 뜸 — armed-board-drag unit 2 이후 탭도 비행 중 노출).
- 스킬 조준(IsAiming) 중 보드 탭 → 배치 미발화. (실기기) UI 위 탭이 보드로 관통하지 않음.
- 사용자 Play 확인 후 `3_handoff_summary.md` 작성 + 커밋.
