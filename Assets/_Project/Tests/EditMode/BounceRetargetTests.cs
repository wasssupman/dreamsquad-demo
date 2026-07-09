using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using Wassup.Battle.Combat.Projectile;

namespace Wassup.Tests.EditMode
{
    // dreamcatcher-attack-mod-bounce unit 1 — pins the pure retarget decision.
    // No World: BounceRetarget.FindNext is geometry over a float3 array.
    public class BounceRetargetTests
    {
        // tileSize 1, big grid, origin 0 → cell = round(coord), no clamping in range.
        private const float TileSize = 1f;
        private static readonly int2 Grid = new int2(128, 128);
        private static readonly float3 Origin = float3.zero;

        private static NativeArray<float3> Make(params float3[] xs)
        {
            var a = new NativeArray<float3>(xs.Length, Allocator.Persistent);
            for (int i = 0; i < xs.Length; i++) a[i] = xs[i];
            return a;
        }

        [Test]
        public void PicksNearest_WithinRange()
        {
            var pos = Make(new float3(1, 0, 0), new float3(2, 0, 0), new float3(0, 0, 3));
            int idx = BounceRetarget.FindNext(float3.zero, -1, pos, 3, TileSize, Grid, Origin);
            Assert.AreEqual(0, idx); // dist 1 is nearest
            pos.Dispose();
        }

        [Test]
        public void SkipsExcludeIndex_PicksNextNearest()
        {
            var pos = Make(new float3(1, 0, 0), new float3(2, 0, 0), new float3(0, 0, 3));
            int idx = BounceRetarget.FindNext(float3.zero, 0, pos, 3, TileSize, Grid, Origin);
            Assert.AreEqual(1, idx); // index 0 excluded → index 1 (dist 2)
            pos.Dispose();
        }

        [Test]
        public void OutOfRange_ReturnsMinusOne()
        {
            var pos = Make(new float3(5, 0, 0)); // Chebyshev 5 > range 3
            int idx = BounceRetarget.FindNext(float3.zero, -1, pos, 3, TileSize, Grid, Origin);
            Assert.AreEqual(-1, idx);
            pos.Dispose();
        }

        [Test]
        public void EmptyOrAllExcluded_ReturnsMinusOne()
        {
            var empty = new NativeArray<float3>(0, Allocator.Persistent);
            Assert.AreEqual(-1, BounceRetarget.FindNext(float3.zero, -1, empty, 3, TileSize, Grid, Origin));
            empty.Dispose();

            var one = Make(new float3(1, 0, 0));
            Assert.AreEqual(-1, BounceRetarget.FindNext(float3.zero, 0, one, 3, TileSize, Grid, Origin));
            one.Dispose();
        }

        [Test]
        public void DistanceTie_ResolvesToLowerIndex()
        {
            // both cell-dist 2, both XZ sqdist 4 → lower index wins.
            var pos = Make(new float3(0, 0, 2), new float3(2, 0, 0));
            int idx = BounceRetarget.FindNext(float3.zero, -1, pos, 3, TileSize, Grid, Origin);
            Assert.AreEqual(0, idx);
            pos.Dispose();
        }

        [Test]
        public void ZeroTileRange_ReturnsMinusOne()
        {
            var pos = Make(new float3(1, 0, 0));
            int idx = BounceRetarget.FindNext(float3.zero, -1, pos, 0, TileSize, Grid, Origin);
            Assert.AreEqual(-1, idx);
            pos.Dispose();
        }
    }
}
