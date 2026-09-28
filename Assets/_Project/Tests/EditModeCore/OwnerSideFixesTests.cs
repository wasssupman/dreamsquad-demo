using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using Wassup.BattleCore;
using Wassup.BattleCore.Trigger;
using Wassup.Skills;
using static Wassup.Tests.EditMode.Core.CoreCardFixtures;

namespace Wassup.Tests.EditMode.Core
{
    // skill-data-table unit 2 — **소유자 쪽 결손.** 스킬은 소유자를 묻지 않고, 소유자마다 달라야 하는 값은 그 값의 담당자가 든다.
    //   · 주체 없는 시전(판 시전 · 판 주기 · 표식)의 진영 = 규칙 인스턴스의 시전 진영(`Binding.CastFaction` — 붙인 쪽이 채운다)
    //   · 표식의 정체 = 효과(`CardBindings.IsMarked` 가 출처 꼬리표를 묻지 않는다)
    //   · 「부착 즉시 첫 발동」은 카드 행 부착 경로 한정(`BindingRegistry.ArmFirstFireOnAttach`) — 유닛 저작 · 온천 위상은 그대로
    // ⚠ 여기 수치는 게임 값이 아니라 픽스처다.
    [TestFixture]
    public class OwnerSideFixesTests
    {
        // ── 시전 진영 = 규칙 인스턴스 값 ────────────────────────────────────

        [Test]
        public void 시전_진영은_붙인_쪽이_채운다_유닛_저작은_그_유닛_카드는_쓴_쪽()
        {
            var def = CoreMatchFixtures.Definition();
            CoreTriggerFixtures.GiveEnemy(def, 0, CoreTriggerFixtures.Probe(TriggerKind.OnKill, new CoreTriggerFixtures.ProbeSkill()));
            var mark = CardRule(TriggerKind.None, EffectKind.BountyMark);
            mark.Effect.Magnitude = 2f;
            mark.Rule.FireCap = 1;
            int card = AddAttachCard(def, "mark", 1, mark);
            def.Cards[card].TargetsEnemies = true;
            var m = CardBattle(def);
            var enemy = CoreTriggerFixtures.SpawnEnemy(m, new int2(5, 2));
            Assert.AreEqual(1, enemy.Bindings.Count);
            Assert.AreEqual(Faction.EnemyUnit, enemy.Bindings[0].CastFaction, "유닛 저작 = 그 유닛의 진영");

            var fired = CoreCombatFixtures.Listen(m, CoreEventKind.TriggerFired);
            Assert.IsTrue(m.Apply(Command.AttachCard(EntryOf(m, card), enemy.Id)).Accepted);
            var markRule = enemy.Bindings[enemy.Bindings.Count - 1];
            Assert.AreEqual(BattleMatch.PlayerFaction, markRule.CastFaction, "카드 = 손패의 주인(숙주가 적이어도)");
            Assert.AreEqual(1, fired.Count);
            Assert.AreEqual(BattleMatch.PlayerFaction, fired[0].Faction, "표식 발동 진영 = 규칙 인스턴스 값(오늘 = 플레이어)");
        }

        [Test]
        public void 판_주기_규칙의_시전_진영은_인스턴스_값에서_나온다()
        {
            var def = CoreMatchFixtures.Definition();
            var seen = new List<Faction>();
            var probe = new CoreTriggerFixtures.ProbeSkill { OnExecute = (c, t, p, ctx) => seen.Add(c.Faction) };
            var row = CoreTriggerFixtures.Probe(TriggerKind.PeriodicTimer, probe);
            row.Rule.PeriodSeconds = BattleMatch.Dt;
            int[] idx = CoreTriggerFixtures.Add(def, row, row);
            def.ConfigHash = def.ComputeConfigHash();
            var m = CoreMatchFixtures.BeginBattle(def);
            Assert.AreEqual(BattleMatch.PlayerFaction, m.Bindings.Attach(null, in def.Bindings[idx[0]], idx[0], m.Clock.Tick).CastFaction,
                            "판 호스트 기본 = 플레이어");
            m.Bindings.Attach(null, in def.Bindings[idx[1]], idx[1], m.Clock.Tick, Faction.EnemyUnit);

            m.Tick();

            CollectionAssert.AreEqual(new[] { BattleMatch.PlayerFaction, Faction.EnemyUnit }, seen,
                                      "감지자가 진영을 손으로 박지 않는다 — 붙인 쪽이 정한 값이 시전자 진영이다");
        }

        // ── 부착 즉시 첫 발동(카드 행 부착 경로 한정) ─────────────────────────

