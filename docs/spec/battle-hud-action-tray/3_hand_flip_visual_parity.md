# 3 — 드림캐쳐 Hand 플립 시각 정합

## 목적

같은 좌표에서 바뀌지만 서로 다른 메뉴처럼 보이는 유닛 트레이와 드림캐쳐 핸드의 외곽 문법을 통일한다. 기존 제자리 X-flip과 상태 전환 안정성은 보존한다. 선행: units 0~2.

## 변경 대상

- `Assets/_Project/Scripts/UI/Dreamcatcher/DreamcatcherHandView.cs`
- `Assets/_Project/Scripts/UI/DefenderSelector.cs`
- `Assets/_Project/Scripts/Data/BattleHudTrayConfig.cs`
- `Assets/_Project/Scenes/BattleScene.unity`

## 구현

- Hand panel이 Config의 hand size/anchored y/outer width를 사용하게 하고 유닛 tray와 같은 border radius·fill·gold edge 계열을 소비한다.
- panel 높이는 카드 fan에 필요한 약 232를 유지하되 폭과 pivot은 Action Tray와 일치시킨다.
- `FlipRoutine`, slomo lease, Open/Close/ForceClose, cost suppression 계약은 변경하지 않는다.
- 중간 flip frame에서 두 배킹이 동시에 보이거나 둘 다 사라지는 flash가 없도록 active/rotation 순서를 검증한다.
- 각성 게이지 위치와 카드 자체 아트는 이 unit에서 재설계하지 않는다.

## 완료 기준

- [x] Tray↔Hand 전환이 좌표 점프 없이 같은 외곽 프레임의 앞뒷면처럼 보임.
- [x] Placement/Battle 어느 phase에서 열고 닫아도 원래 tray size/rail 위치 복원.
- [x] 빠른 연속 토글/ForceClose에서 panel·cost suppression stuck 없음.
- [x] slomo lease가 기존처럼 획득/해제되고 전투 time scale 누수 없음.
- [ ] 16:9/20:9에서 카드 fan과 safe edge 충돌 없음. (unit 5 QA 캡처 세트에서 일괄)

확인 2026-07-12 — HandView 배킹을 trayConfig 공유 문법(라운드 22 + 골드 엣지 + 네이비 fill, handSize/anchoredY Config 소유)으로 통일, config 미할당 시 기존 단색 무회귀 폴백. Play 검증: 핸드 오픈 캡처(카드 fan 정상), Close 후 strip 복원, 연속 토글 x4 stuck 없음, battleScale=1 복귀(slomo 누수 없음). FlipRoutine/Open/Close/suppression 계약 무접촉. 씬 배선 1줄(trayConfig). 콘솔 0. 코드 커밋 `b96aef1e`.
