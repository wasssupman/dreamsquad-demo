# Endless Mode (무한 모드)

> 상태: **완료 2026-07-25** — unit 0~4 구현·검증·커밋 (인계: `5_handoff_summary.md`).
> 브레인스토밍: `docs/plans/2026-07-24-endless-mode-design.md`

## 상위 목표

기존 토너먼트 배틀과 별개인 **스코어어택 무한 모드**. 3분 고정 창에서 **당기기(`ForceNextWave`)로
웨이브를 더 욱여넣어 킬 극대화**. 웨이브 최대 30·고정 10초 간격·타이머 180초. 점수 = 킬 + 누수
페널티, 시간축 0. **무제한(누수로 안 죽음)** v1.

**검증 질문**: "메인 모드를 건드리지 않고, 덱 플래그 하나(`battleMode`)로 무한 모드가 기존 배틀
파이프라인을 타는가? 당기기로 킬을 벌고 **누수로 깎이는 리스크/리워드가 실제로 성립**하는가?"

## 작업 단위

| 파일 | 구분 | 문서 | 목적 |
|---|---|---|---|
| 0 | data/enum | `0_data_contract.md` | `AttackDeck.battleMode` enum + `fixedWaveIntervalSec` (2필드) |
| 1 | 순수/sim | `1_generator_fixed_interval.md` | `WavePatternGenerator` 고정간격 지원 + EditMode 테스트 |
| 2 | bridge | `2_bridge_mode_awareness.md` | 진입(전용 encounter)+간격+누수게이트+시간0+리포트스킵 (모드 분기 전부) |
| 3 | authoring | `3_authoring_and_wiring.md` | `Deck_Endless` + `endlessEncounter` 배선 + dev 토글 |
| 4 | test | `4_playmode_smoke.md` | PlayMode 스모크 + 리스크/리워드 어서션 |
| 5 | handoff | `5_handoff_summary.md` | (구현 종료 후 작성) |

## Feature-wide 계약

1. **모드 seam 은 `AttackDeck.battleMode` enum 하나.** BattleBridge 만 읽어 분기(unit 2 한 곳).
2. **웨이브 스케줄러·트리거타임 계약 불변.** 간격만 데이터(`fixedWaveIntervalSec`, 0=기존 파생)로
   다르게. 당기기(`ForceNextWave`)·리스케줄 그대로 재사용.
3. **점수는 `ScoreMath.Evaluate` 순수함수 그대로.** 모드 차이 = BattleBridge 한 줄
   `remainingMs = IsEndless ? 0`(시간축 0) 뿐. **엔드리스 전용 ScoreRules 에셋 없음** — 메인
   `scoreRules` 재사용(엔드리스 전용 가중은 후속). 인터페이스 3분할 기각(제약 8·10).
4. **v1 은 무제한-only.** 엔드리스는 누수로 죽지 않음 — `defeatEnabled = !IsEndless`(신규 필드 없음).
   스트레스 점수 예산은 `defeatGoalReachedCount` 재사용하되 **높게**(§누수 예산). 개수 기반 패배는 후속.
5. **엔드리스는 공용 풀에 넣지 않는다.** BattleBridge 전용 `endlessEncounter`(serialized) + dev 토글로
   진입. `mapPool.Count` 불변 → `MapPoolSelect` 무손 → **토너먼트/디버그 맵 선택 회귀 0**. 엔드리스는
   토너먼트에 리포트도 안 함.
6. **ECS 맥락 변경 없음.** 스폰은 기존 `QueueWave→SpawnUnit`(Units) 그대로.
7. **하드코딩 금지.** 모든 값(간격·웨이브수·예산·타이머)은 `Deck_Endless` SO 에서.
8. **엔드리스는 기믹 없음(v1).** `_leakAllowancePenalty=0` 전제 → 스트레스=순수 누수 수. 엔드리스 덱은
   `timerDurationSec>0` 필수(타이머가 유일 종료자).

## 누수 예산 (saturation 주의 — critic MAJOR#3)

`ScoreMath` 는 스트레스를 0 에서 floor 한다. 예산(`defeatGoalReachedCount`)을 낮게 잡으면, 안 죽는
엔드리스에서 **예산 초과 누수가 공짜**가 되어 "과당기기+누수방치"가 최적전략이 됨 → 리스크/리워드 붕괴.
→ 예산을 **180초 내 도달 불가능한 값(예 100)**으로 잡아 매 누수가 한계효용을 갖게 한다. unit 4 가
이 성립을 어서션으로 고정.

## 파이프라인 커버리지

**N/A — 신규 플레이 오브젝트 없음, 생성→렌더 경로 불변.** 적/웨이브는 기존 스폰 파이프라인
(`QueueWave→SpawnUnit→Units→enemyViewPool/spineUnitPool`)으로 그대로. 새 아키타입·정거장 없음.
변경 지점은 웨이브 **간격**(데이터)·**점수 한 줄**·**진입 경로**(전용 encounter)뿐.

## 후속 후보 (현 범위 밖)

- **개수 기반 엔드리스 패배** — 누수 N 도달 시 종료. v1 은 무제한만. 추가 시 `!IsEndless` →
  `!(IsEndless && endlessNoDefeat)` + 덱 bool.
- **엔드리스 전용 ScoreRules** — 킬 vs 누수 가중을 메인과 다르게. v1 은 메인 재사용.
- **플레이어용 "무한 모드" 선택 버튼** (아웃게임 UI). v1 은 dev 토글만.
- **엔드리스 리더보드/기록**, 정규 로테이션 편입 정책.
