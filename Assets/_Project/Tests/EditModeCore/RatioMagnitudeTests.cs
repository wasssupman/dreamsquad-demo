using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using Wassup.BattleCore;
using Wassup.BattleCore.Combat;
using Wassup.BattleCore.Combat.Projectile;
using Wassup.BattleCore.Effects;
using Wassup.BattleCore.Trigger;
using Wassup.Skills;
using static Wassup.Tests.EditMode.Core.CoreTriggerFixtures;

namespace Wassup.Tests.EditMode.Core
{
    // skill-data-table unit 3(U7 · U11) — **비율형 수치.** 효과 수치 = 고정 또는 「소유자 스탯 × 비율」.
    //   · 기준 「공격력」 = 평타 한 발의 피해 출력 합 × 공격자 쪽 배율만(대 CC · 수면 · 최전방 · 강타 제외 · 카운터 무전진)
    //   · 값은 **시전 순간**(드레인) 최종 스탯으로 한 번 — 탄 · 버스트 전 발이 그 값을 나른다(착탄 때 되묻지 않는다)
    //   · 주인이 떠나는 사건(죽음 · 퇴근)은 감지 순간 스냅샷 · 주인 없는 시전 · 비율 칸 없는 종류는 검증 거절
    // ⚠ 여기 수치는 게임 값이 아니라 픽스처다.
    [TestFixture]
    public class RatioMagnitudeTests
    {
        private const float Base = 10f;   // 고정구 방어유닛 평타 피해

        private static void Buff(Unit u, StatKind stat, float mul)
            => u.Modifiers.Apply(ModifierKey.Of(SimEntityId.None, stat, CombineOp.Multiplicative), mul, 999f);

        private static RuleRow Ratio(RuleRow r, BasisStat basis, float ratio)
        {
            r.Effect.MagnitudeMode = MagnitudeMode.OwnerStatRatio;
            r.Effect.BasisStat = basis;
            r.Effect.Ratio = ratio;
            return r;
        }

        // ── 순수 함수 ─────────────────────────────────────────────────────────

        [Test]
        public void 공격력_기준은_피해_출력_합_곱하기_공격자_배율이다()
        {
            var outputs = new[]
            {
                new AttackOutputDef { Kind = AttackOutputKind.Damage, Magnitude = 4f },
                new AttackOutputDef { Kind = AttackOutputKind.Heal, Magnitude = 100f },
                new AttackOutputDef { Kind = AttackOutputKind.Damage, Magnitude = 6f },
                new AttackOutputDef { Kind = AttackOutputKind.ApplyStat, Magnitude = 50f },
            };
            Assert.AreEqual(15f, EffectMagnitude.AttackBasis(outputs, 1.5f), 1e-5f, "피해만 센다");
            Assert.AreEqual(0f, EffectMagnitude.AttackBasis(null, 2f));
        }

        [Test]
        public void 비율_칸은_피해와_실드량에만_있다()
        {
            var accepts = new HashSet<EffectKind>
            {
                EffectKind.ProjectileToTarget, EffectKind.SelfTileAoe, EffectKind.SelfOrbitProjectile, EffectKind.AreaBreath,
                EffectKind.AreaCc, EffectKind.AreaDot, EffectKind.EmitProjectilePattern, EffectKind.SelfBlink,
                EffectKind.UltimateLeap, EffectKind.SpawnHazard, EffectKind.GrantShield,
            };
            foreach (EffectKind k in System.Enum.GetValues(typeof(EffectKind)))
                Assert.AreEqual(accepts.Contains(k), EffectMagnitude.AcceptsRatio(k), k.ToString());
        }

