# 3 — Outgame UI 이관

## 목적

Battle만 Height 기준으로 바꾸어 화면 간 스케일이 달라지는 부분 적용을 막는다. 이미 Height인 authored Outgame Canvas는 보존하고, 런타임 팝업/스쿼드 준비 UI와 safe-area 계층을 같은 계약으로 맞춘다. 선행: units 0~2.

## 변경 대상

- `Assets/_Project/Scenes/OutgameScene.unity`
- `Assets/_Project/Scripts/UI/Outgame/SquadPrepView.cs`
- OutgameScene의 로그인/로비/덱빌더/팝업 UI root와 edge control RectTransform

## 구현

- authored Outgame Canvas가 1920×1080/Height 계약을 유지하는지 저장값으로 고정한다.
- `SquadPrepView`의 runtime scaler fallback을 `UiCanvasSetup`으로 교체한다.
- 로비 배경·전면 dim은 `FullBleedRoot`, 로그인/덱빌더/스쿼드 준비/메뉴 조작부는 `SafeAreaRoot` 아래로 분류한다.
- 16:9 중앙 구도는 그대로 두고 20:9에서는 배경만 cover/fill, 핵심 패널은 safe rect 중앙에 남긴다.
- Battle↔Outgame scene 전환 뒤 중복 root나 stale component가 남지 않아야 한다.

## 완료 기준

- [ ] 16:9 로그인/로비/덱빌더/스쿼드 준비 화면 픽셀 구도 회귀 없음.
- [ ] 20:9에서 배경이 찌그러지지 않고 핵심 패널이 세로 클립되지 않는다.
- [ ] 좌우 cutout 방향을 바꿔도 edge 버튼이 safe rect 안에 남는다.
- [ ] Battle 왕복 후 Canvas/root 중복과 MissingReference 에러가 없다.
- [ ] Android/Editor 양쪽에서 터치·마우스 입력 정상.
