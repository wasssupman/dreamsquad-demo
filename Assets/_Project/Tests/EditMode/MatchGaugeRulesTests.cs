using NUnit.Framework;
using Wassup.Sim.Match;

// battle-sim-extraction unit 16-G — 각성 게이지 상태·산식.
//
// 적출 전에는 이 계약을 확인하려면 `DreamcatcherHandController` + 씬을 세워야 했다(클램프·넘침
// 계산이 뷰 이벤트 발화와 한 메서드에 있었다). 골든에는 실리지 않으므로 여기가 유일한 증인이다.
namespace Wassup.Tests.EditMode
{
    public class MatchGaugeRulesTests
    {
        static MatchGaugeRules Armed(int start = 0, int max = 100)
        {
            var g = new MatchGaugeRules();
            g.Reset(start, max);
            return g;
        }

        [Test]
        public void 리셋은_시작값과_상한을_고정한다()
        {
            var g = Armed(20, 100);
            Assert.AreEqual(20, g.Current);
            Assert.AreEqual(100, g.Max);
        }

        [Test]
        public void 시작값이_상한을_넘으면_상한으로_접힌다()
        {
            // 시트 오기(start > max) 를 여기서 접는다 — 그 뒤 산식이 음수 넘침을 만들지 않게.
            var g = Armed(999, 100);
            Assert.AreEqual(100, g.Current);
        }

        [Test]
        public void 음수_시작값과_음수_상한은_0_이다()
        {
            var g = Armed(-5, -5);
            Assert.AreEqual(0, g.Current);
            Assert.AreEqual(0, g.Max);
        }

        [Test]
        public void 획득은_적용량을_돌려준다()
        {
            var g = Armed(0, 100);
            Assert.IsTrue(g.TryGain(30, out int applied, out int over));
            Assert.AreEqual(30, applied);
            Assert.AreEqual(0, over);
            Assert.AreEqual(30, g.Current);
        }

        [Test]
        public void 상한을_넘는_획득은_소멸하고_넘침으로_보고된다()
        {
            // **이월되지 않는다.** 뷰가 그 사실을 알려야 해서 overflowed 를 따로 낸다.
            var g = Armed(90, 100);
            Assert.IsTrue(g.TryGain(30, out int applied, out int over));
            Assert.AreEqual(10, applied);
            Assert.AreEqual(20, over);
            Assert.AreEqual(100, g.Current);
        }

        [Test]
        public void 이미_가득_차면_전부_넘침이고_움직이지_않는다()
        {
            var g = Armed(100, 100);
            Assert.IsFalse(g.TryGain(25, out int applied, out int over), "게이지가 움직이지 않았다");
            Assert.AreEqual(0, applied);
            Assert.AreEqual(25, over, "전량이 넘침으로 보고돼야 뷰가 경고를 띄운다");
            Assert.AreEqual(100, g.Current);
        }

        [Test]
        public void 양수가_아닌_획득은_무시된다()
        {
            var g = Armed(50, 100);
            Assert.IsFalse(g.TryGain(0, out _, out int over));
            Assert.AreEqual(0, over, "넘침도 아니다 — 경고가 뜨면 안 된다");
            Assert.IsFalse(g.TryGain(-10, out _, out _));
            Assert.AreEqual(50, g.Current);
        }

        [Test]
        public void 소비는_바닥이_0_이다()
        {
            var g = Armed(10, 100);
            g.Spend(30);
            Assert.AreEqual(0, g.Current, "음수 게이지는 없다");
        }

        [Test]
        public void 소비_후_잔량이_남는다()
        {
            var g = Armed(50, 100);
            g.Spend(20);
            Assert.AreEqual(30, g.Current);
        }

        [Test]
        public void CanAfford_는_같은_값도_통과시킨다()
        {
            var g = Armed(10, 100);
            Assert.IsTrue(g.CanAfford(10));
            Assert.IsTrue(g.CanAfford(0));
            Assert.IsFalse(g.CanAfford(11));
        }
    }
}
