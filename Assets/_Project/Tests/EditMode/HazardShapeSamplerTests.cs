using NUnit.Framework;
using Unity.Mathematics;
using Wassup.Battle.Effects;

namespace Wassup.Tests.EditMode
{
    public class HazardShapeSamplerTests
    {
        [Test]
        public void SingleCell_Returns_Only_Origin()
        {
            var cells = HazardShapeSampler.Sample(HazardShape.SingleCell, new int2(2, 3), 1);

            Assert.AreEqual(1, cells.Count);
            Assert.AreEqual(new int2(2, 3), cells[0]);
        }

        [Test]
        public void Square3x3_Returns_Nine_Cells()
        {
            var cells = HazardShapeSampler.Sample(HazardShape.Square3x3, new int2(2, 3), 1);

            Assert.AreEqual(9, cells.Count);
            Assert.Contains(new int2(1, 2), cells);
            Assert.Contains(new int2(3, 4), cells);
        }

        [Test]
        public void RadiusSquare_Uses_Chebyshev_Radius()
        {
            var cells = HazardShapeSampler.Sample(HazardShape.RadiusSquare, new int2(0, 0), 2);

            Assert.AreEqual(25, cells.Count);
            Assert.Contains(new int2(-2, -2), cells);
            Assert.Contains(new int2(2, 2), cells);
        }
    }
}
