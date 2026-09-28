using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using Wassup.BattleCore;

namespace Wassup.Tests.EditMode.Core
{
    // battle-core-rebuild unit 4 — 드림캐쳐의 **자원**(효과는 unit 7).
    [TestFixture]
    public class MatchHandDeckTests
    {
        // 부착 10 + 공용 액티브 2 = 12. 마지막 부착 카드가 「인수인계」를 선언한다.
        // unit 7b — 카드는 **규칙을 실어야** 붙는다(규칙 0 줄 카드는 옛 `attached == 0` 처럼 거절된다).
        // 자원 테스트라 규칙은 무해한 한 줄(처치 × 자기 버프)이고, 액티브는 무동작 한 줄(배율 1 = 조용히 소모)이다.
        private static CardDef[] Cards(MatchDefinition def)
        {
            var cards = new CardDef[12];
            var attachRule = CoreCardFixtures.CardRule(Wassup.BattleCore.Trigger.TriggerKind.OnKill,
                                                       Wassup.BattleCore.Trigger.EffectKind.SelfStatBuff);
            attachRule.Effect.StatKind = (int)Wassup.Skills.SkillStatKind.DamageMul;
            attachRule.Effect.Magnitude = 1f;
            int attachRow = CoreTriggerFixtures.Add(def, attachRule)[0];
            var activeRule = CoreCardFixtures.CardProbe(Wassup.BattleCore.Trigger.TriggerKind.None,
                                                        new Wassup.Skills.Concrete.TileStatBurstSkill());
            activeRule.Effect.Magnitude = 1f;
            int activeRow = CoreTriggerFixtures.Add(def, activeRule)[0];
            for (int i = 0; i < 10; i++)
            {
                cards[i] = CardDef.Default();
                cards[i].Id = "attach_" + i;
                cards[i].Kind = CardKind.Attach;
                cards[i].Cost = 15;
                cards[i].DeclaresRetireRecall = i == 9;
                cards[i].Bindings = new[] { attachRow };
            }
            for (int i = 10; i < 12; i++)
            {
                cards[i] = CardDef.Default();
                cards[i].Id = "active_" + i;
                cards[i].Kind = CardKind.Active;
                cards[i].Cost = 20;
                cards[i].CooldownSeconds = 2f;
                cards[i].ActiveBinding = activeRow;
            }
            return cards;
        }

        private static BattleMatch Battle(System.Action<MatchDefinition> tweak = null)
        {
            var def = CoreMatchFixtures.Definition();
            def.Cards = Cards(def);
            def.Mode.Awakening = new AwakeningDef { Start = 60f, Max = 100f };
            tweak?.Invoke(def);
            def.ConfigHash = def.ComputeConfigHash();
            return CoreMatchFixtures.BeginBattle(def);
        }

        private static List<HandDeck.Entry> Hand(BattleMatch match)
        {
            var hand = new List<HandDeck.Entry>();
            match.Hand.Hand(hand);
            return hand;
        }

        private static int FirstOfKind(BattleMatch match, CardKind kind)
        {
            foreach (var e in Hand(match))
                if (match.Definition.Cards[e.CardIndex].Kind == kind) return e.EntryId;
            return -1;
        }

        [Test]
        public void 덱은_12장이고_손패는_앞_N_이다()
        {
            var match = Battle();
            Assert.AreEqual(12, match.Hand.QueueCount);
            Assert.AreEqual(5, Hand(match).Count, "손패 = 큐 앞 N");
            Assert.AreEqual(60f, match.Hand.Gauge, 1e-4f);
            Assert.AreEqual(100f, match.Hand.GaugeMax, 1e-4f);
        }

        [Test]
        public void 같은_시드는_같은_순열이다()
        {
            var a = Hand(Battle());
            var b = Hand(Battle());
            for (int i = 0; i < a.Count; i++) Assert.AreEqual(a[i].EntryId, b[i].EntryId);

            var c = Hand(Battle(d => d.Seed = 4242));
            bool differs = false;
            for (int i = 0; i < a.Count; i++) if (a[i].EntryId != c[i].EntryId) differs = true;
            Assert.IsTrue(differs, "시드가 다르면 순열이 갈린다");
        }

