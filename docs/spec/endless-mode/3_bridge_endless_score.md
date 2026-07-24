# 3 — BattleBridge: 엔드리스 점수 + 토너먼트 스킵

## 목적

엔드리스 결과 점수 = **킬 + 스트레스**, 시간축 0. 스트레스 예산을 패배한계와 분리한다.
엔드리스는 토너먼트에 리포트하지 않는다. **`ScoreMath.Evaluate` 순수함수는 건드리지 않는다** —
BattleBridge 가 넘기는 인자만 모드별로 다르게.

## 변경 대상

- `Assets/_Project/Scripts/Bridge/BattleBridge.cs`
  - `endlessScoreRules` SerializeField 추가
  - `CalculateBattleScore` (라인 ~4018)
  - 토너먼트 리포트 경로 `ReportMatchResult` (라인 ~3973)

## 구현

1. **`[SerializeField] private ScoreRulesData endlessScoreRules;`**
   - `IsEndless => ActiveDeck != null && ActiveDeck.battleMode == BattleMode.Endless`.
   - `CalculateBattleScore` 에서 사용할 rules = `IsEndless && endlessScoreRules != null`
     ? `endlessScoreRules` : `scoreRules`. 엔드리스인데 미배선이면 경고 로그 + `scoreRules` 폴백.
2. **`CalculateBattleScore` 인자 분기**:
   - `perSec`/`perStress` = 위에서 고른 rules 에서. 엔드리스 rules 는 `timeScorePerSecond=0`
     → 시간항 자동 0.
   - `stressLimit` = `IsEndless && ActiveDeck.stressScoreBudget > 0`
     ? `ActiveDeck.stressScoreBudget` : `ActiveDeck.defeatGoalReachedCount`.
     (메인은 `stressScoreBudget=0` 이라 기존 `defeatGoalReachedCount` 유지 — 동작 불변.)
   - 나머지(`remainingMs`, `stressAccrued`, `killScoreTotal`, `defeated`)는 그대로.
3. **토너먼트 리포트 스킵**: `ReportMatchResult` 진입부에서 `if (IsEndless) { /* 로그 후 */ return; }`.
   (봇 리더보드/서버 전송 미발생. 결과 팝업 자체는 표시.)

## 완료 기준

- 컴파일 통과.
- 엔드리스 결과: 시간점수=0, 스트레스=`(stressScoreBudget − 누수)×점당` (0 floor), 킬=정상 누적.
- 메인 결과 점수 **완전 불변**(회귀 없음 — 기존 `ScoreMathTests`/tally 테스트 green).
- 엔드리스 종료 시 토너먼트 리포트 미발생(로그로 확인).
