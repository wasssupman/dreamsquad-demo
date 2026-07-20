using NUnit.Framework;
using Unity.Mathematics;
using Wassup.Battle.Combat;

namespace Wassup.Tests.EditMode
{
    // Burst timing, spread fan, and volley-aware cooldown (defender-directional-
    // volley unit 0). TickBurst drift/carryover and the multi-interval slow frame
    // are the regression-critical cases — they decide whether a 0.1s machine-gun
    // burst stays 0.1s under real frame times.
    public class VolleyMathTests
    {
        [Test]
        public void TickBurst_FiresWhenTimerElapses_NotBefore()
        {
            int remaining = 3;
            float timer = 0.1f;
            Assert.AreEqual(0, VolleyMath.TickBurst(0.05f, ref remaining, ref timer, 0.1f));
            Assert.AreEqual(3, remaining);
            Assert.AreEqual(1, VolleyMath.TickBurst(0.05f, ref remaining, ref timer, 0.1f), "carryover reaches 0 exactly");
            Assert.AreEqual(2, remaining);
        }

        [Test]
        public void TickBurst_SlowFrame_CoversMultipleIntervals()
        {
            int remaining = 5;
            float timer = 0.1f;
            Assert.AreEqual(3, VolleyMath.TickBurst(0.35f, ref remaining, ref timer, 0.1f));
            Assert.AreEqual(2, remaining);
        }

        [Test]
        public void TickBurst_StopsAtZeroRemaining_AndStaysQuiet()
        {
            int remaining = 2;
            float timer = 0.1f;
            Assert.AreEqual(2, VolleyMath.TickBurst(1f, ref remaining, ref timer, 0.1f), "capped by remaining shots");
            Assert.AreEqual(0, remaining);
            Assert.AreEqual(0, VolleyMath.TickBurst(1f, ref remaining, ref timer, 0.1f), "exhausted burst never fires");
        }

        [Test]
        public void TickBurst_TimerRemainder_CarriesAcrossFrames()
        {
            int remaining = 10;
            float timer = 0.1f;
            int total = 0;
            for (int i = 0; i < 25; i++) total += VolleyMath.TickBurst(0.016f, ref remaining, ref timer, 0.1f);
            Assert.AreEqual(4, total, "25 × 16ms = 400ms → exactly 4 intervals, no drift");
        }

        [Test]
        public void TickBurst_NonPositiveInterval_DumpsRemainder()
        {
            int remaining = 4;
            float timer = 0f;
            Assert.AreEqual(4, VolleyMath.TickBurst(0.016f, ref remaining, ref timer, 0f));
            Assert.AreEqual(0, remaining);
        }

        [Test]
        public void SpreadDirection_SingleShotOrZeroAngle_ReturnsBaseDir()
        {
            var baseDir = new float2(1f, 0f);
            Assert.AreEqual(baseDir, VolleyMath.SpreadDirection(baseDir, 0, 1, 30f));
            Assert.AreEqual(baseDir, VolleyMath.SpreadDirection(baseDir, 1, 3, 0f));
        }

        [Test]
        public void SpreadDirection_ThreeShots_CenterStraight_OuterSymmetric()
        {
            var baseDir = new float2(1f, 0f);
            var d0 = VolleyMath.SpreadDirection(baseDir, 0, 3, 30f);
            var d1 = VolleyMath.SpreadDirection(baseDir, 1, 3, 30f);
            var d2 = VolleyMath.SpreadDirection(baseDir, 2, 3, 30f);

            Assert.AreEqual(1f, d1.x, 1e-4f, "middle shot flies straight");
            Assert.AreEqual(0f, d1.y, 1e-4f);
            Assert.AreEqual(d0.x, d2.x, 1e-4f, "outer shots mirror about the base direction");
            Assert.AreEqual(d0.y, -d2.y, 1e-4f);
            float outerRad = math.acos(math.clamp(math.dot(d0, d2), -1f, 1f));
            Assert.AreEqual(math.radians(30f), outerRad, 1e-4f, "outer pair spans the full spread angle");
        }

        [Test]
        public void SpreadDirection_TwoShots_NoStraightShot_FullSpan()
        {
            var baseDir = new float2(0f, 1f);
            var d0 = VolleyMath.SpreadDirection(baseDir, 0, 2, 30f);
            var d1 = VolleyMath.SpreadDirection(baseDir, 1, 2, 30f);
            float span = math.acos(math.clamp(math.dot(d0, d1), -1f, 1f));
            Assert.AreEqual(math.radians(30f), span, 1e-4f);
            Assert.AreEqual(1f, math.length(d0), 1e-4f, "rotation preserves unit length");
        }

        [Test]
        public void CooldownAfterVolley_AddsBurstDuration()
        {
            Assert.AreEqual(2.9f, VolleyMath.CooldownAfterVolley(2f, 10, 0.1f), 1e-4f);
            Assert.AreEqual(2f, VolleyMath.CooldownAfterVolley(2f, 1, 0.1f), 1e-4f, "single shot = plain cooldown");
        }
    }
}
