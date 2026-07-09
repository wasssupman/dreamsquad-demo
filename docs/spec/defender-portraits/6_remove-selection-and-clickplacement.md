# 6 — 인게임 선택 로직 + 클릭 배치 제거 (드래그-드롭 전용)

## 목적

사용자 요청(2026-07-08): 인게임 유닛 선택 UI 의 "선택" 개념을 완전히 제거한다.
선택 프레임/하이라이트뿐 아니라 선택 로직 자체를 없애고, 클릭 배치를 비활성화해
배치를 드래그-드롭 전용으로 만든다. "자연스럽게 선택이 안되도록."

## 변경 대상

- `Assets/_Project/Scripts/UI/DefenderSelector.cs` — 선택 로직/프레임 제거.
- `Assets/_Project/Scripts/Core/PlacementInput.cs` — 클릭 배치 기본 비활성.
- `Assets/_Project/Scripts/UI/DefenderDragPlacementController.cs` — 드래그 종료 시
  클릭 배치 재활성화 제거.
- `Assets/_Project/Scenes/BattleScene.unity` — PlacementInput.clickPlacementEnabled 0.

## 구현

1. **DefenderSelector**: `Update()`(선택 하이라이트), `Select()`, `OnSlotClicked()`,
   `SlotView` 구조체, `_slots`, `SelectionFrameColor` 전부 제거. 슬롯 버튼 onClick 선택
   와이어링 제거(Button 컴포넌트도 미부착). 슬롯은 이제 `DefenderDragSlot` 만 부착된
   순수 드래그 소스. 배경 Image 는 드래그 레이캐스트 타겟 역할만(포트레이트 슬롯은
   투명, 폴백만 단색). draft confirm 시 auto-select / draft start 시 Select(null) 제거.
   → `GameManager.SelectedDefender` 를 더 이상 채우지 않는다.

2. **PlacementInput**: `clickPlacementEnabled` 기본값 `true → false`. 씬 값도 0.
   (선택이 항상 null 이라 `selected == null` 가드로도 no-op 이지만, 명시적으로 비활성.)

3. **DefenderDragPlacementController**: `CleanupSession()` 의 클릭 배치 재활성화
   (`SetClickPlacementEnabled(true)`) 제거 — 드래그 후에도 계속 비활성 유지.
   드래그 시작 시 비활성화 라인은 유지(무해, placementInput 필드 사용 유지).

## 완료 기준

- 컴파일/콘솔 클린. ✅
- (육안) 인게임 배치 스트립에 선택 프레임/하이라이트/딤이 전혀 없다.
- (육안) 타일 클릭으로는 배치되지 않고, 슬롯을 끌어다 놓아야만 배치된다.
- 드래그-드롭 배치(프리뷰·슬로우모·on-place)는 이전과 동일하게 동작한다.

---
완료 확인: 2026-07-09 · 커밋 8f52648c
