using NUnit.Framework;
using Unity.Mathematics;
using Wassup.BattleCore;
using Wassup.BattleCore.Trigger;
using static Wassup.Tests.EditMode.Core.CoreCardFixtures;

namespace Wassup.Tests.EditMode.Core
{
    // battle-core-rebuild unit 7b — 인수인계는 바인딩 effect 가 아니라 **퇴근 회수 규칙의 일부**다(표현 불가 2).
    [TestFixture]
    public class RetireRecallTests
    {
        private static (BattleMatch m, Unit host, int plainA, int plainB, int handover) Setup()
        {
            var def = CoreMatchFixtures.Definition();
            var rule = CardRule(TriggerKind.OnKill, EffectKind.SelfStatBuff);
            rule.Magnitude = 1.1f;
            int a = AddAttachCard(def, "plain_a", 1, rule);
            int b = AddAttachCard(def, "plain_b", 1, rule);
            var hv = CardDef.Default();
            hv.Id = "handover"; hv.Kind = CardKind.Attach; hv.Cost = 1; hv.DeclaresRetireRecall = true;
            int h = AddCard(def, hv);
            // 뒤를 채울 카드 둘(돌아온 카드가 앞/뒤 어디에 섰는지 보이게).
            AddAttachCard(def, "filler_1", 1, rule);
            AddAttachCard(def, "filler_2", 1, rule);
            def.Mode.AttachCap = 3;
            var m = CardBattle(def, awakening: 100f);
            var host = Defender(m, new int2(3, 1));
            // 부착 순서: b → handover → a (부착 순서 자체가 기능이다 — D24)
            Assert.IsTrue(m.Apply(Command.AttachCard(EntryOf(m, b), host.Id)).Accepted);
            Assert.IsTrue(m.Apply(Command.AttachCard(EntryOf(m, h), host.Id)).Accepted);
            Assert.IsTrue(m.Apply(Command.AttachCard(EntryOf(m, a), host.Id)).Accepted);
            return (m, host, a, b, h);
        }

        [Test]
        public void 증상_인수인계_카드를_든_유닛을_퇴근시키면_그_유닛의_다른_카드가_손패_맨_앞에_온다()
        {
            var (m, host, a, b, h) = Setup();
            var detached = CoreCombatFixtures.Listen(m, CoreEventKind.CardDetached);

            Assert.IsTrue(m.Apply(Command.Retire(host.Id)).Accepted);

            var hand = Hand(m);
            Assert.AreEqual(b, hand[0].CardIndex, "부착 순서 그대로 — 먼저 붙인 것이 맨 앞");
            Assert.AreEqual(a, hand[1].CardIndex);
            Assert.AreEqual(h, hand[hand.Count - 1].CardIndex, "선언 카드 자신은 맨 뒤");
            Assert.AreEqual(3, detached.Count, "카드마다 떨어짐 사건 1건");
        }

        [Test]
        public void 사망_경로에는_안_붙는다()
        {
            var (m, host, a, b, h) = Setup();
            host.Inbox.Damage.Add(new DamageEntry { Amount = 99999f, Source = SimEntityId.Match });
            CoreCombatFixtures.Tick(m, 3);

            var hand = Hand(m);
            Assert.AreNotEqual(b, hand[0].CardIndex, "죽음에서는 맨 앞으로 당기지 않는다 — 전부 맨 뒤");
            Assert.AreEqual(b, hand[hand.Count - 3].CardIndex, "떠난 순서(부착 순) 그대로 뒤에 붙는다");
            Assert.AreEqual(h, hand[hand.Count - 2].CardIndex);
            Assert.AreEqual(a, hand[hand.Count - 1].CardIndex);
        }
    }
}
