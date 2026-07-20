# 3 — 방어유닛 드림캐쳐 행

## 목적

체력바와 같은 레이아웃 루트에서 부착 카드 최대 3장을 표시해 간격/폭 충돌을 구조적으로 제거한다.

## 변경 대상

- `Assets/_Project/Scripts/Presentation/UnitOverheadUiLayer.cs`
- `Assets/_Project/Scripts/Presentation/UnitOverheadView.cs`

## 구현

- `AttachmentsChanged` 때 host별 카드 snapshot을 재구성한다.
- defender view에만 전달한다. enemy BountyMark는 제외한다.
- entryId 결정론 순서, Unit 청록/Squad 골드 프레임, `card.art` 폴백.
- 2:3 카드 최대 3장, 전체 폭≤한 타일 투영 폭.

## 완료 기준

- 부착/회수/reset 반영, 1/2/3장 중앙정렬, 적 카드 행 미표시.
