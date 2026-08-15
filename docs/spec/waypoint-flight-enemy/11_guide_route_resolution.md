# 11. 예고선의 레인 기본 경로 해석 (unit 9 켜기의 누락분)

## 목적

**예보와 스폰이 경로 인덱스를 같은 규칙으로 해석하게 한다.**

사용자 실측 증상(2026-08-15): 「웨이포인트 레인인데 경로 가이드가 최단거리로 그려진다」.

unit 9 가 스폰(`SpawnUnit` → `CreateEnemyEntity`)의 경로 해석을 `WaypointRouting.ResolvePathIndex(SO 저작, 레인 기본)` 2축으로 바꿨는데, 같은 값을 소비하는 **예고 생산자를 안 바꿨다**. `BuildSpawnGuideForecasts` 가 `unit.waypointPathIndex`(지상 전원 -1)만 실어, 레인 경로 맵(Coil 레인 0 · Zig 레인 1)에서 **가이드는 최단거리, 유닛은 웨이포인트**로 갈렸다.

`SpawnGuideForecast` 의 다른 필드(`laneIndex`·`traversalLayers`)는 전부 **해석된 런타임 값**인데 `waypointPathIndex` 만 날 SO 값이었던 비대칭이 이 버그의 정체다. 이 unit 이 그 비대칭을 없앤다 — 다음에 이 필드를 읽는 기능(미니맵 등)이 같은 버그를 재생산하지 않는다.

## 변경 대상

- `Assets/_Project/Scripts/Data/WavePatternGenerator.cs` — `BuildSpawnGuideForecasts(entries, IReadOnlyList<int> laneRoutes = null)` 옵셔널 주입. 해석은 기존 `WaypointRouting.ResolvePathIndex` **재사용**(규칙은 계속 한 곳).
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `QueueWave` 가 `_generatedMap.RouteForSpawn(i)` 로 레인 배열을 만들어 전달. 생성기는 Data 레이어라 맵을 모르므로 주입은 호출측 소유.
- `Assets/_Project/Tests/EditMode/WaveSpawnForecastTests.cs` — 우선순위 3케이스.
- `Assets/_Project/Tests/PlayMode/SpawnGuideMatchesWalkTest.cs` — Coil 증상 계측.

**바꾸지 않은 것**: `TryGetSpawnPathSim` — (레인, 경로, 층)을 받아 그리는 저수준 원시함수 계약을 유지한다(테스트 3곳이 명시 인덱스로 호출). 해석 책임은 예보 생산 시점 한 곳에 둔다.

## 완료 기준

- **재현 먼저**: Coil(풀 1) 레인 0 지상 예보의 `waypointPathIndex` 단언이 수정 전 **빨강**(`Expected: 1, But was: -1`) — 확인 2026-08-15.
- **EditMode** `WaveSpawnForecastTests` — 레인 기본이 예보에 실린다 / SO 저작(-1 아님)이 레인 기본을 이긴다(계약 10) / laneRoutes 미주입·부족 배열은 -1 폴백. 14/14 초록.
- **PlayMode** `Coil_RoutedLaneGuide_AdvertisesTheLaneDefaultRoute` — 예보가 경로 1을 싣고, 그 가이드가 웨이포인트 (8,9) 를 경유하며, **경로 1을 실제로 걷는 적들이 가이드 1.6타일 안**을 지난다(표본 >30, 이탈 <35%). 초록.
- **무회귀** — `Duel_EnemiesWalkAlongTheAdvertisedGuideLine` 초록(거점 경유 예고선 불변).

### 같이 발견된 기존 실패 (이 unit 소관 아님 — 기록)

`ValidationWave_ShowsGuides_ThenPassesWaypointsInAuthoredOrder` 가 **unit 5 커밋(34c1603f) 이후 계속 빨강**이다. unit 5 가 라이브 밸런스로 `Enemy_Skimmer.minWaveNumber 8` 을 붙였는데, WaypointLab 검증 덱이 같은 SO 를 공유해 **첫 웨이브 Air 전제**(unit 4 저작)가 깨졌다. 이 unit 의 수정 전/후 모두 동일 실패 — 스태시 대조로 확인. 해법 후보는 랩 전용 Air 검증 유닛 신설(라이브 밸런스와 분리). 별도 unit 로 분리.
