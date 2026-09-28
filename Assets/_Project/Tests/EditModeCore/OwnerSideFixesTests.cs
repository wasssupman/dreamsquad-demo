using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using Wassup.BattleCore;
using Wassup.BattleCore.Effects;
using Wassup.BattleCore.Trigger;
using Wassup.Skills;
using Wassup.Skills.Concrete;
using static Wassup.Tests.EditMode.Core.CoreCardFixtures;

namespace Wassup.Tests.EditMode.Core
{
    // skill-data-table unit 2 — **소유자 쪽 결손.** 스킬은 소유자를 묻지 않고, 소유자마다 달라야 하는 값은 그 값의 담당자가 든다.
    //   · 주체 없는 시전(판 시전 · 판 주기 · 표식)의 진영 = 규칙 인스턴스의 시전 진영(`Binding.CastFaction` — 붙인 쪽이 채운다)
    //   · 표식의 정체 = 효과(`CardBindings.IsMarked` 가 출처 꼬리표를 묻지 않는다)
    //   · 카드 발동 연출(카드 펄스 · 발동 임팩트)의 게이트 = 그 줄이 카드 보유 줄인가(`MatchDefinition.IsCardRow` · U16)
    //   · 「부착 즉시 첫 발동」은 카드 행 부착 경로 한정(`BindingRegistry.ArmFirstFireOnAttach`) — 유닛 저작 · 온천 위상은 그대로
    //   · 연출은 효과 기준(U15) — 강화 오라 꼬리표는 효과(실행자)가 박고, 병합 칸 규칙(`SimIntent.PerBindingSlot`)은 그 꼬리표를 안 읽는다
    // ⚠ 여기 수치는 게임 값이 아니라 픽스처다.
    [TestFixture]
    public class OwnerSideFixesTests
    {
        // ── 시전 진영 = 규칙 인스턴스 값 ────────────────────────────────────

        [Test]
        public void 시전_진영은_붙인_쪽이_채운다_유닛_저작은_그_유닛_카드는_쓴_쪽()
        {
            var def = CoreMatchFixtures.Definition();
            CoreTriggerFixtures.GiveEnemy(def, 0, CoreTriggerFixtures.Probe(TriggerKind.OnKill, new CoreTriggerFixtures.ProbeSkill()));
            var mark = CardRule(TriggerKind.None, EffectKind.BountyMark);
            mark.Effect.Magnitude = 2f;
            mark.Rule.FireCap = 1;
            int card = AddAttachCard(def, "mark", 1, mark);
            def.Cards[card].TargetsEnemies = true;
            var m = CardBattle(def);
            var enemy = CoreTriggerFixtures.SpawnEnemy(m, new int2(5, 2));
            Assert.AreEqual(1, enemy.Bindings.Count);
            Assert.AreEqual(Faction.EnemyUnit, enemy.Bindings[0].CastFaction, "유닛 저작 = 그 유닛의 진영");

            var fired = CoreCombatFixtures.Listen(m, CoreEventKind.TriggerFired);
            Assert.IsTrue(m.Apply(Command.AttachCard(EntryOf(m, card), enemy.Id)).Accepted);
            var markRule = enemy.Bindings[enemy.Bindings.Count - 1];
            Assert.AreEqual(BattleMatch.PlayerFaction, markRule.CastFaction, "카드 = 손패의 주인(숙주가 적이어도)");
            Assert.AreEqual(1, fired.Count);
            Assert.AreEqual(BattleMatch.PlayerFaction, fired[0].Faction, "표식 발동 진영 = 규칙 인스턴스 값(오늘 = 플레이어)");
        }

