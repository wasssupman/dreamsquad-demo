using NUnit.Framework;
using Somnia.Battle.BattleCore.Trigger;
using Somnia.Battle.Skills;
using Somnia.Battle.Skills.Concrete;

namespace Somnia.Battle.Tests.EditMode.Core
{
    // battle-core-rebuild unit 7a — 정적 라우팅 표(트리거별 분기 7 + 폴백).
    [TestFixture]
    public class SkillRoutingTests
    {
        [Test]
        public void 트리거별_분기_일곱은_폴백과_다른_concrete_로_간다()
        {
            Assert.AreEqual(DeathSiteBlastSkill.Id, SkillRouting.SkillIdFor(TriggerKind.OnKill, EffectKind.SelfTileAoe), "시체 폭발");
            Assert.AreEqual(DeathSiteHazardSkill.Id, SkillRouting.SkillIdFor(TriggerKind.OnKill, EffectKind.SpawnHazard), "잿불");
            Assert.AreEqual(DeathSiteBlastSkill.Id, SkillRouting.SkillIdFor(TriggerKind.OnDeath, EffectKind.SelfTileAoe), "작별 선물");
            Assert.AreEqual(SelfAreaBlastSkill.Id, SkillRouting.SkillIdFor(TriggerKind.OnDamagedN, EffectKind.SelfTileAoe), "피격 폭발");
            Assert.AreEqual(SelfAreaBlastSkill.Id, SkillRouting.SkillIdFor(TriggerKind.OnShieldBreak, EffectKind.SelfTileAoe), "파열 폭발");
            Assert.AreEqual(AreaSleepSkill.Id, SkillRouting.SkillIdFor(TriggerKind.OnShieldBreak, EffectKind.AreaSleep), "파열 수면");
            Assert.AreEqual(DeathSiteBlastSkill.Id, SkillRouting.SkillIdFor(TriggerKind.OnRetire, EffectKind.SelfTileAoe), "퇴근 운석");
            Assert.AreEqual(SelfBuffLethalSkill.Id, SkillRouting.SkillIdFor(TriggerKind.None, EffectKind.SelfBuffLethal));
            Assert.AreEqual(DreamCocoonSkill.Id, SkillRouting.SkillIdFor(TriggerKind.None, EffectKind.DreamCocoon));
            Assert.AreEqual(BountyMarkSkill.Id, SkillRouting.SkillIdFor(TriggerKind.None, EffectKind.BountyMark));
            Assert.AreEqual(ThresholdSelfBuffSkill.Id, SkillRouting.SkillIdFor(TriggerKind.HealthThreshold, EffectKind.SelfStatBuff), "빈사 버프는 출처가 다르다");
            // 같은 payload 의 폴백은 살아 있는 시전자의 발밑이다
            Assert.AreEqual(SelfAreaBlastSkill.Id, SkillRouting.SkillIdFor(TriggerKind.HealthThreshold, EffectKind.SelfTileAoe));
            Assert.AreEqual(SelfStatBuffSkill.Id, SkillRouting.SkillIdFor(TriggerKind.OnKill, EffectKind.SelfStatBuff));
        }

        [Test]
        public void OnPlace_x_충전이_라우팅을_찾는다()
        {
            // 옛 전투에서 이 조합이 라우팅 0 을 받아 조용히 죽어 있었다(EditMode 는 전부 초록이었다).
            Assert.AreEqual(GrantSelfChargeSkill.Id,
                SkillRouting.SkillIdFor(TriggerKind.OnPlace, EffectKind.NextAttackDoubleFire));
            Assert.AreEqual(GrantSelfChargeSkill.Id,
                SkillRouting.SkillIdFor(TriggerKind.OnDamagedN, EffectKind.NextAttackDoubleFire));
            Assert.AreEqual(DeathSiteHazardSkill.Id,
                SkillRouting.SkillIdFor(TriggerKind.PeriodicTimer, EffectKind.SpawnHazard),
                "장판도 트리거 블록 밖에 둔다");
        }

