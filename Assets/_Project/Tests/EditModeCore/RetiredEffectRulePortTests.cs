using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using Somnia.Battle.BattleCore;
using Somnia.Battle.BattleCore.Effects;
using Somnia.Battle.BattleCore.Trigger;
using Somnia.Battle.Skills;
using Somnia.Battle.Skills.Concrete;
using static Somnia.Battle.Tests.EditMode.Core.CoreCombatFixtures;

namespace Somnia.Battle.Tests.EditMode.Core
{
    // battle-core-rebuild unit 9 구현 2 — 옛 효과 통합 테스트가 증언하던 **규칙**을 코어로 옮긴다.
    //
    // 짝 지도 `ledgers/retire-test-pairs.md` 「규칙 누락 의심」 12~14. 기존 코어 테스트는 슬롯·`Effective`
    // 값까지만 본다(EffectTileTests · FieldCarrierTests · CardRuleTests 의 순수 `EffectApply.DamageMul`).
    // 여기서는 그 값이 **판의 실제 피해·주기에 곱해지는지**를 판 위에서 묻는다.
    //
    // ⚠ 수치는 픽스처다. 기대값은 전부 기준 판(효과 없음)에서 잰 값 × 건 배율로 파생한다.
    public class RetiredEffectRulePortTests
    {
        private static BattleMatch Match(MatchDefinition def)
        {
            var m = new BattleMatch(def);
            m.Begin();
            return m;
        }

        private static readonly SimEntityId SourceA = new SimEntityId(9001);
        private static readonly SimEntityId SourceB = new SimEntityId(9002);

        // 방어유닛 (4,1) · 적 (5,1) 인접 결투. 적은 체력이 넉넉해 창 동안 안 죽는다.
        // `before` 는 적이 서기 **전에** 방어유닛에게 효과를 거는 자리다(첫 타부터 반영).
        private static (BattleMatch m, Unit defender, Unit enemy, List<CoreEvent> hits) Duel(
            System.Action<Unit> before = null, System.Action<Unit> onEnemy = null)
        {
            var m = Match(Definition(defenderDamage: 10f, defenderCooldown: 1f, enemyHealth: 1e6f));
            m.Apply(Command.DebugSpawnDefender(0, new int2(4, 1)));
            var d = First(m, UnitKind.Defender);
            before?.Invoke(d);
            m.Apply(Command.DebugSpawnEnemy(0, new int2(5, 1)));
            var e = First(m, UnitKind.Enemy);
            onEnemy?.Invoke(e);
            var hits = new List<CoreEvent>();
            m.Bus.Subscribe(CoreEventKind.DamageApplied, 0, ev => { if (ev.B == e.Id) hits.Add(ev); });
            return (m, d, e, hits);
        }

        private static float Sum(List<CoreEvent> hits)
        {
            float s = 0f;
            for (int i = 0; i < hits.Count; i++) s += hits[i].Amount;
            return s;
        }

        // ══ 12 · EffectIntegrationTests ════════════════════════════════════════

        // 옛 EffectIntegrationTests::Combat_Applies_DamageMul_And_AttackSpeedMul_Via_ModifierStats (피해 절반) — 공격력 배율이 실제로 낸 피해에 곱해진다
        [Test]
        public void 공격력_배율이_실제_피해에_곱해진다()
        {
            const float mul = 2f;
            var (bm, _, _, baseHits) = Duel();
            bm.Tick();
            Assert.AreEqual(1, baseHits.Count, "전제 — 기준 판 첫 타");

            var (m, d, _, hits) = Duel(before: u =>
                u.Modifiers.Apply(ModifierKey.Of(SourceA, StatKind.DamageMul, CombineOp.Multiplicative), mul, 100f));
            m.Tick();

            Assert.AreEqual(mul, d.Modifiers.Effective.DamageMul, 1e-5f, "전제 — 실효 배율");
            Assert.AreEqual(1, hits.Count);
            Assert.AreEqual(baseHits[0].Amount * mul, hits[0].Amount, 1e-3f,
                "슬롯·실효 값만 맞고 판의 피해가 그대로면 버프가 «보이기만» 한다");
        }

        // 옛 EffectIntegrationTests::Combat_Applies_SecondSlotMul_Stacked_With_DamageMul_Via_ModifierStats — 증가 계열 두 슬롯은 곱이 아니라 합으로 실제 피해에 반영된다
        [Test]
        public void 가산_공격력_두_슬롯은_합으로_실제_피해에_곱해진다()
        {
            const float a = 1.0f, b = 0.3f;
            var (bm, _, _, baseHits) = Duel();
            bm.Tick();

            var (m, _, _, hits) = Duel(before: u =>
            {
                u.Modifiers.Apply(ModifierKey.Of(SourceA, StatKind.DamageMul, CombineOp.Additive), a, 100f);
                u.Modifiers.Apply(ModifierKey.Of(SourceB, StatKind.DamageMul, CombineOp.Additive), b, 100f);
            });
            m.Tick();

            Assert.AreEqual(1, hits.Count);
            Assert.AreEqual(baseHits[0].Amount * (1f + a + b), hits[0].Amount, 1e-3f,
                "증가는 더해진다 — 곱(×(1+a)(1+b))이면 여기가 갈린다");
        }

