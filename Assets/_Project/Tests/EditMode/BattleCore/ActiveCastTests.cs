using NUnit.Framework;
using Unity.Mathematics;
using Somnia.Battle.BattleCore;
using Somnia.Battle.BattleCore.Trigger;
using Somnia.Battle.Skills;
using Somnia.Battle.Skills.Concrete;
using static Somnia.Battle.Tests.EditMode.Core.CoreCardFixtures;

namespace Somnia.Battle.Tests.EditMode.Core
{
    // battle-core-rebuild unit 7b — 액티브는 **시전자가 없다**. 칸을 조준하고 진영은 플레이어로 접힌다.
    [TestFixture]
    public class ActiveCastTests
    {
        private static RuleRow SlowField(float mul = 0.5f, float seconds = 3f, int tiles = 1)
        {
            var r = CardProbe(TriggerKind.None, new TileStatBurstSkill());
            r.Effect.StatKind = (int)SkillStatKind.MoveSpeedMul;
            r.Effect.Magnitude = mul; r.Effect.Duration = seconds; r.Effect.TileRange = tiles;
            return r;
        }

        [Test]
        public void 시전하면_이_커맨드_안에서_조준_칸의_적에게_걸린다()
        {
            var def = CoreMatchFixtures.Definition();
            int card = AddActiveCard(def, "slow_field", 20, 5f, SlowField());
            var m = CardBattle(def, awakening: 50f);
            var enemy = CoreTriggerFixtures.SpawnEnemy(m, new int2(5, 2));
            var cast = CoreCombatFixtures.Listen(m, CoreEventKind.CardCast);

            Assert.IsTrue(m.Apply(Command.CastActive(EntryOf(m, card), new int2(5, 2))).Accepted);

            Assert.AreEqual(0.5f, enemy.Modifiers.Effective.MoveSpeedMul, 1e-4f, "틱 없이 — Immediate seam");
            Assert.AreEqual(30f, m.Hand.Gauge, 1e-4f);
            Assert.AreEqual(1, cast.Count);
            Assert.AreEqual(SimEntityId.Match, cast[0].A, "시전자가 없다 — 판");
            Assert.AreEqual(1, m.Hand.QueueCount, "뒤로 재활용 — 풀을 안 떠난다");
            Assert.AreEqual(0, m.Bindings.MatchBindings.Count, "발동 1회 수명 — 떨어졌다");
        }

        [Test]
        public void 쿨다운은_판의_시계다_틱이_안_돌면_안_준다()
        {
            var def = CoreMatchFixtures.Definition();
            int card = AddActiveCard(def, "slow_field", 0, 2f, SlowField());
            var m = CardBattle(def);
            m.Apply(Command.CastActive(EntryOf(m, card), new int2(5, 2)));
            Assert.IsFalse(m.Hand.IsReady(card));

            // 슬로모·정지는 **틱 발행률**이다 — 틱이 안 오면 쿨다운도 안 흐른다(옛 벽시계는 흘렀다).
            Assert.IsFalse(m.Hand.IsReady(card));
            CoreCombatFixtures.Tick(m, 119);
            Assert.IsFalse(m.Hand.IsReady(card), "2초 = 120틱");
            m.Tick();
            Assert.IsTrue(m.Hand.IsReady(card));
        }

        [Test]
        public void 성사가_안_되면_차감도_대기도_재활용도_없다_포탈_같은_칸()
        {
            var def = CoreMatchFixtures.Definition();
            var portal = CardProbe(TriggerKind.None, new PortalSkill());
            portal.Effect.Duration = 4f;
            int card = AddActiveCard(def, "portal", 20, 5f, portal, twoCells: true);
            var m = CardBattle(def, awakening: 50f);
            int entry = EntryOf(m, card);

            Assert.AreEqual(RejectReason.SameCell, m.Apply(Command.CastActivePair(entry, new int2(3, 2), new int2(3, 2))).Reason);
            Assert.AreEqual(RejectReason.NeedsSecondCell, m.Apply(Command.CastActive(entry, new int2(3, 2))).Reason);
            Assert.AreEqual(50f, m.Hand.Gauge, 1e-4f);
            Assert.IsTrue(m.Hand.IsReady(card));
            Assert.AreEqual(entry, Hand(m)[0].EntryId);

            var fields = CoreCombatFixtures.Listen(m, CoreEventKind.FieldSpawned);
            Assert.IsTrue(m.Apply(Command.CastActivePair(entry, new int2(3, 2), new int2(8, 2))).Accepted);
            Assert.AreEqual(1, fields.Count, "입구→출구 장이 섰다");
            // unit 7d — 뷰가 두 소용돌이와 빔을 그리려면 출구가 사건에 값으로 실려야 한다(되묻지 않는다).
            Assert.AreEqual(m.Map.CenterOf(new int2(3, 2)), fields[0].SiteFired.Pos, "입구");
            Assert.AreEqual(m.Map.CenterOf(new int2(8, 2)), fields[0].SiteTarget.Pos, "출구");
            Assert.AreEqual(4f, fields[0].Amount, 1e-4f, "지속");
        }

        [Test]
        public void 배치_창에서는_시전이_안_된다()
        {
            var def = CoreMatchFixtures.Definition();
            int card = AddActiveCard(def, "slow_field", 0, 1f, SlowField());
            def.Mode.Awakening = new AwakeningDef { Start = 100f, Max = 100f };
            def.Mode.HandSize = def.Cards.Length;
            var m = new BattleMatch(def);
            m.Begin();

            Assert.AreEqual(RejectReason.NotRunningOrPlacementClosed,
                m.Apply(Command.CastActive(EntryOf(m, card), new int2(5, 2))).Reason, "옛 `CastSkillAtTile` 의 `!_running` 거절");
        }

        [Test]
        public void 디버그_시전은_손패_각성_대기를_건너뛴다()
        {
            var def = CoreMatchFixtures.Definition();
            int card = AddActiveCard(def, "slow_field", 99, 5f, SlowField());
            var m = CardBattle(def, awakening: 0f);
            var enemy = CoreTriggerFixtures.SpawnEnemy(m, new int2(5, 2));

            Assert.IsTrue(m.Apply(Command.DebugCastCard(card, new int2(5, 2))).Accepted);
            Assert.IsTrue(m.Apply(Command.DebugCastCard(card, new int2(5, 2))).Accepted, "대기 없음");
            Assert.AreEqual(0.5f, enemy.Modifiers.Effective.MoveSpeedMul, 1e-4f);
        }
    }
}