        [Test]
        public void 스킬인_payload_는_어느_트리거든_라우팅이_있다_아니면_부착_전용이다()
        {
            // 「스킬인데 라우팅 0」 = 발화하고도 아무 일이 안 일어나는 침묵. bake 가 거절하지만 표 자체도 닫는다.
            foreach (EffectKind p in System.Enum.GetValues(typeof(EffectKind)))
            {
                if (!SkillRouting.IsSkill(p)) continue;
                foreach (TriggerKind t in System.Enum.GetValues(typeof(TriggerKind)))
                {
                    if (t == TriggerKind.None && !SkillRouting.OnlyValidWithNoTrigger(p)) continue;
                    if (t != TriggerKind.None && SkillRouting.OnlyValidWithNoTrigger(p))
                    {
                        Assert.AreEqual(SkillRouting.NotRouted, SkillRouting.SkillIdFor(t, p),
                            $"{t}×{p}: 부착 전용 payload 는 트리거에 라우팅이 없어야 bake 가 거절한다");
                        continue;
                    }
                    Assert.AreNotEqual(SkillRouting.NotRouted, SkillRouting.SkillIdFor(t, p), $"{t}×{p}");
                    Assert.IsNotNull(SkillRouting.Resolve(t, p), $"{t}×{p} 의 concrete 가 레지스트리에 없다");
                }
            }
        }

        [Test]
        public void 스킬이_아닌_payload_는_라우팅이_없다()
        {
            var notSkills = new[]
            {
                EffectKind.None, EffectKind.PlacementAura, EffectKind.HeavyStrike,
                EffectKind.SplitOnDeath, EffectKind.RecallAttachedToFront,
                EffectKind.AreaBarrage, EffectKind.SelfWarmupBuff,
                // skill-data-table unit 8 — 상시 효과 4(빌더가 진영 버프 줄 · 공격 수식자로 편다).
                EffectKind.FactionStatBuff, EffectKind.ProjectileBounce, EffectKind.FrontmostTarget, EffectKind.DamageVsSleeping,
            };
            foreach (var p in notSkills)
            {
                Assert.IsFalse(SkillRouting.IsSkill(p), p.ToString());
                Assert.IsNull(SkillRouting.Resolve(TriggerKind.PeriodicTimer, p), p.ToString());
            }
        }

        [Test]
        public void 레지스트리는_concrete_33_이고_캐스트는_없다()
        {
            Assert.AreEqual(33, SkillRouting.Registry.Count, "34 − CastHazard(28) — 캐스터 제거(계약 9)");
            Assert.IsFalse(SkillRouting.Registry.TryGet(CastHazardSkill.Id, out _));
            for (int id = 1; id <= 34; id++)
                if (id != CastHazardSkill.Id) Assert.IsTrue(SkillRouting.Registry.TryGet(id, out _), $"id {id}");
        }

        [Test]
        public void 감지자_표는_배치_퇴근을_적에게_열지_않는다()
        {
            Assert.IsTrue(SkillRouting.HasDetector(TriggerKind.OnPlace, hostIsEnemy: false));
            Assert.IsFalse(SkillRouting.HasDetector(TriggerKind.OnPlace, hostIsEnemy: true));
            Assert.IsFalse(SkillRouting.HasDetector(TriggerKind.OnRetire, hostIsEnemy: true));
            Assert.IsTrue(SkillRouting.HasDetector(TriggerKind.OnDeath, hostIsEnemy: true));
            Assert.IsFalse(SkillRouting.HasDetector(TriggerKind.None, hostIsEnemy: false), "부착은 감지자가 아니다");
        }

        [Test]
        public void 게이트는_두_조합만_열린다()
        {
            Assert.IsTrue(SkillRouting.GateComboSupported(TriggerKind.OnDamagedN, GateKind.HpBelow, GateSubject.Self));
            Assert.IsTrue(SkillRouting.GateComboSupported(TriggerKind.AttackN, GateKind.HpBelow, GateSubject.EventTarget));
            Assert.IsFalse(SkillRouting.GateComboSupported(TriggerKind.AttackN, GateKind.HpBelow, GateSubject.Self));
            Assert.IsFalse(SkillRouting.GateComboSupported(TriggerKind.OnKill, GateKind.HpBelow, GateSubject.EventTarget));
            Assert.IsTrue(SkillRouting.GatePass(GateKind.HpBelow, 0.3f, 30f, 100f), "경계값 = 통과");
            Assert.IsFalse(SkillRouting.GatePass(GateKind.HpBelow, 0f, 0f, 100f), "무값 카드는 통과 못 한다");
        }
    }

