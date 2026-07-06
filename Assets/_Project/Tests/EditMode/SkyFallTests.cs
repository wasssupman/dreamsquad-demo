using NUnit.Framework;
using Wassup.Battle.Combat.Projectile;

namespace Wassup.Tests.EditMode
{
    // Progress/arrival math for the SkyFall trajectory (unit 7 of
    // projectile-trajectory-payload). This drives the Meteor telegraph timing
    // (warningSec → flightTime), so the boundaries are pinned exactly —
    // especially the legacy warningSec=0 "resolve immediately" behavior.
    public class SkyFallTests
    {
        [Test]
        public void Progress_ZeroElapsed_IsZero()
        {
            Assert.AreEqual(0f, SkyFall.Progress(0f, 2f));
        }

        [Test]
        public void Progress_Midpoint_IsHalf()
        {
            Assert.AreEqual(0.5f, SkyFall.Progress(1f, 2f), 1e-5f);
        }

        [Test]
        public void Progress_AtFlightTime_IsOne()
        {
            Assert.AreEqual(1f, SkyFall.Progress(2f, 2f));
        }

        [Test]
        public void Progress_PastFlightTime_ClampsToOne()
        {
            Assert.AreEqual(1f, SkyFall.Progress(5f, 2f));
        }

        [Test]
        public void Progress_ZeroFlightTime_IsOne()
        {
            // Legacy warningSec=0 semantics: resolve on the first tick.
            Assert.AreEqual(1f, SkyFall.Progress(0f, 0f));
        }

        [Test]
        public void Progress_NegativeFlightTime_IsOne()
        {
            Assert.AreEqual(1f, SkyFall.Progress(0f, -1f));
        }

        [Test]
        public void Arrived_BeforeFlightTime_False()
        {
            Assert.IsFalse(SkyFall.Arrived(1.9f, 2f));
        }

        [Test]
        public void Arrived_AtFlightTime_True()
        {
            Assert.IsTrue(SkyFall.Arrived(2f, 2f));
        }

        [Test]
        public void Arrived_PastFlightTime_True()
        {
            Assert.IsTrue(SkyFall.Arrived(2.1f, 2f));
        }

        [Test]
        public void Arrived_ZeroFlightTime_TrueAtZeroElapsed()
        {
            // First MoveSystem tick (elapsed = dt >= 0) must arrive immediately.
            Assert.IsTrue(SkyFall.Arrived(0f, 0f));
        }
    }
}
