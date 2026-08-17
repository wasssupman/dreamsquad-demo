# 2. Ford·Isle 레인 경로 저작 + 경로 분리 술어

## 목적

지형이 레인을 갈라주는 Duel 과 달리 Ford(중앙 여울 한 덩어리)·Isle(중앙 개방)은 두 스폰의
최단거리가 붙어서 온다. 레인별 웨이포인트 경로(unit 1 에서 부활)로 상·하단 경유를 저작해
가른다. 코드 0 — 에셋 편집과 테스트만.

## 변경 대상

- `Assets/_Project/Data/Maps/MapDocument_Ford.asset` — waypointPaths 1·2 = (10,4)·(10,7),
  `spawnRoutes: [1, 2]` (키 부재 → **삽입**, placeMask 함정과 동일)
- `Assets/_Project/Data/Maps/MapDocument_Isle.asset` — 동일, 경유 (10,3)·(10,8)
- `Assets/_Project/Tests/EditModeAssets/MapDocumentPoolDevEntriesTests.cs` — 분리 술어

## 구현

1. 경유 셀은 플로우 추적으로 사전 검증했다: Ford lane0 x=10 통과 y=4 / lane1 y=7,
   Isle lane0 y=3 / lane1 y=8. 전부 Walk·골 도달.
2. **path 0 은 공중 예약** (`Enemy_Skimmer.waypointPathIndex = 0`, SO 축이 레인 기본을
   이긴다) — 레인 경로는 1·2. Duel 은 routes 비움 유지(지형이 가른다).
3. 술어 `SiegeMap_TwoLanes_TakeDistinctPaths`: Duel = routes 비움 + 플로우 추적으로
   중앙 열 통과 y 가 마음 기준 상/하로 갈림. Ford·Isle = routes 유효(>0, 서로 다름) +
   경유 셀 y 가 마음 기준 하단/상단.

## 완료 기준

- EditMode 신규 술어 3맵 초록, 전량 무회귀
- Ford·Isle import 에러/경고 0 (레인 검증이 파생 스폰 기준으로 통과 — unit 1)
