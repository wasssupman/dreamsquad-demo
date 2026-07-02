using NUnit.Framework;
using Wassup.Battle.Effects;

namespace Wassup.Tests.EditMode
{
    // modifier-stacking-policy Unit 0 — the aggregate combine + clamp invariant.
    // floor/ceil bound multiplicative stacking so distinct-source modifiers can
    // neither decay a stat to ~0 nor run away upward.
    public class ModifierMathTests
    {
        // damageMul-style bounds used across these cases.
        private const float Floor = 0.2f;
        private const float Ceil = 5f;

        [Test]
        public void NoModifiers_ReturnsIdentity()
        {
            Assert.AreEqual(1f, ModifierMath.CombineMul(false, 0f, add: 0f, mul: 1f, Floor, Ceil), 1e-5f);
        }

        [Test]
        public void SingleDebuff_WithinRange_NotClamped()
        {
            // one Debuffer: ×0.6, above the 0.2 floor → passes through.
            Assert.AreEqual(0.6f, ModifierMath.CombineMul(false, 0f, add: 0f, mul: 0.6f, Floor, Ceil), 1e-5f);
        }

        [Test]
        public void StackedDebuff_BelowFloor_ClampedToFloor()
        {
            // five ×0.6 stacks = 0.6^5 ≈ 0.0778 → clamped up to the floor.
            float mul = 0.6f * 0.6f * 0.6f * 0.6f * 0.6f;
            Assert.AreEqual(Floor, ModifierMath.CombineMul(false, 0f, add: 0f, mul: mul, Floor, Ceil), 1e-5f);
        }

        [Test]
        public void StackedBuff_AboveCeil_ClampedToCeil()
        {
            // runaway buff product beyond the ceiling → clamped down.
            Assert.AreEqual(Ceil, ModifierMath.CombineMul(false, 0f, add: 0f, mul: 6f, Floor, Ceil), 1e-5f);
        }

        [Test]
        public void AdditiveModifier_SumsOntoBase()
        {
            // +0.3 additive → 1.3, within range.
            Assert.AreEqual(1.3f, ModifierMath.CombineMul(false, 0f, add: 0.3f, mul: 1f, Floor, Ceil), 1e-5f);
        }

        [Test]
        public void NegativeAdditive_DrivingRawBelowZero_ClampedToFloor()
        {
            // stacked additive debuffs: 1 + (-2) = -1 → floored (never negative).
            Assert.AreEqual(Floor, ModifierMath.CombineMul(false, 0f, add: -2f, mul: 1f, Floor, Ceil), 1e-5f);
        }

        [Test]
        public void ZeroMultiplier_ClampedToFloor()
        {
            // a ×0 modifier would nullify the stat; the floor keeps it above 0.
            Assert.AreEqual(Floor, ModifierMath.CombineMul(false, 0f, add: 0f, mul: 0f, Floor, Ceil), 1e-5f);
        }

        [Test]
        public void MoveBounds_ClampToOwnFloor()
        {
            // move-stat bounds (0.15/3) resolve independently of the damage bounds.
            Assert.AreEqual(0.15f, ModifierMath.CombineMul(false, 0f, add: 0f, mul: 0.03f, 0.15f, 3f), 1e-5f);
            Assert.AreEqual(3f, ModifierMath.CombineMul(false, 0f, add: 0f, mul: 4f, 0.15f, 3f), 1e-5f);
        }

        [Test]
        public void Override_WinsOverAddAndMul_ButStillClamped()
        {
            // override value used instead of (1+add)*mul, and itself clamped.
            Assert.AreEqual(2f, ModifierMath.CombineMul(true, 2f, add: 0.3f, mul: 0.6f, Floor, Ceil), 1e-5f);
            Assert.AreEqual(Ceil, ModifierMath.CombineMul(true, 99f, add: 0f, mul: 1f, Floor, Ceil), 1e-5f);
        }
    }
}
