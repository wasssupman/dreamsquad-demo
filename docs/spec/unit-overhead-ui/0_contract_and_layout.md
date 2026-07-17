# 0 — 좌표·레이아웃 계약

## 목적

해상도·카메라 pitch·유닛 키와 무관하게 5px 간격과 한 타일 폭 제한을 재현할 순수 계산을 만든다.

## 변경 대상

- `Assets/_Project/Scripts/Presentation/UnitOverheadLayout.cs`
- `Assets/_Project/Scripts/Data/UnitOverheadUiStyle.cs`
- `Assets/_Project/Tests/EditMode/UnitOverheadLayoutTests.cs`

## 구현

- actual px ↔ 1080 reference-height px scale 계산.
- renderer screen rect top-center를 기준으로 health/card row rect 계산.
- defender/enemy width fraction + min/max clamp.
- 최대 3장 카드의 높이/폭/간격을 한 타일 투영 폭 안에 축소.
- NaN/0 screen height/tile width 방어.

## 완료 기준

- EditMode에서 5px edge gap, 폭 clamp, 3장 fit, 해상도 scale을 검증한다.
