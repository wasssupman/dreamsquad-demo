using NUnit.Framework;
using Wassup.Core;

namespace Wassup.Tests.EditMode
{
    // endless-mode unit 4 — 무한 모드 점수 산식(순수 ScoreMath)의 리스크/리워드 성립.
    // 시간축 0(무한은 remainingMs=0) + 누수 페널티가 예산까지 선형 감소, 예산 초과 시 0 floor(saturate).
    // 이 두 성질이 "당겨서 킬 벌되 누수는 예산까지 아프다"는 모드 가설의 수학적 코어.
    public class EndlessScoreTests
    {
        private const int PerSec = 100;
        private const int PerStress = 900;

        [Test]
        public void TimeAxisIsZero_WhenRemainingMsZero()
        {
            // 무한 모드는 CalculateBattleScore 에서 remainingMs=0 을 넘긴다(BattleBridge unit 2).
            var s = ScoreMath.Evaluate(remainingMs: 0, stressAccrued: 5, stressLimit: 100,
                killScoreTotal: 1234, defeated: false, PerSec, PerStress);

            Assert.AreEqual(0, s.Time, "remainingMs=0 → 시간점수 0");
            Assert.AreEqual(1234, s.Kill, "킬 점수는 그대로 주력");
            Assert.AreEqual((100 - 5) * PerStress, s.Stress);
        }

        [Test]
        public void LeaksReduceStressByPerStress_UpToBudget()
        {
            const int budget = 100;
            int prev = int.MinValue;
            for (int leaks = 0; leaks <= budget; leaks++)
            {
                var s = ScoreMath.Evaluate(0, leaks, budget, 0, false, PerSec, PerStress);
                Assert.AreEqual((budget - leaks) * PerStress, s.Stress, $"leaks={leaks} 스트레스 점수");
                if (leaks > 0)
                    Assert.AreEqual(prev - PerStress, s.Stress, $"leak {leaks}: 누수 1당 -{PerStress}");
                prev = s.Stress;
            }
        }

        [Test]
        public void StressSaturatesAtZero_BeyondBudget()
        {
            const int budget = 100;
            foreach (int leaks in new[] { 100, 101, 150, 500 })
            {
                var s = ScoreMath.Evaluate(0, leaks, budget, 0, false, PerSec, PerStress);
                Assert.AreEqual(0, s.Stress,
                    $"누수 {leaks}(예산 {budget} 도달/초과)은 스트레스 점수 0 floor — 이 지점부터 페널티 saturate. "
                    + "그래서 무한 모드 예산은 180초 내 도달 불가하게 높게 잡는다(README §누수 예산).");
            }
        }
    }
}