        [Test]
        public void 판_주기_규칙의_시전_진영은_인스턴스_값에서_나온다()
        {
            var def = CoreMatchFixtures.Definition();
            var seen = new List<Faction>();
            var probe = new CoreTriggerFixtures.ProbeSkill { OnExecute = (c, t, p, ctx) => seen.Add(c.Faction) };
            var row = CoreTriggerFixtures.Probe(TriggerKind.PeriodicTimer, probe);
            row.Rule.PeriodSeconds = BattleMatch.Dt;
            int[] idx = CoreTriggerFixtures.Add(def, row, row);
            def.ConfigHash = def.ComputeConfigHash();
            var m = CoreMatchFixtures.BeginBattle(def);
            Assert.AreEqual(BattleMatch.PlayerFaction, m.Bindings.Attach(null, in def.Bindings[idx[0]], idx[0], m.Clock.Tick).CastFaction,
                            "판 호스트 기본 = 플레이어");
            m.Bindings.Attach(null, in def.Bindings[idx[1]], idx[1], m.Clock.Tick, Faction.EnemyUnit);

            m.Tick();

            CollectionAssert.AreEqual(new[] { BattleMatch.PlayerFaction, Faction.EnemyUnit }, seen,
                                      "감지자가 진영을 손으로 박지 않는다 — 붙인 쪽이 정한 값이 시전자 진영이다");
        }

        // ── 부착 즉시 첫 발동(카드 행 부착 경로 한정) ─────────────────────────

        private static RuleRow Periodic(CoreTriggerFixtures.ProbeSkill probe, bool card)
        {
            var r = card ? CardProbe(TriggerKind.PeriodicTimer, probe) : CoreTriggerFixtures.Probe(TriggerKind.PeriodicTimer, probe);
            r.Rule.PeriodSeconds = 1f;
            if (card) r.Effect.Kind = EffectKind.SelfOrbitProjectile;   // 숙주 모델과 무관한 payload(부착 판정 통과용)
            return r;
        }

        [Test]
        public void 같은_틱에_놓고_붙인_카드의_주기는_부착으로_시작하고_유닛_저작_주기는_스폰으로_시작한다()
        {
            var def = CoreMatchFixtures.Definition();
            def.Units[0].Cost = 0;
            var innate = new CoreTriggerFixtures.ProbeSkill();
            var carded = new CoreTriggerFixtures.ProbeSkill();
            CoreTriggerFixtures.GiveUnit(def, 0, Periodic(innate, card: false));
            int c = AddAttachCard(def, "spinner", 1, Periodic(carded, card: true));
            var m = CardBattle(def);

            Assert.AreEqual(RejectReason.None, m.Apply(Command.PlaceDefender(0, new int2(3, 1))).Reason, "배치");
            var host = CoreMatchFixtures.PlacedDefender(m);
            Assert.AreEqual(RejectReason.None, m.Apply(Command.AttachCard(EntryOf(m, c), host)).Reason, "같은 틱 부착");

            m.Tick();
            Assert.AreEqual(1, carded.Count, "카드 = 부착 즉시 첫 발동");
            Assert.AreEqual(0, innate.Count, "유닛 저작 = 스폰부터 한 주기");
            CoreCombatFixtures.Tick(m, 30);
            Assert.AreEqual(1, carded.Count);
            Assert.AreEqual(0, innate.Count);
            CoreCombatFixtures.Tick(m, 60);
            Assert.AreEqual(2, carded.Count, "그 뒤는 주기 그대로");
            Assert.AreEqual(1, innate.Count);
        }

        [Test]
        public void 온천_열기_위상은_같은_숙주에_카드_주기가_붙어도_그대로다()
        {
            var def = CoreGimmickFixtures.With(CoreCombatFixtures.Definition(defenderDamage: 0f), CoreGimmickFixtures.Onsen());
            var carded = new CoreTriggerFixtures.ProbeSkill();
            int c = AddAttachCard(def, "spinner", 1, Periodic(carded, card: true));
            var m = CardBattle(def);
            var heat = new List<CoreEvent>();
            m.Bus.Subscribe(CoreEventKind.GimmickTriggered, 0, e => { if (e.Arg == (int)GimmickKind.Onsen) heat.Add(e); });

            var withCard = Defender(m, new int2(2, 1));
            var control = Defender(m, new int2(4, 1));
            Assert.AreEqual(RejectReason.None, m.Apply(Command.AttachCard(EntryOf(m, c), withCard.Id)).Reason);
            CoreCombatFixtures.Tick(m, 120);

            Assert.Greater(carded.Count, 0, "카드 주기는 돈다");
            int first = heat.Find(e => e.A == withCard.Id).Tick;
            Assert.AreEqual(heat.Find(e => e.A == control.Id).Tick, first, "카드가 붙은 숙주의 열기 위상 = 카드 없는 숙주");
            Assert.Greater(first, 1, "열기는 부착 즉시 발동이 아니다(위상 보정 한 틱만)");
        }

