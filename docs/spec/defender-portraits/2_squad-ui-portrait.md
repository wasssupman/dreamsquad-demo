# 2 — 스쿼드 페이지 포트레이트 표시

## 목적

스쿼드 편성 화면(`SquadBuilderView`, OutgameScene)에서 유닛을 단색+텍스트 대신
포트레이트로 보여준다. 두 지점: (a) 상단 유닛 슬롯, (b) 유닛 선택 피커 모달.

## 변경 대상

- `Assets/_Project/Scripts/UI/Outgame/SquadBuilderView.cs`

## 구현

기존에 스톤 슬롯/피커가 쓰는 아이콘 패턴(`CreateIconImage` / `SetIcon`,
`_stoneSlotIcons`)을 유닛 쪽에도 동일하게 적용한다. 새 헬퍼를 만들지 말고 재사용.

1. **유닛 슬롯 (메인)** — `BuildUnitSlots()` 에서 스톤 슬롯처럼 각 버튼에
   `CreateIconImage` 로 포트레이트 Image 를 추가하고 `_unitSlotIcons` 리스트에 보관.
   `Refresh()` 에서:
   - 빈 슬롯: 아이콘 숨김(`SetIcon(null)`), 라벨 "+", 기존 EmptySlotColor 유지.
   - 채워진 슬롯: `unit.portrait` 를 `SetIcon`, 라벨은 이름 유지(포트레이트 하단에
     겹치도록 스톤 슬롯의 occupied 라벨 배치를 참고). `portrait == null` 이면 현행대로
     이름 텍스트만.

2. **유닛 피커 모달** — `OpenPicker(Unit)` 셀 크기를 스톤 피커처럼 정사각(150x150)
   으로, `BuildUnitPickerItems()` 를 스톤의 `CreateStonePickerButton` 패턴처럼 아이콘+
   라벨 셀로 변경(전용 `CreateUnitPickerButton` 추가 또는 스톤 헬퍼 일반화). 아이콘은
   `unit.portrait`, 라벨은 displayName. 이미-편성(dim/interactable=false) 규칙 유지.

3. `portrait == null` 인 유닛은 아이콘을 숨기고 기존 텍스트 표시로 자연 폴백.

레이아웃 상수(셀/아이콘 크기, 오프셋)는 스톤 쪽 값을 따르되, 유닛 셀에서 이름이
읽히도록 조정. 색상/등급/저장 로직은 건드리지 않는다.

## 완료 기준

- OutgameScene Play → 스쿼드 화면에서 편성된 유닛 슬롯에 포트레이트가 보인다.
- 유닛 슬롯 탭 → 피커 모달의 각 유닛이 포트레이트로 렌더된다.
- 이미-편성 유닛 dim, CLEAR/CLOSE, 저장 등 기존 동작이 그대로 작동한다.
- portrait 미할당 유닛(있다면)은 기존 텍스트 표시로 폴백, 레이아웃 안 깨짐.
- 컴파일/콘솔 클린.

---
완료 확인: 2026-07-08 · 커밋 95e1099b
