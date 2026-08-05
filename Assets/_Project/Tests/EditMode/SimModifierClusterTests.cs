// battle-sim-extraction unit 18-C/6 — 클러스터 조립의 계약 테스트.
//
// 개별 시스템의 규칙은 각 오라클 복제가 진다. 여기서 보는 것은 **배치**다 —
// phase 분리(P2 하나 · P7 다섯)와 그 분리가 만드는 지연 구조.
using NUnit.Framework;
using Wassup.Sim;
using Wassup.Sim.Effects;
using Wassup.Sim.Units;

namespace Wassup.Tests.EditMode
{
    public class SimModifierClusterTests
    {
        private SimWorld _world;
        private SimChannels _channels;
        private ModifierCluster _cluster;
        private SimTick _tick;

        private void Build(params StackThresholdRule[] rules)
        {
            _world = new SimWorld(new SimConfig(1u, 1u, rules));
            _channels = new SimChannels();
            _cluster = new ModifierCluster(_channels);
            _tick = new SimPipeline().Add(_cluster.Steps()).Build();
        }

        [Test]
        public void RegistersOneStepInIntake_AndFiveInModifierTick()
        {
            Build();
            Assert.AreEqual(1, _tick.StepCount(SimPhase.Intake), "#9 ModifierApply");
            Assert.AreEqual(5, _tick.StepCount(SimPhase.ModifierTick), "#28·#29·#30·#31·#32");
        }

        [Test]
        public void EveryStep_LandsInThePhaseItsCaptureNumberImplies()
        {
            // 번호 구간이 phase 를 결정한다(청사진 ③ §1). 어긋나면 이식이 phase 를 잘못 골랐다.
            Build();
            foreach (var s in new ModifierCluster(new SimChannels()).Steps())
                Assert.AreEqual(SimPipeline.PhaseForOrder(s.Order), s.Phase,
                    $"#{s.Order} {s.Name} 의 phase 가 캡처 번호와 어긋난다.");
        }

        [Test]
        public void ExpiryIsReflectedInTheSameTick_BecauseTickPrecedesAggregate()
        {
            // #29 가 #30 보다 앞이라는 배치의 관측점.
            Build();
            var e = _world.Create();
            _world.Set(e, ModifierStats.Identity);
            _channels.StatApply.Enqueue(new StatModifierApplyEvent
            {
                target = e, stat = StatKind.DamageMul, op = CombineOp.Additive,
                magnitude = 1f, duration = 1f, source = e,
            });

            _tick.Run(_world, 0.5f);
            Assert.AreEqual(2f, _world.Get<ModifierStats>(e).damageMul, 1e-4f, "적용됨.");

            _tick.Run(_world, 1f);   // 만료 → 같은 틱에 집계까지
            Assert.AreEqual(1f, _world.Get<ModifierStats>(e).damageMul, 1e-4f,
                "만료(#29)가 집계(#30)보다 앞이라 같은 틱에 복귀한다.");
        }

        [Test]
        public void MaxHealthReadsTheSameTicksMultiplier_BecauseItFollowsAggregate()
        {
            // #31 이 #30 **뒤**라는 배치의 관측점. 앞이었다면 한 틱 늦게 반영된다.
            Build();
            var e = _world.Create();
            _world.Set(e, ModifierStats.Identity);
            _world.Set(e, new Health { value = 100f, max = 100f });
            _channels.StatApply.Enqueue(new StatModifierApplyEvent
            {
                target = e, stat = StatKind.MaxHealthMul, op = CombineOp.Multiplicative,
                magnitude = 0.5f, duration = 100f, source = e,
            });

            _tick.Run(_world, 0.016f);

            Assert.AreEqual(50f, _world.Get<Health>(e).max, 1e-4f,
                "부착·집계·스케일이 한 틱 안에서 끝난다(#9 P2 → #30 → #31).");
        }

        [Test]
        public void FatigueStackAppliesNextTick_BecauseProducerIsAfterConsumer()
        {
            // #28(P7)이 낸 것을 #9(P2)가 받으므로 **구조적 1틱 지연**이다.
            Build();
            var cfg = _world.Create();
            _world.Set(cfg, new BurnoutGimmickConfig
            {
                fatigueInterval = 1f, fatigueAmount = 1, fatigueMaxStack = 9,
                fatiguePerAppDuration = 100f,
            });
            var d = _world.Create();
            _world.Set(d, default(DefenderUnitTag));
            _world.Set(d, ModifierStats.Identity);

            _tick.Run(_world, 1f);
            Assert.IsFalse(_world.HasBuffer<StackModifierSlot>(d),
                "발행은 됐지만 소비자(P2)는 이미 지나갔다 — 이번 틱엔 슬롯이 없다.");
            Assert.AreEqual(1, _channels.StackApply.Count, "채널에 대기 중.");

            _tick.Run(_world, 0.016f);
            Assert.AreEqual(1, _world.GetBuffer<StackModifierSlot>(d)[0].stackCount,
                "다음 틱 앞머리에서 적용된다.");
        }

        [Test]
        public void StackThresholdDerivedStat_AlsoLandsNextTick()
        {
            // #32(P7) → 스탯 채널 → #9(P2). 같은 구조적 지연.
            Build(new StackThresholdRule
            {
                kind = StackKind.Fire, atStack = 1, mode = ThresholdMode.Edge,
                derivedKind = DerivedEffectKind.ApplyStat,
                stat = StatKind.MoveSpeedMul, op = CombineOp.Multiplicative,
                magnitude = 0.5f, duration = 10f,
            });
            var e = _world.Create();
            _world.Set(e, ModifierStats.Identity);
            _channels.StackApply.Enqueue(new StackModifierApplyEvent
            {
                target = e, kind = StackKind.Fire, countDelta = 1, maxStack = 9,
                perAppDuration = 100f, source = SimEntityId.Null,
            });

            _tick.Run(_world, 0.016f);   // 스택 적용(#9) → 임계 발화(#32)
            Assert.AreEqual(1f, _world.Get<ModifierStats>(e).moveSpeedMul, 1e-4f,
                "임계는 터졌지만 스탯 반입은 다음 틱이다.");

            _tick.Run(_world, 0.016f);   // 파생 스탯 반입(#9) → 집계(#30)
            Assert.AreEqual(0.5f, _world.Get<ModifierStats>(e).moveSpeedMul, 1e-4f);
        }
    }
}
