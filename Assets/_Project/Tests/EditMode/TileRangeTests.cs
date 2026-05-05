using NUnit.Framework;
using Unity.Mathematics;
using Wassup.Battle.Movement;

namespace Wassup.Tests.EditMode
{
    public class TileRangeTests
    {
        [Test] public void Chebyshev_Orthogonal1() => Assert.AreEqual(1, GridMath.ChebyshevDistance(new int2(0, 0), new int2(1, 0)));
        [Test] public void Chebyshev_Diagonal1() => Assert.AreEqual(1, GridMath.ChebyshevDistance(new int2(0, 0), new int2(1, 1)));
        [Test] public void Chebyshev_Orthogonal2() => Assert.AreEqual(2, GridMath.ChebyshevDistance(new int2(0, 0), new int2(2, 0)));
        [Test] public void Chebyshev_Mixed() => Assert.AreEqual(2, GridMath.ChebyshevDistance(new int2(0, 0), new int2(2, 1)));
        [Test] public void Chebyshev_Negative() => Assert.AreEqual(2, GridMath.ChebyshevDistance(new int2(3, 3), new int2(1, 2)));

        [Test] public void RangeToTiles_Half() => Assert.AreEqual(1, GridMath.RangeToTiles(0.5f));
        [Test] public void RangeToTiles_OneExact() => Assert.AreEqual(1, GridMath.RangeToTiles(1.0f));
        [Test] public void RangeToTiles_OneAndHalf() => Assert.AreEqual(2, GridMath.RangeToTiles(1.5f));
        [Test] public void RangeToTiles_FourAndHalf() => Assert.AreEqual(5, GridMath.RangeToTiles(4.5f));
        [Test] public void RangeToTiles_FiveAndHalf() => Assert.AreEqual(6, GridMath.RangeToTiles(5.5f));
        [Test] public void RangeToTiles_ThreeExact() => Assert.AreEqual(3, GridMath.RangeToTiles(3.0f));

        [Test] public void InRange_Diagonal1_Range1() => Assert.IsTrue(GridMath.ChebyshevDistance(new int2(0, 0), new int2(1, 1)) <= 1);
        [Test] public void OutOfRange_Orthogonal2_Range1() => Assert.IsFalse(GridMath.ChebyshevDistance(new int2(0, 0), new int2(2, 0)) <= 1);
        [Test] public void InRange_Diagonal2_Range2() => Assert.IsTrue(GridMath.ChebyshevDistance(new int2(0, 0), new int2(2, 2)) <= 2);
        [Test] public void OutOfRange_Orthogonal3_Range2() => Assert.IsFalse(GridMath.ChebyshevDistance(new int2(0, 0), new int2(3, 0)) <= 2);
    }
}
