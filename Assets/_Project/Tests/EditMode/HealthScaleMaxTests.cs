using NUnit.Framework;
using Wassup.Battle.Units;

namespace Wassup.Tests.EditMode
{
    // season-gimmick-overwork unit 1 — Health.ScaleMax 불변식:
    // 축소는 value 를 클램프, 복원은 무료 힐 없음, newMax 는 1 HP 바닥.
    public class HealthScaleMaxTests
    {
        [Test]
        public void Identity_MulOne_ReturnsUnchanged()
        {
            var r = Health.ScaleMax(value: 70f, baseMax: 100f, mul: 1f);
            Assert.AreEqual(70f, r.x, 1e-5f);
            Assert.AreEqual(100f, r.y, 1e-5f);
        }

        [Test]
        public void Shrink_ClampsValueToNewMax()
        {
            // 번아웃 ×0.8: max 100→80, value 90→80 으로 클램프.
            var r = Health.ScaleMax(value: 90f, baseMax: 100f, mul: 0.8f);
            Assert.AreEqual(80f, r.x, 1e-5f);
            Assert.AreEqual(80f, r.y, 1e-5f);
        }

        [Test]
        public void Shrink_ValueBelowNewMax_Unchanged()
        {
            var r = Health.ScaleMax(value: 30f, baseMax: 100f, mul: 0.8f);
            Assert.AreEqual(30f, r.x, 1e-5f);
            Assert.AreEqual(80f, r.y, 1e-5f);
        }

        [Test]
        public void Restore_NoFreeHeal()
        {
            // 번아웃 해제: max 는 baseMax 로 복원되지만 value 는 오르지 않는다.
            var r = Health.ScaleMax(value: 55f, baseMax: 100f, mul: 1f);
            Assert.AreEqual(55f, r.x, 1e-5f);
            Assert.AreEqual(100f, r.y, 1e-5f);
        }

        [Test]
        public void LastRun_NinetyPercentCut()
        {
            // 라스트런 ×0.1: max 200→20, 만피였다면 20 으로.
            var r = Health.ScaleMax(value: 200f, baseMax: 200f, mul: 0.1f);
            Assert.AreEqual(20f, r.x, 1e-5f);
            Assert.AreEqual(20f, r.y, 1e-5f);
        }

        [Test]
        public void TinyBase_FlooredAtOneHp()
        {
            // baseMax*mul < 1 → max 는 1 HP 바닥 (max<=0 의 ratio 가드와 사망 오판 방지).
            var r = Health.ScaleMax(value: 5f, baseMax: 5f, mul: 0.1f);
            Assert.AreEqual(1f, r.y, 1e-5f);
            Assert.AreEqual(1f, r.x, 1e-5f);
        }
    }
}
