using NUnit.Framework;
using Unity.Mathematics;
using Wassup.UI;

namespace Wassup.Tests.EditMode
{
    // Aim-phase pick contract (defender-directional-volley unit 5/9): tapping a cell
    // selects the lane it belongs to. The tap target is the whole lane — a one-tile
    // arrow hides under a finger — and anything off the lanes selects nothing, which
    // keeps the phase open (contract 9) rather than confirming something unintended.
    public class DirectionAimLogicTests
    {
        static readonly int2 Center = new int2(5, 5);
        const int Range = 3;

        static int2 Pick(int x, int y) => DirectionAimLogic.Evaluate(Center, new int2(x, y), Range).cardinal;
        static bool Hit(int x, int y) => DirectionAimLogic.Evaluate(Center, new int2(x, y), Range).hasDirection;

        [Test]
        public void ArrowCell_SelectsItsLane()
        {
            // 화살표가 앉는 칸 = 각 레인의 첫 칸.
            Assert.AreEqual(new int2(1, 0), Pick(6, 5), "right");
            Assert.AreEqual(new int2(-1, 0), Pick(4, 5), "left");
            Assert.AreEqual(new int2(0, 1), Pick(5, 6), "up");
            Assert.AreEqual(new int2(0, -1), Pick(5, 4), "down");
        }

        [Test]
        public void AnyCellDownTheLane_SelectsSameLane()
        {
            Assert.AreEqual(new int2(1, 0), Pick(7, 5), "레인 중간");
            Assert.AreEqual(new int2(1, 0), Pick(8, 5), "레인 끝(사거리)");
        }

        [Test]
        public void OwnCell_SelectsNothing()
        {
            Assert.IsFalse(Hit(5, 5));
        }

        [Test]
        public void PastRange_SelectsNothing()
        {
            Assert.IsTrue(Hit(8, 5), "사거리 끝은 유효");
            Assert.IsFalse(Hit(9, 5), "한 칸 더 = 레인 밖");
        }

        [Test]
        public void OffLaneCell_SelectsNothing()
        {
            Assert.IsFalse(Hit(6, 6), "대각");
            Assert.IsFalse(Hit(7, 6), "레인에서 한 칸 옆 — 폭 1타일");
        }

        [Test]
        public void Release_OverLane_Confirms()
        {
            var s = DirectionAimLogic.Evaluate(Center, new int2(7, 5), Range);
            var r = DirectionAimLogic.OnRelease(s);
            Assert.IsTrue(r.confirmed);
            Assert.AreEqual(new int2(1, 0), r.cardinal);
        }

        [Test]
        public void Release_OffLane_DoesNotConfirm()
        {
            var s = DirectionAimLogic.Evaluate(Center, new int2(7, 7), Range);
            Assert.IsFalse(DirectionAimLogic.OnRelease(s).confirmed, "phase stays open for another try");
        }
    }
}
