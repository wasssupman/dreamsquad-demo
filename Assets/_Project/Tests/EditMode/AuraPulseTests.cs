using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using Wassup.Battle.Combat;

namespace Wassup.Tests.EditMode
{
    // nightmare-whip-aura unit 0 — pulse target pick: Chebyshev boundary
    // inclusive, diagonal = Chebyshev (not Euclidean), same-cell included,
    // results cleared on entry, empty/negative-range degenerate.
    public class AuraPulseTests
    {
        private static int[] Select(int2[] cells, int2 host, int range)
        {
            using var na = new NativeArray<int2>(cells, Allocator.Temp);
            using var results = new NativeList<int>(Allocator.Temp);
            var list = results;
            AuraPulse.SelectTargets(na, host, range, ref list);
            return list.AsArray().ToArray();
        }

        [Test]
        public void Boundary_ExactRangeIncluded_BeyondExcluded()
        {
            var host = new int2(5, 5);
            var cells = new[] { new int2(8, 5), new int2(9, 5), new int2(5, 2), new int2(5, 1) };
            // Chebyshev 3(경계) 포함, 4 제외 — x/y 축 모두.
            CollectionAssert.AreEqual(new[] { 0, 2 }, Select(cells, host, 3));
        }

        [Test]
        public void Diagonal_UsesChebyshev_NotEuclidean()
        {
            var host = new int2(5, 5);
            // (8,8) = Chebyshev 3 (유클리드 ~4.24) → 포함. (8,9) = Chebyshev 4 → 제외.
            var cells = new[] { new int2(8, 8), new int2(8, 9) };
            CollectionAssert.AreEqual(new[] { 0 }, Select(cells, host, 3));
        }

        [Test]
        public void HostCell_SameCellCandidate_Included()
        {
            // self 제외는 arm 의 entity 비교 책임 — 셀 판정은 같은 셀도 대상.
            var host = new int2(4, 4);
            var cells = new[] { new int2(4, 4) };
            CollectionAssert.AreEqual(new[] { 0 }, Select(cells, host, 3));
        }

        [Test]
        public void EmptyPool_NoResults()
        {
            Assert.AreEqual(0, Select(new int2[0], new int2(0, 0), 3).Length);
        }

        [Test]
        public void NegativeRange_SelectsNothing()
        {
            var cells = new[] { new int2(1, 1) };
            Assert.AreEqual(0, Select(cells, new int2(1, 1), -1).Length);
        }

        [Test]
        public void Results_ClearedOnEntry()
        {
            using var na = new NativeArray<int2>(new[] { new int2(2, 2) }, Allocator.Temp);
            using var results = new NativeList<int>(Allocator.Temp);
            var list = results;
            list.Add(99); // 이전 펄스 잔여를 가정
            AuraPulse.SelectTargets(na, new int2(0, 0), 5, ref list);
            CollectionAssert.AreEqual(new[] { 0 }, list.AsArray().ToArray());
        }
    }
}