        [Test]
        public void 해석은_종류의_비율_칸에만_싣고_고정은_그대로다()
        {
            var shot = EffectDef.Default();
            shot.Kind = EffectKind.ProjectileToTarget;
            shot.Magnitude = 1f;
            shot.Damage = 2f;
            Assert.AreEqual(shot, EffectMagnitude.Resolve(in shot, 50f), "고정 = 그대로");

            shot.MagnitudeMode = MagnitudeMode.OwnerStatRatio;
            shot.Ratio = 0.5f;
            var r = EffectMagnitude.Resolve(in shot, 50f);
            Assert.AreEqual(25f, r.Magnitude, 1e-5f, "탄 = Magnitude 칸");
            Assert.AreEqual(2f, r.Damage, "다른 칸은 안 건드린다");

            var pattern = shot;
            pattern.Kind = EffectKind.EmitProjectilePattern;
            r = EffectMagnitude.Resolve(in pattern, 50f);
            Assert.AreEqual(25f, r.Damage, 1e-5f, "발사 명세 = 피해 칸(U10)");
            Assert.AreEqual(1f, r.Magnitude);

            var buff = shot;
            buff.Kind = EffectKind.SelfStatBuff;
            Assert.AreEqual(buff, EffectMagnitude.Resolve(in buff, 50f), "비율 칸 없는 종류는 안 바뀐다(검증이 거절한다)");
        }

        [Test]
        public void 공격력_기준은_대상_조건_배율과_강타를_빼고_카운터를_안_건드린다()
        {
            var def = CoreCombatFixtures.Definition(defenderDamage: Base);
            def.Units[0].Attack.Mods = new[] { new AttackModDef { Kind = AttackModKind.HeavyStrike, Period = 2, DamageMul = 3f },
                                               new AttackModDef { Kind = AttackModKind.DamageVsSleeping, DamageMul = 4f } };
            def.ConfigHash = def.ComputeConfigHash();
            var m = CoreMatchFixtures.BeginBattle(def);
            var d = SpawnDefender(m, new int2(5, 2));
            Buff(d, StatKind.DamageMul, 2f);
            Buff(d, StatKind.DamageVsCcMul, 5f);
            int counter = d.Attack.Mods[0].Counter;

            Assert.AreEqual(Base * 2f, EffectMagnitude.BasisOf(d, BasisStat.Attack), 1e-4f, "공격자 쪽 배율만");
            Assert.AreEqual(Base * 2f, EffectMagnitude.BasisOf(d, BasisStat.Attack), 1e-4f, "두 번 읽어도 같다");
            Assert.AreEqual(counter, d.Attack.Mods[0].Counter, "강타 카운터를 전진시키지 않는다");
            Assert.AreEqual(d.MaxHealth, EffectMagnitude.BasisOf(d, BasisStat.MaxHealth));
        }

        // ── 시전 순간 ─────────────────────────────────────────────────────────

        [Test]
        public void 비율형_수치는_시전_순간의_최종_스탯을_따른다()
        {
            var def = CoreCombatFixtures.Definition(defenderDamage: Base, defenderRange: 0.01f);
            var seen = new List<float>();
            var probe = new ProbeSkill { OnExecute = (c, t, p, ctx) => seen.Add(p.Magnitude) };
            var row = Probe(TriggerKind.PeriodicTimer, probe);
            row.Rule.PeriodSeconds = 9999f;
            row.Effect.Kind = EffectKind.ProjectileToTarget;
            row.Effect.Magnitude = 777f;   // 비율형에서는 읽지 않는다
            GiveUnit(def, 0, Ratio(row, BasisStat.Attack, 1.5f));
            var m = CoreMatchFixtures.BeginBattle(def);
            var d = SpawnDefender(m, new int2(5, 2));
            int inst = d.Bindings[0].InstanceId;

            Assert.IsTrue(m.Apply(Command.DebugFireBinding(d.Id, inst)).Accepted);
            Buff(d, StatKind.DamageMul, 2f);
            Assert.IsTrue(m.Apply(Command.DebugFireBinding(d.Id, inst)).Accepted);

            CollectionAssert.AreEqual(new[] { Base * 1.5f, Base * 2f * 1.5f }, seen, "버프 전 · 후");
            Assert.AreEqual(MagnitudeMode.OwnerStatRatio, d.Bindings[0].Effect.MagnitudeMode, "규칙의 효과 값은 저작 그대로(해석은 사본)");
        }