        // 옛 EffectIntegrationTests::Combat_Applies_DamageMul_And_AttackSpeedMul_Via_ModifierStats (주기 절반) — 공속 배율은 실제 쿨다운을 나눈다(저작 간격은 그대로)
        [Test]
        public void 공속_배율이_실제_쿨다운과_타격_주기에_곱해진다()
        {
            const float mul = 2f;
            var (bm, bd, _, baseHits) = Duel();
            var (m, d, _, hits) = Duel(before: u =>
                u.Modifiers.Apply(ModifierKey.Of(SourceA, StatKind.AttackSpeedMul, CombineOp.Multiplicative), mul, 100f));

            bm.Tick();
            m.Tick();
            Assert.AreEqual(bd.Attack.CooldownRemaining / mul, d.Attack.CooldownRemaining, 1e-4f,
                "첫 타 직후 쿨다운 = 기준 쿨다운 / 공속 배율");
            Assert.AreEqual(bd.Attack.Interval, d.Attack.Interval, 1e-6f, "저작 간격 자체는 안 바뀐다");

            // 창 3초 — 타격 수가 배율만큼 는다(창 경계의 ±1 은 허용).
            int window = (int)math.ceil(3f / BattleMatch.Dt);
            Tick(bm, window);
            Tick(m, window);
            Assert.Greater(baseHits.Count, 1, "전제 — 기준 판도 여러 번 때렸다");
            Assert.AreEqual(baseHits.Count * mul, hits.Count, 1.01f, "공속 ×2 면 같은 창에 두 배 때린다");
        }

        // ══ 13 · DreamcatcherCombatDamageTest ══════════════════════════════════

        // 옛 DreamcatcherCombatDamageTest::DamageMulBuff_IncreasesDealtDamage — 카드(실제 경로)로 건 공격력 버프가 창 동안 실제로 낸 피해를 늘린다
        [Test]
        public void 카드로_건_공격력_버프가_창_동안_실제_피해를_늘린다()
        {
            const float mul = 3f;
            float Dealt(bool buffed)
            {
                var def = Definition(defenderDamage: 10f, defenderCooldown: 1f, enemyHealth: 1e6f);
                int card = CoreCardFixtures.AddSquadCard(def, "fixture_damage_up", 1, SkillStatKind.DamageMul, mul);
                var m = CoreCardFixtures.CardBattle(def);
                var host = CoreCardFixtures.Defender(m, new int2(4, 1));
                if (buffed) Assert.IsTrue(m.Apply(Command.DebugAttachCard(card, host.Id)).Accepted, "카드 부착");
                Tick(m, 5);   // 부착 발동이 가라앉을 틈 — 적은 그 뒤에 선다(두 판이 같은 박자로 때린다)
                if (buffed) Assert.AreEqual(mul, host.Modifiers.Effective.DamageMul, 1e-4f, "전제 — 실효 배율");

                m.Apply(Command.DebugSpawnEnemy(0, new int2(5, 1)));
                var e = First(m, UnitKind.Enemy);
                var hits = new List<CoreEvent>();
                m.Bus.Subscribe(CoreEventKind.DamageApplied, 0, ev => { if (ev.B == e.Id && ev.A == host.Id) hits.Add(ev); });
                Tick(m, (int)math.ceil(6f / BattleMatch.Dt));
                Assert.Greater(hits.Count, 1, "전제 — 창 동안 여러 번 때렸다");
                return Sum(hits);
            }

            float baseDealt = Dealt(false);
            float buffedDealt = Dealt(true);
            Assert.Greater(baseDealt, 0f);
            Assert.AreEqual(baseDealt * mul, buffedDealt, baseDealt * 1e-4f,
                "공격력 버프 카드가 실효 스탯만 바꾸고 실제 피해는 안 늘렸다");
        }