        [Test]
        public void 부착은_풀에서_이탈하고_값은_나중에_치른다()
        {
            var match = Battle();
            match.Apply(Command.PlaceDefender(0, new int2(3, 1)));
            var host = CoreMatchFixtures.PlacedDefender(match);
            int entry = FirstOfKind(match, CardKind.Attach);

            Assert.IsTrue(match.Apply(Command.AttachCard(entry, host)).Accepted);
            Assert.AreEqual(11, match.Hand.QueueCount, "부착 카드는 풀에서 이탈한다");
            Assert.AreEqual(1, match.Hand.OutOfPoolCount);
            Assert.AreEqual(1, match.Hand.CountAttachedTo(host));
            Assert.AreEqual(45f, match.Hand.Gauge, 1e-4f, "60 − 15");
        }

        [Test]
        public void 각성이_모자라면_부착도_차감도_없다()
        {
            var match = Battle(d => d.Mode.Awakening = new AwakeningDef { Start = 5f, Max = 100f });
            match.Apply(Command.PlaceDefender(0, new int2(3, 1)));
            int entry = FirstOfKind(match, CardKind.Attach);

            Assert.AreEqual(RejectReason.InsufficientAwakening,
                match.Apply(Command.AttachCard(entry, CoreMatchFixtures.PlacedDefender(match))).Reason);
            Assert.AreEqual(12, match.Hand.QueueCount, "실패한 부착은 순환도 차감도 하지 않는다");
            Assert.AreEqual(5f, match.Hand.Gauge, 1e-4f);
        }

        [Test]
        public void 부착_상한은_유닛당_셋이다()
        {
            var match = Battle(d => d.Mode.Awakening = new AwakeningDef { Start = 100f, Max = 100f });
            match.Apply(Command.PlaceDefender(0, new int2(3, 1)));
            var host = CoreMatchFixtures.PlacedDefender(match);

            int attached = 0;
            for (int guard = 0; guard < 12 && attached < 4; guard++)
            {
                int entry = FirstOfKind(match, CardKind.Attach);
                if (entry < 0) break;
                var r = match.Apply(Command.AttachCard(entry, host));
                if (r.Accepted) { attached++; continue; }
                Assert.AreEqual(RejectReason.AttachCapReached, r.Reason);
                break;
            }
            Assert.AreEqual(3, attached);
            Assert.IsFalse(match.Hand.CanAttachMore(host));
        }

        [Test]
        public void 액티브는_부착_경로로_못_간다()
        {
            var match = Battle(d => d.Mode.HandSize = 12);   // 액티브가 손패에 확실히 들어오게
            match.Apply(Command.PlaceDefender(0, new int2(3, 1)));
            int active = FirstOfKind(match, CardKind.Active);
            Assert.GreaterOrEqual(active, 0);

            Assert.AreEqual(RejectReason.WrongCardKind,
                match.Apply(Command.AttachCard(active, CoreMatchFixtures.PlacedDefender(match))).Reason);
        }

        [Test]
        public void 액티브는_성공하면_뒤로_재활용되고_대기가_걸린다()
        {
            var match = Battle(d => d.Mode.HandSize = 12);   // 액티브가 손패에 확실히 들어오게
            int active = FirstOfKind(match, CardKind.Active);
            Assert.GreaterOrEqual(active, 0);
            int cardIndex = -1;
            foreach (var e in Hand(match)) if (e.EntryId == active) cardIndex = e.CardIndex;

            Assert.IsTrue(match.Apply(Command.CastActive(active)).Accepted);
            Assert.AreEqual(12, match.Hand.QueueCount, "덱 뒤로 재활용 — 풀을 떠나지 않는다");
            Assert.AreEqual(40f, match.Hand.Gauge, 1e-4f, "60 − 20");
            Assert.IsFalse(match.Hand.IsReady(cardIndex));

            Assert.AreEqual(RejectReason.CardOnCooldown,
                match.Apply(Command.CastActive(active)).Reason);

            for (int t = 0; t < 121; t++) match.Tick();
            Assert.IsTrue(match.Hand.IsReady(cardIndex), "2초는 판의 시계로 잰다");
        }