        // ── 표식의 정체 = 효과 ─────────────────────────────────────────────

        [Test]
        public void 표식_판정은_효과로_가른다_출처를_묻지_않는다()
        {
            var def = CoreMatchFixtures.Definition();
            var m = CoreMatchFixtures.BeginBattle(def);
            var enemy = CoreTriggerFixtures.SpawnEnemy(m, new int2(5, 2));
            Assert.IsFalse(CardBindings.IsMarked(enemy));
            var other = CoreTriggerFixtures.Rule(TriggerKind.None, EffectKind.SelfStatBuff);
            other.Rule.Origin = BindingOrigin.Card;
            CoreTriggerFixtures.AttachRuntime(m, enemy, other);
            Assert.IsFalse(CardBindings.IsMarked(enemy), "카드 출처라도 표식 효과가 아니면 표식이 아니다");

            var mark = CoreTriggerFixtures.Rule(TriggerKind.None, EffectKind.BountyMark);
            Assert.AreNotEqual(BindingOrigin.Card, mark.Rule.Origin, "픽스처 전제 — 카드 출처가 아닌 표식 줄");
            CoreTriggerFixtures.AttachRuntime(m, enemy, mark);
            Assert.IsTrue(CardBindings.IsMarked(enemy), "표식 효과를 든 규칙 = 표식(출처 무관)");
        }

        // ── 카드 발동 연출 = 카드 보유 줄만(U16) ────────────────────────────

        [Test]
        public void 카드_발동_연출의_게이트는_카드_보유_줄에서만_참이다()
        {
            // 뷰(`CoreVfxSpawner.OnTriggerFired` · `CoreUnitOverheadUiLayer`)는 발동 사건의 줄 번호를 이 함수에 묻는다.
            var def = CoreMatchFixtures.Definition();
            var innate = CoreTriggerFixtures.Rule(TriggerKind.PeriodicTimer, EffectKind.SelfStatBuff);
            innate.Rule.PeriodSeconds = BattleMatch.Dt;
            innate.Effect.Magnitude = 1.1f;
            CoreTriggerFixtures.GiveUnit(def, 0, innate);
            var carded = CardRule(TriggerKind.PeriodicTimer, EffectKind.SelfStatBuff);
            carded.Rule.PeriodSeconds = BattleMatch.Dt;
            carded.Effect.Magnitude = 1.1f;
            int c = AddAttachCard(def, "same_effect", 1, carded);
            var m = CardBattle(def);
            var host = Defender(m, new int2(3, 1));
            Assert.AreEqual(RejectReason.None, m.Apply(Command.AttachCard(EntryOf(m, c), host.Id)).Reason);
            var fired = CoreCombatFixtures.Listen(m, CoreEventKind.TriggerFired);

            m.Tick();

            int innateRow = def.Units[0].Bindings[0], cardRow = def.Cards[c].Bindings[0];
            Assert.IsTrue(fired.Exists(e => e.DefIndex == innateRow), "유닛 저작 줄도 발동했다");
            Assert.IsTrue(fired.Exists(e => e.DefIndex == cardRow), "카드 줄도 발동했다");
            foreach (var e in fired)
                Assert.AreEqual(e.DefIndex == cardRow, m.Definition.IsCardRow(e.DefIndex), $"같은 효과 · 같은 숙주 — 줄 {e.DefIndex} 는 보유로만 갈린다");
            Assert.IsFalse(m.Definition.IsCardRow(-1), "런타임 조립 줄");
            Assert.IsFalse(m.Definition.IsCardRow(def.Bindings.Length), "표 밖");
        }

        // ── 연출은 효과 기준(U15) ───────────────────────────────────────────

