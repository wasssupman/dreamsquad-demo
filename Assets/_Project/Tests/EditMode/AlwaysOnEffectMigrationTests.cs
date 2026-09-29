using System.Linq;
using NUnit.Framework;
using Wassup.BattleCore.Trigger;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // skill-data-table unit 8 — 상시 효과 이전 계획(`AlwaysOnEffectMigration` — 순수 · 에셋 0). 규칙 = `8_always_on_effects_as_skills.md`
    // 「굽기 스냅샷 동치의 조건」 3(옛 항목과 1:1 · 순서 보존) + `tables.md` §10(id = 카드 id · 둘 이상이면 `_{k}`).
    public class AlwaysOnEffectMigrationTests
    {
        private static AlwaysOnEffectMigration.CardInput Squad(string id, CardTargetAxis axis, params CardEffect[] effects)
            => new AlwaysOnEffectMigration.CardInput { Id = id, Type = CardType.Squad, Axis = axis, Hosts = HostKinds.Defender, Effects = effects };

        private static AlwaysOnEffectMigration.CardInput Unit(string id, params DcAttackModSpec[] mods)
            => new AlwaysOnEffectMigration.CardInput { Id = id, Type = CardType.Unit, Axis = CardTargetAxis.All, Hosts = HostKinds.Defender, AttackMods = mods };

        [Test]
        public void 옛_항목_하나가_효과_줄_하나고_id_는_카드_id_둘이면_번호가_붙는다()
        {
            var plan = AlwaysOnEffectMigration.Build(new[]
            {
                Squad("one", CardTargetAxis.Cost1, new CardEffect { kind = CardBuffKind.AttackSpeed, percent = 5f }),
                Squad("two", CardTargetAxis.ClassGuardian, new CardEffect { kind = CardBuffKind.EffectiveHealth, percent = 50f },
                                                            new CardEffect { kind = CardBuffKind.AttackSpeed, percent = -50f }),
            }, new string[0]);
            CollectionAssert.AreEqual(new[] { "one", "two_0", "two_1" }, plan.Rows.Select(r => r.EffectId).ToArray());
            CollectionAssert.AreEqual(new[] { 0, 0, 1 }, plan.Rows.Select(r => r.Slot).ToArray(), "소유 줄이 없던 카드 — slot = 옛 항목 순서");
            var v = plan.Rows[1].Values;
            Assert.AreEqual(EffectKind.FactionStatBuff, v.kind);
            Assert.AreEqual(CardBuffKind.EffectiveHealth, v.buffStat);
            Assert.AreEqual(50f, v.percent);
            Assert.AreEqual(CardTargetAxis.ClassGuardian, v.allyFilter, "수혜 대상 = 옛 카드 축(효과의 뜻 — 계약 12)");
            Assert.AreEqual("effects[1]", plan.Rows[2].OldSource);
            Assert.IsFalse(plan.Rows.Any(r => r.Notes.Any(AlwaysOnEffectMigration.IsFlag)), "깃발 0");
        }

        [Test]
        public void 공격_수식자는_튕김만_수와_반경을_옮기고_나머지는_배율만이다()
        {
            var plan = AlwaysOnEffectMigration.Build(new[]
            {
                Unit("bounce", new DcAttackModSpec { kind = DcAttackModKind.ProjectileBounce, count = 2, tileRange = 3, damageMul = 1f }),
                Unit("front", new DcAttackModSpec { kind = DcAttackModKind.FrontmostTarget, count = 1, damageMul = 1.2f }),
            }, new string[0]);
            var b = plan.Rows[0].Values;
            Assert.AreEqual(EffectKind.ProjectileBounce, b.kind);
            Assert.AreEqual(2, b.count);
            Assert.AreEqual(3, b.rangeTiles);
            Assert.AreEqual(1f, b.mul);
            var f = plan.Rows[1].Values;
            Assert.AreEqual(EffectKind.FrontmostTarget, f.kind);
            Assert.AreEqual(0, f.count, "최전방은 수를 안 읽는다");
            Assert.AreEqual(1.2f, f.mul);
            Assert.IsTrue(plan.Rows[1].Notes.Any(AlwaysOnEffectMigration.IsFlag), "안 읽던 칸의 값을 버리면 깃발(해시 변화)");
        }

        [Test]
        public void 기존_id_와_겹치면_번호를_달고_깃발이다_slot_은_있던_줄_뒤다()
        {
            var card = Unit("heavy_strike", new DcAttackModSpec { kind = DcAttackModKind.DamageVsSleeping, damageMul = 2f });
            card.BindingCount = 2;
            var plan = AlwaysOnEffectMigration.Build(new[] { card }, new[] { "heavy_strike" });
            Assert.AreEqual("heavy_strike_2", plan.Rows[0].EffectId);
            Assert.AreEqual(2, plan.Rows[0].Slot, "다음 빈 slot");
            Assert.IsTrue(plan.Rows[0].Notes.Any(AlwaysOnEffectMigration.IsFlag));
        }

        [Test]
        public void 옛_굽기가_안_읽던_항목은_옮기지_않고_이미_이전된_카드는_건너뛴다()
        {
            var squadWithMods = Squad("s", CardTargetAxis.All);
            squadWithMods.AttackMods = new[] { new DcAttackModSpec { kind = DcAttackModKind.FrontmostTarget, damageMul = 1.5f } };
            var enemyCard = Unit("mark", new DcAttackModSpec { kind = DcAttackModKind.FrontmostTarget, damageMul = 1.5f });
            enemyCard.Hosts = HostKinds.Enemy;
            var unitWithEffects = Unit("u");
            unitWithEffects.Effects = new[] { new CardEffect { kind = CardBuffKind.AttackDamage, percent = 5f } };
            var done = Squad("done", CardTargetAxis.All, new CardEffect { kind = CardBuffKind.AttackDamage, percent = 5f });
            done.HasAlwaysOnRows = true;
            var plan = AlwaysOnEffectMigration.Build(new[] { squadWithMods, enemyCard, unitWithEffects, done }, new string[0]);
            Assert.AreEqual(0, plan.Rows.Count);
            Assert.AreEqual(4, plan.Notes.Count);
        }

        [Test]
        public void 배치_오라_효과의_수혜_대상은_드는_카드의_축이고_두_축이면_깃발이다()
        {
            var a = Unit("aura_a");
            a.Axis = CardTargetAxis.All;
            a.PlacementAuraEffectIds = new[] { "aura" };
            a.PlacementAuraFilters = new[] { CardTargetAxis.ClassRanger };
            var b = Unit("aura_b");
            b.Axis = CardTargetAxis.Cost1;
            b.PlacementAuraEffectIds = new[] { "aura" };
            b.PlacementAuraFilters = new[] { CardTargetAxis.ClassRanger };
            var plan = AlwaysOnEffectMigration.Build(new[] { a }, new string[0]);
            Assert.AreEqual(1, plan.AuraFilters.Count);
            Assert.AreEqual(CardTargetAxis.All, plan.AuraFilters[0].To);
            Assert.IsFalse(plan.AuraFilters[0].Notes.Any(AlwaysOnEffectMigration.IsFlag));

            plan = AlwaysOnEffectMigration.Build(new[] { a, b }, new string[0]);
            Assert.AreEqual(1, plan.AuraFilters.Count, "효과 하나 = 쓰기 하나");
            Assert.IsTrue(plan.AuraFilters[0].Notes.Any(AlwaysOnEffectMigration.IsFlag), "두 카드가 다른 축으로 들면 확인 필요");
        }

        [Test]
        public void 표는_옛_항목_새_id_종류_값_slot_을_적는다()
        {
            var plan = AlwaysOnEffectMigration.Build(new[]
            {
                Squad("grail", CardTargetAxis.All, new CardEffect { kind = CardBuffKind.AttackDamage, percent = 70f },
                                                   new CardEffect { kind = CardBuffKind.EffectiveHealth, percent = -40f }),
            }, new string[0]);
            string md = AlwaysOnEffectMigration.Report(plan, null);
            StringAssert.Contains("| `grail` | effects[1] — EffectiveHealth -40% · 카드 axis All | `grail_1` | FactionStatBuff | buff_stat EffectiveHealth · percent -40 · ally_filter All | 1 |", md);
            StringAssert.Contains("새 효과 줄 2", md);
        }
    }
}