        [Test]
        public void 손패_밖_카드는_못_쓴다()
        {
            var match = Battle();
            match.Apply(Command.PlaceDefender(0, new int2(3, 1)));

            var inHand = new HashSet<int>();
            foreach (var e in Hand(match)) inHand.Add(e.EntryId);
            int outside = -1;
            for (int i = 0; i < 12; i++) if (!inHand.Contains(i)) { outside = i; break; }

            Assert.GreaterOrEqual(outside, 0);
            Assert.AreEqual(RejectReason.CardNotInHand,
                match.Apply(Command.AttachCard(outside, CoreMatchFixtures.PlacedDefender(match))).Reason);
        }

        [Test]
        public void 숙주가_죽으면_카드가_큐_맨_뒤로_돌아온다()
        {
            var match = Battle();
            match.Apply(Command.PlaceDefender(0, new int2(3, 1)));
            var host = CoreMatchFixtures.PlacedDefender(match);
            int entry = FirstOfKind(match, CardKind.Attach);
            match.Apply(Command.AttachCard(entry, host));

            var u = match.World.Find(host);
            u.Inbox.Damage.Add(new DamageEntry { Amount = 99999f, Source = SimEntityId.Match });
            match.Tick();
            match.Tick();

            Assert.AreEqual(12, match.Hand.QueueCount);
            Assert.AreEqual(0, match.Hand.AttachedCount);
            var hand = Hand(match);
            foreach (var e in hand)
                Assert.AreNotEqual(entry, e.EntryId, "기본은 맨 뒤 — 떠난 순서 = 돌아오는 순서");
        }

        [Test]
        public void 인수인계는_퇴근에서만_앞으로_당긴다()
        {
            var match = Battle(d =>
            {
                d.Mode.Awakening = new AwakeningDef { Start = 100f, Max = 100f };
                d.Mode.HandSize = 12;   // 12장 전부 손패에 둬서 선언 카드를 확실히 붙인다
            });
            match.Apply(Command.PlaceDefender(0, new int2(3, 1)));
            var host = CoreMatchFixtures.PlacedDefender(match);

            // 선언 카드(9번)와 평범한 카드 둘을 붙인다.
            int declaring = EntryOfCard(match, 9);
            int plain = EntryOfCard(match, 0);
            Assert.IsTrue(match.Apply(Command.AttachCard(plain, host)).Accepted);
            Assert.IsTrue(match.Apply(Command.AttachCard(declaring, host)).Accepted);

            Assert.IsTrue(match.Apply(Command.Retire(host)).Accepted);

            var hand = Hand(match);
            Assert.AreEqual(plain, hand[0].EntryId, "부착 순서 그대로 큐 맨 앞으로 온다");
            Assert.AreEqual(declaring, hand[hand.Count - 1].EntryId,
                "선언 카드 자신은 맨 뒤 — 자기가 연 문으로 자기가 먼저 들어가지 않는다");
        }

        [Test]
        public void 퇴근은_각성을_주지_않는다()
        {
            var match = Battle();
            match.Apply(Command.PlaceDefender(0, new int2(3, 1)));
            float before = match.Hand.Gauge;

            match.Apply(Command.Retire(CoreMatchFixtures.PlacedDefender(match)));
            match.Tick();

            Assert.AreEqual(before, match.Hand.Gauge, 1e-4f,
                "주면 배치→퇴근 반복이 게이지 파밍이 된다(D7)");
        }