        [Test]
        public void 최대_체력_기준은_모디파이어_반영값이다()
        {
            var def = CoreCombatFixtures.Definition(defenderDamage: Base, defenderRange: 0.01f);
            var seen = new List<float>();
            var row = Probe(TriggerKind.PeriodicTimer, new ProbeSkill { OnExecute = (c, t, p, ctx) => seen.Add(p.Magnitude) });
            row.Rule.PeriodSeconds = 9999f;
            row.Effect.Kind = EffectKind.GrantShield;
            GiveUnit(def, 0, Ratio(row, BasisStat.MaxHealth, 0.1f));
            var m = CoreMatchFixtures.BeginBattle(def);
            var d = SpawnDefender(m, new int2(5, 2));
            float max0 = d.MaxHealth;
            Buff(d, StatKind.MaxHealthMul, 2f);
            m.Tick();   // 최대 체력은 모디파이어 단계에서 갱신된다(같은 틱 버프는 한 틱 늦다 — 계약 9)
            Assert.AreEqual(max0 * 2f, d.MaxHealth, 1e-3f, "고정구 전제: 최대 체력이 두 배가 됐다");

            Assert.IsTrue(m.Apply(Command.DebugFireBinding(d.Id, d.Bindings[0].InstanceId)).Accepted);
            Assert.AreEqual(1, seen.Count);
            Assert.AreEqual(max0 * 2f * 0.1f, seen[0], 1e-3f);
        }

        // 비행이 긴 유도탄 — 발사와 착탄 사이에 스탯을 바꿀 틈을 만든다.
        private static (BattleMatch m, Unit d, Unit e, List<CoreEvent> fired, List<CoreEvent> hits) SlowShot(float ratio)
        {
            var def = CoreCombatFixtures.Definition(defenderDamage: Base, enemyHealth: 100000f);
            var slow = ProjectileDef.Default();
            slow.Id = "fixture_slow";
            slow.Speed = 1f;
            def.Projectiles = new[] { slow };
            var rule = Rule(TriggerKind.AttackN, EffectKind.ProjectileToTarget);
            rule.Rule.Period = 1;
            rule.Rule.FireCap = 1;
            rule.Effect.DataIndex = 0;
            GiveUnit(def, 0, Ratio(rule, BasisStat.Attack, ratio));
            var m = CoreMatchFixtures.BeginBattle(def);
            var d = SpawnDefender(m, new int2(4, 2));
            var e = SpawnEnemy(m, new int2(7, 2));
            return (m, d, e, CoreCombatFixtures.Listen(m, CoreEventKind.TriggerFired), CoreCombatFixtures.Listen(m, CoreEventKind.DamageApplied));
        }

        private static void TickUntilFired(BattleMatch m, List<CoreEvent> fired)
        {
            for (int t = 0; t < 600 && fired.Count == 0; t++) m.Tick();
            Assert.AreEqual(1, fired.Count, "고정구 전제: 스킬이 한 번 발동했다");
        }

        private static List<float> SkillHits(List<CoreEvent> hits, Unit e, float normalA, float normalB)
            => hits.FindAll(h => h.B == e.Id && System.Math.Abs(h.Amount - normalA) > 1e-3f && System.Math.Abs(h.Amount - normalB) > 1e-3f)
                   .ConvertAll(h => h.Amount);

        [Test]
        public void 발사_뒤에_걸린_버프는_비행_중인_탄의_피해를_안_바꾼다()
        {
            var (m, d, e, fired, hits) = SlowShot(3f);
            TickUntilFired(m, fired);
            Buff(d, StatKind.DamageMul, 2f);
            CoreCombatFixtures.Tick(m, 60 * 5);

            CollectionAssert.AreEqual(new[] { Base * 3f }, SkillHits(hits, e, Base, Base * 2f),
                                      "시전 순간 값(30) — 착탄 때 되물으면 60");
        }

        [Test]
        public void 발사_전에_걸린_버프는_탄의_피해에_실린다()
        {
            var (m, d, e, fired, hits) = SlowShot(3f);
            Buff(d, StatKind.DamageMul, 2f);
            TickUntilFired(m, fired);
            CoreCombatFixtures.Tick(m, 60 * 5);

            CollectionAssert.AreEqual(new[] { Base * 2f * 3f }, SkillHits(hits, e, Base, Base * 2f));
        }

        [Test]
        public void 비행_중에_주인이_사라져도_탄의_피해는_그대로다()
        {
            var (m, d, e, fired, hits) = SlowShot(3f);
            TickUntilFired(m, fired);
            Assert.IsTrue(m.Apply(Command.DebugDestroy(d.Id)).Accepted);
            CoreCombatFixtures.Tick(m, 60 * 5);

            Assert.IsNull(m.World.Find(d.Id), "주인은 없다");
            CollectionAssert.AreEqual(new[] { Base * 3f }, SkillHits(hits, e, Base, Base * 2f));
        }

