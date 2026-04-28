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
            // hue 0.95 + shift 0.1 should wrap to ~0.05
            Color.RGBToHSV(new Color(0.9f, 0.85f, 0.8f), out float h, out _, out _);
            // Use a color with known high hue instead: pure red-shifted
            var highHue = Color.HSVToRGB(0.95f, 1f, 1f);
            var shifted = ProjectileViewPool.ApplyHueShift(highHue, 0.1f);
            Color.RGBToHSV(shifted, out float resultH, out _, out _);
            Assert.AreEqual(0.05f, resultH, 0.02f, "Hue should wrap from 0.95+0.1 to ~0.05");
        }

        [Test]
        public void RandomSelect_DeterministicWithSeed()
        {
            var rng1 = new System.Random(42);
            var rng2 = new System.Random(42);
            for (int i = 0; i < 100; i++)
                Assert.AreEqual(rng1.Next(3), rng2.Next(3), $"Mismatch at index {i}");
        }

        [Test]
        public void SequentialSelect_WrapsAroundLength()
        {
            int length = 3;
            int count = 0;
            int[] expected = { 0, 1, 2, 0, 1, 2, 0, 1 };
            for (int i = 0; i < expected.Length; i++)
            {
                int idx = count % length;
                Assert.AreEqual(expected[i], idx, $"Step {i}: expected {expected[i]} got {idx}");
                count++;
            }
        }

        [Test]
        public void ScaleJitter_ZeroProducesIdentity()
        {
            var rng = new System.Random(42);
            float jitter = 0f;
            for (int i = 0; i < 20; i++)
            {
                float scaleMul = 1f + (float)(rng.NextDouble() * 2 - 1) * jitter;
                Assert.AreEqual(1f, scaleMul, 0.00001f, $"scaleMul must be exactly 1 when jitter=0 (step {i})");
            }
        }
    }
}
