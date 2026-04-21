# Hover Highlight

**작업 구분**: Phase 3

## 목적

Drag 중 pointer 가 올라간 map tile 을 색상으로 강조해 배치 가능 여부를 즉시 보여준다.

## 변경 대상

- Modify: `Assets/_Project/Scripts/Core/MapView.cs`
- Use: `BattleBridge.CanPlaceDefenderAt`

## 규칙

- valid tile: 청록/밝은 색 highlight
- invalid tile: 붉은 색 highlight
- hover tile 이 바뀌면 이전 tile material 복원
- drag 종료 시 모든 hover highlight clear

## 완료 기준

- drag 중 tile hover 가 보인다.
- invalid tile 도 명확히 보인다.
- hover 종료 후 tile material 이 원래 타입별 material 로 복원된다.
