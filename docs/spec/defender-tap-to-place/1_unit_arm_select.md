# 1 — 유닛 arm(선택) + 트레이 탭

**작업 구분**: feature · 의존: unit 0

## 목적

트레이 슬롯 **탭** = 그 유닛 arm(선택). 드래그와 공존(탭=arm, 끌기=드래그 — EventSystem 드래그 임계값이
자동 구분: 끌기면 OnBeginDrag 가 발화하고 OnPointerClick 은 안 옴). arm 은 컨트롤러 단독 소유(단일 armed).

## 변경 대상

- `Assets/_Project/Scripts/UI/DefenderDragSlot.cs` — `IPointerClickHandler` + `SetArmed`
- `Assets/_Project/Scripts/UI/DefenderDragPlacementController.cs` — `ToggleArm`/`Disarm`/`IsArmed`/`ArmHighlightColor`
- `Assets/_Project/Scripts/Data/DragSwaySettings.cs` — `armHighlightColor`

## 구현

- **슬롯 `OnPointerClick`**:
  1. **비용 게이트(드래그 OnBeginDrag 와 대칭)**: `!_controller.IsArmed(this)` 이고 `CostRuntime.CanAfford` 실패면
     arm 하지 않고 `_costDisplay.PulseInsufficient(부족분)` — 무피드백 arm 금지(리뷰).
     **armed 슬롯의 재탭(=해제)은 비용 무관 허용**(해제가 비용에 막히면 안 됨).
  2. 통과 시 `_controller.ToggleArm(this, _unitData, eventData.position)`.
- **슬롯 `SetArmed(bool)`**: lazy 오버레이(Image, anchor 풀스트레치 ±4px, `raycastTarget=false`, SetAsLastSibling).
  색은 **켤 때마다** `_controller.ArmHighlightColor`(=`DragSwaySettings.armHighlightColor`, 기본 청록 0.35,1,0.9,0.28)
  재적용 — SO 라이브 튜닝 반영. 하드코딩 금지(확정 팝 valid 색과 함께 튜닝).
- **컨트롤러**:
  - `ToggleArm(slot, unit, fromScreen)`: 같은 슬롯 재탭 → `Disarm()`. 아니면 이전 Disarm 후
    `_armedSlot/_armedUnit/_armedFromScreen` 세팅 + `slot.SetArmed(true)`.
  - `Disarm()`: **`?.` 금지** — `_armedSlot` 은 트레이 리빌드(RebuildSlots 가 슬롯 GameObject Destroy)로 파괴될 수
    있고 `?.` 는 Unity destroyed fake-null 을 못 거른다(MissingReferenceException). `if (_armedSlot != null)`(Unity `==`)
    가드 후 SetArmed(false), 필드 클리어.
  - `IsArmed(slot)` => `_armedSlot == slot` (슬롯의 해제-재탭 판정용).
  - `BeginDrag` 진입 시 `Disarm()`(드래그가 arm 을 대체).
- **분리**: `GameManager.SelectedDefender` 무사용(클릭 배치 레거시와 격리). arm 은 탭 배치 전용 상태.

## 완료 기준

- 슬롯 탭 → 하이라이트 on. 타 슬롯 탭 → 이전 off·새로 on. 같은 슬롯 재탭 → off. 슬롯 끌기 → 드래그 정상(arm 해제).
- 비용 부족 유닛 탭 → arm 안 되고 비용 pulse. armed 상태에서 비용이 떨어져도 재탭 해제는 됨.
- 트레이 리빌드(Placement 재진입) 후 Disarm/ToggleArm 에서 예외 없음.
