using System.Collections.Generic;
using NUnit.Framework;
using Somnia.Battle.BattleCore;

namespace Somnia.Battle.Tests.EditMode.Core
{
    // battle-core-rebuild unit 1 완료 기준 ③ — 제출 해금.
    [TestFixture]
    public class SubmitCommandTests
    {
        private BattleMatch _match;
        private List<CoreEvent> _ended;

        [SetUp]
        public void SetUp()
        {
            _match = new BattleMatch(CoreGoldenCorpus.Fixture(1234));
            _ended = new List<CoreEvent>();
            _match.Bus.Subscribe(CoreEventKind.MatchEnded, 0, _ended.Add);
            _match.Begin();
        }

        [Test]
        public void 해금_전_제출은_거절된다()
        {
            var r = _match.Apply(Command.Submit());

            Assert.IsFalse(r.Accepted);
            Assert.AreEqual(RejectReason.SubmitLocked, r.Reason);
            Assert.IsFalse(_match.Clock.Ended, "거절된 제출이 판을 끝내면 안 된다");
        }

        [Test]
        public void 해금_한_틱_전에도_거절된다()
        {
            for (int i = 0; i < 3599; i++) _match.Tick();

            Assert.IsFalse(_match.Clock.SubmitUnlocked);
            Assert.AreEqual(RejectReason.SubmitLocked, _match.Apply(Command.Submit()).Reason);
        }

        [Test]
        public void 해금_시각_그_틱부터_수락된다()
        {
            for (int i = 0; i < 3600; i++) _match.Tick();

            Assert.IsTrue(_match.Clock.SubmitUnlocked, "60초 = 3,600틱에서 열린다");
            var r = _match.Apply(Command.Submit());

            Assert.IsTrue(r.Accepted);
            Assert.IsTrue(_match.Clock.Ended);
            Assert.AreEqual(MatchEndReason.Submitted, _match.Clock.EndReason);
        }

        [Test]
        public void 제출_종료도_MatchEnded_를_한_번만_낸다()
        {
            for (int i = 0; i < 3600; i++) _match.Tick();
            _match.Apply(Command.Submit());

            // 커맨드가 만든 사건은 **그 자리에서** 배달된다(Immediate seam) —
            // 종료 뒤 틱은 no-op 이라 플러시를 틱에 맡기면 영영 안 나온다.
            Assert.AreEqual(1, _ended.Count);
            _match.Tick();   // no-op 이 사건을 다시 내지 않는지 확인
            Assert.AreEqual(1, _ended.Count);
            Assert.AreEqual((int)MatchEndReason.Submitted, _ended[0].Arg);

            // 두 번째 제출은 「이미 끝났다」로 거절된다 — 잠금 사유가 아니다.
            Assert.AreEqual(RejectReason.MatchEnded, _match.Apply(Command.Submit()).Reason);
            Assert.AreEqual(1, _ended.Count);
        }

        [Test]
        public void 제출_뒤_남은_시간은_몰수된다()
        {
            for (int i = 0; i < 3600; i++) _match.Tick();
            _match.Apply(Command.Submit());

            int tickAtSubmit = _match.Clock.Tick;
            for (int i = 0; i < 1000; i++) _match.Tick();

            Assert.AreEqual(tickAtSubmit, _match.Clock.Tick, "제출 뒤에는 시계가 멈춘다");
        }
    }
}
