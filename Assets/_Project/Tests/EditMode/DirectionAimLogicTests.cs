using NUnit.Framework;
using Unity.Mathematics;
using Wassup.UI;

namespace Wassup.Tests.EditMode
{
    // Aim-phase gesture contract (defender-directional-volley unit 5): dead zone,
    // board-axis match, the tie rule, and the release transitions — a dead-zone
    // release must keep the phase open (contract 9), never confirm.
    public class DirectionAimLogicTests
    {
        static readonly float2 Origin = new float2(100f, 100f);
        const float DeadZone = 24f;

        // Battle camera (pitch only, no yaw): board axes land on screen right/up.
        static readonly float2 FlatRight = new float2(1f, 0f);
        static readonly float2 FlatUp = new float2(0f, 1f);

        // Isometric board: both axes project to screen diagonals.
        static readonly float2 IsoRight = math.normalize(new float2(1f, -1f));
        static readonly float2 IsoUp = math.normalize(new float2(1f, 1f));

        [Test]
        public void InsideDeadZone_NoDirection()
        {
            var s = DirectionAimLogic.Evaluate(Origin, Origin + new float2(10f, 10f), DeadZone, FlatRight, FlatUp);
            Assert.IsFalse(s.hasDirection);
        }

        [Test]
        public void AtDeadZoneBoundary_HasDirection()
        {
            var s = DirectionAimLogic.Evaluate(Origin, Origin + new float2(DeadZone, 0f), DeadZone, FlatRight, FlatUp);
            Assert.IsTrue(s.hasDirection);
            Assert.AreEqual(new int2(1, 0), s.cardinal);
        }

        [Test]
        public void FlatBoard_FourCardinals_SnapToDominantAxis()
        {
            Assert.AreEqual(new int2(1, 0), DirectionAimLogic.Evaluate(Origin, Origin + new float2(50f, 10f), DeadZone, FlatRight, FlatUp).cardinal, "right");
            Assert.AreEqual(new int2(-1, 0), DirectionAimLogic.Evaluate(Origin, Origin + new float2(-50f, 10f), DeadZone, FlatRight, FlatUp).cardinal, "left");
            Assert.AreEqual(new int2(0, 1), DirectionAimLogic.Evaluate(Origin, Origin + new float2(10f, 50f), DeadZone, FlatRight, FlatUp).cardinal, "up");
            Assert.AreEqual(new int2(0, -1), DirectionAimLogic.Evaluate(Origin, Origin + new float2(10f, -50f), DeadZone, FlatRight, FlatUp).cardinal, "down");
        }

        [Test]
        public void DiagonalTie_ResolvesToBoardX()
        {
            var s = DirectionAimLogic.Evaluate(Origin, Origin + new float2(40f, 40f), DeadZone, FlatRight, FlatUp);
            Assert.AreEqual(new int2(1, 0), s.cardinal);
            var s2 = DirectionAimLogic.Evaluate(Origin, Origin + new float2(-40f, 40f), DeadZone, FlatRight, FlatUp);
            Assert.AreEqual(new int2(-1, 0), s2.cardinal);
        }

        [Test]
        public void IsoBoard_SwipeAlongProjectedAxis_PicksThatLane()
        {
            // On an iso board the lanes run diagonally on screen, so a swipe must be
            // matched against the projected axes. A screen-cardinal snap would call
            // all four of these "up/right/down/left" and pick the wrong lane.
            var upRight = DirectionAimLogic.Evaluate(Origin, Origin + new float2(50f, 50f), DeadZone, IsoRight, IsoUp);
            Assert.AreEqual(new int2(0, 1), upRight.cardinal, "along projected +Y");

            var downRight = DirectionAimLogic.Evaluate(Origin, Origin + new float2(50f, -50f), DeadZone, IsoRight, IsoUp);
            Assert.AreEqual(new int2(1, 0), downRight.cardinal, "along projected +X");

            var downLeft = DirectionAimLogic.Evaluate(Origin, Origin + new float2(-50f, -50f), DeadZone, IsoRight, IsoUp);
            Assert.AreEqual(new int2(0, -1), downLeft.cardinal, "along projected -Y");

            var upLeft = DirectionAimLogic.Evaluate(Origin, Origin + new float2(-50f, 50f), DeadZone, IsoRight, IsoUp);
            Assert.AreEqual(new int2(-1, 0), upLeft.cardinal, "along projected -X");
        }

        [Test]
        public void IsoBoard_SwipeBetweenTwoLanes_ResolvesByTieRule()
        {
            // Straight up the screen sits exactly between +Y and -X here — there is no
            // "correct" lane, only a deterministic one. Pinned so the choice can't drift.
            var up = DirectionAimLogic.Evaluate(Origin, Origin + new float2(0f, 50f), DeadZone, IsoRight, IsoUp);
            Assert.AreEqual(new int2(-1, 0), up.cardinal, "tie → board X axis");
        }

        [Test]
        public void Release_WithDirection_Confirms()
        {
            var s = DirectionAimLogic.Evaluate(Origin, Origin + new float2(50f, 0f), DeadZone, FlatRight, FlatUp);
            var r = DirectionAimLogic.OnRelease(s);
            Assert.IsTrue(r.confirmed);
            Assert.AreEqual(new int2(1, 0), r.cardinal);
        }

        [Test]
        public void Release_InDeadZone_DoesNotConfirm()
        {
            var s = DirectionAimLogic.Evaluate(Origin, Origin + new float2(5f, 5f), DeadZone, FlatRight, FlatUp);
            Assert.IsFalse(DirectionAimLogic.OnRelease(s).confirmed, "phase stays open for another swipe");
        }
    }
}
