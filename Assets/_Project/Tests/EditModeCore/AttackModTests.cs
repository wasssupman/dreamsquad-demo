using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using Wassup.BattleCore;
using Wassup.BattleCore.Combat;
using Wassup.BattleCore.Effects;
using Wassup.BattleCore.Trigger;
using Wassup.Skills;
using static Wassup.Tests.EditMode.Core.CoreTriggerFixtures;

namespace Wassup.Tests.EditMode.Core
{
    // battle-core-rebuild unit 7a — 공격 수식자 5축(바인딩 밖) · 충전의 부여(스킬)/소비(수식자) 경계.
    [TestFixture]
    public class AttackModTests
    {
        private static (BattleMatch m, Unit d, Unit e, List<CoreEvent> hits) Duel(params AttackModDef[] mods)
        {
            var def = CoreCombatFixtures.Definition(defenderDamage: 10f, enemyHealth: 100000f);
            def.Units[0].Attack.Mods = mods;
            def.ConfigHash = def.ComputeConfigHash();
            var m = CoreMatchFixtures.BeginBattle(def);
            var d = SpawnDefender(m, new int2(5, 2));
            var e = SpawnEnemy(m, new int2(6, 2));
            var hits = CoreCombatFixtures.Listen(m, CoreEventKind.DamageApplied);
            return (m, d, e, hits);
        }

        private static List<float> From(List<CoreEvent> hits, Unit src)
            => hits.FindAll(h => h.A == src.Id).ConvertAll(h => h.Amount);

        [Test]
        public void 강공은_N번째_공격만_배율이_붙는다()
        {
            var (m, d, _, hits) = Duel(new AttackModDef { Kind = AttackModKind.HeavyStrike, Period = 2, DamageMul = 3f });
            CoreCombatFixtures.Tick(m, 60 * 4 + 5);
            var mine = From(hits, d);
            Assert.GreaterOrEqual(mine.Count, 4);
            for (int i = 0; i < mine.Count; i++)
                Assert.AreEqual(i % 2 == 1 ? 30f : 10f, mine[i], 1e-4f, $"{i + 1}타");
        }

        [Test]
        public void 수면_배율은_잠든_대상에게만_붙는다()
        {
            var (m, d, e, hits) = Duel(new AttackModDef { Kind = AttackModKind.DamageVsSleeping, DamageMul = 2f });
            CoreCombatFixtures.Tick(m, 2);
            Assert.AreEqual(10f, From(hits, d)[0], 1e-4f, "깨어 있다");
            e.Cc.Apply(CcSlotKind.Sleep, 99f, float3.zero, SimEntityId.None);
            CoreCombatFixtures.Tick(m, 61);
            var mine = From(hits, d);
            Assert.AreEqual(20f, mine[mine.Count - 1], 1e-4f, "잠들었다(피격 기상은 그 뒤다)");
        }

        [Test]
        public void 최전방_수식자가_있으면_최전방을_물고_주_대상에_배율이_붙는다()
        {
            var (m, d, _, hits) = Duel(new AttackModDef { Kind = AttackModKind.FrontmostTarget, DamageMul = 1.2f });
            Assert.IsTrue(d.Attack.WantsFrontmost);
            CoreCombatFixtures.Tick(m, 2);
            Assert.AreEqual(12f, From(hits, d)[0], 1e-4f);
            Assert.IsTrue(d.Attack.FrontmostTarget.IsNone, "스냅샷은 RESOLVE 에서 풀린다");
        }

        [Test]
        public void 튕김_부여는_수_합_반경_최대_감쇠_곱이다()
        {
            var mods = new List<AttackModState>
            {
                new AttackModState { Def = new AttackModDef { Kind = AttackModKind.ProjectileBounce, Count = 1, TileRange = 2, DamageMul = 0.5f } },
                new AttackModState { Def = new AttackModDef { Kind = AttackModKind.ProjectileBounce, Count = 2, TileRange = 3, DamageMul = 0.8f } },
                new AttackModState { Def = new AttackModDef { Kind = AttackModKind.DamageVsSleeping, DamageMul = 9f } },
            };
            AttackMod.Bounce(mods, out int count, out int range, out float mul);
            Assert.AreEqual(3, count);
            Assert.AreEqual(3, range);
            Assert.AreEqual(0.4f, mul, 1e-5f);
        }

        [Test]
        public void 충전은_스킬이_부여하고_수식자가_소비한다_한_번에_전부()
        {
            var (m, d, _, _) = Duel();
            var resolved = CoreCombatFixtures.Listen(m, CoreEventKind.AttackResolved);
            // 부여 — 스킬(`GrantSelfCharge`)이 의도를 낸다. 소비 규칙은 모른다.
            var grant = SkillRouting.Resolve(TriggerKind.OnDamagedN, EffectKind.NextAttackDoubleFire);
            var rule = Probe(TriggerKind.None, grant);
            rule.Effect.Magnitude = 3f;
            var b = CoreTriggerFixtures.AttachRuntime(m, d, rule, 0);
            m.Triggers.RaiseFor(b, EventAt(Seam.Periodic, d));
            // 주기 seam(전투 앞)에서 부여 → 같은 틱의 START 가 소비한다.
            CoreCombatFixtures.Tick(m, 3);
            int early = resolved.FindAll(e => e.A == d.Id).Count;
            Assert.AreEqual(2, early, "첫 공격 + 즉시 한 번 더(각 발이 온전한 공격)");
            Assert.AreEqual(0, d.Progressive.Charge, "소비 = 전부(옛 컴포넌트 통째 제거)");
            CoreCombatFixtures.Tick(m, 30);
            Assert.AreEqual(2, resolved.FindAll(e => e.A == d.Id).Count, "그 뒤엔 정상 간격");
        }
    }
}
