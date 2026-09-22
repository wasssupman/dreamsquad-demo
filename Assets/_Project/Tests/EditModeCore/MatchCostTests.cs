using NUnit.Framework;
using Unity.Mathematics;
using Wassup.BattleCore;

namespace Wassup.Tests.EditMode.Core
{
    // battle-core-rebuild unit 4 — 배치 자원.
    [TestFixture]
    public class MatchCostTests
    {
        [Test]
        public void 재생은_배치_창이_닫히는_순간_시작한다()
        {
            var def = CoreMatchFixtures.Definition();
            var match = new BattleMatch(def);
            match.Begin();

            Assert.IsFalse(match.Cost.RegenActive, "배치 중에 차면 「고민할수록 이득」이 된다");
            Assert.AreEqual(10f, match.Cost.Current, 1e-4f);

            for (int t = 0; t < 120; t++) match.Tick();
            Assert.AreEqual(10f, match.Cost.Current, 1e-4f, "창이 열려 있는 동안은 그대로다");

            match.Apply(Command.FinishPlacement());
            Assert.IsTrue(match.Cost.RegenActive,
                "옛 전투는 이 스위치를 UI 가 들고 있어 스크립트 진입이 0 에 멎었다(X24)");

            for (int t = 0; t < 60; t++) match.Tick();
            Assert.AreEqual(11f, match.Cost.Current, 0.05f, "초당 1");
        }

        [Test]
        public void 배치_페이즈가_없는_모드는_처음부터_찬다()
        {
            var def = CoreMatchFixtures.Definition();
            def.Mode.PlacementInputEnabled = false;
            def.Mode.PlacementSeconds = 0f;
            def.ConfigHash = def.ComputeConfigHash();

            var match = new BattleMatch(def);
            match.Begin();
            Assert.IsTrue(match.Cost.RegenActive);
        }

        [Test]
        public void 상한에서_멈춘다()
        {
            var match = CoreMatchFixtures.BeginBattle(CoreMatchFixtures.Definition());
            for (int t = 0; t < 60 * 30; t++) match.Tick();
            Assert.AreEqual(15f, match.Cost.Current, 1e-3f);
        }

        [Test]
        public void 화면_숫자는_내림이고_판정은_실수다()
        {
            var match = CoreMatchFixtures.BeginBattle(CoreMatchFixtures.Definition());
            for (int t = 0; t < 30; t++) match.Tick();   // 10.5

            Assert.AreEqual(10, match.Cost.CurrentInt, "「9.9인데 10짜리를 못 놓는다」의 근거");
            Assert.Greater(match.Cost.Current, 10f);
        }

        [Test]
        public void 재생_배율은_반입이_정하고_판_안에서_안_바뀐다()
        {
            var def = CoreMatchFixtures.Definition();
            def.CostRateMultiplier = 2f;
            def.ConfigHash = def.ComputeConfigHash();
            var match = CoreMatchFixtures.BeginBattle(def);

            Assert.AreEqual(2f, match.Cost.RegenRateMultiplier, 1e-4f);
            for (int t = 0; t < 60; t++) match.Tick();
            Assert.AreEqual(12f, match.Cost.Current, 0.05f, "돌 버프가 두 배로 차게 한다");
        }

        [Test]
        public void 지불은_성공_판정_뒤에만_일어난다()
        {
            var def = CoreMatchFixtures.Definition();
            def.Units[0].Cost = 4;
            def.ConfigHash = def.ComputeConfigHash();
            var match = CoreMatchFixtures.BeginBattle(def);

            // 못 놓는 자리 — 차감이 **없어야** 한다.
            match.Apply(Command.PlaceDefender(0, new int2(99, 99)));
            Assert.AreEqual(10f, match.Cost.Current, 1e-4f);

            match.Apply(Command.PlaceDefender(0, new int2(3, 1)));
            Assert.AreEqual(6f, match.Cost.Current, 1e-4f);
        }

        [Test]
        public void 재생은_사건을_내지_않고_지불만_낸다()
        {
            var match = CoreMatchFixtures.BeginBattle(CoreMatchFixtures.Definition());
            var changed = CoreMatchFixtures.Listen(match, CoreEventKind.CostChanged);

            for (int t = 0; t < 300; t++) match.Tick();
            Assert.AreEqual(0, changed.Count, "매 틱 쏘면 판 하나에 만 건이 쌓인다");

            match.Apply(Command.PlaceDefender(0, new int2(3, 1)));
            Assert.AreEqual(1, changed.Count);
            Assert.AreEqual(-2, changed[0].Arg);
        }

        [Test]
        public void 획득은_상한을_넘지_않는다()
        {
            var match = CoreMatchFixtures.BeginBattle(CoreMatchFixtures.Definition());
            Assert.AreEqual(5, match.Cost.Gain(99), "10 → 15 까지만");
            Assert.AreEqual(15f, match.Cost.Current, 1e-4f);
            Assert.AreEqual(0, match.Cost.Gain(5), "이미 꽉 찼다");
        }
    }
}
