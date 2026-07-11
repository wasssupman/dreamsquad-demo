# 2 — Battle UI 전면 이관

## 목적

BattleScene에서 authored/runtime Canvas가 섞여 확대·클립되는 상태를 제거한다. 전면 scrim은 화면 전체를 유지하고, 전투 조작·정보는 safe edge 기준으로 정렬한다. 선행: units 0~1.

## 변경 대상

- `Assets/_Project/Scenes/BattleScene.unity`
- `Assets/_Project/Scripts/UI/{DefenderSelector,CostDisplay,PlacementPhaseView,NextWaveDock,ScoreHudView,MenuPopup,ResultScreen,SkillBar}.cs`
- `Assets/_Project/Scripts/UI/Draft/DraftView.cs`
- `Assets/_Project/Scripts/UI/Dreamcatcher/{AwakeningGaugeView,DreamcatcherSelectionView,DreamcatcherHandView}.cs`

## 구현

- 위 12개 런타임 Canvas 생성 경로를 `UiCanvasSetup`으로 교체하고 독자적인 scaler 생성 코드를 제거한다.
- BattleScene의 authored CanvasScaler도 ScaleWithScreenSize/1920×1080/Height로 저장한다. Constant Pixel Size ResultCanvas를 남기지 않는다.
- 메뉴·점수·타이머·웨이브 독·각성 게이지·스킬·유닛 스트립·드림캐쳐 핸드는 `SafeAreaRoot` 아래로 이동한다.
- Draft/Result의 full-screen dim·scrim은 `FullBleedRoot`, 카드/결과 패널과 버튼은 `SafeAreaRoot`에 둔다.
- 기존 sortingOrder, raycast 순서, phase show/hide, 스트립↔핸드 플립 상태는 바꾸지 않는다.
- 이 unit은 스케일/계층 이관만 수행한다. 트레이 아트와 치수는 후속 spec에서 변경한다.

## 완료 기준

- [ ] 16:9 Draft/Placement/Battle/Hand/Result가 이관 전과 같은 위치·크기로 보인다.
- [ ] 20:9에서 HUD 높이가 16:9 대비 커지지 않고 좌우 여유만 늘어난다.
- [ ] full-screen scrim에 safe-area 모양의 빈 가장자리가 생기지 않는다.
- [ ] 좌/우 edge HUD와 하단 조작부가 주입한 safe rect 밖으로 나가지 않는다.
- [ ] 플립·START·Next Wave·메뉴·스킬 raycast가 정상이며 콘솔 에러 0.
