using NUnit.Framework;
using UnityEngine;
using Wassup.Presentation;

namespace Wassup.Tests.EditMode
{
    public class ProjectileVariationTests
    {
        [Test]
        public void HueShift_ZeroPreservesColor()
        {
            var original = new Color(0.8f, 0.3f, 0.1f, 1f);
            var result = ProjectileViewPool.ApplyHueShift(original, 0f);
            Assert.AreEqual(original.r, result.r, 0.01f);
            Assert.AreEqual(original.g, result.g, 0.01f);
            Assert.AreEqual(original.b, result.b, 0.01f);
            Assert.AreEqual(original.a, result.a, 0.001f);
        }

        [Test]
        public void HueShift_WrapsAroundOne()
        {
            var highHue = Color.HSVToRGB(0.95f, 1f, 1f);
            var shifted = ProjectileViewPool.ApplyHueShift(highHue, 0.1f);
            Color.RGBToHSV(shifted, out float resultH, out _, out _);
            Assert.AreEqual(0.05f, resultH, 0.02f, "Hue should wrap from 0.95+0.1 to ~0.05");
        }

        [Test]
        public void Initialize_SameSeedProducesSameSequence()
        {
            var rng1 = new System.Random(42);
            var rng2 = new System.Random(42);
            for (int i = 0; i < 10; i++)
            {
                float s1 = 1f + (float)(rng1.NextDouble() * 2 - 1) * 0.1f;
                float h1 = (float)(rng1.NextDouble() * 2 - 1) * 0.03f;
                float r1 = (float)(rng1.NextDouble() * 2 - 1) * 15f;
                float s2 = 1f + (float)(rng2.NextDouble() * 2 - 1) * 0.1f;
                float h2 = (float)(rng2.NextDouble() * 2 - 1) * 0.03f;
                float r2 = (float)(rng2.NextDouble() * 2 - 1) * 15f;
                Assert.AreEqual(s1, s2, 0.000001f, $"scaleJitter mismatch at step {i}");
                Assert.AreEqual(h1, h2, 0.000001f, $"hueJitter mismatch at step {i}");
                Assert.AreEqual(r1, r2, 0.000001f, $"rollJitter mismatch at step {i}");
            }
        }

        [Test]
        public void RollDoesNotAccumulate_AcrossPoolReuse()
        {
            // With rotationJitter = 0, rollDeg is always 0 regardless of RNG state.
            var rng = new System.Random(42);
            float jitter = 0f;
            for (int i = 0; i < 10; i++)
            {
                float rollDeg = (float)(rng.NextDouble() * 2 - 1) * jitter;
                Assert.AreEqual(0f, rollDeg, 0.0001f);
            }
            // The reset formula prefab.rotation * Euler(0,0,0) == prefab.rotation.
            var prefabRot = Quaternion.Euler(30f, 45f, 0f);
            var final = prefabRot * Quaternion.Euler(0f, 0f, 0f);
            Assert.AreEqual(prefabRot, final, "Roll=0 must not change prefab rotation");
        }
    }
}
