# 2. 리뷰 후속 3건 — document 연결성 가드 · 로거 실시드 · 패널 무푸시 hydrate

## 목적

`acff0abc` code-review(14건)에서 CONFIRMED 상위 3건을 반영한다. 나머지 잠복/엣지 11건은 Follow-up Backlog 로 이관.

## 변경 대상

- `Assets/_Project/Scripts/Data/MapGrid/MapGridBattleAdapter.cs`
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs`
- `Assets/_Project/Scripts/Logging/BattleLogger.cs`
- `Assets/_Project/Scripts/Core/DraftController.cs`
- `Assets/_Project/Scripts/UI/Draft/MapSettingsPanelView.cs`

## 구현

1. **document 연결성 가드**: 기존 코드는 "MapGrid = Validator 가 connectivity 보장" 가정으로 fallback 검사를 스킵했지만, 수동 document 는 Validator 를 우회한다. `IsUsableDocument`(어댑터 소비 조건과 공유 술어)로 판별해 document 경로는 `AllSpawnsReachGoal` 검사를 거치게 했다. 실패 시 fallback 직선 맵.
2. **로거 실시드**: 로그의 mapSeed 가 `DeriveMapSeed(matchSeed)` 추정치로 남아 실제 빌드 시드(fixedMapSeed/document -1)와 어긋나던 것을, 맵 빌드 직후 `SetActualMapSeed` 로 덮어쓰게 수정 — 로그 기반 맵 재현성 복원.
3. **패널 무푸시 hydrate**: unit 0 의 mapSource 단일 sync 를 일반화 — `SyncMapStateFromBridge()` 가 소스+그리드 크기+goalEdgeOnly+legacy 옵션 전부를 흡수하고, init 시 push 를 제거. 부수 효과로 init 시 `RebuildDraftMap` 3연속(2회 낭비) 제거 + null controller NRE 가드.

## 완료 기준

- [x] compile 0 errors
- [x] Play 스모크 — ArkFunnel 빌드 정상, 콘솔 클린
- [x] `logger.match.mapSeed=-1` (document 실시드) 실측

확인 2026-07-19 — 커밋 `ba4ed7e3`
