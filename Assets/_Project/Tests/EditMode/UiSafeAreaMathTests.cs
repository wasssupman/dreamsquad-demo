using NUnit.Framework;
using UnityEngine;
using Wassup.UI.Layout;

namespace Wassup.Tests.EditMode
{
    public class UiSafeAreaMathTests
    {
        private const float Tolerance = 0.0001f;

        [Test]
        public void FullScreenRect_ReturnsFullScreenAnchors()
        {
            var anchors = UiSafeAreaMath.ToAnchors(
                new Rect(0f, 0f, 1920f, 1080f),
                new Vector2(1920f, 1080f));

            AssertVector(Vector2.zero, anchors.AnchorMin);
            AssertVector(Vector2.one, anchors.AnchorMax);
        }

        [Test]
        public void LeftCutout_IncreasesMinimumXAnchor()
        {
            var anchors = UiSafeAreaMath.ToAnchors(
                new Rect(120f, 0f, 1800f, 1080f),
                new Vector2(1920f, 1080f));

            AssertVector(new Vector2(0.0625f, 0f), anchors.AnchorMin);
            AssertVector(Vector2.one, anchors.AnchorMax);
        }

        [Test]
        public void RightCutout_DecreasesMaximumXAnchor()
        {
            var anchors = UiSafeAreaMath.ToAnchors(
                new Rect(0f, 0f, 1800f, 1080f),
                new Vector2(1920f, 1080f));

            AssertVector(Vector2.zero, anchors.AnchorMin);
            AssertVector(new Vector2(0.9375f, 1f), anchors.AnchorMax);
        }

        [Test]
        public void BottomGestureInset_IncreasesMinimumYAnchor()
        {
            var anchors = UiSafeAreaMath.ToAnchors(
                new Rect(0f, 60f, 1920f, 1020f),
                new Vector2(1920f, 1080f));

            AssertVector(new Vector2(0f, 60f / 1080f), anchors.AnchorMin);
            AssertVector(Vector2.one, anchors.AnchorMax);
        }

        [Test]
        public void CombinedInsets_NormalizeBothAxes()
        {
            var anchors = UiSafeAreaMath.ToAnchors(
                new Rect(100f, 50f, 1720f, 1000f),
                new Vector2(1920f, 1080f));

            AssertVector(new Vector2(100f / 1920f, 50f / 1080f), anchors.AnchorMin);
            AssertVector(new Vector2(1820f / 1920f, 1050f / 1080f), anchors.AnchorMax);
        }

        [TestCase(0f, 1080f)]
        [TestCase(1920f, 0f)]
        [TestCase(-1f, 1080f)]
        public void InvalidScreenSize_FallsBackToFullScreen(float width, float height)
        {
            var anchors = UiSafeAreaMath.ToAnchors(
                new Rect(100f, 50f, 1720f, 1000f),
                new Vector2(width, height));

            AssertVector(Vector2.zero, anchors.AnchorMin);
            AssertVector(Vector2.one, anchors.AnchorMax);
        }

        [TestCase(0f, 100f)]
        [TestCase(100f, 0f)]
        [TestCase(-1f, 100f)]
        public void InvalidSafeAreaSize_FallsBackToFullScreen(float width, float height)
        {
            var anchors = UiSafeAreaMath.ToAnchors(
                new Rect(0f, 0f, width, height),
                new Vector2(1920f, 1080f));

            AssertVector(Vector2.zero, anchors.AnchorMin);
            AssertVector(Vector2.one, anchors.AnchorMax);
        }

        [Test]
        public void OutOfBoundsSafeArea_ClampsToScreen()
        {
            var anchors = UiSafeAreaMath.ToAnchors(
                new Rect(-100f, -50f, 2200f, 1200f),
                new Vector2(1920f, 1080f));

            AssertVector(Vector2.zero, anchors.AnchorMin);
            AssertVector(Vector2.one, anchors.AnchorMax);
        }

        [Test]
        public void NonFiniteInput_FallsBackWithoutNaN()
        {
            var anchors = UiSafeAreaMath.ToAnchors(
                new Rect(float.NaN, 0f, 1920f, 1080f),
                new Vector2(float.PositiveInfinity, 1080f));

            AssertVector(Vector2.zero, anchors.AnchorMin);
            AssertVector(Vector2.one, anchors.AnchorMax);
        }

        private static void AssertVector(Vector2 expected, Vector2 actual)
        {
            Assert.AreEqual(expected.x, actual.x, Tolerance);
            Assert.AreEqual(expected.y, actual.y, Tolerance);
        }
    }
}
