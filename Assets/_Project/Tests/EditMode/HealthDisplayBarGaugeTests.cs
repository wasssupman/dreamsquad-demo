using NUnit.Framework;
using UnityEngine;
using Wassup.Data;
using Wassup.Presentation;

namespace Wassup.Tests.EditMode
{
    // unit-health-display unit 2/3 — 마이크로바 fill 색 + 게이지 색/둘레 세그먼트의
    // 순수 계산 회귀 가드 (투트랙 리뷰 test-gap 반영).
    public class HealthDisplayBarGaugeTests
    {
        private static HealthDisplayStyle NewStyle()
            => ScriptableObject.CreateInstance<HealthDisplayStyle>();

        // ── SafeRatio01 (전역 NaN-safe 정규화) ──────────────────────────
        [Test] public void SafeRatio01_Passthrough() => Assert.That(HealthDisplayStyle.SafeRatio01(0.5f), Is.EqualTo(0.5f).Within(1e-5f));
        [Test] public void SafeRatio01_ClampsNegative() => Assert.That(HealthDisplayStyle.SafeRatio01(-2f), Is.EqualTo(0f));
        [Test] public void SafeRatio01_ClampsOver() => Assert.That(HealthDisplayStyle.SafeRatio01(3f), Is.EqualTo(1f));
        [Test] public void SafeRatio01_NaNIsZero() => Assert.That(HealthDisplayStyle.SafeRatio01(float.NaN), Is.EqualTo(0f));

        // ── HitBar fill / Gauge color (기본 램프 녹→황→적) ──────────────
        [Test]
        public void HitBarFill_FullGreenerThanDying()
        {
            var s = NewStyle();
            Assert.That(s.EvaluateHitBarFill(1f).g, Is.GreaterThan(s.EvaluateHitBarFill(0f).g)); // 만피=녹
            Assert.That(s.EvaluateHitBarFill(0f).r, Is.GreaterThan(s.EvaluateHitBarFill(1f).r)); // 빈사=적
        }

        [Test]
        public void HitBarFill_ClampsAndNaN()
        {
            var s = NewStyle();
            Assert.That(s.EvaluateHitBarFill(-1f), Is.EqualTo(s.EvaluateHitBarFill(0f)));
            Assert.That(s.EvaluateHitBarFill(2f), Is.EqualTo(s.EvaluateHitBarFill(1f)));
            Assert.That(s.EvaluateHitBarFill(float.NaN), Is.EqualTo(s.EvaluateHitBarFill(0f)));
        }

        [Test]
        public void GaugeColor_FullGreenerThanDying_AndClamps()
        {
            var s = NewStyle();
            Assert.That(s.EvaluateGaugeColor(1f).g, Is.GreaterThan(s.EvaluateGaugeColor(0f).g));
            Assert.That(s.EvaluateGaugeColor(-1f), Is.EqualTo(s.EvaluateGaugeColor(0f)));
            Assert.That(s.EvaluateGaugeColor(2f), Is.EqualTo(s.EvaluateGaugeColor(1f)));
        }

        // ── 게이지 둘레 시계방향 세그먼트 (edge 0=top,1=right,2=bottom,3=left) ──
        [Test]
        public void EdgeFill_Full_AllEdgesComplete()
        {
            for (int e = 0; e < 4; e++)
                Assert.That(TileHealthGaugeView.EdgeFill(1f, e), Is.EqualTo(1f).Within(1e-5f), "edge " + e);
        }

        [Test]
        public void EdgeFill_Empty_AllEdgesZero()
        {
            for (int e = 0; e < 4; e++)
                Assert.That(TileHealthGaugeView.EdgeFill(0f, e), Is.EqualTo(0f).Within(1e-5f), "edge " + e);
        }

        [Test]
        public void EdgeFill_QuarterBoundaries()
        {
            // r=0.25: top(0) 가득, right(1) 시작(0)
            Assert.That(TileHealthGaugeView.EdgeFill(0.25f, 0), Is.EqualTo(1f).Within(1e-5f));
            Assert.That(TileHealthGaugeView.EdgeFill(0.25f, 1), Is.EqualTo(0f).Within(1e-5f));
            // r=0.5: right(1) 가득, bottom(2) 시작
            Assert.That(TileHealthGaugeView.EdgeFill(0.5f, 1), Is.EqualTo(1f).Within(1e-5f));
            Assert.That(TileHealthGaugeView.EdgeFill(0.5f, 2), Is.EqualTo(0f).Within(1e-5f));
            // r=0.75: bottom(2) 가득, left(3) 시작
            Assert.That(TileHealthGaugeView.EdgeFill(0.75f, 2), Is.EqualTo(1f).Within(1e-5f));
            Assert.That(TileHealthGaugeView.EdgeFill(0.75f, 3), Is.EqualTo(0f).Within(1e-5f));
        }

        [Test]
        public void EdgeFill_PartialWithinFirstSegment()
            => Assert.That(TileHealthGaugeView.EdgeFill(0.1f, 0), Is.EqualTo(0.4f).Within(1e-5f)); // 0.1/0.25
    }
}
