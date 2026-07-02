using NUnit.Framework;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // unit-stat-projection Unit 1 — pins the exactly-1 projection invariant.
    public class AttackOutputStatsTests
    {
        private static AttackOutput Damage(float magnitude) =>
            new AttackOutput { kind = AttackOutputKind.Damage, magnitude = magnitude };

        private static AttackOutput Heal(float magnitude) =>
            new AttackOutput { kind = AttackOutputKind.Heal, magnitude = magnitude };

        private static AttackOutput Stack() =>
            new AttackOutput { kind = AttackOutputKind.ApplyStack, magnitude = 1f };

        [Test]
        public void TryGet_NullArray_ReturnsFalse()
        {
            Assert.IsFalse(AttackOutputStats.TryGetUniqueMagnitude(null, AttackOutputKind.Damage, out _));
        }

        [Test]
        public void TryGet_EmptyArray_ReturnsFalse()
        {
            Assert.IsFalse(AttackOutputStats.TryGetUniqueMagnitude(new AttackOutput[0], AttackOutputKind.Damage, out _));
        }

        [Test]
        public void TryGet_SingleDamage_ReturnsMagnitude()
        {
            var outputs = new[] { Damage(15f) };

            Assert.IsTrue(AttackOutputStats.TryGetUniqueMagnitude(outputs, AttackOutputKind.Damage, out float m));
            Assert.AreEqual(15f, m);
        }

        [Test]
        public void TryGet_TwoDamageEntries_ReturnsFalse()
        {
            var outputs = new[] { Damage(10f), Damage(5f) };

            Assert.IsFalse(AttackOutputStats.TryGetUniqueMagnitude(outputs, AttackOutputKind.Damage, out _));
        }

        [Test]
        public void TryGet_IgnoresOtherKinds()
        {
            // Debuffer-shaped roster case: Damage 1 + non-damage entries must not
            // make the Damage lookup ambiguous, and each kind resolves separately.
            var outputs = new[] { Damage(12f), Stack(), Heal(8f) };

            Assert.IsTrue(AttackOutputStats.TryGetUniqueMagnitude(outputs, AttackOutputKind.Damage, out float dmg));
            Assert.AreEqual(12f, dmg);
            Assert.IsTrue(AttackOutputStats.TryGetUniqueMagnitude(outputs, AttackOutputKind.Heal, out float heal));
            Assert.AreEqual(8f, heal);
        }

        [Test]
        public void TrySet_SingleDamage_UpdatesMagnitudeOnly()
        {
            var outputs = new[] { Damage(15f), Stack() };

            Assert.IsTrue(AttackOutputStats.TrySetUniqueMagnitude(outputs, AttackOutputKind.Damage, 30f));
            Assert.AreEqual(30f, outputs[0].magnitude);
            Assert.AreEqual(AttackOutputKind.Damage, outputs[0].kind, "kind must never change");
            Assert.AreEqual(1f, outputs[1].magnitude, "other entries must stay untouched");
        }

        [Test]
        public void TrySet_ZeroMatches_ReturnsFalseAndMutatesNothing()
        {
            var outputs = new[] { Stack() };

            Assert.IsFalse(AttackOutputStats.TrySetUniqueMagnitude(outputs, AttackOutputKind.Damage, 30f));
            Assert.AreEqual(1f, outputs[0].magnitude);
        }

        [Test]
        public void TrySet_TwoMatches_ReturnsFalseAndMutatesNothing()
        {
            var outputs = new[] { Damage(10f), Damage(5f) };

            Assert.IsFalse(AttackOutputStats.TrySetUniqueMagnitude(outputs, AttackOutputKind.Damage, 30f));
            Assert.AreEqual(10f, outputs[0].magnitude);
            Assert.AreEqual(5f, outputs[1].magnitude);
        }
    }
}
