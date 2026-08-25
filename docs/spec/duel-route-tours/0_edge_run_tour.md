# 0. T3 가장자리 러너 — 테두리 주행 투어

## 목적

두 레인이 판의 **최외곽 행**(y0 · y9)을 타고 서진하게 만든다. 중앙에 쌓은 방어가 통째로
무력화되고, 강 끝에서 안쪽으로 튀어 오르는 훅이 유일한 병목이 된다.

훅은 저작하지 않는다 — `(13,0)` 에서 `(6,0)` 으로 갈 때 `x=11,y0` 이 강이라 플로우가 y1 으로
올라갔다 내려온다. **지형이 만들어주는 병목**이라 경유점을 더 찍을 필요가 없다.

## 변경 대상

- `Assets/_Project/Data/Maps/MapDocument_Duel.asset` — `waypointPaths` 1·2 신설, `spawnRoutes` 신설
- `Assets/_Project/Tests/EditModeAssets/MapDocumentPoolDevEntriesTests.cs` — Duel 특례 분기 제거

## 구현

### 경로 저작

| index | 용도 | 경유점 |
|---|---|---|
| 0 | 공중 예약 (**불변**) | `(11,4)` |
| 1 | lane0 = 하단 스폰 `(20,3)` | `(18,0)` → `(13,0)` → `(6,0)` |
| 2 | lane1 = 상단 스폰 `(20,5)` | `(18,9)` → `(13,9)` → `(6,9)` |

`spawnRoutes = [1, 2]` (파생 스폰 순서 = `[하단(y−1), 상단(y+1)]`).

`spawnRoutes` 필드는 이 에셋에 **아직 없다**. 정규식 치환은 매치 실패로 조용히 무시되므로
(siege-duel-map 에서 `placeMask` 로 실제로 밟은 함정) `SerializedObject` 로 **삽입**한다.

### 검증한 저작 제약 (계약 3·4)

- 간격: 스폰→18,0 = 3 · 18,0→13,0 = 5 · 13,0→6,0 = 7 · 6,0→골 = 4 (전부 ≥ 2)
- 강 셀 아님(강은 x=11 뿐, 경유점은 x=18·13·6)
- 골 `(2,4)` · 스폰 `(20,3)`·`(20,5)` 와 겹치지 않음
- 거점 footprint 밖 — 본능은 통행을 막지 않지만 경유점을 그 안에 두지 않는다

### 테스트 수정

`SiegeMap_TwoLanes_TakeDistinctPaths` 의 `if (mapName == "Duel")` 특례를 **제거**해 세 공성 맵이
같은 단언(`RouteForSpawn > 0` · 서로 다름 · 첫 경유점이 스폰 쪽 반구)을 받게 한다. Duel 의 첫
경유점은 lane0 `(18,0)` y0 < 4, lane1 `(18,9)` y9 > 4 라 그대로 통과한다.

특례와 함께 `MidColumnCrossingY` 헬퍼도 지운다(유일 호출처였다). **그 단언이 지키던 성질은
동률(200 = 200)을 `FlowFieldBuilder` 의 방향 순서로 깬 결과**였고, 그건 맵 저작의 성질이 아니라
빌더 구현 세부다. 경로를 저작한 지금은 의도가 저작에 드러나 있어 더 강한 pin 이다.

## 완료 기준

- [x] 에셋에 `waypointPaths` 3개 · `spawnRoutes: [1,2]` 가 실제로 들어갔다(디스크 재확인)
- [x] `OnValidate` 경로/레인 경고 0 · 컴파일 에러 0
- [x] EditMode 2,537개 실행 · 이 spec 관련 실패 0
      (`SiegeMap_TwoLanes_TakeDistinctPaths` · `SiegeMap_AuthoredRoutes_PassLaneValidation` 3맵 전부 통과)
      ⚠ 무관한 사전 실패 1건 — `UnitKitCatalogTests.CatalogDescriptions_UseThreeFixedSections`
      (malphite 2행 30자 > 28). 입력(`Defender_Malphite.asset` · 테스트) 둘 다 HEAD 그대로라
      이 작업 이전부터 빨갛다. `docs/reference/test-procedure.md` 의 «EditMode 기지 실패 없음»
      이 stale 하다 — 별건으로 처리한다.
- [ ] **사용자 Play 체감** — 두 레인이 판 위/아래 테두리를 타고 강 끝에서 훅을 그린다.
      중앙 배치만으로는 못 막는다
- [ ] PlayMode `WaypointRoutingLiveTest` · `SpawnGuideMatchesWalkTest` (스폰 예고 라인이 투어를
      따라가는지) — 사용자 확인 후 실행
