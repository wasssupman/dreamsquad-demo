using NUnit.Framework;
using UnityEngine;
using Wassup.Presentation;

namespace Wassup.Tests.EditMode
{
    public class UnitOverheadLayoutTests
    {
        [TestCase(1080f, 1f)]
        [TestCase(2160f, 2f)]
        [TestCase(720f, 0.6666667f)]
        public void ReferenceScale_UsesHeightMatch(float screenHeight, float expected)
            => Assert.That(UnitOverheadLayout.ReferenceScale(screenHeight, 1080f), Is.EqualTo(expected).Within(1e-5f));

        [Test]
        public void BarWidth_UsesFractionAndClamps()
        {
            Assert.That(UnitOverheadLayout.BarWidth(100f, 0.8f, 42f, 78f), Is.EqualTo(78f));
            Assert.That(UnitOverheadLayout.BarWidth(60f, 0.8f, 42f, 78f), Is.EqualTo(48f));
            Assert.That(UnitOverheadLayout.BarWidth(20f, 0.8f, 42f, 78f), Is.EqualTo(42f));
        }

        [Test]
        public void VerticalOffsets_KeepFivePixelContracts()
        {
            Vector2 offsets = UnitOverheadLayout.VerticalOffsets(5f, 8f, 5f);
            Assert.That(offsets.x, Is.EqualTo(5f));
            Assert.That(offsets.y, Is.EqualTo(18f));
            Assert.That(offsets.y - (offsets.x + 8f), Is.EqualTo(5f));
        }

        [Test]
        public void ScreenAnchor_UsesVisualPivotX_NotWeaponBiasedBoundsCenter()
        {
            var weaponBiasedBounds = new Rect(100f, 50f, 160f, 80f);
            Vector2 anchor = UnitOverheadLayout.ScreenAnchor(140f, weaponBiasedBounds);
            Assert.That(anchor, Is.EqualTo(new Vector2(140f, 130f)));
            Assert.That(anchor.x, Is.Not.EqualTo(weaponBiasedBounds.center.x));
        }

        [Test]
        public void InvalidNumbers_DoNotEscapeLayout()
        {
            Assert.That(UnitOverheadLayout.ReferenceScale(float.NaN, 1080f), Is.EqualTo(1f));
            Assert.That(UnitOverheadLayout.BarWidth(float.NaN, 0.8f, 42f, 78f), Is.EqualTo(42f));
            Vector2 size = UnitOverheadLayout.CardSize(float.NaN, float.NaN, 3, float.NaN);
            Assert.That(float.IsNaN(size.x) || float.IsNaN(size.y), Is.False);
            Assert.That(size, Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void ThreeCards_FitWithinTileWidth()
        {
            Vector2 size = UnitOverheadLayout.CardSize(24f, 2f, 3, 44f);
            float row = size.x * 3f + 4f;
            Assert.That(row, Is.LessThanOrEqualTo(44.001f));
            Assert.That(size.y / size.x, Is.EqualTo(1.5f).Within(1e-5f));
        }

        [Test]
        public void CardCount_IsCappedAtThree()
        {
            Vector2 three = UnitOverheadLayout.CardSize(24f, 2f, 3, 80f);
            Vector2 many = UnitOverheadLayout.CardSize(24f, 2f, 9, 80f);
            Assert.That(many, Is.EqualTo(three));
        }

        [Test]
        public void TinyRowWidth_ScalesSpacingAndCardsToFit()
        {
            const float cap = 1f;
            float gap = UnitOverheadLayout.CardSpacing(2f, 3, cap);
            Vector2 size = UnitOverheadLayout.CardSize(24f, gap, 3, cap);
            Assert.That(size.x * 3f + gap * 2f, Is.LessThanOrEqualTo(cap + 1e-5f));
        }
    }
}
