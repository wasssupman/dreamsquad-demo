using NUnit.Framework;
using Unity.Collections;
using Wassup.Battle.Combat;

namespace Wassup.Tests.EditMode
{
    // bomb-thrower-defender unit 2 — 가까운 순 B 상한 선별 순수 검증.
    // cap<=0 무제한 / cap<후보수 절단 / 동률 인덱스 tie-break / cap>=후보수 전원.
    public class AoeTargetCapTests
    {
        static NativeArray<float> Dist(params float[] v)
        {
            var a = new NativeArray<float>(v.Length, Allocator.Temp);
            for (int i = 0; i < v.Length; i++) a[i] = v[i];
            return a;
        }

        [Test]
        public void CapZero_ReturnsAll_InOrder()
        {
            var d = Dist(5f, 1f, 3f);
            var r = new NativeList<int>(Allocator.Temp);
            AoeTargetCap.SelectNearest(d, 0, ref r);
            Assert.AreEqual(3, r.Length, "cap<=0 = unlimited");
            Assert.AreEqual(0, r[0]); Assert.AreEqual(1, r[1]); Assert.AreEqual(2, r[2]);
            d.Dispose(); r.Dispose();
        }

        [Test]
        public void CapNegative_ReturnsAll()
        {
            var d = Dist(2f, 9f);
            var r = new NativeList<int>(Allocator.Temp);
            AoeTargetCap.SelectNearest(d, -1, ref r);
            Assert.AreEqual(2, r.Length);
            d.Dispose(); r.Dispose();
        }

        [Test]
        public void CapBelowCount_KeepsNearest()
        {
            // dist: 0→5, 1→1, 2→3, 3→8 → nearest 2 = idx1(1), idx2(3)
            var d = Dist(5f, 1f, 3f, 8f);
            var r = new NativeList<int>(Allocator.Temp);
            AoeTargetCap.SelectNearest(d, 2, ref r);
            Assert.AreEqual(2, r.Length);
            Assert.AreEqual(1, r[0], "nearest first");
            Assert.AreEqual(2, r[1]);
            d.Dispose(); r.Dispose();
        }

        [Test]
        public void Ties_BreakByIndexAscending()
        {
            var d = Dist(2f, 2f, 2f);
            var r = new NativeList<int>(Allocator.Temp);
            AoeTargetCap.SelectNearest(d, 2, ref r);
            Assert.AreEqual(2, r.Length);
            Assert.AreEqual(0, r[0]);
            Assert.AreEqual(1, r[1]);
            d.Dispose(); r.Dispose();
        }

        [Test]
        public void CapAtOrAboveCount_ReturnsAll_NearestOrdered()
        {
            var d = Dist(4f, 2f);
            var r = new NativeList<int>(Allocator.Temp);
            AoeTargetCap.SelectNearest(d, 5, ref r);
            Assert.AreEqual(2, r.Length);
            Assert.AreEqual(1, r[0], "idx1(2) nearest");
            Assert.AreEqual(0, r[1]);
            d.Dispose(); r.Dispose();
        }

        [Test]
        public void Empty_ReturnsEmpty()
        {
            var d = new NativeArray<float>(0, Allocator.Temp);
            var r = new NativeList<int>(Allocator.Temp);
            AoeTargetCap.SelectNearest(d, 3, ref r);
            Assert.AreEqual(0, r.Length);
            d.Dispose(); r.Dispose();
        }
    }
}
