# 12. 랩 전용 Air 검증 유닛 — 검증 덱을 라이브 밸런스에서 분리

## 목적

**WaypointLab 검증 덱이 라이브 밸런스 값에 얹혀 있지 않게 한다.**

unit 3~4 의 라이브 계측 `ValidationWave_ShowsGuides_ThenPassesWaypointsInAuthoredOrder` 는 첫 웨이브에 (경로 0 지상 · 경로 1 지상 · Air · 지상 대조군)이 모두 있다고 전제한다. 그런데 랩 덱의 Air 슬롯이 **라이브 `Enemy_Skimmer` SO 를 공유**해서, unit 5 가 라이브 밸런스로 `minWaveNumber 8` 을 붙인 순간(34c1603f) 첫 웨이브에서 Skimmer 가 게이트로 걸러졌고 이 테스트는 그때부터 계속 빨강이었다(이후 아무도 안 돌려 미발견 — unit 11 회귀 확인 중 발견). unit 8(wave-concept-blocks)의 8→4 하향으로도 첫 웨이브(게이트 4 > 1)는 못 넘는다.

**검증 하네스는 밸런스가 아니라 기계를 재는 자리다** — 라이브 게이트가 바뀔 때마다 랩이 부러지면 안 된다. `Enemy_WaypointBasic/Alt` 가 이미 dev 전용인 것과 같은 결.

## 변경 대상

- **신규** `Assets/_Project/Data/Enemies/Enemy_WaypointAir.asset` (+.meta) — Skimmer 복제, `id: waypoint_air` · `minWaveNumber 1` · `maxPerWave 0`(캡 없음 = unit 5 이전 랩 편성과 동일한 수량 분배) · `traversalLayers Air` · `waypointPathIndex 0` · `flightLift 1.4` 유지(뷰 lift 단언이 읽는다)
- `Assets/_Project/Scripts/Data/Decks/Deck_WaypointLab.asset` — `attackUnitPool` 의 Skimmer → WaypointAir 교체(같은 슬롯, 풀 순서 불변 → 같은 waveSeed 로 unit 5 이전 편성 복원)
- `Assets/_Project/Tests/PlayMode/WaypointRoutingLiveTest.cs` — unit 9(도달 반경 체비쇼프 1) 이전의 stale 단언 2개 정정: ① 웨이포인트 진입 = 셀 일치 → **반경 1 이내**, ② Air 의 장애물 무시 증거 = 차단 셀 정확 진입 → **차단 셀 도달 반경 안 접근**(완화 후 정확 진입은 정상 동작이 아니고, 지상이었다면 도달 불가 건너뜀으로 접근 자체가 없다)

## 스킬 정거장 (enemy-wave-integration)

| 정거장 | 처리 |
|---|---|
| EnemyCatalog 등재 | **N/A** — dev 전용. 기존 랩 유닛 2종(`WaypointBasic/Alt`)도 미등재 |
| 라이브 덱 편입 | **N/A — 의도적 미편입.** 랩 전용이 존재 이유다. 라이브 Air 는 Skimmer·Dragon 이 담당 |
| 삽입 위치 | 랩 덱 기존 슬롯 교체(맨 뒤 아님) |
| waveSeed / 버전 | **불변** — 랩 덱은 라이브 baseline 대상이 아니고, 교체로 unit 5 이전 편성이 복원되는 것이 목적 |
| 컨셉 배정 | N/A — 랩 덱은 컨셉 풀 없음(레거시 2종 경로) |
| 튜토리얼 | N/A — dev 전용 유닛은 교습 로스터 밖 (`TutorialEntry_TeachesEveryLiveEnemyTypeInTenWaves` 는 라이브 로스터만 센다) |

## 완료 기준

- `ValidationWave_ShowsGuides_ThenPassesWaypointsInAuthoredOrder` **초록** — unit 5 이후 처음
- 실패 3층이 각각 별개 원인이었음을 기록: ① Air 부재(이 unit 의 본체) ② 진입 단언 stale(unit 9 미반영) ③ 차단 셀 단언 stale(동일)
- 라이브 무회귀: 이 unit 은 라이브 덱·라이브 SO 를 건드리지 않는다(신규 에셋 + 랩 덱 + 테스트뿐)
