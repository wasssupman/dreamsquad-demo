using NUnit.Framework;
using Unity.Mathematics;
using Wassup.BattleCore;
using Wassup.BattleCore.Effects;
using Wassup.BattleCore.Trigger;
using Wassup.Skills;
using static Wassup.Tests.EditMode.Core.CoreCardFixtures;

namespace Wassup.Tests.EditMode.Core
{
    // battle-core-rebuild unit 7e ④ — **7a·7b 증상 테스트가 안 들던 비자명 카드 규칙만.** 카드 한 장 = 테스트 한 개.
    //
    // 발동과 효과의 존재는 카탈로그 전량이 `CardEffectWitnessTests`(Assets lane)에서 증언한다. 여기 있는 것은 그 장치가
    // 원리적으로 못 보는 것 — **감지자의 문**(게이트 · 파열 순간 · 퇴근 자리)과 **효과의 결말**(치명 타이머의 죽음 ·
    // 조건부 배율)이다. 강제 발동은 감지자를 건너뛰므로, 이 다섯은 진짜 사건으로 연다.
    //
    // ⚠ 수치는 게임 값이 아니라 픽스처다.
    [TestFixture]
    public class CardRuleTests
    {
        private static SkillEntityId S(SimEntityId id) => CoreSkillContext.ToSkill(id);

        private static void Hit(BattleMatch m, Unit victim, float amount)
            => m.Intents.Apply(new SimIntent
            {
                Kind = SimIntentKind.DealDamage, Target = S(victim.Id), Source = SkillEntityId.None, Amount = amount,
            });

        private static BindingDef Blast(MatchDefinition def, TriggerKind trigger)
        {
            var r = CardRule(trigger, TriggerPayload.SelfTileAoe);
            r.Magnitude = 5f;
            r.TileRange = 1;
            r.Period = 1;
            r.DataIndex = CoreTriggerFixtures.AddBlastProjectile(def);
            return r;
        }

        [Test]
        public void 궁지_폭발은_체력이_게이트_아래로_내려간_피격만_센다()
        {
            // cornered_burst — `OnDamagedN × HpBelow(Self)`. 게이트는 **피격 뒤 체력**으로 묻고(감지자가 체력 갱신 뒤에 올린다),
            // 통과 못 한 피격은 카운터도 안 올린다. 공격 쪽 게이트(처형타)만 테스트가 있었다(`TriggerDispatchTests`).
            var def = CoreMatchFixtures.Definition();
            var rule = Blast(def, TriggerKind.OnDamagedN);
            rule.Gate = GateKind.HpBelow;
            rule.GateSubject = GateSubject.Self;
            rule.GateValue = 0.5f;
            int card = AddAttachCard(def, "fixture_cornered_burst", 1, rule);
            var m = CardBattle(def);
            var host = Defender(m, new int2(3, 1));
            Assert.IsTrue(m.Apply(Command.DebugAttachCard(card, host.Id)).Accepted);
            var fired = CoreMatchFixtures.Listen(m, CoreEventKind.TriggerFired);

            Hit(m, host, host.MaxHealth * 0.2f);
            CoreCombatFixtures.Tick(m, 2);
            Assert.AreEqual(0, fired.Count, "게이트 위(80%)의 피격은 안 센다");

            Hit(m, host, host.MaxHealth * 0.4f);
            CoreCombatFixtures.Tick(m, 2);
            Assert.AreEqual(1, fired.Count, "게이트 아래(40%)로 내려간 그 피격에서 터진다");
        }

        [Test]
        public void 실드가_깨지는_그_피격에서만_파열_규칙이_한_번_터진다()
        {
            // shield_burst — 감지자는 실드 합 **양수 → 0** 전이다. 실드가 일부만 막는 피격 · 실드 없는 피격은 파열이 아니다.
            var def = CoreMatchFixtures.Definition();
            int card = AddAttachCard(def, "fixture_shield_burst", 1, Blast(def, TriggerKind.OnShieldBreak));
            var m = CardBattle(def);
            var host = Defender(m, new int2(3, 1));
            Assert.IsTrue(m.Apply(Command.DebugAttachCard(card, host.Id)).Accepted);
            float shield = host.MaxHealth * 0.2f;
            m.Intents.Apply(new SimIntent
            {
                Kind = SimIntentKind.GrantShield, Target = S(host.Id), Source = S(host.Id), Amount = shield,
            });
            CoreCombatFixtures.Tick(m, 2);
            var fired = CoreMatchFixtures.Listen(m, CoreEventKind.TriggerFired);
            var spawned = CoreMatchFixtures.Listen(m, CoreEventKind.ProjectileSpawned);

            Hit(m, host, shield * 0.5f);
            CoreCombatFixtures.Tick(m, 2);
            Assert.AreEqual(0, fired.Count, "실드가 남은 피격은 파열이 아니다");

            Hit(m, host, shield);
            CoreCombatFixtures.Tick(m, 2);
            Assert.AreEqual(1, fired.Count, "깨지는 그 피격에서 한 번");
            Assert.AreEqual(1, spawned.Count, "폭발이 나갔다");

            Hit(m, host, shield);
            CoreCombatFixtures.Tick(m, 2);
            Assert.AreEqual(1, fired.Count, "실드 없는 피격은 다시 안 터진다");
        }

