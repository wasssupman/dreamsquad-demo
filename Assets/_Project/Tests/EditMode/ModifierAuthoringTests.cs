using NUnit.Framework;
using Wassup.Battle.Effects;

namespace Wassup.Tests.EditMode
{
    // modifier-additive-authoring Unit 1 — the increase/reduction classification.
    // Increases (multiplier >= 1) author as an additive delta so they sum; reductions
    // (< 1) stay multiplicative (diminishing returns + stacking-policy floor).
    public class ModifierAuthoringTests
    {
        [Test]
        public void Increase_AuthorsAsAdditiveDelta()
        {
            ModifierAuthoring.FromMultiplier(1.3f, out var op, out var magnitude);
            Assert.AreEqual(CombineOp.Additive, op);
            Assert.AreEqual(0.3f, magnitude, 1e-5f, "×1.3 buff → +0.3 additive delta");
        }

        [Test]
        public void Identity_AuthorsAsAdditiveZero()
        {
            ModifierAuthoring.FromMultiplier(1f, out var op, out var magnitude);
            Assert.AreEqual(CombineOp.Additive, op);
            Assert.AreEqual(0f, magnitude, 1e-5f, "×1.0 → +0.0 (identity)");
        }

        [Test]
        public void Reduction_StaysMultiplicativeWithRawMultiplier()
        {
            ModifierAuthoring.FromMultiplier(0.6f, out var op, out var magnitude);
            Assert.AreEqual(CombineOp.Multiplicative, op);
            Assert.AreEqual(0.6f, magnitude, 1e-5f, "×0.6 debuff stays multiplicative");
        }

        [Test]
        public void ZeroMultiplier_StaysMultiplicative()
        {
            ModifierAuthoring.FromMultiplier(0f, out var op, out var magnitude);
            Assert.AreEqual(CombineOp.Multiplicative, op);
            Assert.AreEqual(0f, magnitude, 1e-5f);
        }
    }
}