        [Test]
        public void 버스트는_개시_순간_값_하나를_전_발이_공유한다()
        {
            var def = CoreCombatFixtures.Definition(defenderDamage: Base, enemyHealth: 100000f);
            var barrel = ProjectileDef.Default();
            barrel.Id = "fixture_barrel";
            barrel.Movement = (int)MovementKind.SkyFallOnEntity;
            barrel.Payload = (int)PayloadKind.SingleSplash;
            def.Projectiles = new[] { barrel };
            def.Patterns = new[]
            {
                new PatternDef
                {
                    Id = "fixture_burst", BarrelProjectileDefIndex = 0,
                    Selection = (int)Wassup.BattleCore.Combat.Emission.PatternSelectionRule.RoundRobin,
                    Shots = new[]
                    {
                        new PatternShotDef { DirectionT = 0.5f },
                        new PatternShotDef { DirectionT = 0.5f, IntervalAfterPreviousSec = 0.5f },
                        new PatternShotDef { DirectionT = 0.5f, IntervalAfterPreviousSec = 0.5f },
                    },
                    ReselectPerShot = true, TelegraphSec = 0.2f, ScopeTileRange = 4,
                },
            };
            var rule = Rule(TriggerKind.AttackN, EffectKind.EmitProjectilePattern);
            rule.Rule.Period = 1;
            rule.Rule.FireCap = 1;
            rule.Effect.PatternDefIndex = 0;
            GiveUnit(def, 0, Ratio(rule, BasisStat.Attack, 3f));
            var m = CoreMatchFixtures.BeginBattle(def);
            var d = SpawnDefender(m, new int2(4, 2));
            var e = SpawnEnemy(m, new int2(6, 2));
            var fired = CoreCombatFixtures.Listen(m, CoreEventKind.TriggerFired);
            var hits = CoreCombatFixtures.Listen(m, CoreEventKind.DamageApplied);

            TickUntilFired(m, fired);
            Buff(d, StatKind.DamageMul, 2f);   // 첫 발이 나가기 전 · 버스트 도중
            CoreCombatFixtures.Tick(m, 60 * 4);

            CollectionAssert.AreEqual(new[] { Base * 3f, Base * 3f, Base * 3f }, SkillHits(hits, e, Base, Base * 2f),
                                      "세 발 모두 개시 순간 값(30) — 발마다 다시 재면 60");
        }

        // ── 주인이 떠나는 사건 · 주인 없는 시전 ───────────────────────────────

        [TestCase(BasisStat.Attack, 2f)]
        [TestCase(BasisStat.MaxHealth, 0.1f)]
        public void 죽음_발동은_감지_순간_스탯_스냅샷으로_푼다(BasisStat basis, float ratio)
        {
            var def = CoreCombatFixtures.Definition(defenderDamage: Base, defenderRange: 0.01f, enemyHealth: 100000f);
            int blast = AddBlastProjectile(def);
            var gift = Rule(TriggerKind.OnDeath, EffectKind.SelfTileAoe);
            gift.Effect.TileRange = 1;
            gift.Effect.DataIndex = blast;
            GiveUnit(def, 0, Ratio(gift, basis, ratio));
            var m = CoreMatchFixtures.BeginBattle(def);
            var d = SpawnDefender(m, new int2(5, 2));
            var e = SpawnEnemy(m, new int2(6, 2));
            Buff(d, StatKind.DamageMul, 1.5f);
            float expected = (basis == BasisStat.Attack ? Base * 1.5f : d.MaxHealth) * ratio;
            var hits = CoreCombatFixtures.Listen(m, CoreEventKind.DamageApplied);

            m.Intents.Apply(new SimIntent { Kind = SimIntentKind.DealDamage, Target = CoreSkillContext.ToSkill(d.Id),
                                            Source = CoreSkillContext.ToSkill(e.Id), Amount = 99999f });
            CoreCombatFixtures.Tick(m, 4);

            Assert.IsNull(m.World.Find(d.Id), "죽었고 사라졌다");
            var onEnemy = hits.FindAll(h => h.B == e.Id);
            Assert.AreEqual(1, onEnemy.Count, "작별 선물 한 번(평타는 사거리 밖)");
            Assert.AreEqual(expected, onEnemy[0].Amount, 1e-3f, "주인이 없어도 0 으로 새지 않는다");
        }