        [Test]
        public void 같은_효과면_소유자가_달라도_같은_연출_꼬리표와_같은_칸_규칙이다()
        {
            // 강화 오라(`CoreDcAuraVisualPool`)는 이 판정(`HasActiveDreamcatcherModifier`)만 본다 — 카드가 들든 유닛이 들든 같아야 한다.
            var m = CoreMatchFixtures.BeginBattle(CoreMatchFixtures.Definition());
            var innateHost = CoreTriggerFixtures.SpawnDefender(m, new int2(2, 1));
            var cardHost = CoreTriggerFixtures.SpawnDefender(m, new int2(4, 1));
            var innate = CoreTriggerFixtures.Rule(TriggerKind.PeriodicTimer, EffectKind.SelfStatBuff);
            innate.Rule.PeriodSeconds = BattleMatch.Dt;
            innate.Effect.Magnitude = 1.1f;
            var carded = innate;
            carded.Rule.Origin = BindingOrigin.Card;
            Assert.AreNotEqual(innate.Rule.Origin, carded.Rule.Origin, "픽스처 전제 — 소유자만 다르다");
            var a = CoreTriggerFixtures.AttachRuntime(m, innateHost, innate);
            var b = CoreTriggerFixtures.AttachRuntime(m, cardHost, carded);

            for (int t = 0; t < 5; t++) m.Tick();

            foreach (var (host, rule) in new[] { (innateHost, a), (cardHost, b) })
            {
                Assert.IsTrue(ModifierAuraClassifier.HasActiveDreamcatcherModifier(host.Modifiers.Slots), $"{rule.Def.Origin} — 강화 오라");
                var slot = host.Modifiers.Slots[0];
                Assert.AreEqual(ModifierOrigin.Dreamcatcher, slot.Origin, $"{rule.Def.Origin} — 꼬리표는 효과가 박는다");
                Assert.AreEqual(SlotTag.OfBinding(rule.InstanceId), slot.Key.Tag, $"{rule.Def.Origin} — 규칙 인스턴스 칸(옛 규칙 그대로)");
            }
        }

        [Test]
        public void 빈사폭주도_광란과_같은_강화_오라를_켠다_병합_칸은_트리거마다_그대로다()
        {
            // U15 후속 — 같은 효과(`SelfStatBuff`)는 트리거가 달라도 같은 연출 꼬리표다. 예전엔 빈사폭주(`HealthThreshold`)
            // 만 꼬리표가 달라 강화 오라가 안 켜졌다(`SelfStatBuffSkill.cs` 옛 주석) — 그 예외를 걷는다.
            // 병합 칸 규칙(`PerBindingSlot`)은 트리거별로 그대로 — 광란은 규칙 인스턴스 칸, 빈사폭주는 배치 칸.
            var attackRow = CoreTriggerFixtures.Rule(TriggerKind.AttackN, EffectKind.SelfStatBuff);
            attackRow.Effect.Magnitude = 1.1f;
            var thresholdRow = CoreTriggerFixtures.Rule(TriggerKind.HealthThreshold, EffectKind.SelfStatBuff);
            thresholdRow.Effect.Magnitude = 1.1f;
            Assert.AreEqual(SelfStatBuffSkill.Id, attackRow.Rule.SkillId, "픽스처 전제 — 라우팅이 광란으로 간다");
            Assert.AreEqual(ThresholdSelfBuffSkill.Id, thresholdRow.Rule.SkillId, "픽스처 전제 — 라우팅이 빈사폭주로 간다");

            var m = CoreMatchFixtures.BeginBattle(CoreMatchFixtures.Definition());
            var attackHost = CoreTriggerFixtures.SpawnDefender(m, new int2(2, 1));
            var thresholdHost = CoreTriggerFixtures.SpawnDefender(m, new int2(4, 1));
            var attackBinding = CoreTriggerFixtures.AttachRuntime(m, attackHost, attackRow);
            var thresholdBinding = CoreTriggerFixtures.AttachRuntime(m, thresholdHost, thresholdRow);

            // 강제 발화 — 카운터·게이트·감지자를 건너뛰고 실행자만(tools.md 「트리거 강제 발화」). 공격 N회·빈사 진입을
            // 실제로 재현하지 않아도 「이 트리거가 이 효과를 실행하면 무엇이 걸리나」를 직접 잰다.
            Assert.IsTrue(m.Apply(Command.DebugFireBinding(attackHost.Id, attackBinding.InstanceId)).Accepted);
            Assert.IsTrue(m.Apply(Command.DebugFireBinding(thresholdHost.Id, thresholdBinding.InstanceId)).Accepted);

            Assert.IsTrue(ModifierAuraClassifier.HasActiveDreamcatcherModifier(attackHost.Modifiers.Slots), "광란 — 강화 오라(기존 동작 무변)");
            Assert.IsTrue(ModifierAuraClassifier.HasActiveDreamcatcherModifier(thresholdHost.Modifiers.Slots), "빈사폭주 — 같은 효과라 같은 강화 오라(U15)");

            var attackSlot = attackHost.Modifiers.Slots[0];
            var thresholdSlot = thresholdHost.Modifiers.Slots[0];
            Assert.AreEqual(ModifierOrigin.Dreamcatcher, attackSlot.Origin, "광란 — 꼬리표(무변)");
            Assert.AreEqual(ModifierOrigin.Dreamcatcher, thresholdSlot.Origin, "빈사폭주 — 꼬리표가 같아졌다");

            Assert.AreEqual(SlotTag.OfBinding(attackBinding.InstanceId), attackSlot.Key.Tag, "광란 — 규칙 인스턴스 칸(무변)");
            Assert.AreEqual(new SlotTag(SlotKind.OnPlace, 0), thresholdSlot.Key.Tag, "빈사폭주 — 배치 칸(무변, 꼬리표와 무관)");
        }

