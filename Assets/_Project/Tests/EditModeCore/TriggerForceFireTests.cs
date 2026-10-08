using NUnit.Framework;
using Unity.Mathematics;
using Somnia.Battle.BattleCore;
using Somnia.Battle.BattleCore.Trigger;

namespace Somnia.Battle.Tests.EditMode.Core
{
    // battle-core-rebuild unit 7d — **규칙 강제 발화와 「왜 안 터졌나」** (tools.md 「트리거 강제 발화」의 코어 쪽).
    //
    // 강제 발화는 카운터·게이트·감지자를 건너뛰고 **실행자만** 부른다 — 단 발동 상한은 지킨다(상한 소진이
    // 「왜 안 터졌나」의 한 원인인데 도구가 그것을 넘으면 원인을 가린다). 커맨드 콜스택(`Immediate`)에서 곧 돈다.
    // 진단의 네 원인(감지자 없음 · 조건 불통과 · 상한 소진 · 떨어짐)은 코어가 판정한다(`BindingDiagnosis`).
    [TestFixture]
    public class TriggerForceFireTests
    {
        private static BattleMatch Board(RuleRow rule, out Unit owner, out Binding binding)
        {
            var def = CoreCombatFixtures.Definition(defenderDamage: 0f);
            CoreTriggerFixtures.GiveUnit(def, 0, rule);
            var m = CoreMatchFixtures.BeginBattle(def);
            owner = CoreTriggerFixtures.SpawnDefender(m, new int2(4, 2));
            binding = owner.Bindings[owner.Bindings.Count - 1];
            return m;
        }

        [Test]
        public void 강제_발화는_카운터를_건너뛰고_커맨드_안에서_곧_실행된다()
        {
            var probe = new CoreTriggerFixtures.ProbeSkill();
            var rule = CoreTriggerFixtures.Probe(TriggerKind.AttackN, probe);
            rule.Rule.Period = 5;   // 공격 다섯 번째마다 — 한 번도 안 쳤다
            var m = Board(rule, out var owner, out var b);

            var r = m.Apply(Command.DebugFireBinding(owner.Id, b.InstanceId));
            Assert.IsTrue(r.Accepted, r.Reason.ToString());
            Assert.AreEqual(1, probe.Count, "틱을 기다리지 않는다(Immediate)");
            Assert.AreEqual(0, b.Counter, "카운터는 안 움직인다 — 사건이 아니라 발동이다");
            Assert.AreEqual(1, b.FireCount);
        }

        [Test]
        public void 강제_발화도_발동_상한은_넘지_못한다()
        {
            var probe = new CoreTriggerFixtures.ProbeSkill();
            var rule = CoreTriggerFixtures.Probe(TriggerKind.AttackN, probe);
            rule.Rule.Period = 5;
            rule.Rule.FireCap = 1;
            var m = Board(rule, out var owner, out var b);

            m.Apply(Command.DebugFireBinding(owner.Id, b.InstanceId));
            m.Apply(Command.DebugFireBinding(owner.Id, b.InstanceId));
            Assert.AreEqual(1, probe.Count);
            Assert.AreEqual(BindingStatus.FireCapSpent, BindingDiagnosis.Diagnose(b, m.Triggers));
        }

        [Test]
        public void 없는_규칙_번호와_없는_주인은_거절한다()
        {
            var probe = new CoreTriggerFixtures.ProbeSkill();
            var m = Board(CoreTriggerFixtures.Probe(TriggerKind.AttackN, probe), out var owner, out var b);
            Assert.AreEqual(RejectReason.NoSuchEntity, m.Apply(Command.DebugFireBinding(owner.Id, b.InstanceId + 100)).Reason);
            Assert.AreEqual(RejectReason.NoSuchEntity, m.Apply(Command.DebugFireBinding(new SimEntityId(9999), b.InstanceId)).Reason);
            Assert.AreEqual(0, probe.Count);
        }

        [Test]
        public void 진단_사건이_한_번도_안_났으면_감지자_없음_났는데_안_찼으면_조건_불통과()
        {
            var probe = new CoreTriggerFixtures.ProbeSkill();
            var rule = CoreTriggerFixtures.Probe(TriggerKind.OnDamagedN, probe);
            rule.Rule.Period = 3;
            var m = Board(rule, out var owner, out var b);
            Assert.AreEqual(BindingStatus.NoDetector, BindingDiagnosis.Diagnose(b, m.Triggers), "아무도 안 때렸다");

            owner.Inbox.Damage.Add(new DamageEntry { Amount = 1f, Source = SimEntityId.None });
            CoreCombatFixtures.Tick(m, 2);
            Assert.Greater(m.Triggers.SensedCount(TriggerKind.OnDamagedN), 0, "피격 사실은 올라왔다");
            Assert.AreEqual(BindingStatus.ConditionNotMet, BindingDiagnosis.Diagnose(b, m.Triggers), "3회 중 1회");
            Assert.AreEqual(0, probe.Count);
        }

        [Test]
        public void 진단_폴링_감지는_감지자_없음이_될_수_없다()
        {
            var probe = new CoreTriggerFixtures.ProbeSkill();
            var rule = CoreTriggerFixtures.Probe(TriggerKind.PeriodicTimer, probe);
            rule.Rule.PeriodSeconds = 100f;
            var m = Board(rule, out _, out var b);
            CoreCombatFixtures.Tick(m, 2);
            Assert.AreEqual(BindingStatus.ConditionNotMet, BindingDiagnosis.Diagnose(b, m.Triggers), "주기가 아직이다");
        }

        [Test]
        public void 진단_떨어진_규칙은_떨어짐이다()
        {
            var probe = new CoreTriggerFixtures.ProbeSkill();
            var rule = CoreTriggerFixtures.Probe(TriggerKind.AttackN, probe);
            rule.Rule.Lifetime = BindingLifetime.Timed;
            rule.Rule.LifetimeSeconds = 0.05f;
            var m = Board(rule, out _, out var b);
            CoreCombatFixtures.Tick(m, 10);
            Assert.IsTrue(b.Detached);
            Assert.AreEqual(BindingStatus.Detached, BindingDiagnosis.Diagnose(b, m.Triggers));
        }
    }
}
