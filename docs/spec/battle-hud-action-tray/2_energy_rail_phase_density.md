# 2 — 코스트 레일과 Phase 밀도

## 목적

363×112 부유 코스트 배지를 트레이 상단의 compact energy rail로 축소한다. Placement/Battle 전환 때 스트립과 레일이 함께 움직여 12→44px로 벌어지는 현행 결함을 제거한다. 선행: units 0~1.

## 변경 대상

- `Assets/_Project/Scripts/UI/CostDisplay.cs`
- `Assets/_Project/Scripts/UI/DefenderSelector.cs`
- `Assets/_Project/Scripts/Data/BattleHudTrayConfig.cs`
- `Assets/_Project/Scenes/BattleScene.unity`

## 구현

- 기존 bolt, `N/max`, segmented bar 정보는 유지하고 Config의 약 264×64 rail 치수에 맞게 재배치한다.
- rail은 bottom-center safe root 기준으로 tray top edge에 overlap되며, placement/battle tray height로 y를 계산한다.
- `DefenderSelector.OnPhaseChanged`와 `CostDisplay.OnPhaseChanged`가 같은 Config geometry를 소비해 size/position이 한 frame에 정합되게 한다.
- 표시 결정은 계속 `CostDisplay.RefreshVisible()`이 단독 소유하고 HandView는 `SetSuppressed` 신호만 보낸다.
- 첫 구현은 snap 전환으로 둔다. 160ms tween은 시각 필요성이 확인될 때만 후속으로 추가한다.

## 완료 기준

- [ ] Placement에서 rail과 tray가 하나의 클러스터로 읽힘.
- [ ] Battle에서 rail이 tray를 추종하고 사이에 44px 공백이 생기지 않음.
- [ ] 0/부분 regen/10 상태에서 숫자·segment 가독성 유지.
- [ ] Hand open 시 rail 퇴장, close 시 현재 phase 위치로 정확히 복귀.
- [ ] 보드 최고 가림선이 현행 y=276보다 낮아지고 캡처로 비교 기록.
