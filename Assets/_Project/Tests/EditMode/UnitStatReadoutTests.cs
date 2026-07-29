using NUnit.Framework;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // selection-hand-attach unit 10 — 표시용 스탯 결정 로직.
    //
    // 이 테스트는 **산식의 문서**를 겸한다. sim 쪽 거울은 AttackSystem 의 START(공격속도) ·
    // RESOLVE(공격력) 이고, 표시를 위해 sim 을 리팩터하지 않는 것이 결정이다(spec unit 10 D).
    // 그러므로 sim 산식이 바뀌면 여기 기대값도 함께 봐야 한다.
    public class UnitStatReadoutTests
    {
        private const float Tol = 1e-4f;

        // ── CooldownToRate ────────────────────────────────────────────────────

        [Test]
        public void CooldownToRate_NoModifier_IsInverseOfCooldown()
        {
            // 쿨다운 2초 = 초당 0.5회.
            Assert.AreEqual(0.5f, UnitStatMath.CooldownToRate(2f, 1f), Tol);
        }

        [Test]
        public void CooldownToRate_SpeedMulScalesRateLinearly()
        {
            // 공속 2배 = 간격 절반 = rate 2배. sim: interval = cooldown × (1/speedMul).
            Assert.AreEqual(1f, UnitStatMath.CooldownToRate(2f, 2f), Tol);
            // 공속 0.5배 = 간격 2배 = rate 절반.
            Assert.AreEqual(0.25f, UnitStatMath.CooldownToRate(2f, 0.5f), Tol);
        }

        [Test]
        public void CooldownToRate_ZeroOrNegativeCooldown_ReturnsZero()
        {
            // 0 나눗셈 가드 — 공격 못 하는 유닛(캐스터 등)은 rate 0 으로 표시된다.
            Assert.AreEqual(0f, UnitStatMath.CooldownToRate(0f, 1f), Tol);
            Assert.AreEqual(0f, UnitStatMath.CooldownToRate(-1f, 1f), Tol);
        }

        [Test]
        public void CooldownToRate_NonPositiveSpeedMul_TreatedAsOne()
        {
            // sim 과 같은 처리(effectiveCooldownMul = speedMul > 0 ? 1/speedMul : 1).
            // 여기서만 다르게 방어하면 표시가 sim 과 어긋난다.
            float expected = UnitStatMath.CooldownToRate(2f, 1f);
            Assert.AreEqual(expected, UnitStatMath.CooldownToRate(2f, 0f), Tol);
            Assert.AreEqual(expected, UnitStatMath.CooldownToRate(2f, -3f), Tol);
        }

        // ── ResolveDelta ──────────────────────────────────────────────────────

        [Test]
        public void ResolveDelta_Increase_ReturnsPositiveSignAndMagnitude()
        {
            int sign = UnitStatMath.ResolveDelta(30f, 42f, UnitStatMath.DefaultDeltaEpsilon, out var mag);
            Assert.AreEqual(1, sign);
            Assert.AreEqual(12f, mag, Tol);
        }

        [Test]
        public void ResolveDelta_Decrease_ReturnsNegativeSignAndPositiveMagnitude()
        {
            // magnitude 는 항상 양수 — 부호는 sign 이 나른다(뷰가 ▼ 글리프를 붙인다).
            int sign = UnitStatMath.ResolveDelta(30f, 21f, UnitStatMath.DefaultDeltaEpsilon, out var mag);
            Assert.AreEqual(-1, sign);
            Assert.AreEqual(9f, mag, Tol);
        }

        [Test]
        public void ResolveDelta_WithinEpsilon_IsFlatWithZeroMagnitude()
        {
            // 부동소수 잔차가 ▲0 칩으로 새면 안 된다 — 뷰는 sign 0 에서 칩을 그리지 않는다.
            int sign = UnitStatMath.ResolveDelta(30f, 30.001f, UnitStatMath.DefaultDeltaEpsilon, out var mag);
            Assert.AreEqual(0, sign);
            Assert.AreEqual(0f, mag, Tol);
        }

        [Test]
        public void ResolveDelta_ExactlyEqual_IsFlat()
        {
            int sign = UnitStatMath.ResolveDelta(42f, 42f, UnitStatMath.DefaultDeltaEpsilon, out var mag);
            Assert.AreEqual(0, sign);
            Assert.AreEqual(0f, mag, Tol);
        }

        [Test]
        public void ResolveDelta_JustOverEpsilon_IsNotFlat()
        {
            // 경계 바로 바깥은 잡혀야 한다(epsilon 이 진짜 변화를 삼키지 않는지).
            float eps = UnitStatMath.DefaultDeltaEpsilon;
            int sign = UnitStatMath.ResolveDelta(30f, 30f + eps * 2f, eps, out var mag);
            Assert.AreEqual(1, sign);
            Assert.AreEqual(eps * 2f, mag, Tol);
        }
    }
}
