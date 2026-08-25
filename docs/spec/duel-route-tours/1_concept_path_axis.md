# 1. 웨이브 컨셉에 경로 축을 놓는다

## 목적

경로를 고르는 축이 **«적 종류»(전 맵 공통)** 와 **«입구»(맵 전체 웨이브)** 둘뿐이라, «이번 웨이브만
다른 길로» 가 표현되지 않는다. 슬롯에 축을 하나 더 놓아 **컨셉이 길을 고르게** 한다.

`WaveConceptSlot` 이 이미 가진 축과 같은 성격이다 — `laneGroup`(어디로 들어오나) ·
`classFilter`(무엇이 오나) · `altitude`(어느 고도로) 옆에 **`pathIndex`(어느 길로)**. 특례가 아니라
축 추가다.

## 변경 대상

| # | 파일 | 변경 |
|---|---|---|
| 1 | `Scripts/Data/WaveConceptData.cs` | `WaveConceptSlot` 에 `public int pathIndex = -1` |
| 2 | `Scripts/Data/GeneratedWavePlan.cs` | `WaveSpawnGroup` · `ExpandedWaveSpawn` 에 `pathIndex` (기본 -1) |
| 3 | `Scripts/Data/WavePatternGenerator.cs:998` | 컨셉 슬롯 → 그룹 생성 시 `slots[s].pathIndex` 전달 |
| 4 | `Scripts/Data/WavePatternGenerator.cs:655~690` | `ExpandWave` 의 `AddEntry`/`AddEntryAt` 가 값을 싣는다 |
| 5 | `Scripts/Bridge/BattleBridge.cs:514·2311` | `PendingSpawnEntry.pathIndex` (레거시 `:1397` 은 -1) |
| 6 | `Scripts/Bridge/BattleBridge.cs:9071·9080` | `CreateEnemyEntity(..., conceptPathIndex)` 로 전달 |
| 7 | `Scripts/Battle/Movement/WaypointProgress.cs` | `ResolvePathIndex` 3축 |
| 8 | `Scripts/Data/WavePatternGenerator.cs:630` | 스폰 예고도 같은 함수를 부르므로 **인자 하나만** 추가 |

**8번이 이 설계의 배당금이다.** 예고와 스폰이 이미 `ResolvePathIndex` **한 함수**를 공유하고 있어서
(waypoint-flight-enemy unit 11 이 「가이드는 최단거리, 유닛은 웨이포인트」 버그를 그렇게 닫았다),
축을 늘려도 두 곳이 갈릴 수 없다.

## 우선순위 — 좁은 쪽이 이긴다

```
적 SO 지정 (AttackUnitData.waypointPathIndex) >= 0   → 그것   (종의 정체성)
아니면 웨이브 컨셉 (WaveConceptSlot.pathIndex)  >= 0   → 그것   (이번 편성의 성격)
아니면 레인 기본 (MapDocument.spawnRoutes[lane]) >= 0  → 그것   (맵의 성질)
셋 다 없으면 -1                                        → 골 직행
```

기존 계약(«좁은 쪽이 이긴다»)의 연장이다. Skimmer 는 어느 컨셉에 실려도 자기 공중 경로를 탄다 —
비행 경로는 종의 정체성이고 컨셉이 그것을 덮으면 강을 못 건넌다.

`ResolvePathIndex` 가 이 우선순위의 유일한 정본이라는 계약은 그대로다. 호출부에서 삼항으로
풀지 않는다 — 풀면 EditMode 로 고정할 지점이 사라진다.

## 지켜야 할 선

- **rng 를 새로 소비하지 않는다.** 저작 필드를 읽기만 한다. 소비하면 `PickConcept`/`AssignLanes`
  이후의 난수열이 전부 밀려 **웨이브 편성이 통째로 재추첨**되고 지금 밸런스가 날아간다.
- **`laneGroup` 처럼 배정 로직을 두지 않는다.** lane 은 «위상» 이라 실제 번호를 시드가 고르지만,
  경로는 저작값이 곧 인덱스다. 그래서 `AssignLanes` 에 대응하는 것이 없다.
- **보스·호위·레거시 덱 스폰 경로는 -1 로 둔다**(`:284`·`:297`·`:336`·`:1397`). 기본값이 현행이라
  그 경로들은 코드가 바뀌어도 거동이 같다.

## 완료 기준

- [x] EditMode: `ResolvePathIndex` 3축 우선순위 — SO 우선 · 컨셉 차순 · 레인 폴백 · 전부 -1 이면 -1
      (`WaypointRoutingTests` 4건. 비행 적이 컨셉에 덮이지 않는 단언 포함)
- [x] EditMode: 컨셉 슬롯의 `pathIndex` 가 `WaveSpawnGroup` → `ExpandedWaveSpawn` 까지 보존된다
- [x] EditMode: `BuildSpawnGuideForecasts` 가 컨셉 경로를 반영한다 — 5웨이브 예고가 `L1:p2`
- [x] **미저작 상태의 생성 결과가 변경 전과 동일** — 같은 시드(20261972)로 15웨이브를 뽑아
      대조: 컨셉·유닛·수량·lane·시각 전부 일치. 재추첨 없음
- [x] EditMode 2,558개 실행 · 이 unit 관련 실패 0 · 컴파일 에러 0
      (무관한 사전 실패 1건 = `UnitKitCatalogTests` malphite 2행 30자, HEAD 부터 빨감)

**확인 2026-08-23** — 사용자 Play 확인 완료(5웨이브에만 컨셉 경로가 붙는 것을 육안 확인).