        [Test]
        public void 퇴근_운석은_비워진_칸_중심에_몸_없이_떨어진다()
        {
            // severance_meteor — `OnRetire × SelfTileAoe` = 자리에 떨어지는 것(제약 13). 퇴근은 커맨드라 `Immediate` seam 이고
            // 자리는 파괴 **직전에** 스냅샷한 칸 중심, 원점 항은 0(7b 8-1). 몸이 붙으면 배스티온이 퇴근할 때만 운석이 넓어진다.
            var def = CoreMatchFixtures.Definition();
            int card = AddAttachCard(def, "fixture_severance_meteor", 1, Blast(def, TriggerKind.OnRetire));
            var m = CardBattle(def);
            var host = Defender(m, new int2(3, 1));
            Assert.IsTrue(m.Apply(Command.DebugAttachCard(card, host.Id)).Accepted);
            var vacated = m.Map.CenterOf(host.Footprint.Anchor);
            var spawned = CoreMatchFixtures.Listen(m, CoreEventKind.ProjectileSpawned);

            Assert.IsTrue(m.Apply(Command.Retire(host.Id)).Accepted);
            CoreCombatFixtures.Tick(m, 2);

            Assert.AreEqual(1, spawned.Count, "퇴근 한 번에 운석 하나");
            Assert.AreEqual(0f, spawned[0].SiteFired.OriginBody, 1e-6f, "몸 0 = 그 자리는 칸이다");
            Assert.AreEqual(vacated.x, spawned[0].SiteTarget.Pos.x, 1e-4f, "비워진 칸 중심");
            Assert.AreEqual(vacated.z, spawned[0].SiteTarget.Pos.z, 1e-4f, "비워진 칸 중심");
        }

        [Test]
        public void 마지막_불꽃은_시간이_끝나면_죽고_처치로_세지_않는다()
        {
            // last_flame — 치명 타이머의 끝은 **출처 없는 죽음**이다(옛 `LethalTimerSystem` 이 `DeadTag` 를 붙였다). 처치 사건이
            // 안 나므로 각성·점수를 주지 않는다. 7b 는 「타이머가 선다」까지만 봤다(`CardAttachTests`).
            var def = CoreMatchFixtures.Definition();
            var rule = CardRule(TriggerKind.None, TriggerPayload.SelfBuffLethal);
            rule.Magnitude = 1.9f;
            rule.Duration = 0.5f;
            rule.FireCap = 1;
            int card = AddAttachCard(def, "fixture_last_flame", 1, rule);
            var m = CardBattle(def, awakening: 50f);
            var host = Defender(m, new int2(3, 1));
            Assert.IsTrue(m.Apply(Command.DebugAttachCard(card, host.Id)).Accepted);
            var slain = CoreMatchFixtures.Listen(m, CoreEventKind.UnitSlain);
            float gauge = m.Hand.Gauge;

            CoreCombatFixtures.Tick(m, MatchClock.TicksOf(rule.Duration, BattleMatch.Dt) + 3);

            Assert.IsNull(m.World.Find(host.Id), "시간이 끝나면 죽고 사라진다");
            Assert.AreEqual(0, slain.Count, "출처 없는 죽음 — 처치 사건이 없다");
            Assert.AreEqual(gauge, m.Hand.Gauge, 1e-5f, "그래서 각성도 안 준다");
        }

        [Test]
        public void 파쇄의_찬가_피해_배율은_군중_제어나_지속_피해_중인_대상에게만_붙는다()
        {
            // shatter_hymn — Squad `DamageVsCc`. 배율은 **때리는 쪽**에 걸리고 대상 조건(군중 제어 ∨ 지속 피해)은 피해 산식이 묻는다
            // (`EffectApply.DamageMul`). 걸리는 것(스탯 슬롯)은 프로브가 보지만 조건부 소비는 여기서만 보인다.
            var def = CoreMatchFixtures.Definition();
            int card = AddSquadCard(def, "fixture_shatter_hymn", 1, SkillStatKind.DamageVsCcMul, 1.5f);
            var m = CardBattle(def);
            var host = Defender(m, new int2(3, 1));
            m.Apply(Command.DebugSpawnEnemy(0, new int2(6, 1)));
            var plain = m.World.Units[m.World.Units.Count - 1];
            m.Apply(Command.DebugSpawnEnemy(0, new int2(6, 3)));
            var stunned = m.World.Units[m.World.Units.Count - 1];
            m.Apply(Command.DebugSpawnEnemy(0, new int2(7, 3)));
            var burning = m.World.Units[m.World.Units.Count - 1];
            float baseline = EffectApply.DamageMul(host, stunned);

            Assert.IsTrue(m.Apply(Command.DebugAttachCard(card, host.Id)).Accepted);
            m.Intents.Apply(new SimIntent
            {
                Kind = SimIntentKind.ApplyCc, Target = S(stunned.Id), Selector = (int)SkillCcKind.Stun, Duration = 10f,
            });
            m.Intents.Apply(new SimIntent
            {
                Kind = SimIntentKind.ApplyDot, Target = S(burning.Id), Amount = 1f, HitThreshold = 1f, Duration = 10f,
            });
            CoreCombatFixtures.Tick(m, 2);

            Assert.IsTrue(stunned.Cc.Any && burning.Dot.Any, "전제 — 둘 다 걸렸다");
            Assert.AreEqual(baseline, EffectApply.DamageMul(host, plain), 1e-5f, "아무것도 안 걸린 대상엔 안 붙는다");
            Assert.AreEqual(baseline * 1.5f, EffectApply.DamageMul(host, stunned), 1e-4f, "군중 제어 중");
            Assert.AreEqual(baseline * 1.5f, EffectApply.DamageMul(host, burning), 1e-4f, "지속 피해 중");
        }
    }
}