        [Test]
        public void 주인_없는_시전의_비율형은_드레인이_말하고_0으로_푼다()
        {
            var def = CoreMatchFixtures.Definition();
            var seen = new List<float>();
            var row = Probe(TriggerKind.PeriodicTimer, new ProbeSkill { OnExecute = (c, t, p, ctx) => seen.Add(p.Magnitude) });
            row.Rule.PeriodSeconds = BattleMatch.Dt;
            row.Effect.Kind = EffectKind.ProjectileToTarget;
            row.Effect.Magnitude = 5f;
            int[] idx = Add(def, Ratio(row, BasisStat.Attack, 2f));
            def.ConfigHash = def.ComputeConfigHash();
            var m = CoreMatchFixtures.BeginBattle(def);
            var warns = new List<string>();
            m.Report = s => warns.Add(s);
            m.Bindings.Attach(null, in def.Bindings[idx[0]], idx[0], m.Clock.Tick);

            m.Tick();

            CollectionAssert.AreEqual(new[] { 0f }, seen, "기준이 없다 — 저작 고정값(5)으로 조용히 새지 않는다");
            Assert.IsTrue(warns.Exists(w => w.Contains("비율형")), "검증이 거절했어야 한다고 말한다");
        }

        // ── 남의 사건(U17) ────────────────────────────────────────────────────

        // 「남의 배치」 규칙을 든 숙주(종류 0 · 평타 Base)와 놓이는 유닛(종류 1 · 평타 Base×7 · 체력 900).
        // 기준이 놓인 유닛에서 나오면 값이 7배 / 900 기준으로 갈린다.
        [TestCase(BasisStat.Attack, 1f)]
        [TestCase(BasisStat.Attack, 2f)]
        [TestCase(BasisStat.MaxHealth, 1f)]
        public void 남의_배치_비율형은_규칙_소유자의_스탯을_기준으로_한다(BasisStat basis, float hostMul)
        {
            const float ratio = 1.5f;
            var def = CoreMatchFixtures.Definition();
            def.Units[0].Cost = 0;
            def.Units[0].Attack.Outputs = new[] { new AttackOutputDef { Kind = AttackOutputKind.Damage, Magnitude = Base } };
            var placedType = CoreMatchFixtures.Defender("placed");
            placedType.Cost = 0;
            placedType.Health = 900f;
            placedType.Attack.Outputs = new[] { new AttackOutputDef { Kind = AttackOutputKind.Damage, Magnitude = Base * 7f } };
            def.Units = new[] { def.Units[0], placedType };
            def.Roster = new[] { 0, 1 };
            var seen = new List<float>();
            var casters = new List<int>();
            var row = Probe(TriggerKind.OnPlace, new ProbeSkill { OnExecute = (c, t, p, ctx) => { seen.Add(p.Magnitude); casters.Add(c.Unit.Value); } });
            row.Rule.Subject = BindingSubject.Any;
            row.Rule.SubjectFilter = BindingSubjectFilter.PlacedDefender;
            row.Effect.Kind = EffectKind.ProjectileToTarget;
            row.Effect.Magnitude = 777f;   // 비율형에서는 읽지 않는다
            int idx = Add(def, Ratio(row, basis, ratio))[0];
            def.ConfigHash = def.ComputeConfigHash();
            var m = CoreMatchFixtures.BeginBattle(def);
            var host = SpawnDefender(m, new int2(3, 1));
            CoreCombatFixtures.Tick(m, 3);   // 숙주 자신의 등장 사건을 흘려보낸다 — 규칙은 그 뒤에 붙는다(부착 카드)
            Assert.IsNotNull(m.Bindings.Attach(host, in def.Bindings[idx], idx, m.Clock.Tick));
            if (basis == BasisStat.Attack) Buff(host, StatKind.DamageMul, hostMul);
            float hostMax = host.MaxHealth;

            Assert.AreEqual(RejectReason.None, m.Apply(Command.PlaceDefender(1, new int2(7, 3))).Reason, "배치");
            var placed = m.World.Units[m.World.Units.Count - 1];
            Buff(placed, StatKind.DamageMul, 3f);   // 놓인 유닛의 스탯은 무관해야 한다
            CoreCombatFixtures.Tick(m, 3);

            float expected = (basis == BasisStat.Attack ? Base * hostMul : hostMax) * ratio;
            CollectionAssert.AreEqual(new[] { expected }, seen, "기준 = 숙주(놓인 유닛이면 70·210 / 900)");
            CollectionAssert.AreEqual(new[] { CoreSkillContext.ToSkill(placed.Id).Value }, casters, "발동 주체는 놓인 유닛 그대로(U3)");
        }