        [Test]
        public void 병합_칸_규칙은_연출_꼬리표를_읽지_않는다()
        {
            var m = CoreMatchFixtures.BeginBattle(CoreMatchFixtures.Definition());
            var host = CoreTriggerFixtures.SpawnDefender(m, new int2(2, 1));
            var rule = CoreTriggerFixtures.AttachRuntime(m, host, CoreTriggerFixtures.Rule(TriggerKind.PeriodicTimer, EffectKind.SelfStatBuff));
            var id = CoreSkillContext.ToSkill(host.Id);
            m.Intents.Begin(rule, BattleMatch.PlayerFaction, null);
            // 강화 오라 꼬리표인데 칸 규칙은 끔 → 배치 칸 / 다른 꼬리표인데 칸 규칙은 켬 → 인스턴스 칸.
            m.Intents.Apply(new SimIntent
            {
                Kind = SimIntentKind.ApplyStatModifier, Target = id, Source = id, Selector = (int)SkillStatKind.DamageMul,
                Op = SkillCombineOp.FromAuthoredMultiplier, Origin = SkillModifierOrigin.Dreamcatcher, Amount = 1.1f, StackId = 7,
            });
            m.Intents.Apply(new SimIntent
            {
                Kind = SimIntentKind.ApplyStatModifier, Target = id, Source = id, Selector = (int)SkillStatKind.AttackSpeedMul,
                Op = SkillCombineOp.FromAuthoredMultiplier, Origin = SkillModifierOrigin.OnPlace, PerBindingSlot = true, Amount = 1.1f,
            });
            m.Intents.End();

            Assert.AreEqual(2, host.Modifiers.Count);
            Assert.AreEqual(new SlotTag(SlotKind.OnPlace, 7), host.Modifiers.Slots[0].Key.Tag, "꼬리표만으로는 인스턴스 칸이 아니다");
            Assert.AreEqual(SlotTag.OfBinding(rule.InstanceId), host.Modifiers.Slots[1].Key.Tag, "칸 규칙이 인스턴스 칸을 정한다");
        }

        [Test]
        public void 표식_효과_줄이_붙는_사건은_소유자와_무관하게_효과_종류를_싣는다()
        {
            // 표식 별(`CoreStatusFxSpawner`)은 이 사건의 효과 종류로 켠다 — 카드 부착 사건(`CardAttached`)이 아니다.
            var m = CoreMatchFixtures.BeginBattle(CoreMatchFixtures.Definition());
            var enemy = CoreTriggerFixtures.SpawnEnemy(m, new int2(5, 2));
            var attached = CoreCombatFixtures.Listen(m, CoreEventKind.BindingAttached);
            var mark = CoreTriggerFixtures.Rule(TriggerKind.None, EffectKind.BountyMark);
            Assert.AreNotEqual(BindingOrigin.Card, mark.Rule.Origin, "픽스처 전제 — 카드가 아닌 소유자");
            CoreTriggerFixtures.AttachRuntime(m, enemy, mark);
            m.Tick();   // 사건은 틱 경계에서 배달된다
            Assert.IsTrue(attached.Exists(e => e.A == enemy.Id && (EffectKind)(int)e.Amount == EffectKind.BountyMark),
                          "카드가 아닌 소유자의 표식 줄도 표식 효과로 보인다");
        }
    }
}
