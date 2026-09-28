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
    }
}
