using Wassup.Core;
using Wassup.Core.Session;

namespace Wassup.Sim.Match
{
    /// <summary>
    /// battle-sim-extraction unit 14 — 판이 언제 끝나고 누가 이겼는지, 그리고 점수·유출을 소유한다.
    ///
    /// 이 규칙이 Bridge 에 남으면 sim lib 이 반쪽이 된다 — 서버권위(M3)도 리플레이 검증도 "끝"의
    /// 정의를 sim 이 몰라 성립하지 않는다. 그래서 여기로 옮겼다.
    ///
    /// **엔진 무참조**: `UnityEngine`/`Entities` 를 참조하지 않는다(`ScoreMath` 선례 — 그 파일은
    /// `using` 지시문이 0개다). ⚠ `MatchSeed` 는 선례가 **아니다**: `GenerateRandom()` 이
    /// `UnityEngine.Random.Range` 를 정규화 참조로 부른다(`Core/MatchSeed.cs:25`). 순수한 것은
    /// `Derive*`·`Mix` 뿐이라, sim 이주 시 그 진입점을 떼는 분할이 선행한다(unit 18).
    /// 입력은 plain 값이고 SO 는 호출자(Bridge)가 풀어서 `Configure` 로 넘긴다. 그래서 이 규칙은
    /// EditMode 에서 씬 없이 테스트되고, unit 17 의 asmdef 격리에 그대로 실린다.
    ///
    /// **부작용 없음**: 로그·HUD·연출은 하나도 하지 않는다. 판정만 돌려주고 그것을 무엇에 쓸지는
    /// 호출자가 정한다.
    /// </summary>
    public sealed class MatchOutcomeRules
    {
        // ── 매치 조건(Configure 로 주입) ──────────────────────────────────
        private bool _hasDeck;
        private int _defeatGoalReachedCount;
        private float _timerDurationSec;
        private bool _endless;
        // 초기값을 두지 않는다(리뷰 반영). 두 `Configure` 호출처가 항상 덮어쓰므로 관측 불가한
        // 죽은 값이면서, `ScoreRulesData` 기본값·Bridge 폴백과 함께 같은 상수의 **3번째 사본**이
        // 됐다. 미설정 상태에서 0점이 나오는 것이 조용히 기본 밸런스로 채점되는 것보다 낫다.
        private int _timeScorePerSecond;
        private int _stressScorePerPoint;

        // ── 매치 누적(경계에서 리셋) ─────────────────────────────────────
        private int _goalReachedCount;
        private int _leakAllowancePenalty;
        private int _killScoreTotal;
        private bool _resultShown;

        /// <summary>
        /// 한 판의 조건을 고정한다(배치 진입 시점 — 유출 HUD 가 그때부터 한계를 그린다).
        /// `hasDeck=false` 는 덱 미배선(테스트/툴 씬)이고, 그때 유효 유출 한계는 **선불 차감과
        /// 무관하게 0** 이다 — 기존 `ActiveDeck != null ? … : 0` 계약.
        /// </summary>
        public void Configure(bool hasDeck, int defeatGoalReachedCount, bool endless,
            int timeScorePerSecond, int stressScorePerPoint)
        {
            _hasDeck = hasDeck;
            _defeatGoalReachedCount = defeatGoalReachedCount;
            _endless = endless;
            _timeScorePerSecond = timeScorePerSecond;
            _stressScorePerPoint = stressScorePerPoint;
        }

        /// <summary>
        /// 제한시간은 `Configure` 와 **따로 들어온다** — 작성 플랜이 자기 타이머를 가질 수 있어
        /// 전투 시작(웨이브 플랜 확정) 시점에야 값이 정해진다.
        /// </summary>
        public void SetTimerDurationSec(float timerDurationSec) => _timerDurationSec = timerDurationSec;

        /// 매치 경계(BeginPlacement) — 누적 전부 소멸. 선불 차감은 이월 금지다.
        public void ResetMatch()
        {
            _goalReachedCount = 0;
            _leakAllowancePenalty = 0;
            _killScoreTotal = 0;
            _resultShown = false;
        }

        /// <summary>
        /// battle-score-formula 계약 9 — 킬점수는 **전투 시계와 짝**이다. teardown 없이
        /// `StartBattle` 이 다시 불리는 경로가 있어 `ResetMatch` 만으로는 이월된다.
        /// </summary>
        public void ResetKillScore() => _killScoreTotal = 0;

        /// LegacyTrace 의 restart 프렐류드가 결과 래치만 되돌린다.
        public void ClearResultLatch() => _resultShown = false;

        public bool ResultShown => _resultShown;
        /// <summary>
        /// 리뷰 반영(M1) — `endless` 의 **단일 진실**. 이 값은 `Configure` 로 굳고, Bridge 가
        /// `ActiveDeck` 을 라이브로 다시 읽으면 덱 교체 축에서 두 판정이 갈린다(유출 패배 억제와
        /// HUD 분모가 서로 다른 답을 내는 형태). 그래서 소비자는 전부 이것을 읽는다.
        /// </summary>
        public bool IsEndless => _endless;
        public int GoalReachedCount => _goalReachedCount;
        public int LeakAllowancePenalty => _leakAllowancePenalty;
        public int KillScoreTotal => _killScoreTotal;
        public float TimerDurationSec => _timerDurationSec;

