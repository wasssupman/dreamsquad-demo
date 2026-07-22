using NUnit.Framework;
using Wassup.Data.MapGrid;

namespace Wassup.Tests.EditMode
{
    // random-map-pool unit 0 — seed→인덱스 선택의 결정론·범위·엣지(int.MinValue/count≤1) 고정.
    public class MapPoolSelectTests
    {
        [Test]
        public void SingleOrEmptyCount_ReturnsZero()
        {
            Assert.AreEqual(0, MapPoolSelect.SelectIndex(12345, 1));
            Assert.AreEqual(0, MapPoolSelect.SelectIndex(12345, 0));
            Assert.AreEqual(0, MapPoolSelect.SelectIndex(-9999, 1));
        }

        [Test]
        public void AlwaysInRange_AcrossManySeeds()
        {
            int[] counts = { 2, 3, 5, 8 };
            foreach (int count in counts)
                for (int seed = -1000; seed <= 1000; seed++)
                {
                    int idx = MapPoolSelect.SelectIndex(seed, count);
                    Assert.GreaterOrEqual(idx, 0, $"seed={seed} count={count}");
                    Assert.Less(idx, count, $"seed={seed} count={count}");
                }
        }

        [Test]
        public void IntMinValueSeed_NoOverflow_ValidIndex()
        {
            // (uint)int.MinValue = 2147483648. Math.Abs 였다면 오버플로로 터졌을 입력.
            // 2147483648 % 2 = 0, % 3 = 2 — 값 자체보다 "예외 없이 범위 내"가 핵심.
            Assert.AreEqual(0, MapPoolSelect.SelectIndex(int.MinValue, 2));
            Assert.AreEqual(2, MapPoolSelect.SelectIndex(int.MinValue, 3));
            int idx = MapPoolSelect.SelectIndex(int.MinValue, 8);
            Assert.GreaterOrEqual(idx, 0);
            Assert.Less(idx, 8);
        }

        [Test]
        public void Deterministic_SameInputSameResult()
        {
            for (int seed = -50; seed <= 50; seed++)
                Assert.AreEqual(
                    MapPoolSelect.SelectIndex(seed, 4),
                    MapPoolSelect.SelectIndex(seed, 4));
        }

        [Test]
        public void BothIndicesReachable_ForCountTwo()
        {
            bool sawZero = false, sawOne = false;
            for (int seed = 0; seed < 32 && !(sawZero && sawOne); seed++)
            {
                int idx = MapPoolSelect.SelectIndex(seed, 2);
                if (idx == 0) sawZero = true;
                else if (idx == 1) sawOne = true;
            }
            Assert.IsTrue(sawZero, "count=2 에서 인덱스 0 이 한 번도 안 나옴");
            Assert.IsTrue(sawOne, "count=2 에서 인덱스 1 이 한 번도 안 나옴");
        }
    }
}
