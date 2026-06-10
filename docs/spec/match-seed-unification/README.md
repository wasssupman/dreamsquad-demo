# Spec — match-seed-unification (단일 매치 시드)

**상태: 초안 2026-06-10** — 승인 대기. 구현 전.

## 목표

맵과 웨이브를 **하나의 매치 시드(matchSeed)** 에서 결정론적으로 파생시킨다. 현재 시드는 3개로 완전히 분리돼 있다:

| 대상 | 현재 시드 출처 | 기본 동작 |
|---|---|---|
| 드래프트(유닛 풀) | `DraftController.GenerateSeed()` = `TickCount ^ UnityRandom` | 매 판 랜덤 |
| **맵** | `mapSettings.EffectiveSeed` (`defaultSeed=0` → `DateTime.Ticks`) | 매 판 랜덤 |
| **웨이브** | `deck.ResolveWaveSeed()` (`waveSeed=0` → `1`) | **고정** |

맵은 매 판 바뀌고 웨이브는 고정이라, 같은 판에서 두 요소가 서로 다른 시드 계열을 따른다. 이 spec 은 맵·웨이브를 **GameManager 가 소유하는 단일 matchSeed** 에서 salt 분리로 파생시켜, 한 판 안에서 일관되고 같은 matchSeed 면 항상 재현되게 한다.

## 검증 질문

> 동일한 matchSeed 를 고정해 Play 를 두 번 했을 때, **생성된 맵과 웨이브 구성이 완전히 동일**한가? 그리고 matchSeed 를 미지정으로 두면 **매 판 맵·웨이브가 함께(같은 계열로) 바뀌는가**?

## 확정된 아키텍처 결정 (2026-06-10, 사용자 승인)

1. **소유/주입**: `GameManager` 가 매치당 matchSeed 1개를 생성·소유하고 `BattleBridge` 에 주입한다. (대안: BattleBridge 자체 보유 → 기각. 비동기 토너먼트에서 서버 주입 확장이 GameManager 소유라야 자연스러움.)
2. **파생**: `mapSeed = MatchSeed.DeriveMapSeed(matchSeed)`, `waveSeed = MatchSeed.DeriveWaveSeed(matchSeed)`. 서로 다른 salt 로 **계열을 decorrelate** 한다 (같은 int 를 그대로 쓰지 않음 — 맵 크기와 웨이브 수가 우연히 상관되는 것 방지).
3. **범위 = 맵 + 웨이브만**. 드래프트 시드는 현행 유지(후속 후보). visualSeed(투사체 jitter)는 맵 계열에 속하므로 함께 matchSeed 파생으로 전환.
4. **미지정 기본 = 매 판 새 시드**. `GameManager` 의 고정 시드 노브(`debugFixedMatchSeed`)가 0 이면 매 판 시간 기반 새 matchSeed, 0 이 아니면 재현용 고정.

## feature-wide 계약 (load-bearing)

- **단일 소스 of truth**: 라이브 매치 시드는 `GameManager` 가 보유한 `matchSeed` 하나뿐. 맵·웨이브는 여기서만 파생한다. 다른 코드가 독자 시드를 만들지 않는다.
- **파생 경로**: `MatchSeed` 정적 유틸(`Core/MatchSeed.cs`)이 `GenerateRandom()` / `DeriveMapSeed` / `DeriveWaveSeed` / `DeriveVisualSeed` 를 제공. 순수 함수 — 같은 입력 = 같은 출력. EditMode 단위 테스트로 결정론·decorrelation 보장.
- **주입 시점**: `GameManager` 는 **맵을 빌드하는 `PrepareDraftMap()` 호출 이전에** `battleBridge.SetMatchSeed(matchSeed)` 를 호출한다. Draft 경로·Squad 경로 양쪽 모두.
- **SO 시드 노브 강등**: `mapSettings.defaultSeed` 와 `deck.waveSeed` 는 더 이상 라이브 시드를 결정하지 않는다(읽기 중단). 재현 고정은 `GameManager.debugFixedMatchSeed` 에서. SO 필드는 직렬화 호환을 위해 제거하지 않고 deprecated 주석만 단다.
- **생성기 불변**: `MapGridGenerator`/`ProceduralMapGenerator`/`WavePatternGenerator` 의 `int seed` 시그니처는 그대로. **변경은 BattleBridge 의 시드 *출처* 와 GameManager 주입뿐** — blast radius 최소(map-origin-placement 와 동일 전략).
- **재현 로깅**: `GameManager` 는 생성한 matchSeed 를 `Debug.Log` 로 남긴다(콘솔에서 매치 재현 가능). 정식 로그 스키마 반영은 후속.

## 구현 문서 목록

| 파일 | 작업 구분 | 목적 |
|---|---|---|
| `0_match_seed_util.md` | foundation | `Core/MatchSeed.cs` 정적 유틸 + EditMode 결정론/decorrelation 테스트 |
| `1_gamemanager_owns_seed.md` | owner | GameManager 가 matchSeed 생성·로그·주입 (PrepareDraftMap 이전, 양 경로) + `SetMatchSeed` API |
| `2_bridge_map_seed.md` | consume | BattleBridge `_matchSeed` 저장, `BuildMapForBattle`·visualSeed 가 `DeriveMapSeed`/`DeriveVisualSeed` 사용 |
| `3_bridge_wave_seed.md` | consume | `TryInitializeGeneratedWaves` 가 `DeriveWaveSeed` 를 명시 시드로 `WavePatternGenerator` 에 전달 |
| `4_handoff_summary.md` | handoff | 구현 종료 인계 (구현 시 작성) |

## 비목표 / 후속 후보

- **드래프트 시드 통합** — 이번 범위 밖. matchSeed 에서 드래프트까지 파생하면 매치 전체 재현. 별도 spec.
- **비동기 토너먼트 시드 공유** — 서버/상대가 같은 matchSeed 를 받는 배선. GameManager 소유 구조가 이를 위한 토대. 별도 spec.
- **정식 매치 로그 스키마에 matchSeed 필드 추가** — 지금은 Debug.Log 만. BattleLogSchema 반영은 후속.
- **SO 시드 필드 제거** — deprecated 표기만. 실제 필드/에셋 정리는 후속.
