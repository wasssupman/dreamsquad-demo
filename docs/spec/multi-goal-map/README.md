# Multi-Goal Map — 맵당 골 N개 지원

**상태: 완료 2026-07-23** (유닛 0~6 구현·검증. 사용자 Play 육안만 남음)

## 목표

지금은 맵당 골이 **1칸**으로 하드와이어돼 있어 분리 복도들이 골에서 강제 합류한다. 골을 **1~4개**로 늘려, 명일방주식 다출구 맵 — **각 스폰이 자기 전용 골로 나가는 완전 독립 복도** — 을 가능하게 한다. `분리된 경로` 요구의 가장 순수한 형태.

## 왜 contained feature 인가 (조사 결과)

- **경로탐색은 이미 멀티-소스.** `FlowFieldBuilder.BuildFromSources(sources[])` 존재(보스-디펜더 필드용) — 모든 골이 dist=0에서 동시 확산, 각 셀 flow 는 **최근접 골**로. 단일 골 `Build` 는 1-소스 래퍼일 뿐.
- **골 도달 = `FlowFieldSingleton.dist[idx]==0`** 로 통일 가능. dist 는 이미 싱글턴에 저장돼 있고 이미 `BattleBridge:1721` 에서 쓰는 방식. 골 개수에 **무관**해지는 우아한 전환점.
- **점수/스트레스 예산 불변.** 누수 = `GoalReachedEvent` 카운트라 어느 출구든 1누수. budget-equality 계약 그대로.

바꾸는 건 골 **저장**(단일→목록), **도달 판정 3곳**(`cell==goalCell` → `dist==0`), **연결성 검증**, **authoring/렌더**. 경로탐색·예산은 안 건드림.

## 최근접-골 라우팅 (설계 기본값)

골 배정은 명시하지 않는다. 공유 flow field 가 각 셀을 **최근접 골**로 보내므로, **분리 복도**에선 각 복도가 자기 연결 컴포넌트라 그 안의 적은 자동으로 **자기 골로만** 나간다(모호성 0). 복도가 갈라지면 그 지점에서 가까운 골로. → per-lane 골 배정 로직 불필요.

## 회귀 안전 (단일 골 무변화)

`goals=[g]` 이면 소스 1개·`dist==0` 은 그 한 칸 → **기존 단일골과 바이트 동일**. 유닛 0~5 는 멀티골 맵이 실제로 생기는 유닛 6 전까지 현 5맵 동작을 1비트도 안 바꾼다(무형 롤아웃).

## 작업 단위 목록

| # | 작업 구분 | 문서 | 목적 |
|---|---|---|---|
| 0 | Data | `0_goals_data_contract.md` | `MapDocument.goals[]` + `GeneratedMap.goals` 추가(단일 `goal`=primary 병존), ToGeneratedMap/Dispose |
| 1 | ECS(Effects) | `1_multisource_flowfield.md` | `BattleBridge.BuildFlowField` → `BuildFromSources(goals)` |
| 2 | ECS(Movement/Effects) | `2_goal_reached_dist_zero.md` | 골 도달 판정 3곳 `cell==goalCell` → `dist[idx]==0` |
| 3 | Data | `3_multigoal_connectivity.md` | `AllSpawnsReachGoal` → `AllSpawnsReachAnyGoal`(멀티-소스 BFS) |
| 4 | Editor | `4_painter_multigoal.md` | `MapPainterWindow` 골 N개 페인팅 + 검증 |
| 5 | Presentation | `5_multigoal_render.md` | 골 마커 + 구조물 프랍 N개 렌더 |
| 6 | Content | `6_separated_corridor_maps.md` | 분리 복도 × 멀티골 명일방주식 맵 제작(풀 교체) |
| 7 | Handoff | `7_handoff_summary.md` | 인계 (종료 시) |

## Feature-wide 계약

