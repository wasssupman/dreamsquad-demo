using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using Somnia.Battle.BattleCore;
using Somnia.Battle.BattleCore.Combat;
using Somnia.Battle.BattleCore.Trigger;
using Somnia.Battle.Skills;
using static Somnia.Battle.Tests.EditMode.Core.CoreCardFixtures;

namespace Somnia.Battle.Tests.EditMode.Core
{
    // battle-core-rebuild unit 7b — 부착은 **동기 트랜잭션이고 순서가 규칙이다**(① 적용 → ② 차감 → ③ 순환).
    [TestFixture]
    public class CardAttachTests
    {
        private static RuleRow LastFlame()
        {
            var r = CardRule(TriggerKind.None, EffectKind.SelfBuffLethal);
            r.Effect.Magnitude = 1.9f;   // 배율(bake 가 % → 배율로 바꿔 싣는다)
            r.Effect.Duration = 5f;
            r.Rule.FireCap = 1;
            return r;
        }

        [Test]
        public void 부착_즉시_규칙은_커맨드_콜스택_안에서_실행되고_그_뒤에_값을_치른다()
        {
            var def = CoreMatchFixtures.Definition();
            int card = AddAttachCard(def, "last_flame", 20, LastFlame());
            var m = CardBattle(def, awakening: 50f);
            var host = Defender(m, new int2(3, 1));
            var fired = CoreCombatFixtures.Listen(m, CoreEventKind.TriggerFired);
            var attached = CoreCombatFixtures.Listen(m, CoreEventKind.CardAttached);
            int tickBefore = m.Clock.Tick;

            Assert.IsTrue(m.Apply(Command.AttachCard(EntryOf(m, card), host.Id)).Accepted);

            Assert.AreEqual(tickBefore, m.Clock.Tick, "틱을 한 번도 안 돌렸다");
            Assert.AreEqual(1, fired.Count, "① 적용 — 규칙이 이 커맨드 안에서 발동했다(Immediate seam)");
            Assert.IsTrue(host.Progressive != null && host.Progressive.LethalActive, "치명 타이머가 이미 섰다");
            Assert.Greater(host.Modifiers.Effective.AttackSpeedMul, 1.5f, "공속 버프가 이미 붙었다");
            Assert.AreEqual(30f, m.Hand.Gauge, 1e-4f, "② 차감");
            Assert.AreEqual(0, m.Hand.QueueCount, "③ 순환 — 풀에서 이탈");
            Assert.AreEqual(1, attached.Count);
            Assert.AreEqual(host.Id, attached[0].A);
            Assert.AreEqual(card, attached[0].DefIndex, "카드 줄을 값으로 싣는다");
        }

        [Test]
        public void 실패한_부착은_무차감_무순환이다_이중_상태()
        {
            var def = CoreMatchFixtures.Definition();
            int a = AddAttachCard(def, "last_flame_a", 20, LastFlame());
            int b = AddAttachCard(def, "last_flame_b", 20, LastFlame());
            var m = CardBattle(def, awakening: 100f);
            var host = Defender(m, new int2(3, 1));
            Assert.IsTrue(m.Apply(Command.AttachCard(EntryOf(m, a), host.Id)).Accepted);
            float gauge = m.Hand.Gauge;
            int queue = m.Hand.QueueCount;

            var r = m.Apply(Command.AttachCard(EntryOf(m, b), host.Id));

            Assert.AreEqual(RejectReason.DuplicateState, r.Reason, "치명 타이머가 이미 있다 — 카드 전체 거절");
            Assert.AreEqual(gauge, m.Hand.Gauge, 1e-4f, "무차감");
            Assert.AreEqual(queue, m.Hand.QueueCount, "무순환");
            Assert.AreEqual(1, m.Hand.CountAttachedTo(host.Id));
        }

