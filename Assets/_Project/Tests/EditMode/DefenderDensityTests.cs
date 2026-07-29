using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using Wassup.Battle.Combat;

namespace Wassup.Tests.EditMode
{
    // boss-jjangssen unit 4 — 밀집 착지 앵커 선택 고정 (제약 10, sim-critical 타겟팅).
    // 결정론이 핵심이다: 같은 배치에서 매번 같은 셀이 나와야 리플레이가 재현된다.
    public class DefenderDensityTests
    {
        private static readonly int2 Grid = new int2(20, 10);

        private static NativeArray<int2> Cells(params int[] xy)
        {
            var a = new NativeArray<int2>(xy.Length / 2, Allocator.Temp);
            for (int i = 0; i < a.Length; i++) a[i] = new int2(xy[i * 2], xy[i * 2 + 1]);
            return a;
        }

        [Test]
        public void EmptyPoolReturnsFalse()
        {
            var cells = new NativeArray<int2>(0, Allocator.Temp);
            try
            {
                Assert.IsFalse(DefenderDensity.TryFindDensestCell(cells, 2, Grid, out _, out int count));
                Assert.AreEqual(0, count);
            }
            finally { cells.Dispose(); }
        }

        // 3기 뭉친 쪽 vs 1기 떨어진 쪽 → 뭉친 쪽이 뽑힌다.
        [Test]
        public void PicksTheClusterOverTheLoner()
        {
            var cells = Cells(2, 2, 3, 2, 2, 3, 15, 8);
            try
            {
                Assert.IsTrue(DefenderDensity.TryFindDensestCell(cells, 2, Grid, out int2 densest, out int count));
                Assert.AreEqual(3, count, "반경 2 안에 3기");
                Assert.IsTrue(densest.x <= 3 && densest.y <= 3, $"클러스터 쪽이어야 하는데 {densest}");
                Assert.AreNotEqual(new int2(15, 8), densest, "외톨이가 뽑히면 안 된다");
            }
            finally { cells.Dispose(); }
        }

        // 동점이면 row-major 키 최소(= y 먼저, 그 다음 x)가 뽑힌다. 청크 순서 의존 금지.
        [Test]
        public void TiesResolveToLowestRowMajorKey()
        {
            // 두 쌍이 서로 멀리 떨어져 각각 count 2 로 동점. 입력 순서는 일부러 뒤집어 넣는다.
            var cells = Cells(10, 7, 11, 7, 1, 1, 2, 1);
            try
            {
                Assert.IsTrue(DefenderDensity.TryFindDensestCell(cells, 1, Grid, out int2 densest, out int count));
                Assert.AreEqual(2, count);
                Assert.AreEqual(new int2(1, 1), densest, "row-major 키가 가장 작은 (1,1) 이어야 한다");
            }
            finally { cells.Dispose(); }
        }

        // 같은 입력이면 항상 같은 출력 — 입력 순서만 바꿔도 결과가 같아야 한다.
        [Test]
        public void ResultIsIndependentOfInputOrder()
        {
            var a = Cells(4, 4, 5, 4, 4, 5, 12, 2);
            var b = Cells(12, 2, 4, 5, 5, 4, 4, 4);
            try
            {
                Assert.IsTrue(DefenderDensity.TryFindDensestCell(a, 2, Grid, out int2 da, out int ca));
                Assert.IsTrue(DefenderDensity.TryFindDensestCell(b, 2, Grid, out int2 db, out int cb));
                Assert.AreEqual(ca, cb);
                Assert.AreEqual(da, db, "입력 순서가 결과를 바꾸면 결정론이 깨진다");
            }
            finally { a.Dispose(); b.Dispose(); }
        }

        // radius <= 0 은 "자기 셀만" — 같은 셀에 겹친 유닛 수로 판정한다.
        [Test]
        public void ZeroRadiusCountsOnlySameCell()
        {
            var cells = Cells(6, 6, 6, 6, 1, 1, 2, 2, 3, 3);
            try
            {
                Assert.IsTrue(DefenderDensity.TryFindDensestCell(cells, 0, Grid, out int2 densest, out int count));
                Assert.AreEqual(2, count, "(6,6) 에 2기 겹침");
                Assert.AreEqual(new int2(6, 6), densest);
            }
            finally { cells.Dispose(); }
        }

        // 단일 유닛이면 그 셀. count 1.
        [Test]
        public void SingleDefenderPicksItsOwnCell()
        {
            var cells = Cells(7, 3);
            try
            {
                Assert.IsTrue(DefenderDensity.TryFindDensestCell(cells, 3, Grid, out int2 densest, out int count));
                Assert.AreEqual(1, count);
                Assert.AreEqual(new int2(7, 3), densest);
            }
            finally { cells.Dispose(); }
        }
    }
}
