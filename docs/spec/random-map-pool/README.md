# Random Map Pool — 풀에서 매판 랜덤 맵 + 맵별 웨이브

**상태: 초안 (승인 대기) 2026-07-22**

## 목표

단일 고정 맵(`MapDocument_ArkFunnel`)만 나오던 인게임 플레이를, **N장 제작 맵 풀에서 매판 하나를 랜덤 선택**하는 구조로 바꾼다. 이번 spec 은 **시스템 + 맵 2종**(ArkFunnel + 신규 1종)으로 검증한다. 나머지 3종은 후속 후보.

여기에 더해, **각 맵은 자기만의 공격 덱(적 웨이브 패턴)** 을 함께 갖는다. 풀 엔트리 = `(MapDocument, AttackDeck)` 쌍이라, 맵이 뽑히면 그 맵의 덱이 함께 뽑혀 **맵마다 다른 적 패턴**이 나온다. 맵의 **스폰 지점 개수**는 웨이브 레인 분배와 직결되므로(런타임 자동 round-robin), 각 덱의 웨이브 수량/호위 수를 스폰 개수에 맞춰 튜닝한다.

새 시스템은 최소화한다 — matchSeed 는 이미 매판 랜덤이므로, 새 랜덤 소스 없이 기존 seed 인프라(`MatchSeed.DeriveMapSeed`)로 인덱스만 고른다. 재현 가능(비동기 토너먼트/디버그 안전)은 공짜로 따라온다.

## 작업 단위 목록

| # | 작업 구분 | 문서 | 목적 |
|---|---|---|---|
| 0 | Data+Test | `0_mappool_data_and_select.md` | `MapDocumentPool` SO((맵,덱) 엔트리) + `MapPoolSelect.SelectIndex` 순수함수 + EditMode 테스트 |
| 1 | Code | `1_bridge_pool_and_deck_resolution.md` | BattleBridge: seed 로 (맵,덱) 인코운터 resolve, deck 소비를 `ActiveDeck` 경유, guard 술어 일치, `fixedMapSeed` 핀 전환 |
| 2 | Asset | `2_second_map_authoring.md` | 신규 맵 1종(스폰 2개) 레이아웃 설계·검증·베이크 |
| 3 | Asset | `3_per_map_attack_decks.md` | 맵별 덱: ArkFunnel=WaveA 재사용, 신규 맵용 덱 1종 신설(스폰 수에 맞춘 튜닝, WaveA 참조) |
| 4 | Wire+Verify | `4_pool_asset_wiring_and_verify.md` | `MapDocumentPool.asset` 생성·배선, `fixedMapSeed=0`, 브리핑 스트립 일치, Play 랜덤·패턴 실증 |
| 5 | Handoff | `5_handoff_summary.md` | 인계 (feature 종료 시 작성) |

## Feature-wide 계약

- **맵 선택 = `MapPoolSelect.SelectIndex(seed, pool.Count)`**. `seed` 는 기존 로컬값 그대로(`fixedMapSeed != 0 ? fixedMapSeed : MatchSeed.DeriveMapSeed(matchSeed)`). **라이브 매판 랜덤은 `fixedMapSeed = 0` 필요**(비0 = 인덱스 고정 = 테스트 핀). 이것이 "매판 같은 맵"이던 원인의 정확한 해제 지점.
- **풀 엔트리 = `(MapDocument document, AttackDeck deck)` 쌍**. 맵과 덱은 **항상 같은 인덱스로 함께 선택** → 맵마다 고정된 적 패턴. 독립 선택 아님.
- **웨이브 구성은 스폰 개수와 무관**, 레인 분배만 런타임 스폰 수(`_generatedMap.spawns.Length`)로 자동 round-robin(`ExpandWave`/`EffectiveSpawnIndex`, laneCount≤2 는 authored 존중, 3+ 는 deckIndex 결정론). **"어울림"은 코드가 아니라 데이터 튜닝** — 각 덱의 `minUnitsPerWave ≥ 스폰수`(이상적으로 배수), `bossEscort ≥ 스폰수−1`.
- **document 소비 조건은 한 곳**: `MapGridBattleAdapter.IsUsableDocument`. adapter `Build` 와 BattleBridge connectivity-guard 가 **선택된 doc** 기준으로 같은 술어를 쓴다.
- **덱 소비는 `ActiveDeck` 경유**. 풀이 비었거나 선택 엔트리가 미완성(doc unusable / deck null)이면 **레거시 serialized `deck`/`mapDocument` 폴백** → 무회귀.
- **브리핑 웨이브 스트립은 `ActiveDeck` 반영**. 맵/덱은 draft-stage prebuild(`BuildMapForBattle`)에서 확정되므로, 스트립도 선택된 덱을 읽어야 브리핑 = 실전 일치(안 하면 시각 불일치 버그).
- **재현**: 같은 matchSeed → 같은 (맵, 덱). `GameManager.debugFixedMatchSeed` 로 특정 인코운터를 핀.
- **점수 예산은 전 맵 동일** (토너먼트 공정, 2026-07-22 사용자 결정): 모든 맵 덱은 `defeatGoalReachedCount = 10`·`timerDurationSec = 180`(WaveA 값) **고정** → 시간(18,000)·스트레스(9,000) 예산 상한이 전 맵 동일. 킬 점수는 accumulator라 본질적으로 가변(웨이브 구성 의존 — 산식 계약, `score-formula.md`)이지만, 각 덱의 적 volume(waveCount·unitsPerWave 범위·보스 cadence)을 WaveA와 같게 둬 **킬 상한도 동급**으로 유지(킬 값은 유닛 종류와 무관 = 잡몹 일괄 100). **맵마다 다른 건 적 구성(유닛 종류·레인 분배·pacing)뿐** — 채점 기준·판 길이·패배 조건·점수 상한은 불변.

## 파이프라인 커버리지

N/A — 플레이 오브젝트(유닛/적/투사체/해저드/VFX) 신설 없음. `MapDocument`(맵 데이터)·`AttackDeck`(웨이브 데이터)는 기존 파이프라인의 **소스만 추가**하며, 이후 정거장(flow field → 타일맵 페인트 → 적 스폰/이동)은 전부 기존 경로를 그대로 소비한다. `docs/reference/object-pipeline-map.md` 구조 변경 없음.

## 후속 후보 (이 spec 밖)

- 맵 3종 추가 authoring (풀 5종 완성) + 각 맵 덱.
- 즉시-반복 방지(연속 같은 맵/덱 회피).
- 시즌/테마별 맵 풀(현재는 전역 단일 풀).
- 풀에서 **usable 엔트리만** 필터해 선택(현재는 선택 후 unusable → 레거시 폴백).
- 전용 맵/덱 authoring 에디터 툴(현재 execute_code authoring).
