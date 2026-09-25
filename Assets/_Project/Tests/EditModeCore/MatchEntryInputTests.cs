using NUnit.Framework;
using Unity.Mathematics;
using Wassup.BattleCore;

namespace Wassup.Tests.EditMode.Core
{
    // battle-core-rebuild unit 8b — **로비 입력이 정의표로 들어오는 칸 셋**(온보딩 첫 손패 · 보너스 억제 · 첫 유닛 체력 낮추기).
    // 판을 짓는 쪽(`MatchEntry` · `MatchDefinitionBuilder`)은 Unity 층이라 여기서는 코어가 그 칸을 «쓰는가» 만 본다.
    public sealed class MatchEntryInputTests
    {
        private static MatchDefinition DeckOf(int cards, int pinned, int seed)
        {
            var def = CoreMatchFixtures.Definition(seed);
            for (int i = 0; i < cards; i++)
            {
                var c = CardDef.Default();
                c.Id = "c" + i;
                c.Kind = CardKind.Attach;
                c.Cost = 1;
                CoreCardFixtures.AddCard(def, c);
            }
            def.PinnedHandFront = pinned;
            return def;
        }

        [Test]
        public void 고정한_앞_장은_어느_시드에서도_손패_앞에_그_순서로_선다()
        {
            for (int seed = 1; seed <= 12; seed++)
            {
                var def = DeckOf(8, 2, seed);
                var m = CoreCardFixtures.CardBattle(def);
                var hand = CoreCardFixtures.Hand(m);
                Assert.GreaterOrEqual(hand.Count, 2);
                Assert.AreEqual(0, hand[0].CardIndex, $"seed {seed}");
                Assert.AreEqual(1, hand[1].CardIndex, $"seed {seed}");
            }
        }

        [Test]
        public void 고정_0_이면_섞기가_그대로다_기존_판과_같은_순열()
        {
            var a = CoreCardFixtures.Hand(CoreCardFixtures.CardBattle(DeckOf(8, 0, 5)));
            var b = CoreCardFixtures.Hand(CoreCardFixtures.CardBattle(DeckOf(8, 0, 5)));
            Assert.AreEqual(a.Count, b.Count);
            for (int i = 0; i < a.Count; i++) Assert.AreEqual(a[i].CardIndex, b[i].CardIndex);
        }

        [Test]
        public void 기본값이면_해시_입력에_안_실린다_골든_무변()
        {
            var def = CoreMatchFixtures.Definition();
            string before = def.CanonicalText();
            Assert.IsFalse(before.Contains("pinnedHandFront"));
            Assert.IsFalse(before.Contains("bonusPullSuppressed"));
            def.PinnedHandFront = 3;
            def.BonusPullSuppressed = true;
            string after = def.CanonicalText();
            StringAssert.Contains("pinnedHandFront=3", after);
            StringAssert.Contains("bonusPullSuppressed=1", after);
        }

        [Test]
        public void 정의표의_억제는_판_시작에_담당자로_들어간다()
        {
            var def = CoreMatchFixtures.Definition();
            def.BonusPullSuppressed = true;
            var m = CoreMatchFixtures.BeginBattle(def);
            Assert.IsTrue(m.Waves.BonusPullSuppressed);

            var off = CoreMatchFixtures.BeginBattle(CoreMatchFixtures.Definition());
            Assert.IsFalse(off.Waves.BonusPullSuppressed, "정의표가 안 켠 판은 억제가 없다(지난 판의 값을 물려받지 않는다)");
        }

        [Test]
        public void 최대_체력_비율_피해는_출처_없이_다음_피해_단계에서_깎인다()
        {
            var def = CoreMatchFixtures.Definition();
            var m = CoreMatchFixtures.BeginBattle(def);
            var u = CoreCardFixtures.Defender(m, new int2(2, 1));
            float max = u.MaxHealth;
            Assert.Greater(max, 0f);

            var r = m.Apply(Command.DamageMaxHealthRatio(u.Id, 0.9f));
            Assert.IsTrue(r.Accepted);
            m.Tick();
            Assert.AreEqual(max * 0.1f, u.Health, max * 1e-4f, "최대 × 0.9 가 깎여 10% 가 남는다");
            Assert.IsFalse(u.Dead);
        }

        [Test]
        public void 비율_0_이나_없는_대상은_거절된다()
        {
            var m = CoreMatchFixtures.BeginBattle(CoreMatchFixtures.Definition());
            var u = CoreCardFixtures.Defender(m, new int2(2, 1));
            Assert.IsFalse(m.Apply(Command.DamageMaxHealthRatio(u.Id, 0f)).Accepted);
            Assert.IsFalse(m.Apply(Command.DamageMaxHealthRatio(new SimEntityId(99999), 0.5f)).Accepted);
        }
    }
}
