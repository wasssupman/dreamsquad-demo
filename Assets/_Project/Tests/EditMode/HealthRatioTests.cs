using NUnit.Framework;
using Wassup.Battle.Units;

namespace Wassup.Tests.EditMode
{
    // unit-health-display unit 0 계약: hpRatio = Health.ComputeRatio(newHp, max).
    // DamageNumberEvent.hpRatio + BattleBridge 틴트 poll 의 단일 정의 회귀 가드.
    public class HealthRatioTests
    {
        [Test]
        public void FullHealth_ReturnsOne()
            => Assert.That(Health.ComputeRatio(100f, 100f), Is.EqualTo(1f).Within(1e-5f));

        [Test]
        public void HalfHealth_ReturnsHalf()
            => Assert.That(Health.ComputeRatio(50f, 100f), Is.EqualTo(0.5f).Within(1e-5f));

        [Test]
        public void KillingBlow_ReturnsZero()
            => Assert.That(Health.ComputeRatio(0f, 100f), Is.EqualTo(0f).Within(1e-5f));

        [Test]
        public void NegativeValue_ClampsToZero()
            => Assert.That(Health.ComputeRatio(-30f, 100f), Is.EqualTo(0f).Within(1e-5f));

        [Test]
        public void OverMax_ClampsToOne()
            => Assert.That(Health.ComputeRatio(150f, 100f), Is.EqualTo(1f).Within(1e-5f));

        [Test]
        public void ZeroMax_ReturnsZero()
            => Assert.That(Health.ComputeRatio(10f, 0f), Is.EqualTo(0f).Within(1e-5f));

        [Test]
        public void NegativeMax_ReturnsZero()
            => Assert.That(Health.ComputeRatio(10f, -5f), Is.EqualTo(0f).Within(1e-5f));
    }
}