    // battle-core-rebuild unit 7a — 형 카탈로그(제약 13 「효과의 형」).
    [TestFixture]
    public class RangeCatalogTests
    {
        [Test]
        public void 몸에서_나오는_것_열은_SelfArea_다()
        {
            var selfArea = new[]
            {
                EffectKind.SelfTileAoe, EffectKind.AreaSleep, EffectKind.AreaCc,
                EffectKind.AreaDot, EffectKind.AreaApplyStack, EffectKind.AreaTaunt,
                EffectKind.AllyMoveSpeedAura, EffectKind.AllyStatAura,
                EffectKind.OpponentStatAura, EffectKind.GrantShield,
            };
            foreach (var p in selfArea)
            {
                var spec = RangeCatalog.Resolve(TriggerKind.OnPlace, p, 2);
                Assert.AreEqual(RangeMetric.SelfArea, spec.Metric, p.ToString());
                Assert.AreEqual(2f, spec.RadiusTiles, p.ToString());
                Assert.AreEqual(3.5f, spec.RadiusWithOrigin(1.5f), 1e-6f, "원점 항 = host 몸");
            }
        }

        [Test]
        public void DeathSiteBlast_x_OnDeath_와_OnRetire_는_다른_형이다()
        {
            var death = RangeCatalog.Resolve(TriggerKind.OnDeath, EffectKind.SelfTileAoe, 1);
            var retire = RangeCatalog.Resolve(TriggerKind.OnRetire, EffectKind.SelfTileAoe, 1);
            Assert.AreEqual(RangeMetric.SelfArea, death.Metric, "시체가 터진다 = 몸에서 나오는 것");
            Assert.AreEqual(RangeMetric.CellArea, retire.Metric, "운석이 비워진 칸에 내린다 = 자리에 떨어지는 것");
            Assert.AreEqual(2.5f, death.RadiusWithOrigin(1.5f), 1e-6f);
            Assert.AreEqual(1.5f, retire.RadiusWithOrigin(1.5f), 1e-6f, "퇴근한 유닛의 몸은 안 붙는다");
            Assert.AreEqual(RangeShape.None,
                RangeCatalog.Resolve(TriggerKind.OnKill, EffectKind.SelfTileAoe, 1).Shape,
                "처치 = 죽인 적의 자리 — 부착 시점엔 모른다");
        }

        [Test]
        public void 탄_비행_거리는_원점_항_0_이다()
        {
            var spec = RangeCatalog.Resolve(TriggerKind.OnPlace, EffectKind.EmitProjectilePattern, 4);
            Assert.AreEqual(RangeMetric.Euclidean, spec.Metric);
            Assert.AreEqual(4f, spec.RadiusWithOrigin(1.5f), 1e-6f);
        }

        [Test]
        public void 범위가_아닌_것은_None_이다_지어내지_않는다()
        {
            var none = new[]
            {
                EffectKind.AreaBreath, EffectKind.SelfOrbitProjectile, EffectKind.ProjectileToTarget,
                EffectKind.ApplyCcToTarget, EffectKind.SelfStatBuff, EffectKind.GainCost,
                EffectKind.SelfBlink, EffectKind.SpawnHazard,
            };
            foreach (var p in none)
                Assert.AreEqual(RangeShape.None, RangeCatalog.Resolve(TriggerKind.PeriodicTimer, p, 3).Shape, p.ToString());
            Assert.AreEqual(RangeShape.None, RangeCatalog.Resolve(TriggerKind.OnPlace, EffectKind.GrantShield, 0).Shape,
                "실드 반경 0 = 자기만");
            Assert.AreEqual(0f, RangeSpec.None.RadiusWithOrigin(1f), "None 은 안 그린다(fail-closed)");
        }
    }
}
