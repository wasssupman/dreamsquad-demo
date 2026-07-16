using NUnit.Framework;
using Unity.Mathematics;
using Wassup.UI;

namespace Wassup.Tests.EditMode
{
    // Aim-phase gesture contract (defender-directional-volley unit 5): dead zone,
    // dominant-axis snap, the horizontal tie rule, and the release transitions —
    // a dead-zone release must keep the phase open (contract 9), never confirm.
    public class DirectionAimLogicTests
    {
        static readonly float2 Origin = new float2(100f, 100f);
        const float DeadZone = 24f;

        [Test]
        public void InsideDeadZone_NoDirection()
        {
            var s = DirectionAimLogic.Evaluate(Origin, Origin + new float2(10f, 10f), DeadZone);
            Assert.IsFalse(s.hasDirection);
        }

        [Test]
        public void AtDeadZoneBoundary_HasDirection()
        {
            var s = DirectionAimLogic.Evaluate(Origin, Origin + new float2(DeadZone, 0f), DeadZone);
            Assert.IsTrue(s.hasDirection);
            Assert.AreEqual(new int2(1, 0), s.cardinal);
        }

        [Test]
        public void FourCardinals_SnapToDominantAxis()
        {
            Assert.AreEqual(new int2(1, 0), DirectionAimLogic.Evaluate(Origin, Origin + new float2(50f, 10f), DeadZone).cardinal, "right");
            Assert.AreEqual(new int2(-1, 0), DirectionAimLogic.Evaluate(Origin, Origin + new float2(-50f, 10f), DeadZone).cardinal, "left");
            Assert.AreEqual(new int2(0, 1), DirectionAimLogic.Evaluate(Origin, Origin + new float2(10f, 50f), DeadZone).cardinal, "up");
            Assert.AreEqual(new int2(0, -1), DirectionAimLogic.Evaluate(Origin, Origin + new float2(10f, -50f), DeadZone).cardinal, "down");
        }

        [Test]
        public void DiagonalTie_ResolvesHorizontal()
        {
            var s = DirectionAimLogic.Evaluate(Origin, Origin + new float2(40f, 40f), DeadZone);
            Assert.AreEqual(new int2(1, 0), s.cardinal);
            var s2 = DirectionAimLogic.Evaluate(Origin, Origin + new float2(-40f, 40f), DeadZone);
            Assert.AreEqual(new int2(-1, 0), s2.cardinal);
        }

        [Test]
        public void Release_WithDirection_Confirms()
        {
            var s = DirectionAimLogic.Evaluate(Origin, Origin + new float2(50f, 0f), DeadZone);
            var r = DirectionAimLogic.OnRelease(s);
            Assert.IsTrue(r.confirmed);
            Assert.AreEqual(new int2(1, 0), r.cardinal);
        }

        [Test]
        public void Release_InDeadZone_DoesNotConfirm()
        {
            var s = DirectionAimLogic.Evaluate(Origin, Origin + new float2(5f, 5f), DeadZone);
            Assert.IsFalse(DirectionAimLogic.OnRelease(s).confirmed, "phase stays open for another swipe");
        }
    }
}
