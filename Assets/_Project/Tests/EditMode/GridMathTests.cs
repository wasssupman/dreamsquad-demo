using NUnit.Framework;
using Unity.Mathematics;
using Wassup.Battle.Movement;

namespace Wassup.Tests.EditMode
{
    public class GridMathTests
    {
        [Test]
        public void WorldToCell_Origin_ReturnsZero()
        {
            var cell = GridMath.WorldToCell(new float3(0, 0, 0), tileSize: 1f, gridSize: new int2(20, 10));
            Assert.AreEqual(new int2(0, 0), cell);
        }

        [Test]
        public void WorldToCell_ExactCellCenter_ReturnsCell()
        {
            var cell = GridMath.WorldToCell(new float3(5, 0, 3), tileSize: 1f, gridSize: new int2(20, 10));
            Assert.AreEqual(new int2(5, 3), cell);
        }

        [Test]
        public void WorldToCell_Rounds_NotFloors()
        {
            // 0.6 should round to 1, not floor to 0
            var cell = GridMath.WorldToCell(new float3(0.6f, 0, 0.4f), tileSize: 1f, gridSize: new int2(20, 10));
            Assert.AreEqual(new int2(1, 0), cell);
        }

        [Test]
        public void WorldToCell_OutOfBounds_ClampsToEdge()
        {
            var cellHigh = GridMath.WorldToCell(new float3(100, 0, 100), tileSize: 1f, gridSize: new int2(20, 10));
            Assert.AreEqual(new int2(19, 9), cellHigh);

            var cellLow = GridMath.WorldToCell(new float3(-10, 0, -10), tileSize: 1f, gridSize: new int2(20, 10));
            Assert.AreEqual(new int2(0, 0), cellLow);
        }

        [Test]
        public void WorldToCell_DifferentTileSize_Scales()
        {
            var cell = GridMath.WorldToCell(new float3(10, 0, 5), tileSize: 2f, gridSize: new int2(20, 10));
            Assert.AreEqual(new int2(5, 3), cell);
        }

        [Test]
        public void CellToWorldCenter_MatchesWorldToCellInverse()
        {
            var world = GridMath.CellToWorldCenter(new int2(7, 4), tileSize: 1f);
            Assert.AreEqual(7f, world.x);
            Assert.AreEqual(0f, world.y);
            Assert.AreEqual(4f, world.z);
        }
    }
}
