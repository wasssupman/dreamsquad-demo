using NUnit.Framework;
using Wassup.UI;

namespace Wassup.Tests.EditMode
{
    // dreamcatcher-orb-dock unit 8 — 항아리 독은 회차가 오르는 순간에만 터진다.
    // «언제 올랐나» 를 결정하는 건 이 순수 계산이라, 연출 트리거의 회귀는 여기서 잡는다.
    // 라이브 수치(코스트 20 / 상한 100)를 리터럴로 못박지 않고 인자로 넘긴다.
    public class AwakeningChargeTests
    {
        [Test]
        public void UnitCost_PicksCheapestCard()
        {
            Assert.AreEqual(15, AwakeningCharge.UnitCost(30, 15, 20));
            Assert.AreEqual(20, AwakeningCharge.UnitCost(20, 20, 20));
        }

        [Test]
        public void UnitCost_IgnoresNonPositiveCosts()
        {
            // 0/음수는 "비용 없음" 이지 "가장 싼 카드" 가 아니다.
            Assert.AreEqual(20, AwakeningCharge.UnitCost(0, 20, 30));
            Assert.AreEqual(0, AwakeningCharge.UnitCost(0, 0, 0));
        }

        [Test]
        public void CountOf_FloorsToWholeCharges()
        {
            const int unit = 20;
            Assert.AreEqual(0, AwakeningCharge.CountOf(0, unit));
            Assert.AreEqual(0, AwakeningCharge.CountOf(unit - 1, unit));
            Assert.AreEqual(1, AwakeningCharge.CountOf(unit, unit));
            Assert.AreEqual(2, AwakeningCharge.CountOf(unit * 2 + unit / 2, unit));
            Assert.AreEqual(5, AwakeningCharge.CountOf(unit * 5, unit));
        }

        [Test]
        public void CountOf_IsZeroWhenChargeConceptDoesNotHold()
        {
            // 코스트가 없으면 회차 판정이 성립하지 않는다 → 연출도 안 터진다.
            Assert.AreEqual(0, AwakeningCharge.CountOf(80, 0));
            Assert.AreEqual(0, AwakeningCharge.CountOf(-5, 20));
        }

        [Test]
        public void ChargeBurst_FiresOnlyWhenCountRises()
        {
            const int unit = 20;
            // 킬 단위 획득(2~5점)으로 경계를 넘기 전엔 조용하다.
            Assert.IsFalse(Rises(20, 23, unit));
            Assert.IsFalse(Rises(23, 26, unit));
            Assert.IsFalse(Rises(26, 39, unit));
            // 경계를 넘는 그 한 번만 터진다.
            Assert.IsTrue(Rises(39, 41, unit));
            Assert.IsFalse(Rises(41, 44, unit));
            // 한 번에 두 회분을 넘겨도 «상승» 은 한 번(뷰가 한방으로 합친다).
            Assert.IsTrue(Rises(35, 80, unit));
            // 소비/리셋으로 내려갈 땐 안 터진다.
            Assert.IsFalse(Rises(60, 40, unit));
            Assert.IsFalse(Rises(80, 20, unit));
        }

        private static bool Rises(int previous, int current, int unitCost) =>
            AwakeningCharge.CountOf(current, unitCost) > AwakeningCharge.CountOf(previous, unitCost);
    }
}
