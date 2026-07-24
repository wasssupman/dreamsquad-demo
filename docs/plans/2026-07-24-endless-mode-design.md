# Endless Mode — Design (thin)

> 브레인스토밍 결과물. 구현 상세는 `docs/spec/endless-mode/` 에 있다. 이 문서는 목표·아키텍처 요약·포인터만.

## 목표

기존 토너먼트 배틀(메인 모드)과 별개인 **스코어어택 "무한 모드"**. 3분 고정 창 안에서
**당기기(`ForceNextWave`)로 웨이브를 더 욱여넣어 킬을 극대화**하는 리스크/리워드 모드.

- 웨이브: 최대 30, **고정 10초 간격**(웨이브수 의존 아님), 타이머 180초.
- 당기기 없으면 ~18웨이브만 스폰. 공격적으로 당길수록 적 폭증 → 킬 ↑ / 누수 위험 ↑.
- 점수 = **킬 + 스트레스(누수 페널티)**, **시간축 0**.

## 아키텍처 요약

**모드 seam = `AttackDeck.battleMode` enum `{ Main, Endless }` 단일.** BattleBridge 가
`StartBattle` 에서 읽어 4가지만 분기:

1. 웨이브 간격 — `fixedWaveIntervalSec`(>0) vs `duration/waveCount`.
2. 누수/패배 — 변수: `defeatGoalReachedCount<=0`=무제한(안 죽음) / >0=개수 도달 시 패배.
3. 점수 — 엔드리스 전용 `ScoreRules`(시간 0) + 스트레스 예산(`stressScoreBudget`)을 패배한계와 분리.
4. 토너먼트 리포트 — 엔드리스는 안 함.

**재활용(신규 코드 0)**: `ForceNextWave`+리스케줄, `SpawnAlertPresenter`, `QueueDueWaves`,
`_killScoreTotal`, 순수함수 `ScoreMath.Evaluate`, 결과 팝업.

**통합**: 엔드리스 = 풀 엔트리 하나(기존 맵 문서 + 신규 `Deck_Endless`). 진입은
**DevMapOverride 로 인덱스 강제**. 랜덤/토너먼트 선택은 엔드리스 엔트리 **제외**.

**ECS 경계**: 변경 없음. 스폰은 기존 `QueueWave→SpawnUnit`(Units) 그대로.

## 결정 로그 (브레인스토밍)

- 웨이브 공급: 고정 10초 × 180초, 최대 30(상한). 당기기가 핵심 스킬 레버 — 확정.
- 패배: 누수 한계를 변수로 (무제한 or 개수). 스트레스 점수 예산은 패배한계와 분리.
- 점수: 시간축 제거(스코어어택). 킬이 주력, 누수는 페널티.
- 진입: A안 — 랜덤/토너먼트에서 제외, DevMapOverride 로 명시 진입. 플레이어 버튼은 후속.
- 스코어 인터페이스 3분할(`ScoreProc/Main/Inf`)은 **기각** — 차이가 로직이 아니라 값이라
  순수함수 `ScoreMath` + enum + 데이터로 충분(제약 8·10). 상세는 spec README 계약 3.

## 포인터

- 구현 스펙: `docs/spec/endless-mode/README.md`
- 밸런싱 참조: `docs/reference/map-wave-balancing.md`
- 점수 산식: `docs/reference/score-formula.md`
