using NUnit.Framework;
using UnityEngine;
using Wassup.Core.Session;
using Wassup.Sim.Match;

namespace Wassup.Tests.EditMode
{
    // battle-sim-extraction unit 14 — 승패·점수·유출 규칙.
    //
    // 리뷰 Test Gap 1: 이 타입은 씬 의존이 0 인 plain 객체인데도 테스트가 하나도 없었다 — 적출로
    // 가장 싸게 얻을 수 있었던 회귀 방지를 놓친 자리다. 골든은 이 규칙의 **일부만** 덮는다:
    // 코퍼스는 단일 덱·단일 맵이라 `endless` 축과 몽마의 계약 지불을 전혀 거치지 않는다.
    public class MatchOutcomeRulesTests
    {
        private const int DefeatLimit = 3;
        private const float Timer = 100f;

        private MatchOutcomeRules _rules;

        [SetUp]
        public void SetUp()
        {
            _rules = new MatchOutcomeRules();
            _rules.Configure(hasDeck: true, defeatGoalReachedCount: DefeatLimit, endless: false,
                timeScorePerSecond: 100, stressScorePerPoint: 900);
            _rules.SetTimerDurationSec(Timer);
            _rules.ResetMatch();
        }

        // ── 유출 → 패배 ────────────────────────────────────────────────────

        [Test]
        public void RegisterGoalReached_TriggersDefeat_ExactlyAtTheLimit()
        {
            Assert.AreEqual(MatchOutcome.None, _rules.RegisterGoalReached(out int l1));
            Assert.AreEqual(DefeatLimit, l1, "유효 한계는 선불 차감이 없으면 덱 값");
            Assert.AreEqual(MatchOutcome.None, _rules.RegisterGoalReached(out _));
            Assert.AreEqual(MatchOutcome.Defeat, _rules.RegisterGoalReached(out _),
                "한계와 같아지는 순간 패배다(초과가 아니라 도달)");
        }

        // 래치는 1회성이다 — 마감 뒤 유출이 더 들어와도 두 번 마감하지 않는다.
        [Test]
        public void RegisterGoalReached_LatchesOnce()
        {
            for (int i = 0; i < DefeatLimit; i++) _rules.RegisterGoalReached(out _);
            Assert.IsTrue(_rules.ResultShown);

            Assert.AreEqual(MatchOutcome.None, _rules.RegisterGoalReached(out _),
                "이미 마감된 판은 다시 마감되지 않는다");
            Assert.AreEqual(DefeatLimit + 1, _rules.GoalReachedCount,
                "그래도 누적은 계속 오른다(스트레스 점수 입력)");
        }

        // endless-mode 계약 4 — 무한 모드는 누수로 죽지 않지만 누적은 쌓인다.
        [Test]
        public void Endless_NeverLosesToLeaks_ButStillAccrues()
        {
            _rules.Configure(hasDeck: true, defeatGoalReachedCount: DefeatLimit, endless: true,
                timeScorePerSecond: 100, stressScorePerPoint: 900);

            for (int i = 0; i < DefeatLimit + 5; i++)
                Assert.AreEqual(MatchOutcome.None, _rules.RegisterGoalReached(out _));

            Assert.IsFalse(_rules.ResultShown);
            Assert.AreEqual(DefeatLimit + 5, _rules.GoalReachedCount);
            Assert.IsTrue(_rules.IsEndless, "endless 의 단일 진실이 여기다(리뷰 M1)");
        }

        // 덱 미배선이면 유효 한계는 **선불 차감과 무관하게** 0 이다(적출 전 계약 보존).
        [Test]
        public void NoDeck_EffectiveLeakLimitIsZero_RegardlessOfPenalty()
        {
            _rules.Configure(hasDeck: false, defeatGoalReachedCount: 0, endless: false,
                timeScorePerSecond: 100, stressScorePerPoint: 900);
            Assert.AreEqual(0, _rules.EffectiveLeakLimit);
            Assert.AreEqual(0, _rules.StressLimit);
        }

        // ── 몽마의 계약 (비가역 선불) ──────────────────────────────────────

        [Test]
        public void TryPayLeakAllowance_RefusesPaymentThatWouldLeaveNoRoom()
        {
            // 잔여 3 → cost 2 는 잔여 1 을 남기므로 통과, cost 3 은 0 을 남기므로 거절.
            Assert.IsFalse(_rules.TryPayLeakAllowance(DefeatLimit),
                "지불로 즉시 패배 상태가 되는 것은 구조적으로 금지된다");
            Assert.AreEqual(0, _rules.LeakAllowancePenalty, "거절은 아무것도 차감하지 않는다");

            Assert.IsTrue(_rules.TryPayLeakAllowance(DefeatLimit - 1));
            Assert.AreEqual(DefeatLimit - 1, _rules.LeakAllowancePenalty);
            Assert.AreEqual(1, _rules.RemainingLeakAllowance);
        }

