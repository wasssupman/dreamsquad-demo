using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using Wassup.BattleCore;
using Wassup.BattleCore.Effects;
using Wassup.BattleCore.Trigger;
using Wassup.Skills;
using static Wassup.Tests.EditMode.Core.CoreTriggerFixtures;

namespace Wassup.Tests.EditMode.Core
{
    // battle-core-rebuild unit 7a — 등록부: 수명 5종 · fireCap ≠ lifetime · revokeOnExpire · InstanceId 단조(F1).
    [TestFixture]
    public class BindingRegistryTests
    {
        private static BattleMatch Match(MatchDefinition def = null)
        {
            var m = new BattleMatch(def ?? CoreCombatFixtures.Definition());
            m.Begin();
            return m;
        }

        private static BindingDef Periodic(ISkill skill, float period = BattleMatch.Dt)
        {
            var d = Probe(TriggerKind.PeriodicTimer, skill);
            d.PeriodSeconds = period;
            return d;
        }

        [Test]
        public void 저작_규칙은_스폰에서_붙고_소멸에서_사건과_함께_떨어진다()
        {
            var def = CoreCombatFixtures.Definition();
            var probe = new ProbeSkill();
            GiveUnit(def, 0, Periodic(probe), Periodic(probe));
            var m = Match(def);
            var attached = CoreCombatFixtures.Listen(m, CoreEventKind.BindingAttached);
            var detached = CoreCombatFixtures.Listen(m, CoreEventKind.BindingDetached);

            var d = SpawnDefender(m, new int2(5, 2));
            Assert.AreEqual(2, d.Bindings.Count);
            Assert.Less(d.Bindings[0].InstanceId, d.Bindings[1].InstanceId, "부착 순 = InstanceId 오름차순");
            m.Tick();
            Assert.AreEqual(2, attached.Count);

            m.Apply(Command.DebugDestroy(d.Id));
            m.Tick();
            Assert.AreEqual(2, detached.Count, "계약 7 — 모든 소멸은 소멸 사건을 낸다");
            Assert.AreEqual((float)BindingDetachReason.OwnerRemoved, detached[0].Amount);
        }

        [Test]
        public void InstanceId_는_판_안에서_단조_증가하고_재사용되지_않는다()
        {
            var def = CoreCombatFixtures.Definition();
            GiveUnit(def, 0, Periodic(new ProbeSkill(), 99f));
            var m = Match(def);
            var seen = new HashSet<int>();
            int last = 0;
            for (int k = 0; k < 4; k++)
            {
                var d = SpawnDefender(m, new int2(5, 2));
                int id = d.Bindings[0].InstanceId;
                Assert.Greater(id, last, "F1 — 낡은 핸들이 새 대상을 가리키지 않게");
                Assert.IsTrue(seen.Add(id));
                last = id;
                m.Apply(Command.DebugDestroy(d.Id));
            }
        }

        [Test]
        public void fireCap_은_수명이_아니다_1회_발동_뒤에도_붙어_있다()
        {
            var probe = new ProbeSkill();
            var m = Match();
            var d = SpawnDefender(m, new int2(5, 2));
            var rule = Periodic(probe);
            rule.FireCap = 1;
            rule.Lifetime = BindingLifetime.Owner;
            var b = m.Bindings.Attach(d, in rule, -1, 0);
            CoreCombatFixtures.Tick(m, 10);
            Assert.AreEqual(1, probe.Count, "fireCap 1");
            Assert.IsFalse(b.Detached, "표식처럼 — 발동 1회 + 소유자 소멸까지 부착(정정 5)");
            Assert.AreEqual(1, d.Bindings.Count);
        }

        [Test]
        public void UntilFireCap_수명은_다_쓰면_떨어진다()
        {
            var probe = new ProbeSkill();
            var m = Match();
            var d = SpawnDefender(m, new int2(5, 2));
            var rule = Periodic(probe);
            rule.FireCap = 2;
            rule.Lifetime = BindingLifetime.UntilFireCap;
            var detached = CoreCombatFixtures.Listen(m, CoreEventKind.BindingDetached);
            var b = m.Bindings.Attach(d, in rule, -1, 0);
            CoreCombatFixtures.Tick(m, 10);
            Assert.AreEqual(2, probe.Count);
            Assert.IsTrue(b.Detached);
            Assert.AreEqual((float)BindingDetachReason.FireCapReached, detached[0].Amount);
        }

