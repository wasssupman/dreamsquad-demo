using NUnit.Framework;
using Unity.Mathematics;
using Wassup.Battle.Combat.Projectile.Emission;

namespace Wassup.Tests.EditMode
{
    public class PatternDirectionTests
    {
        [TestCase(0f, -30f)]
        [TestCase(0.5f, 0f)]
        [TestCase(1f, 30f)]
        [TestCase(-2f, -30f)]
        [TestCase(3f, 30f)]
        public void Resolve_MapsDirectionTWithinMinMax(float directionT, float expectedAngleDeg)
        {
            float2 actual = PatternDirection.Resolve(new float2(1f, 0f), -30f, 30f, directionT);
            float radians = math.radians(expectedAngleDeg);
            float2 expected = new float2(math.cos(radians), math.sin(radians));

            Assert.That(actual.x, Is.EqualTo(expected.x).Within(0.0001f));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(0.0001f));
        }

        [Test]
        public void Resolve_RotatesRelativeToBaseDirection()
        {
            float2 actual = PatternDirection.Resolve(new float2(0f, 1f), -20f, 40f, 1f / 3f);

            Assert.That(actual.x, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(actual.y, Is.EqualTo(1f).Within(0.0001f));
        }
    }
}
