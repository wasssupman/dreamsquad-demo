# 7. 스택 행 뷰 — UnitOverheadView.ShowStacks [presentation]

## 목적

오버헤드 뷰에 드림캐쳐 행 위 스택 아이콘 행을 렌더. 아이콘 + 카운트 배지, 풀링, `StackRowBottom` 배치. `ShowCards` 패턴 미러. 아이콘 부재/데이터 부재 시 무표시(무크래시) — unit 8(데이터)·unit 9(아이콘) 전에도 컴파일·동작.

## 변경 대상

- `Assets/_Project/Scripts/Presentation/UnitOverheadView.cs`

## 구현

- **Show 시그니처 확장**: 끝에 optional `IReadOnlyList<OverheadStackEntry> stacks = null, StackIconRegistry stackIcons = null` 추가 → 기존 호출부(Layer)는 무변경 컴파일(unit 8 이 실데이터 주입).
- **`ShowCards` → `float` 반환**: 카드행 높이(카드 없으면 0). 스택행이 그 위에 얹히도록.
- **`ShowStacks`**: 레지스트리에 아이콘 있는 스택(count>0)만 `StackRowMax` 개까지 수집(`_visibleStacks`) → 슬롯 풀(`_stacks`)에 아이콘 Image + 카운트 배지(TMP + plate) 렌더. 폭 초과 시 아이콘 축소. 위치 = `UnitOverheadLayout.StackRowBottom(카드행 bottom, 카드행 높이, StackGap)`, 가로 중앙정렬.
- **배지**: 아이콘 우상단, `StackBadgeHeightFraction`·`StackBadgeColor`·`StackBadgePlate` 스타일. count 텍스트 상시(≥1).
- `EnsureStackSlots`(EnsureCardSlots 미러) + `Rebuild` 에서 `_stacks.Clear()`.
- TMP: 단일 `Wassup.Runtime` asm 이 TMPro 참조(UI 선례) → `TextMeshProUGUI` 사용 가능.

## 완료 기준

- Unity 재컴파일 CS 에러 0. ✅
- stacks=null(기본) 이면 스택 슬롯 전부 비활성(기존 화면 무변화) — dormant 안전.
- (unit 8+9+10 후 Play) 피로도/열기 아이콘 + 카운트가 드림캐쳐 행 위에 표시.
