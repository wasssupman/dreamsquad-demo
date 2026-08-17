# siege-lane-spawn — 공성 2스폰 파생과 레인 경로 부활

> ## 목표 3줄
>
> 1. **공성 맵이 진짜 2레인이 된다** — 적 마음의 상/하단 타일 2개가 파생 스폰이 되어, 각 스폰이 자기 최단거리(또는 저작 경로)를 찾아간다.
> 2. **레인별 웨이포인트 경로가 공성에서 부활한다** — `spawnRoutes` 는 이미 완성된 기능이고 공성 파생에서만 버려지고 있다(`MapDocumentBuilder` 의 무조건 Dispose). 되살리기만 한다.
> 3. **laneCount 1 → 2** 로 「원거리」(2레인 협공) 컨셉이 공성 맵에서 구조적으로 부활한다.

상태: **구현 완료 2026-08-17** (unit 0~2 커밋 `f825a86d`·`bfbed4c3`·`a74b7e67`) · PlayMode·사용자 Play 체감 확인 대기

## 배경 (실측 근거)

- 현행: 공성 = 적 마음 셀 1개가 파생 스폰 → `GeneratorLaneCount` 1 → 원거리 영구 제외 + 전 웨이브 단일 레인.
- 단일 경로의 원인은 **반 칸 어긋난 대칭축**이다. 지형은 y=5.5 미러인데 마음이 y=5 라 하단 다리가 항상 최단(플로우 필드 dist 88 vs 92). 결정론이라 동률이어도 갈라지지 않는다 — 스폰을 나누는 것만이 해법.
- 상/하단 2스폰 검증 완료(플로우 필드 오프라인 재현): Duel 은 상단 스폰 → 상단 다리(y8) / 하단 스폰 → 하단 다리(y3) 로 **지형이 갈라준다**. Ford(중앙 여울 1개)·Isle(중앙 개방)은 지형만으로 안 갈라져 **레인 경로 저작**(unit 3)이 필요하다.
- 3맵 전부 마음 (18,5), 상단 (18,6)·하단 (18,4) 모두 Walk — 지형 수정 불요.

## 작업 단위

> 각 unit 은 자기 테스트를 지참한다 — 파생 변경이 기존 단언을 깨므로 회귀선을 별도 unit 으로 미루면 그 커밋이 빨간 채 남는다.

| # | 작업 | 문서 |
|---|---|---|
| 0 | 스폰 파생 1→2 — 마음 (x, y−1)·(x, y+1), **파생 순서 = 레인 번호** 계약, 저작 검증 확장(추가만), 기존 단언 갱신 + 순서 pin | 0_derived_dual_spawn.md |
| 1 | `spawnRoutes` 공성 부활 — 공성 분기에서 doc 값으로 **재구축**(길이 = 파생 스폰 수일 때만; 저작 spawns 가 비어 배열이 길이 0으로 만들어지므로 Dispose 조건화가 아니라 재구축이다) + 단언 반전 pin | 1_spawn_routes_revival.md |
| 2 | Ford·Isle 레인 경로 저작 — `waypointPaths` 1·2 신설 + `spawnRoutes` 배선(에셋 직접 편집) + 경로 분리 술어 | 2_ford_isle_lane_routes.md |
| 3 | handoff | 3_handoff_summary.md |

## Feature-wide 계약

1. **파생 순서 = 레인 번호**: 파생 스폰은 `[하단(y−1), 상단(y+1)]` 순 = lane 0, 1. 이 순서가 `spawnRoutes` 인덱스·`EffectiveSpawnIndex`·스폰 예고의 공통 전제다. 뒤집히면 두 레인의 경로가 서로 바뀌므로 테스트로 pin 한다.
2. **마음 셀 자체는 더 이상 스폰이 아니다.** 저작 검증(`ValidateStructures`)의 Walk 검사를 마음 셀에서 **상/하단 2셀**로 확장한다(경계 검사 포함). 연결성(`AllSpawnsReachGoal`)은 두 스폰 모두에 성립해야 한다.
3. **`spawnRoutes` 불변식 유지**: «미생성 이거나 정확히 spawns 길이». 파생 스폰(2)과 저작 길이가 다르면 현행처럼 버린다 — 다른 레인의 경로를 조용히 읽는 사고를 그대로 막는다.
4. **path 0 은 공중 예약이다.** `Enemy_Skimmer.waypointPathIndex = 0`(SO 축, 전역)이 Duel `waypointPaths[0]`(강 셀)을 탄다. 레인 경로는 **1·2 에 추가**한다. SO 지정 > 레인 기본이므로 Skimmer 는 영향 없다.
5. **웨이브 편성 재추첨은 이 spec 의 의도된 결과다.** laneCount 1→2 는 `PickConcept` 후보(원거리 부활)와 `AssignLanes` rng 소비를 바꾼다. 새 baseline 확정(시드 재선정)은 `wave-ramp-two-phase` unit 3 이 담당 — **이 spec 이 먼저**여야 한다.
6. 모드 파생 규칙(`적 마음 1기 = Siege`)과 «공성 spawns 저작 금지»는 불변. 바뀌는 것은 파생이 채우는 **개수**뿐이다.

## 파이프라인 커버리지 (적/웨이브 아키타입 대조)

| 정거장 | 이 spec 의 변경 |
|---|---|
| 덱 → `WavePatternGenerator` | laneCount 2 전달 (`GeneratorLaneCount` 는 `spawns.Length` 파생이라 코드 무변경) |
| `MapDocumentBuilder.ToGeneratedMap` | **변경** — 스폰 파생 1→2 + `spawnRoutes` 조건부 채택 |
| `QueueWave` 예고 (`BuildSpawnGuideForecasts`) | N/A — laneRoutes 주입 경로 기존 그대로, 레인 2가 되며 자동 성립 |
| `SpawnUnit` (`RouteForSpawn`→`ResolvePathIndex`) | N/A — 완성된 기능, 데이터만 살아난다 |
| 스폰 마커/예고 라인 뷰 | N/A — spawns 배열 소비, 개수 무관 |
| 런타임 스폰 셀 폐쇄(`CloseCellLayers`) | N/A — 루프가 길이 따라 2셀 폐쇄 (배치 −1칸, 허용) |

## 검증 주의

- PlayMode 중 Duel 을 참조하는 테스트(`SpawnGuideMatchesWalkTest`·`InstinctNearestTargetMeasureTest` 등)는 스폰 위치 변화의 영향권이다 — Unity 가동 시 PlayMode lane 재실행 필수.
- 페인터는 공성 `spawnRoutes` 저작을 모른다(공성은 저작 spawns 가 없어 UI 가 안 뜬다). 저작은 에셋 직접 편집 — 이 spec 에서 에디터 툴링은 만들지 않는다.

## 후속 후보

- **페인터의 공성 레인 경로 저작 지원** — 필요해지면 별건.
- **본능 프랍 진영 구분·강 시각 표현** — siege-duel-map 에서 이관된 기존 후보 유지.
- **라이브 풀 편입** (Count 6→7 재추첨 동반) — 사용자 결정 대기, `wave-ramp-two-phase` 검증 후.
