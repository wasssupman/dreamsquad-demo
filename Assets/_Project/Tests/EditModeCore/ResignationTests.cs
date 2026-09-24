using NUnit.Framework;
using Unity.Mathematics;
using Wassup.BattleCore;

namespace Wassup.Tests.EditMode.Core
{
    // battle-core-rebuild unit 6b2 — **사직서 누적과 임계.** 유닛이 줍지 않는다 — 판 위 장수가 전부다.
    [TestFixture]
    public class ResignationTests
    {
        private static BattleMatch Board(GimmickDef gimmick)
        {
            var def = CoreGimmickFixtures.With(CoreCombatFixtures.Definition(), gimmick);
            var m = new BattleMatch(def);
            m.Begin();
            return m;
        }

        [Test]
        public void 사직서는_판_위에_쌓이고_떨어질_때마다_장수를_값으로_나른다()
        {
            var m = Board(CoreGimmickFixtures.ClockOut(threshold: 3));
            var dropped = CoreCombatFixtures.Listen(m, CoreEventKind.ResignationDropped);
            var fired = CoreCombatFixtures.Listen(m, CoreEventKind.ResignationThreshold);
            m.Apply(Command.DebugDropResignation(new int2(2, 2)));
            m.Apply(Command.DebugDropResignation(new int2(2, 2)));   // 같은 칸에 겹쳐도 된다
            CoreCombatFixtures.Tick(m, 5);

            Assert.AreEqual(2, m.World.Resignations.Count);
            Assert.AreEqual(new[] { 1, 2 }, new[] { dropped[0].Arg, dropped[1].Arg }, "떨어진 뒤 판 위 장수");
            Assert.AreEqual(0, fired.Count, "임계 밑");
        }

        [Test]
        public void 한_틱에_임계를_여러_번_넘으면_임계마다_한_건이고_오래된_것부터_소모된다()
        {
            // level 폴링 — 7장 / 임계 3 = 두 번(사양, rev 3 §2). 남는 1장은 가장 늦게 떨어진 것.
            var m = Board(CoreGimmickFixtures.ClockOut(threshold: 3, meteorCount: 4));
            var fired = CoreCombatFixtures.Listen(m, CoreEventKind.ResignationThreshold);
            var consumed = CoreCombatFixtures.Listen(m, CoreEventKind.ResignationConsumed);
            for (int i = 0; i < 7; i++) m.Apply(Command.DebugDropResignation(new int2(i, 0)));
            var last = m.World.Resignations[6].Id;
            m.Tick();

            Assert.AreEqual(2, fired.Count);
            Assert.AreEqual(4, fired[0].Arg, "운석 발수 스냅샷 — 실행은 unit 7");
            Assert.AreEqual(6, consumed.Count, "장마다 소멸 사건(계약 7)");
            Assert.AreEqual(1, m.World.Resignations.Count);
            Assert.AreEqual(last, m.World.Resignations[0].Id, "가장 오래된 것부터");
        }

        [Test]
        public void 퇴근_기믹이_안_뽑힌_판에서는_사직서가_안_떨어진다()
        {
            var m = Board(CoreGimmickFixtures.RedBull());
            var r = m.Apply(Command.DebugDropResignation(new int2(1, 1)));
            Assert.IsFalse(r.Accepted);
            Assert.AreEqual(RejectReason.GimmickInactive, r.Reason);
        }

        [Test]
        public void 헤드리스_하네스에서_커맨드_예약만으로_사직서가_선다()
        {
            var def = CoreGimmickFixtures.With(CoreCombatFixtures.Definition(), CoreGimmickFixtures.ClockOut(threshold: 5));
            var schedule = new CommandSchedule()
                .Add(2, Command.DebugDropResignation(new int2(1, 1)))
                .Add(4, Command.DebugDropResignation(new int2(2, 1)));
            var result = CoreHarness.Run(def, schedule, 10, "resignation_debug");
            Assert.AreEqual(2, result.Match.World.Resignations.Count);
            Assert.IsTrue(result.Trace.events.Exists(e => e.channel == CoreTraceChannel.ResignationDropped));
        }
    }
}
