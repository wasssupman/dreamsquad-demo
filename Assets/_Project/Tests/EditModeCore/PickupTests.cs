using NUnit.Framework;
using Unity.Mathematics;
using Wassup.BattleCore;
using Wassup.BattleCore.Effects;

namespace Wassup.Tests.EditMode.Core
{
    // battle-core-rebuild unit 6b2 — **레드불 픽업과 라스트런.**
    [TestFixture]
    public class PickupTests
    {
        private static readonly int2 DefenderCell = new int2(5, 2);

        private static (BattleMatch m, Unit defender) Board(GimmickDef gimmick)
        {
            var def = CoreGimmickFixtures.With(CoreCombatFixtures.Definition(defenderDamage: 0f), gimmick);
            var m = new BattleMatch(def);
            m.Begin();
            m.Apply(Command.DebugSpawnDefender(0, DefenderCell));
            return (m, CoreCombatFixtures.First(m, UnitKind.Defender));
        }

        private static int Ticks(float seconds) => (int)math.ceil(seconds / BattleMatch.Dt);

        [Test]
        public void 안_먹힌_픽업은_수명이_끝나면_사라지고_만료_사건을_낸다()
        {
            var (m, _) = Board(CoreGimmickFixtures.RedBull(lifetime: 1f));
            var expired = CoreCombatFixtures.Listen(m, CoreEventKind.PickupExpired);
            Assert.IsTrue(m.Apply(Command.DebugSpawnPickup(new int2(0, 0))).Accepted);
            Assert.AreEqual(1, m.World.Pickups.Count);

            // 놓인 틱에는 안 깎인다(옛 스폰은 만료 패스 뒤에 섰다).
            m.Tick();
            Assert.AreEqual(1f, m.World.Pickups[0].Remaining, 0f, "놓인 틱엔 수명이 안 흐른다");
            CoreCombatFixtures.Tick(m, Ticks(1f) - 2);
            Assert.AreEqual(1, m.World.Pickups.Count, "아직 수명 안");
            CoreCombatFixtures.Tick(m, 3);   // 누적 오차 여유 한 틱
            Assert.AreEqual(0, m.World.Pickups.Count);
            Assert.AreEqual(1, expired.Count, "계약 7 — 만료도 소멸 사건을 낸다");
        }

        [Test]
        public void 증상_옆_칸을_스치는_유닛도_레드불을_먹는다_제약13_자()
        {
            // 옛 전투는 **같은 칸**이어야 먹었다. 새 코어는 「자리에 떨어지는 것(칸 반폭) + 내 몸」이다.
            // 몸 반경 0.5 유닛 중심에서 옆 칸 중심까지 1.0 = 0 + 0.5 + 0.5 → 닿는다.
            var (m, d) = Board(CoreGimmickFixtures.RedBull());
            var taken = CoreCombatFixtures.Listen(m, CoreEventKind.PickupTaken);
            m.Apply(Command.DebugSpawnPickup(DefenderCell + new int2(1, 0)));
            m.Tick();
            Assert.AreEqual(1, taken.Count, "옆 칸 — 몸이 닿는다");
            Assert.AreEqual(d.Id, taken[0].B);
            Assert.AreEqual(0, m.World.Pickups.Count);
        }

        [Test]
        public void 몸이_안_닿는_칸의_레드불은_안_먹는다()
        {
            var (m, _) = Board(CoreGimmickFixtures.RedBull());
            var taken = CoreCombatFixtures.Listen(m, CoreEventKind.PickupTaken);
            m.Apply(Command.DebugSpawnPickup(DefenderCell + new int2(1, 1)));   // 대각 √2 > 1
            m.Apply(Command.DebugSpawnPickup(DefenderCell + new int2(2, 0)));
            m.Tick();
            Assert.AreEqual(0, taken.Count);
            Assert.AreEqual(2, m.World.Pickups.Count);
        }

