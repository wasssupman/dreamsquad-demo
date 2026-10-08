using NUnit.Framework;
using Unity.Mathematics;
using Somnia.Battle.BattleCore;
using Somnia.Battle.BattleCore.Trigger;
using static Somnia.Battle.Tests.EditMode.Core.CoreCardFixtures;

namespace Somnia.Battle.Tests.EditMode.Core
{
    // battle-core-rebuild unit 7b — 살찌운 제물. `fireCap 1` + **표식된 적이 사라질 때까지 부착**(정정 5 · M9).
    [TestFixture]
    public class BountyMarkTests
    {
        private static int Bounty(MatchDefinition def, float rewardMul = 3f, float dmgTakenMul = 0.7f)
        {
            var rule = CardRule(TriggerKind.None, EffectKind.BountyMark);
            rule.Effect.Magnitude = rewardMul;
            rule.Effect.HitThreshold = dmgTakenMul;   // bake 가 「받는 피해 −30%」를 배율로 싣는다
            rule.Rule.FireCap = 1;
            int c = AddAttachCard(def, "fattened_offering", 20, rule);
            def.Cards[c].TargetsEnemies = true;
            return c;
        }

        [Test]
        public void 두_효과는_원자로_걸리고_부착은_소멸까지_유지된다()
        {
            var def = CoreMatchFixtures.Definition();
            int card = Bounty(def);
            var m = CardBattle(def, awakening: 50f);
            var enemy = CoreTriggerFixtures.SpawnEnemy(m, new int2(5, 2));

            Assert.IsTrue(m.Apply(Command.AttachCard(EntryOf(m, card), enemy.Id)).Accepted);

            Assert.AreEqual(3f, enemy.AwakeningRewardMul, 1e-4f, "각성 배율");
            Assert.AreEqual(0.7f, enemy.Modifiers.Effective.DmgTakenMul, 1e-4f, "받는 피해 감소 — 같은 커맨드 안에서");
            Assert.IsTrue(CardBindings.IsMarked(enemy), "1회 발동 뒤에도 붙어 있다(수명 ≠ 발동 상한)");
            Assert.AreEqual(1, m.Hand.OutOfPoolCount);
        }

        [Test]
        public void 증상_표식_붙인_적을_잡으면_각성이_배로_들어오고_카드가_손패로_돌아온다()
        {
            var def = CoreMatchFixtures.Definition();
            int card = Bounty(def);
            var m = CardBattle(def, awakening: 20f);
            var enemy = CoreTriggerFixtures.SpawnEnemy(m, new int2(5, 2));
            m.Apply(Command.AttachCard(EntryOf(m, card), enemy.Id));
            Assert.AreEqual(0f, m.Hand.Gauge, 1e-4f);
            Assert.AreEqual(0, m.Hand.QueueCount);

            enemy.Inbox.Damage.Add(new DamageEntry { Amount = 99999f, Source = SimEntityId.Match });
            CoreCombatFixtures.Tick(m, 3);

            Assert.AreEqual(2f * 3f, m.Hand.Gauge, 1e-4f, "처치 보상 2 × 배율 3");
            Assert.AreEqual(1, m.Hand.QueueCount, "카드가 큐로 돌아왔다");
            Assert.AreEqual(0, m.Hand.OutOfPoolCount);
        }

        [Test]
        public void 방어유닛에는_안_붙고_같은_적에_두_번_못_붙인다_값은_안_치른다()
        {
            var def = CoreMatchFixtures.Definition();
            int a = Bounty(def);
            int b = Bounty(def);
            var m = CardBattle(def, awakening: 100f);
            var d = Defender(m, new int2(3, 1));
            var enemy = CoreTriggerFixtures.SpawnEnemy(m, new int2(5, 2));

            Assert.AreEqual(RejectReason.NotAnEnemy, m.Apply(Command.AttachCard(EntryOf(m, a), d.Id)).Reason);
            Assert.IsTrue(m.Apply(Command.AttachCard(EntryOf(m, a), enemy.Id)).Accepted);
            float gauge = m.Hand.Gauge;
            Assert.AreEqual(RejectReason.DuplicateState, m.Apply(Command.AttachCard(EntryOf(m, b), enemy.Id)).Reason,
                "이중 표식 — 배율이 두 번 곱해진다");
            Assert.AreEqual(gauge, m.Hand.Gauge, 1e-4f);
            Assert.AreEqual(3f, enemy.AwakeningRewardMul, 1e-4f);
        }

        [Test]
        public void 표식은_부착_상한_밖이다()
        {
            var def = CoreMatchFixtures.Definition();
            var rule = CardRule(TriggerKind.OnKill, EffectKind.SelfStatBuff);
            rule.Effect.Magnitude = 1.1f;
            int u1 = AddAttachCard(def, "u1", 1, rule);
            int mark = Bounty(def);
            def.Mode.AttachCap = 1;
            var m = CardBattle(def, awakening: 100f);
            var d = Defender(m, new int2(3, 1));
            var enemy = CoreTriggerFixtures.SpawnEnemy(m, new int2(5, 2));
            m.Apply(Command.AttachCard(EntryOf(m, u1), d.Id));
            Assert.IsFalse(m.Hand.CanAttachMore(d.Id));

            Assert.IsTrue(m.Apply(Command.AttachCard(EntryOf(m, mark), enemy.Id)).Accepted, "D14 — 상한은 방어유닛 손패 규칙");
        }

        [Test]
        public void 유출된_표식_적은_보상_없이_카드만_돌아온다()
        {
            var def = CoreMatchFixtures.Definition();
            int card = Bounty(def);
            var m = CardBattle(def, awakening: 20f);
            var enemy = CoreTriggerFixtures.SpawnEnemy(m, new int2(5, 2));
            m.Apply(Command.AttachCard(EntryOf(m, card), enemy.Id));

            m.Apply(Command.DebugDestroy(enemy.Id));   // 처치가 아닌 소멸(유출과 같은 축 — 귀속된 죽음 없음)
            m.Tick();

            Assert.AreEqual(0f, m.Hand.Gauge, 1e-4f, "처치 사건이 없으면 보상도 없다");
            Assert.AreEqual(1, m.Hand.QueueCount);
        }
    }
}