        [Test]
        public void 처치와_사망은_각성을_준다()
        {
            var match = Battle(d => d.Mode.Awakening = new AwakeningDef { Start = 0f, Max = 100f });
            for (int t = 0; t < 120; t++) match.Tick();

            // 적 하나를 잡는다 — 각성 보상 2.
            foreach (var u in match.World.Units)
                if (u.Faction == Wassup.Skills.Faction.EnemyUnit && !u.Dead)
                {
                    u.Inbox.Damage.Add(new DamageEntry { Amount = 99999f, Source = SimEntityId.Match });
                    break;
                }
            match.Tick();
            Assert.AreEqual(2f, match.Hand.Gauge, 1e-4f);

            // 방어유닛이 죽으면 그 유닛의 보상(4).
            match.Apply(Command.PlaceDefender(0, new int2(3, 1)));
            var d0 = match.World.Find(new SimEntityId(match.World.Units[match.World.Units.Count - 1].Id.Value));
            d0.Inbox.Damage.Add(new DamageEntry { Amount = 99999f, Source = SimEntityId.Match });
            match.Tick();
            Assert.AreEqual(6f, match.Hand.Gauge, 1e-4f);
        }

        [Test]
        public void 상한을_넘은_각성은_소멸하고_그_손실이_세어진다()
        {
            var match = Battle(d => d.Mode.Awakening = new AwakeningDef { Start = 98f, Max = 100f });
            Assert.AreEqual(2f, match.Hand.Gain(10f), 1e-4f);
            Assert.AreEqual(100f, match.Hand.Gauge, 1e-4f);
            Assert.AreEqual(8f, match.Hand.OverflowLost, 1e-4f,
                "화면이 「넘쳤다」를 알릴 근거가 없으면 플레이어는 안 받은 줄 안다");
        }

        // unit 7c — 손패 화면의 딤·드래그 게이트는 코어 preflight 를 읽는다(뷰가 `gauge >= cost` 를 다시 세지 않게).
        // 그 preflight 가 **커밋과 같은 답**을 내는지를 건다 — 갈리면 「밝은 카드인데 거절」이 돌아온다.
        [Test]
        public void 쓸_수_있나_preflight_는_커밋과_같은_답이다()
        {
            var match = Battle(d => { d.Mode.HandSize = 12; d.Mode.Awakening = new AwakeningDef { Start = 5f, Max = 100f }; });
            match.Apply(Command.PlaceDefender(0, new int2(3, 1)));
            var host = CoreMatchFixtures.PlacedDefender(match);
            int attach = FirstOfKind(match, CardKind.Attach);
            int active = FirstOfKind(match, CardKind.Active);

            Assert.AreEqual(RejectReason.InsufficientAwakening, match.Hand.UsableReason(attach));
            Assert.AreEqual(match.Hand.UsableReason(attach), match.Apply(Command.AttachCard(attach, host)).Reason);
            Assert.AreEqual(match.Hand.UsableReason(active), match.Apply(Command.CastActive(active)).Reason);
            Assert.AreEqual(RejectReason.CardNotInHand, match.Hand.UsableReason(9999));

            match.Hand.Gain(95f);
            Assert.AreEqual(RejectReason.None, match.Hand.UsableReason(active));
            Assert.IsTrue(match.Apply(Command.CastActive(active)).Accepted);
            Assert.AreEqual(RejectReason.CardOnCooldown, match.Hand.UsableReason(active),
                "대기가 각성보다 먼저다(커밋 순서)");
            Assert.AreEqual(match.Hand.UsableReason(active), match.Apply(Command.CastActive(active)).Reason);
            Assert.AreEqual(RejectReason.None, match.Hand.UsableReason(attach));
        }

        private static int EntryOfCard(BattleMatch match, int cardIndex)
        {
            foreach (var e in Hand(match)) if (e.CardIndex == cardIndex) return e.EntryId;
            return -1;
        }
    }
}
