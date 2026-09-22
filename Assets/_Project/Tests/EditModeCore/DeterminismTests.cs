using NUnit.Framework;
using Unity.Mathematics;
using Wassup.BattleCore;

namespace Wassup.Tests.EditMode.Core
{
    // battle-core-rebuild unit 1 완료 기준 ② — 결정론.
    //
    // 「같은 정의표 + 같은 스케줄 = 같은 트레이스」. 이 단언이 깨지는 방식은 대개
    // 시간(`Time`·`DateTime`)·난수(`System.Random`)·순회 순서(사전·해시셋) 셋이고,
    // 셋 다 코어에서 금지돼 있다. 그 금지가 실제로 지켜지는지를 여기서 실행으로 묻는다.
    [TestFixture]
    public class DeterminismTests
    {
        private static CommandSchedule Schedule() => new CommandSchedule()
            .Add(5, Command.DebugSpawnEnemy(0, new int2(0, 0)))
            .Add(5, Command.PlaceDefender(0, new int2(1, 1)))
            .Add(30, Command.DebugSpawnEnemy(0, new int2(2, 2)))
            .Add(60, Command.DebugDestroy(new SimEntityId(1)))
            .Add(90, Command.Submit());   // 해금 전이라 거절 — 거절도 결정론이어야 한다

        [Test]
        public void 같은_입력을_두_번_돌리면_트레이스가_같다()
        {
            var a = CoreHarness.Run(CoreGoldenCorpus.Fixture(1234), Schedule(), 600, "determinism");
            var b = CoreHarness.Run(CoreGoldenCorpus.Fixture(1234), Schedule(), 600, "determinism");

            Assert.IsNull(a.Trace.DiffAgainst(b.Trace), "두 실행의 트레이스가 갈렸다");
            Assert.AreEqual(a.Trace.Serialize(), b.Trace.Serialize(), "직렬화 바이트까지 같아야 한다");
            Assert.AreEqual(a.Trace.finalStateHash, b.Trace.finalStateHash);
        }

        [Test]
        public void 거절_receipt_도_두_실행이_같다()
        {
            var a = CoreHarness.Run(CoreGoldenCorpus.Fixture(1234), Schedule(), 600, "determinism");
            var b = CoreHarness.Run(CoreGoldenCorpus.Fixture(1234), Schedule(), 600, "determinism");

            Assert.AreEqual(a.Receipts.Count, b.Receipts.Count);
            for (int i = 0; i < a.Receipts.Count; i++)
            {
                Assert.AreEqual(a.Receipts[i].Accepted, b.Receipts[i].Accepted, $"receipt #{i}");
                Assert.AreEqual(a.Receipts[i].Reason, b.Receipts[i].Reason, $"receipt #{i}");
            }
        }

        [Test]
        public void 직렬화_왕복이_바이트로_같다()
        {
            var run = CoreHarness.Run(CoreGoldenCorpus.Fixture(1234), Schedule(), 600, "determinism");
            string text = run.Trace.Serialize();
            string again = CoreTrace.Deserialize(text).Serialize();

            Assert.AreEqual(text, again, "왕복을 통과하지 못하는 기록은 골든이 될 자격이 없다");
        }

        [Test]
        public void 옛_계열_트레이스는_코어_리더가_거절한다()
        {
            // 포맷은 같아도 채널 어휘가 다르다. 구분자가 없으면 골든 하나가 조용히
            // 엉뚱한 채널 이름으로 읽힌다.
            var legacy = new Wassup.Core.Trace.LegacyTraceV0 { scenario = "old", configHash = "deadbeefdeadbeef" };
            Assert.Throws<System.FormatException>(() => CoreTrace.Deserialize(legacy.Serialize()));
        }

        [Test]
        public void 버스는_모르는_종류를_바로_거절한다()
        {
            var bus = new EventBus();

            // 기본값(`None`)으로 남은 이벤트가 새어 들어오면 구독자가 0명이라 조용히
            // 사라진다 — 「이벤트가 안 온다」를 며칠 쫓게 되는 종류의 실패다.
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => bus.Subscribe(CoreEventKind.None, 0, _ => { }));
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => bus.Publish(default));
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => bus.Subscribe(CoreEventKind._Count, 0, _ => { }));
        }

        [Test]
        public void 시드가_다르면_난수_계열이_갈린다()
        {
            var a = new RngStreams(1234);
            var b = new RngStreams(4321);

            Assert.AreNotEqual(a.Wave.NextUInt(), b.Wave.NextUInt());

            // 같은 시드의 계열끼리는 상관이 없어야 한다(salt 분리의 목적).
            var c = new RngStreams(1234);
            Assert.AreNotEqual(c.Map.NextUInt(), c.Wave.NextUInt());
        }

        [Test]
        public void 같은_시드는_같은_난수열을_낸다()
        {
            var a = new RngStreams(777);
            var b = new RngStreams(777);
            for (int i = 0; i < 16; i++) Assert.AreEqual(a.Visual.NextUInt(), b.Visual.NextUInt());
        }
    }
}
