# 3 — 인게임 유닛 선택 UI 포트레이트 표시

## 목적

전투 중 배치 스트립(`DefenderSelector`, BattleScene)의 각 유닛 슬롯을 단색+이름
대신 포트레이트로 보여준다. 선택 하이라이트는 유지한다.

## 변경 대상

- `Assets/_Project/Scripts/UI/DefenderSelector.cs`

## 구현

현재 각 슬롯은 `Image background`(visualMaterial._BaseColor 로 채색) + 중앙 이름
TMP 로 구성되고, `Update()` 가 선택 여부에 따라 background.color 를 lerp 한다.

1. `SlotView` 에 `Image portrait` 추가. `RebuildSlots()` 에서 슬롯 GameObject 안에
   포트레이트 Image 자식을 만들어 슬롯을 채우도록(anchor stretch, `preserveAspect
   = true`, `raycastTarget = false`) 배치하고, `data.portrait` 를 할당.
   - `portrait != null`: 포트레이트 Image 활성, 이름 라벨은 하단 소형으로(또는 숨김).
   - `portrait == null`: 포트레이트 Image 비활성 → 기존 단색+중앙 이름 폴백 유지.

2. **선택 하이라이트 유지** — background 는 포트레이트 뒤 프레임/틴트로 남긴다.
   `Update()` 의 선택 lerp 는 background(프레임)에 계속 적용하고, 포트레이트가 슬롯을
   가득 채우면 선택 표시가 안 보일 수 있으므로 다음 중 하나로 명확화:
   - 포트레이트에 약간의 패딩을 줘 background 프레임이 테두리로 보이게, 또는
   - 선택 시 포트레이트에 tint/스케일 등 가시적 강조.
   구체 방식은 구현자가 육안 확인하며 결정(계약: 선택된 슬롯이 한눈에 구분되어야 함).

3. 드래그 배치(`DefenderDragSlot`, `DefenderDragPlacementController`) 로직은 변경
   없음 — 슬롯 컴포넌트 구성만 바뀐다. 드래그 시작 raycast 를 위해 포트레이트
   Image 는 `raycastTarget = false`(슬롯 루트 Image/Button 이 입력 수신).

## 완료 기준

- BattleScene Play → 드래프트/스쿼드 확정 후 배치 스트립 슬롯에 포트레이트가 보인다.
- 슬롯 선택 시 선택된 유닛이 시각적으로 구분된다(하이라이트 유지).
- 드래그-드롭 배치가 이전과 동일하게 동작한다.
- portrait 미할당 유닛은 기존 단색+이름 폴백.
- 컴파일/콘솔 클린.

---
완료 확인: 2026-07-08 · 커밋 95e1099b
