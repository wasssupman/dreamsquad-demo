using System.Collections.Generic;
using NUnit.Framework;
using Somnia.Battle.BattleCore;

namespace Somnia.Battle.Tests.EditMode.Core
{
    // battle-core-rebuild unit 1 완료 기준 ① — 시계와 종료 통로.
    [TestFixture]
    public class MatchClockTests
    {
        private static BattleMatch Begin(out List<CoreEvent> ended)
        {
            var match = new BattleMatch(CoreGoldenCorpus.Fixture(1234));
            var log = new List<CoreEvent>();
            match.Bus.Subscribe(CoreEventKind.MatchEnded, 0, log.Add);
            match.Begin();
            ended = log;
            return match;
        }

        [Test]
        public void 판_길이가_10800틱이다()
        {
            var match = Begin(out _);
            Assert.AreEqual(10800, match.Clock.TotalTicks, "180초 ÷ (1/60) = 10,800");
            Assert.AreEqual(3600, match.Clock.SubmitUnlockTick, "제출 해금 60초");
        }

        [Test]
        public void 만료_직전까지는_안_끝난다()
        {
            var match = Begin(out var ended);
            for (int i = 0; i < 10799; i++) match.Tick();

            Assert.IsFalse(match.Clock.Ended, "10,799틱에서는 아직 안 끝난다");
            Assert.AreEqual(0, ended.Count);
            Assert.AreEqual(10799, match.Clock.Tick);
        }

        [Test]
        public void 만료틱에_complete_가_정확히_한_번()
        {
            var match = Begin(out var ended);
            for (int i = 0; i < 10800; i++) match.Tick();

            Assert.IsTrue(match.Clock.Ended);
            Assert.AreEqual(MatchEndReason.Complete, match.Clock.EndReason);
            Assert.AreEqual(1, ended.Count, "MatchEnded 는 정확히 1회");
            Assert.AreEqual(10800, ended[0].Tick);
            Assert.AreEqual((int)MatchEndReason.Complete, ended[0].Arg);
        }

        [Test]
        public void 종료_뒤_틱은_no_op()
        {
            var match = Begin(out var ended);
            for (int i = 0; i < 10800; i++) match.Tick();

            int tickAtEnd = match.Clock.Tick;
            float timeAtEnd = match.Clock.BattleTime;
            ulong stateAtEnd = match.World.StateHash();

            for (int i = 0; i < 600; i++) match.Tick();

            Assert.AreEqual(tickAtEnd, match.Clock.Tick, "종료 뒤 틱이 올라가면 안 된다(계약 5)");
            Assert.AreEqual(timeAtEnd, match.Clock.BattleTime);
            Assert.AreEqual(stateAtEnd, match.World.StateHash());
            Assert.AreEqual(1, ended.Count, "종료 사건이 두 번 나면 안 된다");
        }

        [Test]
        public void 경과시간은_누산이_아니라_틱_곱하기_dt()
        {
            var match = Begin(out _);
            for (int i = 0; i < 10800; i++) match.Tick();

            // 1/60f 를 10,800번 더하면 부동소수 오차가 쌓여 180 에서 벗어난다.
            // 시계가 `Tick * dt` 로 계산하므로 오차가 한 번의 곱셈뿐이다.
            Assert.AreEqual(180f, match.Clock.BattleTime, 1e-3f);
            Assert.AreEqual(0f, match.Clock.Remaining, 1e-3f);
        }

        [Test]
        public void 종료_뒤_커맨드는_전부_거절된다()
        {
            var match = Begin(out _);
            for (int i = 0; i < 10800; i++) match.Tick();

            var receipt = match.Apply(Command.DebugSpawnEnemy(0, new Unity.Mathematics.int2(0, 0)));
            Assert.IsFalse(receipt.Accepted);
            Assert.AreEqual(RejectReason.MatchEnded, receipt.Reason);
        }
    }
}
