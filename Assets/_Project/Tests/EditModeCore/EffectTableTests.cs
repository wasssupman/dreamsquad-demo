using System.Collections.Generic;
using NUnit.Framework;
using Wassup.BattleCore;
using Wassup.BattleCore.Trigger;
using Wassup.Skills;
using static Wassup.Tests.EditMode.Core.CoreCardFixtures;
using Probe = Wassup.BattleCore.CardProbe;

namespace Wassup.Tests.EditMode.Core
{
    // skill-data-table unit 1a — **효과 표로 옮겨도 판이 같다**(헤드리스 동치 증명). 같은 고정구를 두 벌 만들고 한 벌만
    // 규칙 줄의 효과를 효과 표로 옮긴다(빌더가 하는 번역 `EffectDef.MoveInline` 그대로) — 해시 · 정본 텍스트 · 카드 프로브의
    // 판 결과가 같아야 한다. 라이브 에셋의 굽기 동치는 Assets lane 의 굽기 스냅샷(파일 diff 0)이 본다.
    //
    // ⚠ 수치는 게임 값이 아니라 픽스처다.
    [TestFixture]
    public class EffectTableTests
    {
        private static MatchDefinition Deck()
        {
            var def = CoreMatchFixtures.Definition();
            var lf = CardRule(TriggerKind.None, EffectKind.SelfBuffLethal);
            lf.Magnitude = 1.9f;
            lf.Duration = 5f;
            lf.FireCap = 1;
            AddAttachCard(def, "fixture_last_flame", 1, lf);

            var fa = CardRule(TriggerKind.AttackN, EffectKind.ApplyCcToTarget);
            fa.Period = 3;
            fa.CcKind = (int)SkillCcKind.Stun;
            fa.Duration = 1f;
            AddAttachCard(def, "fixture_frost_arrow", 1, fa);

            var fw = CardRule(TriggerKind.OnDeath, EffectKind.SelfTileAoe);
            fw.Magnitude = 7f;
            fw.TileRange = 1;
            fw.DataIndex = CoreTriggerFixtures.AddBlastProjectile(def);
            AddAttachCard(def, "fixture_farewell", 1, fw);

            AddSquadCard(def, "fixture_squad", 1, SkillStatKind.DamageMul, 1.5f);
            return def;
        }

        // 빌더의 번역(`BindingDefinitionBuilder.MoveEffects`)과 같은 모양 — id = `{카드}.{자리}`.
        private static MatchDefinition MovedToTable(MatchDefinition def)
        {
            var effects = new List<EffectDef>(def.Effects);
            for (int i = 0; i < def.Cards.Length; i++)
            {
                ref var c = ref def.Cards[i];
                Move(def, effects, c.Bindings, c.Id + ".");
                Move(def, effects, c.SquadBindings, c.Id + ".squad");
            }
            def.Effects = effects.ToArray();
            return def;
        }

        private static void Move(MatchDefinition def, List<EffectDef> effects, int[] rows, string prefix)
        {
            for (int k = 0; rows != null && k < rows.Length; k++)
                EffectDef.MoveInline(effects, ref def.Bindings[rows[k]], prefix + k);
        }

        [Test]
        public void 효과_표로_옮겨도_정의표_해시와_정본_텍스트가_같다()
        {
            var inline = Deck();
            var moved = MovedToTable(Deck());

            Assert.AreEqual(inline.CanonicalText(), moved.CanonicalText(), "해시는 해석된 효과 값만 본다(README 계약 8)");
            Assert.AreEqual(inline.ComputeConfigHash(), moved.ComputeConfigHash());
            Assert.AreEqual(inline.Bindings.Length, moved.Effects.Length, "소유자 줄마다 효과 줄 하나(id `{소유자}.{자리}`)");
            for (int r = 0; r < moved.Bindings.Length; r++)
            {
                Assert.GreaterOrEqual(moved.Bindings[r].EffectIndex, 0, "옮긴 줄은 효과 표를 가리킨다");
                // 인라인 칸은 비었다 — 효과 값이 표에만 있다(값이 두 벌이 아니다).
                Assert.AreEqual(EffectDef.Default().Magnitude, moved.Bindings[r].InlineEffect().Magnitude);
                Assert.AreEqual(EffectKind.None, moved.Bindings[r].InlineEffect().Kind);
            }
            Assert.AreEqual("fixture_farewell.0", moved.Effects[moved.Bindings[moved.Cards[2].Bindings[0]].EffectIndex].Id);
        }

        [Test]
        public void 효과_표로_옮긴_판이_인라인_판과_같은_일을_한다()
        {
            var inline = Deck();
            var moved = MovedToTable(Deck());
            inline.ConfigHash = inline.ComputeConfigHash();
            moved.ConfigHash = moved.ComputeConfigHash();

            var a = Probe.RunAll(inline);
            var b = Probe.RunAll(moved);
            TestContext.WriteLine(Probe.FormatTable(b));
            Assert.AreEqual(a.Length, b.Length);
            for (int i = 0; i < a.Length; i++)
            {
                Assert.AreEqual(a[i].Ok, b[i].Ok, a[i] + " ↔ " + b[i]);
                CollectionAssert.AreEqual(a[i].Witnessed, b[i].Witnessed, a[i] + " ↔ " + b[i]);
            }
            Assert.AreEqual(Probe.FormatTable(a), Probe.FormatTable(b));
            Assert.AreEqual(Probe.CanonicalDeckText(inline), Probe.CanonicalDeckText(moved));
        }

        [Test]
        public void 같은_id_같은_값이면_효과_줄_하나를_가리킨다()
        {
            var table = new List<EffectDef>();
            var x = BindingDef.Default(); x.Magnitude = 3f;
            var y = BindingDef.Default(); y.Magnitude = 3f;
            var z = BindingDef.Default(); z.Magnitude = 3f;
            int ix = EffectDef.MoveInline(table, ref x, "card.0");
            int iy = EffectDef.MoveInline(table, ref y, "card.0");   // 같은 카드 두 장
            int iz = EffectDef.MoveInline(table, ref z, "other.0");  // 값이 같아도 소유자가 다르면 다른 줄(U13)
            Assert.AreEqual(ix, iy);
            Assert.AreNotEqual(ix, iz);
            Assert.AreEqual(2, table.Count);
            Assert.AreEqual(ix, EffectDef.MoveInline(table, ref x, "card.0"), "이미 옮긴 줄은 그대로");
        }

        [Test]
        public void 효과_줄_번호_음수는_인라인_칸을_읽는다()
        {
            var def = new MatchDefinition();
            var row = BindingDef.Default();
            row.Magnitude = 4f;
            Assert.AreEqual(-1, row.EffectIndex, "기본 = 효과 표를 안 가리킨다(0 은 유효 줄 — S4)");
            var e = def.EffectOf(in row);
            Assert.AreEqual(4f, e.Magnitude);
            Assert.AreEqual("", e.Id);
        }
    }
}