        [Test]
        public void 이_숙주에서_한_줄도_안_도는_카드는_거절되고_값을_안_치른다()
        {
            var def = CoreMatchFixtures.Definition();
            // 통통구슬 — 재조준 가능한 경로를 요구한다. 근접 숙주(경로 없음)에서는 한 줄도 안 돈다.
            var card = CardDef.Default();
            card.Id = "bouncy";
            card.Kind = CardKind.Attach;
            card.Cost = 15;
            card.AttackMods = new[] { new AttackModDef { Kind = AttackModKind.ProjectileBounce, Count = 2, TileRange = 3, DamageMul = 1f } };
            int c = AddCard(def, card);
            var m = CardBattle(def, awakening: 50f);
            var host = Defender(m, new int2(3, 1));

            var r = m.Apply(Command.AttachCard(EntryOf(m, c), host.Id));

            Assert.AreEqual(RejectReason.NeedsHomingRoute, r.Reason);
            Assert.AreEqual(50f, m.Hand.Gauge, 1e-4f);
            Assert.AreEqual(1, m.Hand.QueueCount);
            Assert.AreEqual(0, host.Attack.Mods.Count, "수식자가 안 붙었다");
        }

        [Test]
        public void 부착_상한_셋은_Unit_카드와_Squad_카드가_같이_센다()
        {
            var def = CoreMatchFixtures.Definition();
            var rule = CardRule(TriggerKind.OnKill, EffectKind.SelfStatBuff);
            rule.Effect.Magnitude = 1.1f;
            int u1 = AddAttachCard(def, "u1", 1, rule);
            int u2 = AddAttachCard(def, "u2", 1, rule);
            int s1 = AddSquadCard(def, "s1", 1, SkillStatKind.DamageMul, 1.1f);
            int u3 = AddAttachCard(def, "u3", 1, rule);
            def.Mode.AttachCap = 3;
            var m = CardBattle(def, awakening: 100f);
            var host = Defender(m, new int2(3, 1));

            Assert.IsTrue(m.Apply(Command.AttachCard(EntryOf(m, u1), host.Id)).Accepted);
            Assert.IsTrue(m.Apply(Command.AttachCard(EntryOf(m, s1), host.Id)).Accepted);
            Assert.IsTrue(m.Apply(Command.AttachCard(EntryOf(m, u2), host.Id)).Accepted);
            Assert.AreEqual(RejectReason.AttachCapReached, m.Apply(Command.AttachCard(EntryOf(m, u3), host.Id)).Reason);
        }

        [Test]
        public void 같은_카드_두_장은_사망에서도_장수만큼_터진다()
        {
            var def = CoreCombatFixtures.Definition(defenderDamage: 0f);
            int blast = CoreTriggerFixtures.AddBlastProjectile(def);
            var gift = CardRule(TriggerKind.OnDeath, EffectKind.SelfTileAoe);
            gift.Effect.Magnitude = 5f; gift.Effect.TileRange = 1; gift.Effect.DataIndex = blast;
            int a = AddAttachCard(def, "farewell_a", 1, gift);
            int b = AddAttachCard(def, "farewell_b", 1, gift);
            var m = CardBattle(def);
            var host = Defender(m, new int2(5, 2));
            m.Apply(Command.AttachCard(EntryOf(m, a), host.Id));
            m.Apply(Command.AttachCard(EntryOf(m, b), host.Id));
            var spawned = CoreCombatFixtures.Listen(m, CoreEventKind.ProjectileSpawned);

            host.Inbox.Damage.Add(new DamageEntry { Amount = 99999f, Source = SimEntityId.Match });
            CoreCombatFixtures.Tick(m, 4);

            Assert.AreEqual(2, spawned.Count,
                "억제 키 = 규칙 인스턴스(E2) — 같은 카드 두 장은 둘 다 터진다(사용자 결정 ①, 옛 64비트 마스크는 하나만)");
        }