        // 옛 DreamcatcherCombatDamageTest::DamageVsCc_BoostsDamage_AgainstCcdEnemy — 대 CC 배율은 CC 걸린 적에게 실제 피해를 올리고, 배율이 없어도 CC 적은 정상 피해를 받는다
        [Test]
        public void 대_군중_제어_배율은_기절한_적에게_실제_피해를_올린다()
        {
            const float mul = 3f;
            void Stun(Unit e) => e.Cc.Apply(CcSlotKind.Stun, 100f, float3.zero, SimEntityId.None);
            void VsCc(Unit d) => d.Modifiers.Apply(
                ModifierKey.Of(SourceA, StatKind.DamageVsCcMul, CombineOp.Multiplicative), mul, 100f);

            var (pm, _, _, plainHits) = Duel();                                  // 기준: 버프 없음 · CC 없음
            var (sm, _, _, stunnedNoBuff) = Duel(onEnemy: Stun);                 // CC 적 · 버프 없음
            var (bm, _, _, boosted) = Duel(before: VsCc, onEnemy: Stun);         // CC 적 · 버프
            var (fm, _, _, freeBuffed) = Duel(before: VsCc);                     // CC 없는 적 · 버프
            pm.Tick(); sm.Tick(); bm.Tick(); fm.Tick();

            Assert.AreEqual(1, plainHits.Count);
            Assert.AreEqual(1, stunnedNoBuff.Count, "배율 없이도 CC 적은 정상적으로 맞는다 — 무적 회귀 없음");
            Assert.AreEqual(plainHits[0].Amount, stunnedNoBuff[0].Amount, 1e-3f);
            Assert.AreEqual(plainHits[0].Amount * mul, boosted[0].Amount, 1e-3f, "CC 걸린 적에게 배율이 실제 피해로 붙는다");
            Assert.AreEqual(plainHits[0].Amount, freeBuffed[0].Amount, 1e-3f, "CC 없는 적에게는 안 붙는다");
        }

        // ══ 14 · ActiveSlowFieldTest ═══════════════════════════════════════════

        private static RuleRow SlowField(float mul, float seconds, int tiles = 1)
        {
            var r = CoreCardFixtures.CardProbe(TriggerKind.None, new TileStatBurstSkill());
            r.Effect.StatKind = (int)SkillStatKind.MoveSpeedMul;
            r.Effect.Magnitude = mul; r.Effect.Duration = seconds; r.Effect.TileRange = tiles;
            return r;
        }

        private static (BattleMatch m, int card) SlowBoard(float mul, float seconds)
        {
            var def = CoreMatchFixtures.Definition();
            int card = CoreCardFixtures.AddActiveCard(def, "fixture_slow_field", 0, 1f, SlowField(mul, seconds));
            return (CoreCardFixtures.CardBattle(def), card);
        }

        // 옛 ActiveSlowFieldTest::SlowField_SlowsSnapshotEnemies_NotLateArrivals_AndTheyWalkSlower — 감속장은 시전 순간 스냅샷(rules F35): 나중에 들어온 적은 안 걸린다
        [Test]
        public void 감속장은_시전_순간_스냅샷이라_나중에_들어온_적은_안_걸린다()
        {
            const float mul = 0.5f;
            var (m, card) = SlowBoard(mul, 30f);
            var cell = new int2(5, 2);
            var early = CoreTriggerFixtures.SpawnEnemy(m, cell);

            Assert.IsTrue(m.Apply(Command.CastActive(CoreCardFixtures.EntryOf(m, card), cell)).Accepted, "시전");
            Assert.AreEqual(mul, early.Modifiers.Effective.MoveSpeedMul, 1e-4f, "시전 순간 반경 안 적은 걸린다");

            var late = CoreTriggerFixtures.SpawnEnemy(m, cell);
            Tick(m, 10);

            Assert.AreEqual(mul, early.Modifiers.Effective.MoveSpeedMul, 1e-4f, "지속 중에는 유지된다");
            Assert.AreEqual(1f, late.Modifiers.Effective.MoveSpeedMul, 1e-4f,
                "시전 후 진입자는 안 걸린다 — 감속장을 장판으로 바꾸면 여기가 빨개져야 한다");
        }

        // 옛 ActiveSlowFieldTest::SlowField_WearsOff_AfterDuration — 지속이 지나면 원속으로 돌아온다
        [Test]
        public void 감속장은_지속이_지나면_풀린다()
        {
            const float mul = 0.5f, life = 0.8f;
            var (m, card) = SlowBoard(mul, life);
            var cell = new int2(5, 2);
            var e = CoreTriggerFixtures.SpawnEnemy(m, cell);

            Assert.IsTrue(m.Apply(Command.CastActive(CoreCardFixtures.EntryOf(m, card), cell)).Accepted, "시전");
            Assert.AreEqual(mul, e.Modifiers.Effective.MoveSpeedMul, 1e-4f, "먼저 걸린다");

            Tick(m, (int)math.floor(life * 0.5f / BattleMatch.Dt));
            Assert.AreEqual(mul, e.Modifiers.Effective.MoveSpeedMul, 1e-4f, "지속 안에서는 유지된다");

            Tick(m, (int)math.ceil(life / BattleMatch.Dt) + 5);
            Assert.AreEqual(1f, e.Modifiers.Effective.MoveSpeedMul, 1e-4f, "지속이 지나면 원속 — 영구 감속 회귀를 잡는다");
        }
    }
}
