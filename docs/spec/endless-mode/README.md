# Endless Mode (무한 모드)

> 상태: 착수 예정 (설계 확정 2026-07-24) — 구현 전
> 브레인스토밍: `docs/plans/2026-07-24-endless-mode-design.md`

## 상위 목표

기존 토너먼트 배틀과 별개인 **스코어어택 무한 모드**. 3분 고정 창에서 **당기기(`ForceNextWave`)로
웨이브를 더 욱여넣어 킬 극대화**. 웨이브 최대 30·고정 10초 간격·타이머 180초. 점수 = 킬 + 누수
페널티, 시간축 0.

**검증 질문**: "메인 모드를 건드리지 않고, 덱 플래그 하나(`battleMode`)로 무한 모드가 기존 배틀
파이프라인을 타는가? 당기기로 킬을 벌고 누수로 깎이는 리스크/리워드가 실제로 성립하는가?"

## 작업 단위

| 파일 | 구분 | 문서 | 목적 |
|---|---|---|---|
| 0 | data/enum | `0_data_contract.md` | `AttackDeck.battleMode` enum + `fixedWaveIntervalSec` + `stressScoreBudget` + ScoreRules Range |
| 1 | 순수/sim | `1_generator_fixed_interval.md` | `WavePatternGenerator` 고정간격 지원 + EditMode 테스트 |
| 2 | bridge | `2_bridge_interval_and_leak_gate.md` | 고정간격 생성 연결 + 누수 무제한/개수 게이트 |
| 3 | bridge | `3_bridge_endless_score.md` | 엔드리스 점수(시간0·스트레스예산) + 토너먼트 리포트 스킵 |
| 4 | authoring | `4_authoring_assets.md` | `Deck_Endless`·`ScoreRules_Endless`·풀 엔트리·씬 배선 |
| 5 | bridge/select | `5_selection_exclusion.md` | 랜덤/토너먼트 선택에서 엔드리스 제외 + DevMapOverride 진입 + 테스트 |
| 6 | test | `6_playmode_smoke.md` | PlayMode 스모크 + 결과 라벨 |
| 7 | handoff | `7_handoff_summary.md` | (구현 종료 후 작성) |

## Feature-wide 계약

1. **모드 seam 은 `AttackDeck.battleMode` enum 하나.** BattleBridge 만 읽어 분기한다. 모드 조건을
   다른 클래스/시스템에 흩지 않는다.
2. **웨이브 스케줄러·트리거타임 계약 불변.** 간격만 데이터(`fixedWaveIntervalSec`, 0=기존 파생)로
   다르게 뽑는다. 당기기(`ForceNextWave`)와 리스케줄 로직을 그대로 재사용한다.
3. **점수는 `ScoreMath.Evaluate` 순수함수 그대로.** 모드 차이 = BattleBridge 가 넘기는 인자
   (`stressLimit`, 어느 `ScoreRules`)뿐. ScoreMath 코드는 바꾸지 않는다. (인터페이스 3분할 기각 —
   차이가 로직이 아니라 값. 제약 8·10.)
4. **패배한계와 스트레스 예산은 분리한다.** `defeatGoalReachedCount`(패배 게이트, `<=0`=무제한) 와
   `stressScoreBudget`(스트레스 점수 예산)은 별개 값. 메인 모드는 `stressScoreBudget=0` 으로 기존처럼
   `defeatGoalReachedCount` 를 예산으로 재사용(동작 불변).
5. **엔드리스는 토너먼트에 리포트하지 않는다.** 랜덤/토너먼트 맵 선택에서도 제외된다. 진입은
   DevMapOverride(추후 전용 버튼) 로만.
6. **ECS 맥락 변경 없음.** 스폰은 기존 `QueueWave→SpawnUnit`(Units) 경로 그대로.
7. **하드코딩 금지.** 모든 값(간격·웨이브수·누수한계·예산·배점)은 `Deck_Endless` /
   `ScoreRules_Endless` SO 에서 나온다.

## 파이프라인 커버리지

**N/A — 신규 플레이 오브젝트 없음, 생성→렌더 경로 불변.** 무한 모드는 적/웨이브를 기존 스폰
파이프라인(`QueueWave→SpawnUnit→Units 맥락→enemyViewPool/spineUnitPool`)으로 **그대로** 낸다.
새 아키타입·정거장·앵커 파일 없음. 변경 지점은 (a) 웨이브 **간격**(데이터), (b) **점수** 인자,
(c) 맵 **선택 정책** 뿐이라 `object-pipeline-map.md` 의 정거장 표를 새로 채울 항목이 없다.

## 후속 후보 (현 범위 밖)

- **플레이어용 "무한 모드" 선택 버튼** (아웃게임 UI). 이번엔 DevMapOverride 로만 진입.
- **랭크/정규 로테이션 편입 정책** — 엔드리스를 정규 매치에 노출할지 (매치메이킹/프로덕트 결정).
- **엔드리스 전용 난이도 곡선** — 현재는 기존 ramp(min→max)·보스 5마다 재사용.
- **엔드리스 리더보드/기록 저장.**