        [Test]
        public void TryPayLeakAllowance_RejectsNonPositive()
        {
            Assert.IsFalse(_rules.TryPayLeakAllowance(0));
            Assert.IsFalse(_rules.TryPayLeakAllowance(-1));
            Assert.AreEqual(0, _rules.LeakAllowancePenalty);
        }

        // 계약 8 — 스트레스 한계는 덱 **원본값**이고 누적 쪽에 차감분이 들어간다.
        [Test]
        public void Penalty_MovesIntoAccrual_NotIntoTheStressLimit()
        {
            _rules.TryPayLeakAllowance(1);
            Assert.AreEqual(DefeatLimit, _rules.StressLimit, "한계는 원본값 유지");
            Assert.AreEqual(1, _rules.StressAccrued, "차감분은 누적으로");
            Assert.AreEqual(DefeatLimit - 1, _rules.EffectiveLeakLimit, "패배 비교용 한계만 줄어든다");
        }

        // ── 타이머 vs 전멸, 그리고 래치 우선순위 ───────────────────────────

        [Test]
        public void CheckTimer_IsVictoryTimeout_OnlyAtOrPastDuration()
        {
            Assert.AreEqual(MatchOutcome.None, _rules.CheckTimer(Timer - 0.01f));
            Assert.AreEqual(MatchOutcome.VictoryTimeout, _rules.CheckTimer(Timer));
        }

        [Test]
        public void CheckTimer_IsInertWhenDurationIsZero()
        {
            _rules.SetTimerDurationSec(0f);
            Assert.AreEqual(MatchOutcome.None, _rules.CheckTimer(9999f),
                "제한시간 0 = 무제한(엔들리스 작성 플랜)");
        }

        // 같은 프레임에 둘이 성립해도 먼저 래치한 쪽만 이긴다.
        [Test]
        public void FirstLatchWins_TimerThenVictory()
        {
            Assert.AreEqual(MatchOutcome.VictoryTimeout, _rules.CheckTimer(Timer));
            Assert.AreEqual(MatchOutcome.None,
                _rules.CheckVictory(allWavesQueued: true, noAttackersRemain: true),
                "이미 마감된 판은 전멸 승리로 다시 마감되지 않는다");
        }

        [Test]
        public void CheckVictory_RequiresBothWavesQueuedAndFieldEmpty()
        {
            Assert.AreEqual(MatchOutcome.None,
                _rules.CheckVictory(allWavesQueued: false, noAttackersRemain: true));
            Assert.AreEqual(MatchOutcome.None,
                _rules.CheckVictory(allWavesQueued: true, noAttackersRemain: false));
            Assert.AreEqual(MatchOutcome.Victory,
                _rules.CheckVictory(allWavesQueued: true, noAttackersRemain: true));
        }

        // ── 계약 9 — 킬점수는 전투 시계와 짝이다 ──────────────────────────

        [Test]
        public void ResetKillScore_ClearsOnlyKillScore()
        {
            _rules.AddKillScore(500);
            _rules.RegisterGoalReached(out _);
            _rules.TryPayLeakAllowance(1);

            _rules.ResetKillScore();

            Assert.AreEqual(0, _rules.KillScoreTotal, "시계와 짝인 값만 0");
            Assert.AreEqual(1, _rules.GoalReachedCount, "유출 누적은 유지(매치 경계가 아니다)");
            Assert.AreEqual(1, _rules.LeakAllowancePenalty, "선불 차감도 유지");
        }

        [Test]
        public void ResetMatch_ClearsEverythingIncludingTheLatch()
        {
            _rules.AddKillScore(500);
            for (int i = 0; i < DefeatLimit; i++) _rules.RegisterGoalReached(out _);
            Assert.IsTrue(_rules.ResultShown);

            _rules.ResetMatch();

            Assert.AreEqual(0, _rules.KillScoreTotal);
            Assert.AreEqual(0, _rules.GoalReachedCount);
            Assert.AreEqual(0, _rules.LeakAllowancePenalty, "선불 차감은 매치 경계에서 소멸(이월 금지)");
            Assert.IsFalse(_rules.ResultShown);
        }

        // ── 남은 시간 · 점수 ──────────────────────────────────────────────

