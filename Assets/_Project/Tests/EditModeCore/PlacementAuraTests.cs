using NUnit.Framework;
using Unity.Mathematics;
using Wassup.BattleCore;
using Wassup.BattleCore.Effects;
using Wassup.BattleCore.Trigger;
using Wassup.Skills;
using Wassup.Skills.Concrete;
using static Wassup.Tests.EditMode.Core.CoreCardFixtures;

namespace Wassup.Tests.EditMode.Core
{
    // battle-core-rebuild unit 7b — 배치 오라는 **바인딩 둘**이다(정정 3 · H6). 회수가 비대칭이라 한 줄로 못 접는다.
    [TestFixture]
    public class PlacementAuraTests
    {
        // bake(`CardDefinitionBuilder`)가 `PlacementAura` 한 메커닉을 펴는 모양 그대로.
        private static int AuraCard(MatchDefinition def, float asMul, float sleepSec)
        {
            var speed = CardProbe(TriggerKind.OnPlace, new SelfStatBuffSkill());
            speed.Payload = EffectKind.PlacementAura;
            speed.Subject = BindingSubject.Any;
            speed.StatKind = (int)SkillStatKind.AttackSpeedMul;
            speed.Magnitude = asMul;
            speed.RevokeOnExpire = true;
            var sleep = CardProbe(TriggerKind.OnPlace, new PlacementSleepSkill());
            sleep.Payload = EffectKind.PlacementAura;
            sleep.Subject = BindingSubject.Any;
            sleep.Duration = sleepSec;
            sleep.RevokeOnExpire = false;
            return AddAttachCard(def, "slow_awakening", 1, speed, sleep);
        }

        [Test]
        public void 이미_있는_유닛과_숙주는_안_받고_새로_배치된_유닛만_받는다()
        {
            var def = CoreMatchFixtures.Definition();
            def.Units[0].Cost = 1;
            int card = AuraCard(def, 1.5f, 2f);
            var m = CardBattle(def);
            var host = Defender(m, new int2(3, 1));
            var old = Defender(m, new int2(5, 1));
            Assert.IsTrue(m.Apply(Command.AttachCard(EntryOf(m, card), host.Id)).Accepted);
            Assert.AreEqual(0, old.Modifiers.Count);
            Assert.AreEqual(0, host.Modifiers.Count);

            m.Apply(Command.PlaceDefender(0, new int2(7, 3)));
            var placed = m.World.Units[m.World.Units.Count - 1];
            CoreCombatFixtures.Tick(m, 3);

            Assert.AreEqual(1.5f, placed.Modifiers.Effective.AttackSpeedMul, 1e-4f);
            Assert.IsTrue(placed.Cc.IsActive(CcSlotKind.Sleep), "배치되자마자 잔다");
        }

        [Test]
        public void 숙주가_죽으면_공속은_소급_회수되고_수면은_안_걷힌다()
        {
            var def = CoreMatchFixtures.Definition();
            def.Units[0].Cost = 1;
            var second = CoreMatchFixtures.Defender("second");
            second.Cost = 1;
            def.Units = new[] { def.Units[0], second };   // 숙주 종류는 사망 대기에 걸린다 — 늦은 배치는 다른 종류로
            def.Roster = new[] { 0, 1 };
            int card = AuraCard(def, 1.5f, 5f);
            var m = CardBattle(def);
            var host = Defender(m, new int2(3, 1));
            m.Apply(Command.AttachCard(EntryOf(m, card), host.Id));
            m.Apply(Command.PlaceDefender(0, new int2(7, 3)));
            var placed = m.World.Units[m.World.Units.Count - 1];
            CoreCombatFixtures.Tick(m, 3);
            Assert.IsTrue(placed.Cc.IsActive(CcSlotKind.Sleep));

            host.Inbox.Damage.Add(new DamageEntry { Amount = 99999f, Source = SimEntityId.Match });
            CoreCombatFixtures.Tick(m, 3);

            Assert.IsNull(m.World.Find(host.Id));
            Assert.AreEqual(1f, placed.Modifiers.Effective.AttackSpeedMul, 1e-4f, "revokeOnExpire true — 소급 중화");
            Assert.IsTrue(placed.Cc.IsActive(CcSlotKind.Sleep), "revokeOnExpire false — 등록부에서만 빠진다");

            var r = m.Apply(Command.PlaceDefender(1, new int2(9, 3)));
            Assert.IsTrue(r.Accepted, r.Reason.ToString());
            var late = m.World.Units[m.World.Units.Count - 1];
            CoreCombatFixtures.Tick(m, 3);
            Assert.AreEqual(0, late.Modifiers.Count, "숙주가 떠난 뒤 배치분은 상속이 없다");
            Assert.IsFalse(late.Cc.IsActive(CcSlotKind.Sleep));
        }
    }
}