- **골 목록이 source of truth**: `MapDocument.goals`(1~4, 각 Walk). `goal`(단일)은 `goals[0]` = **primary**(단일-점 소비자·마이그레이션 폴백용). `goals` 비면 `[goal]` 로 폴백(기존 5맵 무마이그레이션 통과).
- **🔴 GeneratedMap-레벨 폴백(리뷰 B1)**: `GeneratedMap` 은 **6개 생산자**가 있고(ToGeneratedMap·BuildFallbackLinear·BuildFromFixture·BuildFromManual·ProceduralMapGenerator·CellClassifier), 대부분 `goal` 만 세팅한다. `goals` NativeArray 를 **소비 지점**(BuildFlowField 유닛1·connectivity 유닛3)에서 `goals.IsCreated && Length>0 ? goals : [goal]` 로 폴백해 **모든 생산자**를 커버한다(cleanup 스펙과 독립). 유지되는 안전망 `BuildFallbackLinear` 는 `goals` 를 명시 세팅. **`GeneratedMap.IsCreated` 에 `goals.IsCreated` 를 넣지 않는다**(런타임 5곳 + 테스트 픽스처 ~10곳이 IsCreated=false 로 뒤집힘 — 리뷰).
- **골 판정 = `FlowFieldSingleton.IsGoalCell(cell)`**(구현 변경 — 유닛 2 참조): 초안의 `dist==0` 은 다수 EditMode 픽스처가 dist 를 all-zero 로 둬서 전부 골로 오판시켜 폐기. 대신 싱글턴 `goals` 집합 멤버십(**미설정 시 goalCell 폴백**)으로 판정 → 픽스처 무변경 통과. 4곳 전환: **reached**(MovementSystem)·**wall 예외**(MovementCellTrim)·**해저드 검증**(EffectSpawner)·**스모크 proxy**(MovementIntegritySmokeTest).
- **최근접-골 라우팅**: `BuildFromSources` 로 emergent. per-lane 배정 없음.
- **예산 불변**: 누수 이벤트=1/leak. `defeatGoalReachedCount`/timer/kill budget 무변경. 골 개수는 스트레스 예산과 무관.
- **회귀 안전**: 단일 골 맵은 유닛 0~5 후에도 동작 동일(goals=[g] ⇒ 소스1·dist==0 한 칸). `BuildFromSources` 는 유효 소스에만 dist=0 → 회귀 0(리뷰 CONFIRM).
- **병렬 단일골 표현도 확장(리뷰 M2)**: `BoardVisualPlan.goal`(배경 프랍 클리어런스·`BackgroundPropPlacer.IsNearSpawnOrGoal`)도 goals[] 로 → 유닛 5. 골 비주얼 앵커(`_goalVisualAnchorWorld`·튜토리얼)는 **primary 단일 유지**(의도).
- **라이브 스모크(리뷰 M1)**: `MovementIntegritySmokeTest` 가 `cell==goalCell` 을 walkability proxy 로 씀 → 멀티골 풀에서 red. `IsGoalCell` 로 전환(유닛 2).
- **ECS 경계**: `FlowFieldSingleton` 은 Effects 소유(그대로). MovementSystem 은 dist **읽기만**. 새 맥락/NativeQueue 불필요(`GoalReachedEvent` 재사용).
- **리뷰 매칭**: 유닛 1·2 는 ECS 시뮬 변경 → **ecs-reviewer**. 유닛 0(struct)·3(순수 BFS)·4(에디터)·5(Mono 렌더)·6(콘텐츠) → 일반 리뷰.

## 파이프라인 커버리지

골 **정거장**이 1→N 으로 바뀐다(마커 페인트 + 구조물 프랍). `docs/reference/object-pipeline-map.md` 의 goal/spawn 구조물 프랍 경로 대조 필요 — 소비를 `map.goal` 단일에서 `map.goals` 순회로 확장(생성 파이프라인 자체는 불변, 골 인스턴스 수만 N). 유닛 5 에서 맵 갱신 여부 확인.

## 후속 후보

- 골별 시각 구분/개별 목표(현재 전 골 동일 취급).
- 스폰↔골 명시 배정(현재 최근접 emergent).
- 즉시-반복 방지·아웃게임 맵 프리뷰(random-map-pool 후속에서 이관).
