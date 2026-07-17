# 2 — 하단 액션 코너 재배치

## 목적

Placement에서는 각성을 숨기고 전투 시작을 우하단에 유지한다. Battle에서는 진행 액션과
각성 자원을 좌우 코너로 분리해 겹침을 제거한다.

## 변경 대상

- `Assets/_Project/Scripts/UI/NextWaveDock.cs`
- `Assets/_Project/Scripts/UI/PlacementPhaseView.cs`

## 구현

- `NextWaveDock.DockPanel`: bottom-right anchor/pivot/(-40,40)에서 bottom-left
  anchor/pivot/(40,40)으로 변경한다.
- `PlacementPhaseView.StartButtonWrap`: 기존 bottom-right `(-40,40)` 계약을 유지한다.
- 각성 버튼은 Placement에서 숨기고 Battle에서만 우하단 `SafeAreaRoot`에 표시한다.
- 중앙 하단 action tray와의 간격, 좌측 MENU와의 세로 분리를 16:9/20:9에서 확인한다.

## 완료 기준

- Placement: 우하단 전투 시작만 노출되고 중앙 unit tray와 겹치지 않는다.
- Battle: 좌하단 타이머/다음 웨이브, 우하단 각성 버튼이 겹치지 않는다.
- wide aspect에서 좌우 UI가 safe edge를 따라가고 중앙 tray 크기는 변하지 않는다.
