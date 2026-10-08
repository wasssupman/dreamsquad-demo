using NUnit.Framework;
using Somnia.Battle.BattleCore;
using Somnia.Battle.BattleCore.Trigger;
using Somnia.Battle.Skills;
using static Somnia.Battle.Tests.EditMode.Core.CoreCardFixtures;
using Probe = Somnia.Battle.BattleCore.CardProbe;

namespace Somnia.Battle.Tests.EditMode.Core
{
    // battle-core-rebuild unit 7e — **카드 프로브 자체**가 고정구 정의표로 도는가(순수 C# — 헤드리스 lane).
    // 라이브 카드 전량은 Assets lane(`CardEffectWitnessTests`)이 본다. 여기는 장치가 ○ 와 × 를 가른다는 것만 못박는다.
    //
    // ⚠ 수치는 게임 값이 아니라 픽스처다.
    [TestFixture]
    public class CardProbeTests
    {
        private static MatchDefinition Deck(out int lastFlame, out int frostArrow, out int farewell)
        {
            var def = CoreMatchFixtures.Definition();
            var lf = CardRule(TriggerKind.None, EffectKind.SelfBuffLethal);
            lf.Effect.Magnitude = 1.9f;
            lf.Effect.Duration = 5f;
            lf.Rule.FireCap = 1;
            lastFlame = AddAttachCard(def, "fixture_last_flame", 1, lf);

            var fa = CardRule(TriggerKind.AttackN, EffectKind.ApplyCcToTarget);
            fa.Rule.Period = 3;
            fa.Effect.CcKind = (int)SkillCcKind.Stun;
            fa.Effect.Duration = 1f;
            frostArrow = AddAttachCard(def, "fixture_frost_arrow", 1, fa);

            var fw = CardRule(TriggerKind.OnDeath, EffectKind.SelfTileAoe);
            fw.Effect.Magnitude = 7f;
            fw.Effect.TileRange = 1;
            fw.Effect.DataIndex = CoreTriggerFixtures.AddBlastProjectile(def);
            farewell = AddAttachCard(def, "fixture_farewell", 1, fw);
            def.ConfigHash = def.ComputeConfigHash();
            return def;
        }

        [Test]
        public void 구워졌고_발동하고_그_종류의_효과가_걸리면_통과다()
        {
            var def = Deck(out int lastFlame, out int frostArrow, out int farewell);
            var a = Probe.Run(def, lastFlame);
            Assert.IsTrue(a.Ok, a.ToString());
            CollectionAssert.IsSubsetOf(new[] { "ApplyStatModifier", "StartLethalTimer" }, a.Witnessed, a.ToString());

            var b = Probe.Run(def, frostArrow);
            Assert.IsTrue(b.Ok, "대상형 공격 규칙은 감지자 모양의 사건으로 발동한다 — " + b);
            CollectionAssert.Contains(b.Witnessed, "ApplyCc");

            var c = Probe.Run(def, farewell);
            TestContext.WriteLine(Probe.FormatTable(new[] { a, b, c }));
            Assert.IsTrue(c.Ok, c.ToString());
            CollectionAssert.Contains(c.Witnessed, "SpawnProjectile");
        }

        [Test]
        public void 의도를_기록만_하고_적용하지_않으면_실패로_떨어진다_반증()
        {
            var def = Deck(out int lastFlame, out _, out int farewell);
            foreach (int card in new[] { lastFlame, farewell })
            {
                var r = Probe.Run(def, card, new CardProbeOptions { MuteIntents = true });
                TestContext.WriteLine(r.ToString());
                Assert.IsTrue(r.Baked && r.Fired, "구워졌고 발동은 했다 — " + r);
                Assert.IsFalse(r.Ok, "아무 일도 안 일어났는데 ○ 면 장치가 증언을 못 한다 — " + r);
                Assert.IsNotEmpty(r.Missing);
            }
        }

        [Test]
        public void 실행자가_없는_규칙은_굽기_실패다_그리고_원본_정의표는_무변이다()
        {
            var def = Deck(out int lastFlame, out _, out _);
            string hash = def.ComputeConfigHash();
            int row = def.Cards[lastFlame].Bindings[0];
            var effect = def.Bindings[row].Skill;

            Probe.RunAll(def);
            Assert.AreEqual(hash, def.ComputeConfigHash(), "프로브는 복사본에서 돈다");
            Assert.AreSame(effect, def.Bindings[row].Skill, "원본 규칙의 실행자를 감싸지 않았다");

            def.Bindings[row].Skill = null;
            var r = Probe.Run(def, lastFlame);
            Assert.IsFalse(r.Baked, r.ToString());
            Assert.IsFalse(r.Ok);
        }
    }
}
