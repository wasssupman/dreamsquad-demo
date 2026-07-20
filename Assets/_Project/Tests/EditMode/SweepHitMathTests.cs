using NUnit.Framework;
using Unity.Mathematics;
using Wassup.Battle.Combat.Projectile;

namespace Wassup.Tests.EditMode
{
    // Segment-sweep hit test for the PathHit payload (defender-directional-volley
    // unit 0). The clamp-to-endpoint and stall-frame cases are where a naive
    // line-distance test would wrongly hit targets behind/ahead of the segment.
    public class SweepHitMathTests
    {
        [Test]
        public void TargetOnSegment_Hits()
        {
            Assert.IsTrue(SweepHitMath.SegmentHits(new float2(0, 0), new float2(2, 0), new float2(1f, 0f), 0.1f));
        }

        [Test]
        public void TargetNearMidpoint_WithinRadius_Hits()
        {
            Assert.IsTrue(SweepHitMath.SegmentHits(new float2(0, 0), new float2(2, 0), new float2(1f, 0.25f), 0.3f));
            Assert.IsFalse(SweepHitMath.SegmentHits(new float2(0, 0), new float2(2, 0), new float2(1f, 0.35f), 0.3f));
        }

        [Test]
        public void TargetPastEndpoint_ClampsToEndpointDistance()
        {
            Assert.IsTrue(SweepHitMath.SegmentHits(new float2(0, 0), new float2(2, 0), new float2(2.2f, 0f), 0.3f), "just past currPos, inside radius");
            Assert.IsFalse(SweepHitMath.SegmentHits(new float2(0, 0), new float2(2, 0), new float2(2.5f, 0f), 0.3f), "past currPos, outside radius — the segment does not extend");
            Assert.IsFalse(SweepHitMath.SegmentHits(new float2(0, 0), new float2(2, 0), new float2(-0.5f, 0f), 0.3f), "behind prevPos");
        }

        [Test]
        public void ZeroLengthSegment_DegradesToPointTest()
        {
            var p = new float2(1f, 1f);
            Assert.IsTrue(SweepHitMath.SegmentHits(p, p, new float2(1f, 1.2f), 0.3f));
            Assert.IsFalse(SweepHitMath.SegmentHits(p, p, new float2(1f, 1.5f), 0.3f));
        }

        [Test]
        public void BoundaryDistance_ExactRadius_Hits()
        {
            Assert.IsTrue(SweepHitMath.SegmentHits(new float2(0, 0), new float2(2, 0), new float2(1f, 0.3f), 0.3f));
        }
    }
}