        private static RuleRow Periodic(CoreTriggerFixtures.ProbeSkill probe, bool card)
        {
            var r = card ? CardProbe(TriggerKind.PeriodicTimer, probe) : CoreTriggerFixtures.Probe(TriggerKind.PeriodicTimer, probe);
            r.Rule.PeriodSeconds = 1f;
            if (card) r.Effect.Kind = EffectKind.SelfOrbitProjectile;   // 숙주 모델과 무관한 payload(부착 판정 통과용)
            return r;
        }

        [Test]
        public void 같은_틱에_놓고_붙인_카드의_주기는_부착으로_시작하고_유닛_저작_주기는_스폰으로_시작한다()
        {
            var def = CoreMatchFixtures.Definition();
            def.Units[0].Cost = 0;
            var innate = new CoreTriggerFixtures.ProbeSkill();
            var carded = new CoreTriggerFixtures.ProbeSkill();
            CoreTriggerFixtures.GiveUnit(def, 0, Periodic(innate, card: false));
            int c = AddAttachCard(def, "spinner", 1, Periodic(carded, card: true));
            var m = CardBattle(def);

            Assert.AreEqual(RejectReason.None, m.Apply(Command.PlaceDefender(0, new int2(3, 1))).Reason, "배치");
            var host = CoreMatchFixtures.PlacedDefender(m);
            Assert.AreEqual(RejectReason.None, m.Apply(Command.AttachCard(EntryOf(m, c), host)).Reason, "같은 틱 부착");

            m.Tick();
            Assert.AreEqual(1, carded.Count, "카드 = 부착 즉시 첫 발동");
            Assert.AreEqual(0, innate.Count, "유닛 저작 = 스폰부터 한 주기");
            CoreCombatFixtures.Tick(m, 30);
            Assert.AreEqual(1, carded.Count);
            Assert.AreEqual(0, innate.Count);
            CoreCombatFixtures.Tick(m, 60);
            Assert.AreEqual(2, carded.Count, "그 뒤는 주기 그대로");
            Assert.AreEqual(1, innate.Count);
        }

        [Test]
        public void 온천_열기_위상은_같은_숙주에_카드_주기가_붙어도_그대로다()
        {
            var def = CoreGimmickFixtures.With(CoreCombatFixtures.Definition(defenderDamage: 0f), CoreGimmickFixtures.Onsen());
            var carded = new CoreTriggerFixtures.ProbeSkill();
            int c = AddAttachCard(def, "spinner", 1, Periodic(carded, card: true));
            var m = CardBattle(def);
            var heat = new List<CoreEvent>();
            m.Bus.Subscribe(CoreEventKind.GimmickTriggered, 0, e => { if (e.Arg == (int)GimmickKind.Onsen) heat.Add(e); });

            var withCard = Defender(m, new int2(2, 1));
            var control = Defender(m, new int2(4, 1));
            Assert.AreEqual(RejectReason.None, m.Apply(Command.AttachCard(EntryOf(m, c), withCard.Id)).Reason);
            CoreCombatFixtures.Tick(m, 120);

            Assert.Greater(carded.Count, 0, "카드 주기는 돈다");
            int first = heat.Find(e => e.A == withCard.Id).Tick;
            Assert.AreEqual(heat.Find(e => e.A == control.Id).Tick, first, "카드가 붙은 숙주의 열기 위상 = 카드 없는 숙주");
            Assert.Greater(first, 1, "열기는 부착 즉시 발동이 아니다(위상 보정 한 틱만)");
        }

        // ── 표식의 정체 = 효과 ─────────────────────────────────────────────

        [Test]
        public void 표식_판정은_효과로_가른다_출처를_묻지_않는다()
        {
            var def = CoreMatchFixtures.Definition();
            var m = CoreMatchFixtures.BeginBattle(def);
            var enemy = CoreTriggerFixtures.SpawnEnemy(m, new int2(5, 2));
            Assert.IsFalse(CardBindings.IsMarked(enemy));
            var other = CoreTriggerFixtures.Rule(TriggerKind.None, EffectKind.SelfStatBuff);
            other.Rule.Origin = BindingOrigin.Card;
            CoreTriggerFixtures.AttachRuntime(m, enemy, other);
            Assert.IsFalse(CardBindings.IsMarked(enemy), "카드 출처라도 표식 효과가 아니면 표식이 아니다");

            var mark = CoreTriggerFixtures.Rule(TriggerKind.None, EffectKind.BountyMark);
            Assert.AreNotEqual(BindingOrigin.Card, mark.Rule.Origin, "픽스처 전제 — 카드 출처가 아닌 표식 줄");
            CoreTriggerFixtures.AttachRuntime(m, enemy, mark);
            Assert.IsTrue(CardBindings.IsMarked(enemy), "표식 효과를 든 규칙 = 표식(출처 무관)");
        }
    }
}
