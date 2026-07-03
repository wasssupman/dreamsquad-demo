using NUnit.Framework;
using UnityEngine;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    public class HealthDisplayStyleTests
    {
        private static HealthDisplayStyle NewStyle()
            => ScriptableObject.CreateInstance<HealthDisplayStyle>();

        [Test]
        public void EvaluateTint_FullHealth_ReturnsWhite()
        {
            // 만피(1.0)는 무틴트 계약 — 원색 유지. full HP 에서 시각 변화 0 을 보장.
            var style = NewStyle();
            Color c = style.EvaluateTint(1f);
            Assert.That(c.r, Is.EqualTo(1f).Within(0.001f));
            Assert.That(c.g, Is.EqualTo(1f).Within(0.001f));
            Assert.That(c.b, Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void EvaluateTint_LowerHealthIsDarker()
        {
            var style = NewStyle();
            Assert.That(style.EvaluateTint(0f).grayscale,
                Is.LessThan(style.EvaluateTint(1f).grayscale));
        }

        [Test]
        public void EvaluateTint_ClampsBelowZero()
        {
            var style = NewStyle();
            Assert.That(style.EvaluateTint(-0.5f), Is.EqualTo(style.EvaluateTint(0f)));
        }

        [Test]
        public void EvaluateTint_ClampsAboveOne()
        {
            var style = NewStyle();
            Assert.That(style.EvaluateTint(1.5f), Is.EqualTo(style.EvaluateTint(1f)));
        }

        [Test]
        public void EvaluateTint_NaN_TreatedAsZero()
        {
            // max<=0 → value/max = NaN. NaN 은 0(빈사)으로 처리해야 함.
            var style = NewStyle();
            Assert.That(style.EvaluateTint(float.NaN), Is.EqualTo(style.EvaluateTint(0f)));
        }
    }
}