        [Test]
        public void 먹으면_공속이_오르고_라스트런이_끝나면_최대체력의_비율만큼_피해를_입는다()
        {
            var (m, d) = Board(CoreGimmickFixtures.RedBull(mul: 1.5f, duration: 1f, fraction: 0.5f));
            float maxHp = d.MaxHealth;
            m.Apply(Command.DebugSpawnPickup(DefenderCell));
            m.Tick();

            Assert.AreEqual(1.5f, d.Modifiers.Effective.AttackSpeedMul, 1e-5f, "공속 버프는 즉시");
            Assert.IsTrue(d.Progressive != null && d.Progressive.LastRunActive);
            Assert.AreEqual(maxHp, d.Health, 1e-4f, "crash 는 아직");

            CoreCombatFixtures.Tick(m, Ticks(1f) + 1);
            Assert.IsFalse(d.Progressive.LastRunActive);
            Assert.AreEqual(maxHp * 0.5f, d.Health, 1e-3f, "crash = 최대 체력 × 비율");
            Assert.AreEqual(1f, d.Modifiers.Effective.AttackSpeedMul, 1e-5f, "버프는 스스로 만료");
        }

        [Test]
        public void 라스트런_중인_유닛은_또_못_먹고_픽업은_판에_남는다()
        {
            var (m, d) = Board(CoreGimmickFixtures.RedBull(lifetime: 30f, duration: 1f));
            m.Apply(Command.DebugSpawnPickup(DefenderCell));
            m.Tick();
            Assert.IsTrue(d.Progressive.LastRunActive);

            m.Apply(Command.DebugSpawnPickup(DefenderCell));
            CoreCombatFixtures.Tick(m, 10);
            Assert.AreEqual(1, m.World.Pickups.Count, "재소비 락 — 밟아도 남는다");

            // crash 로 값을 치른 뒤에야 다시 먹는다.
            CoreCombatFixtures.Tick(m, Ticks(1f) + 2);
            Assert.AreEqual(0, m.World.Pickups.Count);
            Assert.IsTrue(d.Progressive.LastRunActive, "두 번째 라스트런");
        }

        [Test]
        public void 라스트런은_군중_제어로_안_멈추고_사망하면_같이_사라진다()
        {
            var pg = new ProgressiveStates();
            pg.BeginLastRun(1f, 0.5f);
            pg.Interrupt(ProgressInterrupt.Cc);
            Assert.IsTrue(pg.LastRunActive, "CC 로는 안 멈춘다");
            pg.Interrupt(ProgressInterrupt.Death);
            Assert.IsFalse(pg.LastRunActive, "사망이면 같이 사라진다");
            pg.BeginLastRun(1f, 0.5f);
            pg.Interrupt(ProgressInterrupt.Retire);
            Assert.IsFalse(pg.LastRunActive, "퇴근이면 같이 사라진다");
        }

        // ── 6c 후속 — 라스트런의 끝도 사건이다(계약 7) ────────────────────────
        //
        // 켜짐은 `PickupTaken` 이 알리는데 닫힘이 사건이 없으면 표식을 끄는 쪽이 정본 플래그를
        // 폴링한다(6c 의 임시 다리). 닫히는 경로마다 **정확히 한 건**인지 본다.

        private static System.Collections.Generic.List<CoreEvent> LastRunOn(out BattleMatch m, out Unit d,
                                                                              float duration = 1f)
        {
            var (mm, dd) = Board(CoreGimmickFixtures.RedBull(duration: duration));
            m = mm; d = dd;
            var ended = CoreCombatFixtures.Listen(m, CoreEventKind.LastRunEnded);
            m.Apply(Command.DebugSpawnPickup(DefenderCell));
            m.Tick();
            Assert.IsTrue(d.Progressive != null && d.Progressive.LastRunActive, "라스트런이 열려야 한다");
            return ended;
        }

        [Test]
        public void 라스트런이_시간으로_끝나면_crash_사유로_닫힘_사건이_한_번_난다()
        {
            var ended = LastRunOn(out var m, out var d);
            CoreCombatFixtures.Tick(m, Ticks(1f) + 5);
            Assert.AreEqual(1, ended.Count, "닫힘은 한 번");
            Assert.AreEqual(d.Id.Value, ended[0].A.Value);
            Assert.AreEqual((int)LastRunEndReason.Crash, ended[0].Arg);
        }