        [Test]
        public void RemainingBattleSeconds_ClampsAtZero()
        {
            Assert.AreEqual(40f, _rules.RemainingBattleSeconds(60f), 0.0001f);
            Assert.AreEqual(0f, _rules.RemainingBattleSeconds(Timer), 0.0001f);
            Assert.AreEqual(0f, _rules.RemainingBattleSeconds(Timer + 5f), 0.0001f,
                "초과분은 음수로 새지 않는다 — 버팀 승리가 0 을 받는 근거");
        }

        /// <summary>
        /// 리뷰 Test Gap 4 — `ConcludeMatch` 가 버팀 승리의 리터럴 `0f` 를 이 함수로 대체했다.
        /// 그 등가성은 "`CheckTimer` 성립 시각에서 이 함수가 정확히 0" 에 의존하고, 그 0 은
        /// **클램프가 보장**한다(고정 dt 누적으로 `duration − clock` 이 미세 음수일 수 있다).
        /// 그 의존을 여기에 고정한다.
        /// </summary>
        [Test]
        public void AtTimerExpiry_RemainingIsExactlyZero_EvenWithAccumulatedDt()
        {
            var rules = new MatchOutcomeRules();
            rules.Configure(hasDeck: true, defeatGoalReachedCount: DefeatLimit, endless: false,
                timeScorePerSecond: 100, stressScorePerPoint: 900);
            rules.SetTimerDurationSec(Timer);

            // 0.05f 를 2000회 누적하면 부동소수 오차로 Timer 를 미세하게 넘거나 못 미친다.
            float clock = 0f;
            for (int i = 0; i < 2000; i++) clock += 0.05f;

            Assert.AreEqual(MatchOutcome.VictoryTimeout, rules.CheckTimer(clock),
                "누적 오차가 있어도 제한시간에 도달하면 마감된다");
            Assert.AreEqual(0f, rules.RemainingBattleSeconds(clock), 0f,
                "마감 시각의 남은 시간은 정확히 0 이어야 한다(리터럴 0f 제거의 근거)");
        }

        // 반올림 규칙이 `Mathf.RoundToInt`(짝수 반올림)와 같아야 골든이 유지된다.
        [Test]
        public void CalculateScore_RoundsTheSameWayAsMathfRoundToInt()
        {
            const float remainingSec = 40.0005f; // ×1000 → 40000.5 → 짝수 반올림 대상
            var rules = new MatchOutcomeRules();
            rules.Configure(hasDeck: true, defeatGoalReachedCount: DefeatLimit, endless: false,
                timeScorePerSecond: 1, stressScorePerPoint: 0);
            rules.SetTimerDurationSec(remainingSec);
            rules.ResetMatch();

            int expectedMs = Mathf.RoundToInt(remainingSec * 1000f);
            // 시간점수 = 남은ms × 초당점수 / 1000 이고 초당점수 1 이므로 ms/1000 이 그대로 나온다.
            Assert.AreEqual(expectedMs / 1000, rules.CalculateScore(defeated: false, clockSec: 0f).Time,
                "반올림 규칙이 Mathf.RoundToInt 와 갈리면 골든이 흔들린다");
        }

        // endless 는 시간축 0 — 조기 클리어로 남은 시간이 있어도 시간점수가 새지 않는다.
        [Test]
        public void Endless_ZeroesTheTimeAxis()
        {
            _rules.Configure(hasDeck: true, defeatGoalReachedCount: DefeatLimit, endless: true,
                timeScorePerSecond: 100, stressScorePerPoint: 900);
            Assert.AreEqual(0, _rules.CalculateScore(defeated: false, clockSec: 0f).Time,
                "남은 시간이 100초여도 엔들리스는 시간점수 0");
        }

        // 버팀 승리는 패배가 아니다 — defeated:true 를 넘기면 스트레스점수까지 죽는다.
        [Test]
        public void Defeat_ZeroesTimeScore_ButButtressVictoryKeepsStress()
        {
            _rules.RegisterGoalReached(out _); // 누적 1 → 스트레스 여유 남음

            var lost = _rules.CalculateScore(defeated: true, clockSec: 0f);
            var survived = _rules.CalculateScore(defeated: false, clockSec: Timer);

            Assert.AreEqual(0, lost.Time, "패배는 시간점수 0");
            Assert.AreEqual(0, survived.Time, "버팀 승리도 남은 시간이 0 이라 시간점수 0");
            Assert.Greater(survived.Stress, 0, "그러나 스트레스점수는 살아 있다");
        }
    }
}
