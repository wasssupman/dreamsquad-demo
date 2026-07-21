using NUnit.Framework;
using Unity.Mathematics;
using Wassup.Battle.Combat;

namespace Wassup.Tests.EditMode
{
    // bomb-thrower-defender unit 0 — 착지 셀 오프셋(4방향×N) + grid 경계 valid 판정
    // 순수 검증. World 불필요(plain int2 산술).
    public class BombLandingTests
    {
        static readonly int2 Grid = new int2(20, 10);

        [Test]
        public void East_OffsetsX_Positive()
        {
            BombLanding.ResolveCell(new int2(5, 5), new int2(1, 0), 3, Grid, out var cell, out var valid);
            Assert.AreEqual(new int2(8, 5), cell);
            Assert.IsTrue(valid);
        }

        [Test]
        public void West_OffsetsX_Negative()
        {
            BombLanding.ResolveCell(new int2(5, 5), new int2(-1, 0), 3, Grid, out var cell, out var valid);
            Assert.AreEqual(new int2(2, 5), cell);
            Assert.IsTrue(valid);
        }

        [Test]
        public void North_OffsetsY_Positive()
        {
            BombLanding.ResolveCell(new int2(5, 5), new int2(0, 1), 2, Grid, out var cell, out var valid);
            Assert.AreEqual(new int2(5, 7), cell);
            Assert.IsTrue(valid);
        }

        [Test]
        public void South_OffsetsY_Negative()
        {
            BombLanding.ResolveCell(new int2(5, 5), new int2(0, -1), 2, Grid, out var cell, out var valid);
            Assert.AreEqual(new int2(5, 3), cell);
            Assert.IsTrue(valid);
        }

        [Test]
        public void OffGrid_EastPastRightEdge_Invalid()
        {
            BombLanding.ResolveCell(new int2(18, 5), new int2(1, 0), 3, Grid, out var cell, out var valid);
            Assert.AreEqual(new int2(21, 5), cell);
            Assert.IsFalse(valid, "x=21 >= gridSize.x=20 → off-grid");
        }

        [Test]
        public void OffGrid_SouthPastBottom_Invalid()
        {
            BombLanding.ResolveCell(new int2(5, 1), new int2(0, -1), 3, Grid, out _, out var valid);
            Assert.IsFalse(valid, "y=-2 < 0 → off-grid");
        }

        [Test]
        public void Edge_LandsExactlyOnLastColumn_Valid()
        {
            BombLanding.ResolveCell(new int2(16, 0), new int2(1, 0), 3, Grid, out var cell, out var valid);
            Assert.AreEqual(new int2(19, 0), cell);
            Assert.IsTrue(valid, "x=19 = gridSize.x-1 → 마지막 열, 유효");
        }

        [Test]
        public void Edge_Origin_ZeroTiles_Valid()
        {
            BombLanding.ResolveCell(new int2(0, 0), new int2(0, 1), 0, Grid, out var cell, out var valid);
            Assert.AreEqual(new int2(0, 0), cell);
            Assert.IsTrue(valid);
        }
    }
}