        // ── 검증 · 해시 ───────────────────────────────────────────────────────

        [Test]
        public void 검증은_주인_없는_시전과_비율_칸_없는_종류의_비율형을_거절하고_남의_사건은_허용한다()
        {
            var ok = new EffectCombo { Trigger = TriggerKind.AttackN, Payload = EffectKind.ProjectileToTarget, Magnitude = MagnitudeMode.OwnerStatRatio };
            Assert.AreEqual(ComboVerdict.Allowed, EffectComboRule.Check(in ok), "소유자가 시전 · 비율 칸 있음");

            var death = ok; death.Trigger = TriggerKind.OnDeath; death.Payload = EffectKind.SelfTileAoe;
            Assert.AreEqual(ComboVerdict.Allowed, EffectComboRule.Check(in death), "주인이 떠나는 사건은 스냅샷으로 허용");

            var ownerless = ok; ownerless.CastHasNoOwner = true;
            Assert.AreEqual(ComboVerdict.NoRatioBasis, EffectComboRule.Check(in ownerless));
            var flatOwnerless = ownerless; flatOwnerless.Magnitude = MagnitudeMode.Flat;
            Assert.AreEqual(ComboVerdict.Allowed, EffectComboRule.Check(in flatOwnerless), "고정은 주인이 없어도 된다");

            var any = ok; any.Trigger = TriggerKind.OnPlace; any.Subject = BindingSubject.Any; any.Payload = EffectKind.SelfTileAoe;
            Assert.AreEqual(ComboVerdict.Allowed, EffectComboRule.Check(in any), "남의 사건 — 기준 = 규칙 소유자(숙주 · U17)");
            var anyOwnerless = any; anyOwnerless.CastHasNoOwner = true;
            Assert.AreEqual(ComboVerdict.NoRatioBasis, EffectComboRule.Check(in anyOwnerless), "판 호스트 소유 남의 사건은 여전히 기준 없음");

            var buff = ok; buff.Payload = EffectKind.SelfStatBuff;
            Assert.AreEqual(ComboVerdict.NoRatioField, EffectComboRule.Check(in buff));
        }

        [Test]
        public void 고정은_해시에_안_쓰고_비율형은_쓴다()
        {
            MatchDefinition With(MagnitudeMode mode, BasisStat basis, float ratio)
            {
                var def = CoreCombatFixtures.Definition();
                var r = Rule(TriggerKind.AttackN, EffectKind.ProjectileToTarget);
                r.Rule.Period = 1;
                r.Effect.MagnitudeMode = mode;
                r.Effect.BasisStat = basis;
                r.Effect.Ratio = ratio;
                GiveUnit(def, 0, r);
                return def;
            }
            string flat = With(MagnitudeMode.Flat, BasisStat.Attack, 0f).ConfigHash;
            Assert.AreEqual(flat, With(MagnitudeMode.Flat, BasisStat.MaxHealth, 5f).ConfigHash, "고정이면 기준 · 비율 칸은 해시 밖");
            string a = With(MagnitudeMode.OwnerStatRatio, BasisStat.Attack, 2f).ConfigHash;
            Assert.AreNotEqual(flat, a);
            Assert.AreNotEqual(a, With(MagnitudeMode.OwnerStatRatio, BasisStat.MaxHealth, 2f).ConfigHash);
            Assert.AreNotEqual(a, With(MagnitudeMode.OwnerStatRatio, BasisStat.Attack, 3f).ConfigHash);
        }
    }
}