        /// battle-leak-limit-hud unit 0 — 패배 비교·HUD·저주 지불이 공유하는 유효 한계.
        public int EffectiveLeakLimit => _hasDeck ? _defeatGoalReachedCount - _leakAllowancePenalty : 0;

        /// subconscious-curse-expansion unit 1 (몽마의 계약) — 잔여 유출 허용치.
        public int RemainingLeakAllowance => EffectiveLeakLimit - _goalReachedCount;

        /// <summary>
        /// battle-score-formula unit 7 — 스트레스점수의 입력. 한계는 덱 **원본값**이고
        /// `EffectiveLeakLimit`(차감 후)이 아니다 — 차감분은 누적 쪽에 있다(계약 8).
        /// </summary>
        public int StressAccrued => _goalReachedCount + _leakAllowancePenalty;
        public int StressLimit => _hasDeck ? _defeatGoalReachedCount : 0;

        public void AddKillScore(int killScore) => _killScoreTotal += killScore;

        /// <summary>
        /// 몽마의 계약 선불 지불. 지불 후 잔여가 1 미만이면 거절 — "지불로 즉시 패배" 를 구조적으로
        /// 금지한다. 성공 시 **비가역**: host 사망 revoke 는 hosted 버프만 회수하고 이 오프셋은
        /// 되돌리지 않는다.
        /// </summary>
        public bool TryPayLeakAllowance(int cost)
        {
            if (cost <= 0) return false;
            if (RemainingLeakAllowance - cost < 1) return false;
            _leakAllowancePenalty += cost;
            return true;
        }

        /// <summary>
        /// 적 1기 유출. 누적을 올리고 그것이 패배를 트리거하는지 판정한다.
        /// endless-mode unit 2 — 무한 모드는 누수로 죽지 않는다(계약 4). 카운트/HUD 는 그대로
        /// 누적돼 스트레스 점수에 반영된다.
        /// </summary>
        public MatchOutcome RegisterGoalReached(out int leakLimit)
        {
            _goalReachedCount++;
            leakLimit = EffectiveLeakLimit;
            if (_endless || _resultShown || _goalReachedCount < leakLimit) return MatchOutcome.None;
            _resultShown = true;
            return MatchOutcome.Defeat;
        }

        /// <summary>
        /// 제한시간 소진 = **버팀 승리**. 패배가 아니다 — `defeated:true` 로 점수를 계산하면
        /// 스트레스점수까지 죽는다(남은 시간이 0 이라 시간점수는 이미 자동으로 0 이다).
        /// </summary>
        public MatchOutcome CheckTimer(float clockSec)
        {
            if (_resultShown) return MatchOutcome.None;
            if (_timerDurationSec <= 0f) return MatchOutcome.None;
            if (clockSec < _timerDurationSec) return MatchOutcome.None;
            _resultShown = true;
            return MatchOutcome.VictoryTimeout;
        }

        /// 전멸 승리 = 덱의 모든 스폰이 큐잉되었고 살아 있는 공격 유닛이 없다.
        public MatchOutcome CheckVictory(bool allWavesQueued, bool noAttackersRemain)
        {
            if (_resultShown) return MatchOutcome.None;
            if (!allWavesQueued) return MatchOutcome.None;
            if (!noAttackersRemain) return MatchOutcome.None;
            _resultShown = true;
            return MatchOutcome.Victory;
        }

        /// <summary>
        /// 조회 시점의 남은 초. `_running` 이 내려간 뒤에도 유효하다(결과 팝업 스탬프용).
        /// `Mathf.Max` 대신 명시 클램프 — 이 타입은 UnityEngine 을 참조하지 않는다.
        /// </summary>
        public float RemainingBattleSeconds(float clockSec)
        {
            float left = _timerDurationSec - clockSec;
            return left < 0f ? 0f : left;
        }

        /// <summary>
        /// battle-score-formula unit 3 — 예산 소모 모델. 계산은 `ScoreMath` 순수 함수가 하고
        /// 여기서는 입력을 모아 넘긴다.
        ///
        /// endless-mode unit 2 — 무한 모드는 시간축 0(스코어어택). 조기 클리어로 남은 시간이
        /// 있어도 시간점수가 새지 않게 0 으로 고정한다.
        ///
        /// 반올림은 `Mathf.RoundToInt` 와 **같은 규칙**(짝수 반올림)을 유지해야 골든이 유지된다 —
        /// `Mathf.RoundToInt(f)` 는 `(int)System.Math.Round((double)f)` 다.
        /// </summary>
        public ScoreMath.BattleScore CalculateScore(bool defeated, float clockSec)
        {
            int remainingMs = _endless
                ? 0
                : (int)System.Math.Round((double)(RemainingBattleSeconds(clockSec) * 1000f));
            return ScoreMath.Evaluate(remainingMs, StressAccrued, StressLimit, _killScoreTotal,
                defeated, _timeScorePerSecond, _stressScorePerPoint);
        }
    }
}