        [Test]
        public void Timed_수명은_판의_시계로_만료된다()
        {
            var m = Match();
            var d = SpawnDefender(m, new int2(5, 2));
            var rule = Periodic(new ProbeSkill(), 99f);
            rule.Lifetime = BindingLifetime.Timed;
            rule.LifetimeSeconds = 0.5f;
            var detached = CoreCombatFixtures.Listen(m, CoreEventKind.BindingDetached);
            var b = m.Bindings.Attach(d, in rule, -1, 0);
            CoreCombatFixtures.Tick(m, 29);
            Assert.IsFalse(b.Detached);
            CoreCombatFixtures.Tick(m, 3);
            Assert.IsTrue(b.Detached);
            Assert.AreEqual((float)BindingDetachReason.Expired, detached[0].Amount);
        }

        [Test]
        public void Match_수명은_판_호스트에_붙고_유닛_소멸과_무관하다()
        {
            var probe = new ProbeSkill();
            var m = Match();
            var rule = Periodic(probe);
            rule.Lifetime = BindingLifetime.Match;
            var b = m.Bindings.Attach(null, in rule, -1, 0);
            Assert.AreEqual(SimEntityId.Match, b.Owner);
            Assert.AreEqual(1, m.Bindings.MatchBindings.Count);
            CoreCombatFixtures.Tick(m, 3);
            Assert.AreEqual(3, probe.Count, "판 호스트 주기 규칙도 돈다(레드불 cadence 의 자리 — 7d)");
        }

        [Test]
        public void Manual_수명은_명시로_뗄_때까지다()
        {
            var probe = new ProbeSkill();
            var m = Match();
            var d = SpawnDefender(m, new int2(5, 2));
            var rule = Periodic(probe);
            rule.Lifetime = BindingLifetime.Manual;
            var b = m.Bindings.Attach(d, in rule, -1, 0);
            CoreCombatFixtures.Tick(m, 2);
            Assert.IsTrue(m.Bindings.Detach(b, BindingDetachReason.Manual, 2));
            CoreCombatFixtures.Tick(m, 3);
            Assert.AreEqual(2, probe.Count, "뗀 뒤엔 안 난다");
            Assert.IsFalse(m.Bindings.Detach(b, BindingDetachReason.Manual, 3), "두 번 떼지 않는다");
        }

        [Test]
        public void revokeOnExpire_참이면_그_규칙이_건_스탯을_슬롯째_거둔다()
        {
            foreach (bool revoke in new[] { true, false })
            {
                var m = Match();
                var d = SpawnDefender(m, new int2(5, 2));
                var ally = SpawnDefender(m, new int2(6, 2));
                var rule = Rule(TriggerKind.PeriodicTimer, TriggerPayload.AllyStatAura);
                rule.PeriodSeconds = BattleMatch.Dt;
                rule.Magnitude = 50f;          // +50%
                rule.Duration = 99f;
                rule.TileRange = 3;
                rule.StatKind = (int)SkillStatKind.DamageMul;
                rule.RevokeOnExpire = revoke;
                var b = m.Bindings.Attach(d, in rule, -1, 0);
                m.Tick();
                Assert.AreEqual(1.5f, ally.Modifiers.Effective.DamageMul, 1e-5f, "걸렸다");
                m.Bindings.Detach(b, BindingDetachReason.Manual, m.Clock.Tick);
                Assert.AreEqual(revoke ? 1f : 1.5f, ally.Modifiers.Effective.DamageMul, 1e-5f,
                    revoke ? "소급 중화(배치 오라 공속)" : "등록부 제거만(수면)");
            }
        }

        [Test]
        public void 유닛_상한을_넘는_부착은_거절하고_말한다()
        {
            var m = Match();
            var said = new List<string>();
            m.Report = said.Add;
            var d = SpawnDefender(m, new int2(5, 2));
            var rule = Periodic(new ProbeSkill(), 99f);
            for (int i = 0; i < BindingRegistry.MaxPerUnit; i++) Assert.IsNotNull(m.Bindings.Attach(d, in rule, -1, 0));
            Assert.IsNull(m.Bindings.Attach(d, in rule, -1, 0));
            Assert.IsTrue(said.Exists(s => s.Contains("상한")), "조용한 폐기 금지");
        }
    }
}
