using NUnit.Framework;
using Wassup.Core;

namespace Wassup.Tests.EditMode
{
    // battle-score-formula unit 1 — 최종 점수 산식의 회귀 고정.
    // 승패 규칙이 ScoreMath 안에 있으므로(spec 계약 3) 종료 3종의 배점도 여기서 덮는다.
    public class ScoreMathTests
    {
        // 현행 튜닝값 (ScoreRules.asset). 산식 자체는 이 값을 모른다.
        private const int PerSec = 100;
        private const int PerStress = 900;
        private const int Limit = 10;      // deck.defeatGoalReachedCount (unit 5 적용 후)
        private const int KillFull = 10300; // 고정 시드 65기 전멸 (잡몹 63×100 + 보스 2×2000)

        private static ScoreMath.BattleScore Eval(int remainingMs, int accrued, int kill, bool defeated,
            int limit = Limit)
            => ScoreMath.Evaluate(remainingMs, accrued, limit, kill, defeated, PerSec, PerStress);

        // ── 예산 만점 ──────────────────────────────────────────────────────
        [Test]
        public void FullBudget_MatchesDocumentedTotals()
        {
            var s = Eval(remainingMs: 180_000, accrued: 0, kill: KillFull, defeated: false);
            Assert.AreEqual(18_000, s.Time, "시간 예산 = 180초 × 100");
            Assert.AreEqual(9_000, s.Stress, "스트레스 예산 = 한계 10 × 900");
            Assert.AreEqual(10_300, s.Kill);
            Assert.AreEqual(37_300, s.Total);
        }

        [Test]
        public void Total_IsSumOfThreeAxes()
        {
            var s = Eval(120_400, accrued: 2, kill: 9_400, defeated: false);
            Assert.AreEqual(s.Time + s.Stress + s.Kill, s.Total);
        }

        // ── 종료 3종 ───────────────────────────────────────────────────────
        // 패배: 시간 0(명시 분기) + 스트레스 0(누적 == 한계라 자동) → 킬점수만 남는다.
        [Test]
        public void Defeat_KeepsOnlyKillScore()
        {
            var s = Eval(remainingMs: 120_000, accrued: Limit, kill: 5_600, defeated: true);
            Assert.AreEqual(0, s.Time, "패배는 남은 시간이 있어도 시간점수 0");
            Assert.AreEqual(0, s.Stress);
            Assert.AreEqual(5_600, s.Kill);
            Assert.AreEqual(5_600, s.Total);
        }

        // 빨리 무너지든 늦게 무너지든 패배 점수는 킬점수뿐 — 현행 산식의 부호 역전
        // (오래 끌수록 점수가 오르던 문제) 재발 방지.
        [Test]
        public void Defeat_TimeScoreIsZeroRegardlessOfWhenItHappened()
        {
            var early = Eval(150_000, accrued: Limit, kill: 3_000, defeated: true);
            var late = Eval(5_000, accrued: Limit, kill: 3_000, defeated: true);
            Assert.AreEqual(early.Total, late.Total);
        }

        // 버팀 승리(victory_timeout): 남은 시간이 0 이라 분기 없이 시간점수가 0 이 된다.
        [Test]
        public void TimeoutSurvival_ZeroTimeScore_ButStressAndKillStand()
        {
            var s = Eval(remainingMs: 0, accrued: 3, kill: 7_200, defeated: false);
            Assert.AreEqual(0, s.Time);
            Assert.AreEqual((Limit - 3) * PerStress, s.Stress);
            Assert.AreEqual(7_200, s.Kill);
        }

        // ── 스트레스 불변식 ────────────────────────────────────────────────
        // 비패배 종료면 누적 ≤ 한계 − 1 이 보장되므로 최소 1점분(900)이 남는다.
        [Test]
        public void Victory_AlwaysKeepsAtLeastOneStressPoint()
        {
            var s = Eval(10_000, accrued: Limit - 1, kill: 0, defeated: false);
            Assert.AreEqual(PerStress, s.Stress);
        }

        // 몽마의 계약을 9회 재부착한 뒤 무유출 승리 — 지불이 스트레스축에서 그대로 깎인다.
        [Test]
        public void PactPayments_CountAsStressAccrual()
        {
            var noPact = Eval(10_000, accrued: 0, kill: 0, defeated: false);
            var ninePacts = Eval(10_000, accrued: 9, kill: 0, defeated: false);
            Assert.AreEqual(9_000, noPact.Stress);
            Assert.AreEqual(900, ninePacts.Stress);
            Assert.AreEqual(8_100, noPact.Stress - ninePacts.Stress, "계약 9회 = 8,100점 소각");
        }

        [Test]
        public void StressScore_IsLinear()
        {
            for (int accrued = 0; accrued <= Limit; accrued++)
            {
                var s = Eval(0, accrued, kill: 0, defeated: false);
                Assert.AreEqual((Limit - accrued) * PerStress, s.Stress, $"누적 {accrued}");
            }
        }

        // ── 절삭 ───────────────────────────────────────────────────────────
        [Test]
        public void SubTenMilliseconds_TruncateToZero()
        {
            Assert.AreEqual(0, Eval(9, accrued: 0, kill: 0, defeated: false).Time);
            Assert.AreEqual(1, Eval(10, accrued: 0, kill: 0, defeated: false).Time);
        }

        [Test]
        public void TimeScore_MatchesDirectFormula()
        {
            // 초 경계를 넘는 값에서도 ms × 초당점수 / 1000 과 결과가 같아야 한다
            // (구현이 오버플로 회피를 위해 초/나머지로 쪼개 계산한다).
            foreach (int ms in new[] { 1, 999, 1_000, 1_001, 12_345, 124_000, 179_999 })
            {
                var s = Eval(ms, accrued: 0, kill: 0, defeated: false);
                Assert.AreEqual(ms * PerSec / 1000, s.Time, $"{ms}ms");
            }
        }

        // ── 방어 (정상 경로에서는 도달 불가) ────────────────────────────────
        [Test]
        public void NegativeRemainingTime_ClampsToZero()
        {
            Assert.AreEqual(0, Eval(-5_000, accrued: 0, kill: 0, defeated: false).Time);
        }

        // defeatGoalReachedCount ≤ 0 인 덱 오저작 — 음수 총점이 나오면 안 된다.
        [Test]
        public void MisauthoredZeroLimit_DoesNotProduceNegativeScore()
        {
            var s = Eval(0, accrued: 1, kill: 0, defeated: false, limit: 0);
            Assert.AreEqual(0, s.Stress);
            Assert.AreEqual(0, s.Total);
        }

        [Test]
        public void NegativeKillTotal_ClampsToZero()
        {
            Assert.AreEqual(0, Eval(0, accrued: Limit, kill: -1, defeated: false).Kill);
        }

        // 한계와 점당점수는 곱해서 예산이 된다 — 짝으로 움직여야 한다는 계약의 고정.
        [Test]
        public void LimitAndPerPoint_MultiplyIntoTheSameBudget()
        {
            var tight = ScoreMath.Evaluate(0, 0, 10, 0, false, PerSec, 900);
            var loose = ScoreMath.Evaluate(0, 0, 30, 0, false, PerSec, 300);
            Assert.AreEqual(tight.Stress, loose.Stress, "한계 10×900 == 한계 30×300");
        }
    }
}
