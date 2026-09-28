using System.Collections.Generic;
using NUnit.Framework;
using Wassup.BattleCore;
using Wassup.BattleCore.Trigger;
using Wassup.Skills;
using static Wassup.Tests.EditMode.Core.CoreCardFixtures;

namespace Wassup.Tests.EditMode.Core
{
    // skill-data-table unit 1a·1b — **효과 표**(규칙 줄 → 효과 줄) 계약. 효과 값의 정본은 효과 줄 하나이고, 해시는 **해석된
    // 값**만 본다(README 계약 8 — 효과 id · 표 순서는 해시 밖). 라이브 에셋의 굽기 동치는 Assets lane 의 굽기 스냅샷(파일 diff 0)이 본다.
    //
    // ⚠ 수치는 게임 값이 아니라 픽스처다.
    [TestFixture]
    public class EffectTableTests
    {
        private static MatchDefinition Deck()
        {
            var def = CoreMatchFixtures.Definition();
            var lf = CardRule(TriggerKind.None, EffectKind.SelfBuffLethal);
            lf.Effect.Magnitude = 1.9f;
            lf.Effect.Duration = 5f;
            lf.Rule.FireCap = 1;
            AddAttachCard(def, "fixture_last_flame", 1, lf);

            var fw = CardRule(TriggerKind.OnDeath, EffectKind.SelfTileAoe);
            fw.Effect.Magnitude = 7f;
            fw.Effect.TileRange = 1;
            fw.Effect.DataIndex = CoreTriggerFixtures.AddBlastProjectile(def);
            AddAttachCard(def, "fixture_farewell", 1, fw);

            AddSquadCard(def, "fixture_squad", 1, SkillStatKind.DamageMul, 1.5f);
            return def;
        }

        [Test]
        public void 효과_id_와_표_순서는_해시_밖이다()
        {
            var a = Deck();
            var b = Deck();
            // 효과 표를 거꾸로 세우고 id 를 바꾼다 — 규칙 줄이 가리키는 **값**은 그대로다.
            int n = b.Effects.Length;
            var reversed = new EffectDef[n];
            for (int i = 0; i < n; i++) { reversed[n - 1 - i] = b.Effects[i]; reversed[n - 1 - i].Id = "other." + i; }
            b.Effects = reversed;
            for (int r = 0; r < b.Bindings.Length; r++) b.Bindings[r].EffectIndex = n - 1 - b.Bindings[r].EffectIndex;

            Assert.AreEqual(a.CanonicalText(), b.CanonicalText());
            Assert.AreEqual(a.ComputeConfigHash(), b.ComputeConfigHash());
        }

        [Test]
        public void 효과_줄의_값은_해시에_든다()
        {
            var a = Deck();
            var b = Deck();
            b.Effects[b.Bindings[b.Cards[1].Bindings[0]].EffectIndex].Magnitude = 8f;
            Assert.AreNotEqual(a.ComputeConfigHash(), b.ComputeConfigHash(), "효과 값이 바뀌면 조건이 바뀐 것이다");
        }

        [Test]
        public void 같은_id_같은_값이면_효과_줄_하나를_가리킨다()
        {
            var table = new List<EffectDef>();
            var x = EffectDef.Default(); x.Magnitude = 3f; x.Id = "card.0";
            var z = x; z.Id = "other.0";
            int ix = EffectDef.Intern(table, in x);
            int iy = EffectDef.Intern(table, in x);   // 같은 카드 두 장
            int iz = EffectDef.Intern(table, in z);   // 값이 같아도 소유자가 다르면 다른 줄(U13)
            Assert.AreEqual(ix, iy);
            Assert.AreNotEqual(ix, iz);
            Assert.AreEqual(2, table.Count);
        }

        [Test]
        public void 효과_줄_번호_음수는_효과_값이_없다()
        {
            var def = new MatchDefinition();
            var row = BindingDef.Default();
            Assert.AreEqual(-1, row.EffectIndex, "기본 = 효과 표를 안 가리킨다(0 은 유효 줄 — S4)");
            var e = def.EffectOf(in row);
            Assert.AreEqual(EffectKind.None, e.Kind, "판 규칙이 런타임에 조립하는 코어 효과 줄 — 값은 GimmickDef 가 든다");
            Assert.AreEqual(-1, e.DataIndex);
            Assert.AreEqual("", e.Id);
        }
    }
}
