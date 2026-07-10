using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using Wassup.Battle.Combat;

namespace Wassup.Tests.EditMode
{
    // nightmare-catcher unit 2 — deterministic epicenter rotation: row-major
    // stable ordering, snapshot-order independence, round-robin wrap, empty -1.
    public class BarrageEpicenterTests
    {
        private static readonly int2 Grid = new int2(16, 16);

        private static int Select(int2[] cells, int fireCount)
        {
            using var na = new NativeArray<int2>(cells, Allocator.Temp);
            return BarrageEpicenter.Select(na, fireCount, Grid);
        }

        [Test]
        public void EmptyPool_ReturnsMinusOne()
        {
            Assert.AreEqual(-1, Select(new int2[0], 0));
        }

        [Test]
        public void RoundRobin_WalksRowMajorOrder_AndWraps()
        {
            // row-major 오름차순: (1,0) → (3,0) → (0,2) → 다시 (1,0).
            var cells = new[] { new int2(0, 2), new int2(1, 0), new int2(3, 0) };
            Assert.AreEqual(1, Select(cells, 0), "fireCount 0 → (1,0)");
            Assert.AreEqual(2, Select(cells, 1), "fireCount 1 → (3,0)");
            Assert.AreEqual(0, Select(cells, 2), "fireCount 2 → (0,2)");
            Assert.AreEqual(1, Select(cells, 3), "wrap → (1,0)");
        }

        [Test]
        public void SnapshotOrder_DoesNotChangeRotation()
        {
            // 청크 레이아웃이 바뀌어 스냅샷 순서가 달라져도 같은 셀 순회.
            var a = new[] { new int2(0, 2), new int2(1, 0), new int2(3, 0) };
            var b = new[] { new int2(3, 0), new int2(0, 2), new int2(1, 0) };
            for (int k = 0; k < 6; k++)
            {
                var cellA = a[Select(a, k)];
                var cellB = b[Select(b, k)];
                Assert.AreEqual(cellA, cellB, $"fireCount {k}: 순서 무관 동일 진앙");
            }
        }

        [Test]
        public void SameInput_SameResult_Deterministic()
        {
            var cells = new[] { new int2(5, 5), new int2(2, 7), new int2(9, 1), new int2(0, 0) };
            for (int k = 0; k < 8; k++)
                Assert.AreEqual(Select(cells, k), Select(cells, k));
        }
    }
}