        [Test]
        public void 같은_카드_두_장은_처치에서도_장수만큼_터진다()
        {
            var def = CoreCombatFixtures.Definition(defenderDamage: 0f);
            int blast = CoreTriggerFixtures.AddBlastProjectile(def);
            var corpse = CardRule(TriggerKind.OnKill, EffectKind.SelfTileAoe);
            corpse.Effect.Magnitude = 5f; corpse.Effect.TileRange = 1; corpse.Effect.DataIndex = blast;
            int a = AddAttachCard(def, "corpse_a", 1, corpse);
            int b = AddAttachCard(def, "corpse_b", 1, corpse);
            var m = CardBattle(def);
            var host = Defender(m, new int2(5, 2));
            var enemy = CoreTriggerFixtures.SpawnEnemy(m, new int2(6, 2));
            m.Apply(Command.AttachCard(EntryOf(m, a), host.Id));
            m.Apply(Command.AttachCard(EntryOf(m, b), host.Id));
            var spawned = CoreCombatFixtures.Listen(m, CoreEventKind.ProjectileSpawned);

            enemy.Inbox.Damage.Add(new DamageEntry { Amount = 99999f, Source = host.Id });
            CoreCombatFixtures.Tick(m, 3);

            Assert.AreEqual(2, spawned.Count, "처치 쪽 억제 루프도 같이 움직인다(두 문)");
        }

        [Test]
        public void 카드의_주기_규칙은_붙자마자_첫_발동한다()
        {
            var def = CoreMatchFixtures.Definition();
            var probe = new CoreTriggerFixtures.ProbeSkill();
            var rule = CardProbe(TriggerKind.PeriodicTimer, probe);
            rule.Effect.Kind = EffectKind.SelfOrbitProjectile;   // 불꽃 팽이 — 숙주 모델과 무관한 payload
            rule.Rule.PeriodSeconds = 6f;
            int c = AddAttachCard(def, "flame_spinner", 1, rule);
            var m = CardBattle(def);
            var host = Defender(m, new int2(3, 1));
            Assert.IsTrue(m.Apply(Command.AttachCard(EntryOf(m, c), host.Id)).Accepted);

            m.Tick();

            Assert.AreEqual(1, probe.Count, "붙이자마자 주기만큼 조용하면 「안 붙었다」로 읽힌다(사용자 결정 2026-08-16)");
            CoreCombatFixtures.Tick(m, 300);
            Assert.AreEqual(1, probe.Count, "그 뒤는 주기 그대로(6초 전엔 두 번째가 없다)");
        }

        [Test]
        public void 디버그_부착은_자원을_건너뛰고_판정은_지나며_숙주가_떠나면_큐로_안_돌아온다()
        {
            var def = CoreMatchFixtures.Definition();
            int card = AddAttachCard(def, "last_flame", 99, LastFlame());
            var m = CardBattle(def, awakening: 0f);
            var host = Defender(m, new int2(3, 1));
            var attached = CoreCombatFixtures.Listen(m, CoreEventKind.CardAttached);

            Assert.IsTrue(m.Apply(Command.DebugAttachCard(card, host.Id)).Accepted, "각성 0 · 손패 무관");
            Assert.AreEqual(RejectReason.DuplicateState, m.Apply(Command.DebugAttachCard(card, host.Id)).Reason, "판정은 지난다");
            Assert.IsTrue(host.Progressive.LethalActive);
            int queue = m.Hand.QueueCount;
            m.Apply(Command.Retire(host.Id));
            Assert.AreEqual(queue, m.Hand.QueueCount, "덱에 없던 카드다");
            Assert.AreEqual(1, attached.Count);
        }

        [Test]
        public void 부착_핸들은_판_안에서_단조_증가하고_재사용되지_않는다()
        {
            var def = CoreMatchFixtures.Definition();
            var rule = CardRule(TriggerKind.OnKill, EffectKind.SelfStatBuff);
            rule.Effect.Magnitude = 1.1f;
            int card = AddAttachCard(def, "plain", 1, rule);
            var m = CardBattle(def);
            var attached = CoreCombatFixtures.Listen(m, CoreEventKind.CardAttached);
            var a = Defender(m, new int2(3, 1));
            m.Apply(Command.AttachCard(EntryOf(m, card), a.Id));
            m.Apply(Command.Retire(a.Id));
            var b = Defender(m, new int2(5, 1));
            m.Apply(Command.AttachCard(EntryOf(m, card), b.Id));

            Assert.AreEqual(2, attached.Count);
            Assert.Greater(attached[1].Amount, attached[0].Amount, "F1 — 낡은 핸들이 새 대상을 가리키지 않는다");
        }
    }
}
