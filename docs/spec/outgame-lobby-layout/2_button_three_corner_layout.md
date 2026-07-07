# 2 — 버튼 3-코너 재배치

## 목적

중앙 세로 스택이던 로비 버튼을 화면 3-코너로 재배치한다. 모든 버튼은 `MenuButtons`(=menuRoot) 하위에 유지해 로그인 게이트를 그대로 받는다. RectTransform 앵커/위치만 조정, 코드 변경 없음.

## 변경 대상

- OutgameScene: `MenuCanvas/MenuButtons` 하위 버튼들의 RectTransform
  - `StartButton`, `SquadButton`, `DreamcatcherButton`, `TestModeButton`, `DevButtons`(+ `StatRefreshButton`/`StatRefreshResult`/`ResetAccountButton`)

## 구현

현재는 전부 pivot 중앙 세로 스택(StartButton y=110, Squad y=10, Dreamcatcher y=-90, TestMode y=-180, DevButtons 하위 -270/-360). 이를 코너 앵커 기반으로 재배치한다.

1. **우상단 (개발용)**: `DevButtons`와 `TestModeButton`을 top-right 앵커(anchor min/max = 1,1, pivot 1,1)로 이동, 우상단에서 아래로 세로 스택. TestModeButton은 DevButtons에 넣지 않고 형제로 나란히(항상 표시). `StatRefreshResult` 텍스트는 StatRefreshButton 근처를 따라가도록 앵커 정리.
2. **좌하단**: `SquadButton`, `DreamcatcherButton`을 bottom-left 앵커(min/max = 0,0, pivot 0,0)로 이동, 좌하단에서 위로 세로 스택.
3. **우하단**: `StartButton`(Play)을 bottom-right 앵커(min/max = 1,0, pivot 1,0)로 이동.
4. 각 그룹 코너 여백(margin) 일관되게(예: 화면 가장자리에서 40~60px). CanvasScaler reference resolution 기준 좌표로 배치.
5. 버튼 크기/비주얼은 현행 유지 (리스킨은 스코프 밖).

## 완료 기준

- Play 시 우상단에 개발용(TestMode/StatRefresh/ResetAccount), 좌하단에 Squad/Dreamcatcher, 우하단에 Play 버튼이 코너 정렬로 보인다.
- 각 버튼 클릭 동작이 기존과 동일 (Squad/Dreamcatcher 패널 열림, TestMode 패널, Play → BattleScene, ResetAccount → 로그인 복귀).
- 로그인 게이트 정상 (로그아웃 시 버튼 그룹 숨김, 배경은 유지).
- 서로 다른 해상도(예: 16:9, 20:9)에서 코너 앵커가 유지되어 겹침/잘림 없음.
- `read_console` 에러 없음.
