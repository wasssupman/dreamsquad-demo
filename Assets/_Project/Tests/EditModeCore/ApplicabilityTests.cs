using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using Wassup.BattleCore;
using Wassup.BattleCore.Combat;
using Wassup.BattleCore.Combat.Projectile;
using Wassup.BattleCore.Trigger;
using static Wassup.Tests.EditMode.Core.CoreCardFixtures;

namespace Wassup.Tests.EditMode.Core
{
    // battle-core-rebuild unit 7b — **preflight 와 커밋이 같은 답**(옛 `DcApplicability` 가 세운 규율). 두 벌이면 「붙는데 무효」.
    [TestFixture]
    public class ApplicabilityTests
    {
        [Test]
        public void 숙주_프로필은_탄_선언이_아니라_실제_경로를_읽는다()
        {
            var def = CoreCombatFixtures.Definition(policy: AttackPolicy.Bomb);
            CoreCombatFixtures.GiveProjectile(def, MovementKind.HomingToEntity, PayloadKind.SingleSplash);
            var m = CoreMatchFixtures.BeginBattle(def);
            var bomber = CoreTriggerFixtures.SpawnDefender(m, new int2(3, 2));

            var p = HostProfile.Of(bomber, def);

            Assert.AreEqual(HostArchetype.BombThrow, p.Archetype);
            Assert.AreEqual(HostRoute.Grenade, p.Route, "폭탄맨의 탄은 유도로 선언돼도 경로는 수류탄이다");
            Assert.IsTrue(p.HasDamageOutput);
        }

        [Test]
        public void 대상을_안_주는_숙주에는_대상_효과가_안_붙는다()
        {
            var host = new HostProfile { Archetype = HostArchetype.BombThrow, Route = HostRoute.Grenade, TargetsEnemies = true, HasDamageOutput = true };
            var cc = CoreTriggerFixtures.Rule(TriggerKind.None, EffectKind.ApplyCcToTarget);
            Assert.AreEqual(RejectReason.NeedsTargetContext, Applicability.Evaluate(in cc.Rule, in cc.Effect, in host));
            var dagger = CoreTriggerFixtures.Rule(TriggerKind.None, EffectKind.ProjectileToTarget);
            Assert.AreEqual(RejectReason.NeedsFallbackRange, Applicability.Evaluate(in dagger.Rule, in dagger.Effect, in host));
            dagger.Effect.TileRange = 4;
            Assert.AreEqual(RejectReason.None, Applicability.Evaluate(in dagger.Rule, in dagger.Effect, in host), "폴백 반경이 있으면 스스로 찾는다");
            var gated = CoreTriggerFixtures.Rule(TriggerKind.None, EffectKind.SelfTileAoe);
            gated.Rule.Gate = GateKind.HpBelow; gated.Rule.GateSubject = GateSubject.EventTarget;
            Assert.AreEqual(RejectReason.NeedsTargetContext, Applicability.Evaluate(in gated.Rule, in gated.Effect, in host),
                "평가할 수단이 없는 게이트는 조건 없는 발동이 된다 — 거절");
            var heavy = new AttackModDef { Kind = AttackModKind.HeavyStrike, Period = 3, DamageMul = 2f };
            Assert.AreEqual(RejectReason.NeedsTargetContext, Applicability.EvaluateAttackMod(in heavy, in host));
        }

        [Test]
        public void 튕김은_재조준_가능한_경로에만_붙는다()
        {
            var bounce = new AttackModDef { Kind = AttackModKind.ProjectileBounce, Count = 2, DamageMul = 1f };
            foreach (var (route, ok) in new[] { (HostRoute.Homing, true), (HostRoute.Directional, true),
                                                (HostRoute.Ballistic, false), (HostRoute.Grenade, false), (HostRoute.None, false) })
            {
                var host = new HostProfile { Route = route, HasDamageOutput = true };
                Assert.AreEqual(ok ? RejectReason.None : RejectReason.NeedsHomingRoute,
                                Applicability.EvaluateAttackMod(in bounce, in host), route.ToString());
            }
        }

        [Test]
        public void 조준_preflight_와_커밋은_같은_답을_낸다()
        {
            var def = CoreMatchFixtures.Definition();
            var cards = new List<int>();
            var stack = CardRule(TriggerKind.AttackN, EffectKind.ApplyStackToTarget);
            stack.Rule.Period = 3; stack.Effect.Magnitude = 1f;
            cards.Add(AddAttachCard(def, "ember_bite", 1, stack));
            var bounce = CardDef.Default();
            bounce.Id = "bouncy"; bounce.Kind = CardKind.Attach; bounce.Cost = 1;
            bounce.AttackMods = new[] { new AttackModDef { Kind = AttackModKind.ProjectileBounce, Count = 2, DamageMul = 1f } };
            cards.Add(AddCard(def, bounce));
            var req = CardDef.Default();
            req.Id = "guardian_only"; req.Kind = CardKind.Attach; req.Cost = 1; req.DeclaresRetireRecall = true;
            req.Requirement = new AttachRequirementDef { Kind = AttachRequirementKind.Class, Role = 2 };
            cards.Add(AddCard(def, req));
            var mark = CardRule(TriggerKind.None, EffectKind.BountyMark);
            mark.Effect.Magnitude = 2f; mark.Rule.FireCap = 1;
            int markCard = AddAttachCard(def, "mark", 1, mark);
            def.Cards[markCard].TargetsEnemies = true;
            cards.Add(markCard);

            var m = CardBattle(def, awakening: 100f);
            var host = Defender(m, new int2(3, 1));
            var enemy = CoreTriggerFixtures.SpawnEnemy(m, new int2(6, 2));
            foreach (int c in cards)
                foreach (var target in new[] { host.Id, enemy.Id })
                {
                    var pre = m.Hand.WouldAttach(c, target);
                    var got = m.Apply(Command.AttachCard(EntryOf(m, c), target));
                    Assert.AreEqual(pre, got.Accepted ? RejectReason.None : got.Reason, def.Cards[c].Id + " → " + target);
                    if (got.Accepted) break;   // 붙었으면 그 카드는 손패를 떠났다
                }
        }

        [Test]
        public void 부착_제한의_무효_저작은_어디에도_안_붙는다()
        {
            var host = CoreMatchFixtures.Defender("any");
            host.Role = 2;
            var bad = new AttachRequirementDef { Kind = AttachRequirementKind.Class, Role = 2, Invalid = true };
            Assert.IsFalse(Applicability.MeetsRequirement(in bad, in host), "fail-closed");
            var id = new AttachRequirementDef { Kind = AttachRequirementKind.UnitId, UnitId = "Any" };
            Assert.IsFalse(Applicability.MeetsRequirement(in id, in host), "id 는 ordinal — 대소문자가 다르면 다른 유닛");
        }
    }
}
