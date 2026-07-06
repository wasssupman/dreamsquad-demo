using NUnit.Framework;
using Unity.Mathematics;
using Wassup.Battle.Combat.Projectile;

namespace Wassup.Tests.EditMode
{
    // Pure trajectory math for the BallisticArc arm (unit 3 of
    // projectile-trajectory-payload). Endpoints, apex, and flight-time derivation
    // are the gameplay-relevant contract (the shell must land ON the locked cell).
    public class BallisticArcTests
    {
        [Test]
        public void ArcPosition_Endpoints_Are_Origin_And_Impact()
        {
            var origin = new float3(1f, 0f, 2f);
            var impact = new float3(5f, 0f, 8f);

            var p0 = BallisticArc.ArcPosition(origin, impact, 3f, 0f);
            var p1 = BallisticArc.ArcPosition(origin, impact, 3f, 1f);

            Assert.AreEqual(origin.x, p0.x, 1e-4f);
            Assert.AreEqual(origin.y, p0.y, 1e-4f);
            Assert.AreEqual(origin.z, p0.z, 1e-4f);
            Assert.AreEqual(impact.x, p1.x, 1e-4f);
            Assert.AreEqual(impact.y, p1.y, 1e-4f, "arc term is 0 at t=1 → lands exactly on the cell");
            Assert.AreEqual(impact.z, p1.z, 1e-4f);
        }

        [Test]
        public void ArcPosition_Apex_At_Half_Adds_ArcHeight()
        {
            var p = BallisticArc.ArcPosition(new float3(0, 0, 0), new float3(4, 0, 0), 2f, 0.5f);
            Assert.AreEqual(2f, p.x, 1e-4f, "XZ midpoint");
            Assert.AreEqual(0f, p.z, 1e-4f);
            Assert.AreEqual(2f, p.y, 1e-4f, "sin(pi/2) * arcHeight = arcHeight");
        }

        [Test]
        public void ArcPosition_Y_Is_Symmetric_About_Half()
        {
            float ya = BallisticArc.ArcPosition(new float3(0, 0, 0), new float3(10, 0, 0), 5f, 0.25f).y;
            float yb = BallisticArc.ArcPosition(new float3(0, 0, 0), new float3(10, 0, 0), 5f, 0.75f).y;
            Assert.AreEqual(ya, yb, 1e-4f);
        }

        [Test]
        public void FlightTime_Is_Horizontal_Distance_Over_Speed()
        {
            // XZ distance 10 (Y ignored), speed 5 → 2s (> minTime 0.3)
            float t = BallisticArc.FlightTime(new float3(0, 0, 0), new float3(10, 99, 0), 5f, 0.3f);
            Assert.AreEqual(2f, t, 1e-4f);
        }

        [Test]
        public void FlightTime_Floors_At_MinTime_For_PointBlank()
        {
            // dist 0.1 / speed 5 = 0.02 < 0.3 → clamped up
            float t = BallisticArc.FlightTime(new float3(0, 0, 0), new float3(0.1f, 0, 0), 5f, 0.3f);
            Assert.AreEqual(0.3f, t, 1e-4f);
        }

        [Test]
        public void FlightTime_Zero_Speed_Falls_Back_To_MinTime()
        {
            float t = BallisticArc.FlightTime(new float3(0, 0, 0), new float3(10, 0, 0), 0f, 0.3f);
            Assert.AreEqual(0.3f, t, 1e-4f);
        }
    }
}