        [Test]
        public void 라스트런_중에_죽으면_사망_사유로_닫힘_사건이_한_번_난다()
        {
            var ended = LastRunOn(out var m, out var d, duration: 30f);
            d.Inbox.Damage.Add(new DamageEntry { Amount = d.MaxHealth * 10f, Source = SimEntityId.None });
            CoreCombatFixtures.Tick(m, 5);
            Assert.AreEqual(1, ended.Count, "사망에서 한 번 — 이어지는 제거에서 또 나지 않는다");
            Assert.AreEqual((int)LastRunEndReason.Death, ended[0].Arg);
        }

        [Test]
        public void 라스트런_중에_퇴근하면_퇴근_사유로_닫힘_사건이_한_번_난다()
        {
            var ended = LastRunOn(out var m, out var d, duration: 30f);
            var id = d.Id;
            Assert.IsTrue(m.Apply(Command.Retire(id)).Accepted, "퇴근 거절");
            CoreCombatFixtures.Tick(m, 2);
            Assert.AreEqual(1, ended.Count);
            Assert.AreEqual(id.Value, ended[0].A.Value);
            Assert.AreEqual((int)LastRunEndReason.Retire, ended[0].Arg);
        }

        [Test]
        public void 라스트런_중에_죽음도_퇴근도_아닌_제거면_제거_사유로_닫힘_사건이_한_번_난다()
        {
            var ended = LastRunOn(out var m, out var d, duration: 30f);
            Assert.IsTrue(m.Apply(Command.DebugDestroy(d.Id)).Accepted);
            CoreCombatFixtures.Tick(m, 2);
            Assert.AreEqual(1, ended.Count);
            Assert.AreEqual((int)LastRunEndReason.Removed, ended[0].Arg);
        }

        [Test]
        public void 레드불이_안_뽑힌_판에서는_픽업을_못_놓는다()
        {
            var (m, _) = Board(CoreGimmickFixtures.Onsen());
            var r = m.Apply(Command.DebugSpawnPickup(new int2(0, 0)));
            Assert.IsFalse(r.Accepted);
            Assert.AreEqual(RejectReason.GimmickInactive, r.Reason);
        }

        [Test]
        public void 시드_자리는_같은_시드면_같은_칸이고_동시_상한에서_멈춘다()
        {
            int2 First()
            {
                var (m, _) = Board(CoreGimmickFixtures.RedBull(maxActive: 2, lifetime: 30f));
                m.Apply(Command.DebugSpawnPickupSeeded());
                return m.World.Pickups[0].Cell;
            }
            Assert.AreEqual(First(), First(), "RngStreams.Pickup — 같은 시드 같은 자리");

            var (b, _) = Board(CoreGimmickFixtures.RedBull(maxActive: 2, lifetime: 30f));
            Assert.IsTrue(b.Apply(Command.DebugSpawnPickupSeeded()).Accepted);
            Assert.IsTrue(b.Apply(Command.DebugSpawnPickupSeeded()).Accepted);
            Assert.IsFalse(b.Apply(Command.DebugSpawnPickupSeeded()).Accepted, "동시 상한");
            Assert.AreNotEqual(b.World.Pickups[0].Cell, b.World.Pickups[1].Cell, "한 칸에 하나");
        }

        [Test]
        public void 헤드리스_하네스에서_커맨드_예약만으로_픽업이_선다()
        {
            var def = CoreGimmickFixtures.With(CoreCombatFixtures.Definition(), CoreGimmickFixtures.RedBull(lifetime: 30f));
            var schedule = new CommandSchedule().Add(3, Command.DebugSpawnPickup(new int2(0, 0)));
            var result = CoreHarness.Run(def, schedule, 10, "pickup_debug");
            Assert.AreEqual(1, result.Match.World.Pickups.Count);
            Assert.IsTrue(result.Trace.events.Exists(e => e.channel == CoreTraceChannel.PickupSpawned),
                          "트레이스 채널이 열려 있다");
        }
    }
}
