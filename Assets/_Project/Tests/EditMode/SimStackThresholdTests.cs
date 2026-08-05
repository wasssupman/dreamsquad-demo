// battle-sim-extraction unit 18-C/6 — 신 sim `StackModifierTickSystem` 의 **오라클 복제**.
//
// 원본 대응(`ModifierFrameworkTests`):
//   Test 3  StackModifier_MultiThreshold_FourToSeven_Fires_All_Crossed_Thresholds
//   —       StackThresholdRegistry_UnregisteredKind_ReturnsEmpty_AndFiresNothing
//
// 구 sim 의 `StackThresholdRegistry`(sim 소유 static) 자리를 `SimConfig` 가 받았다. 구 테스트가
// `Register(...)` 로 주입하던 것을 여기서는 **월드 생성 시** 넣는다 — 18-A/4 가 config 를
// 생성자 필수 인자로 만든 이유가 이것이다(등록을 잊으면 컴파일이 안 된다).
using System.Collections.Generic;
using NUnit.Framework;
using Wassup.Sim;
using Wassup.Sim.Effects;

namespace Wassup.Tests.EditMode
{
    public class SimStackThresholdTests
    {
        private SimWorld _world;
        private SimChannel<StatModifierApplyEvent> _statChannel;
        private SimChannel<StackModifierApplyEvent> _stackChannel;
        private SimChannel<EnemyCcEvent> _ccChannel;
        private SimChannel<DotApplyEvent> _dotChannel;
        private ModifierApplySystem _apply;
        private StackModifierTickSystem _stackTick;

        /// 월드를 임계 규칙과 함께 짓는다. 규칙 목록은 **저작 순서 그대로** 보존된다.
        private void Build(params StackThresholdRule[] rules)
        {
            _world = new SimWorld(new SimConfig(
                pickupSeed: 1u, bombSeedBase: 1u,
                stackThresholds: new List<StackThresholdRule>(rules)));

            _statChannel = new SimChannel<StatModifierApplyEvent>();
            _stackChannel = new SimChannel<StackModifierApplyEvent>();
            _ccChannel = new SimChannel<EnemyCcEvent>();
            _dotChannel = new SimChannel<DotApplyEvent>();
            _apply = new ModifierApplySystem(_statChannel, _stackChannel);
            _stackTick = new StackModifierTickSystem(_ccChannel, _dotChannel, _statChannel);
        }

        private SimEntityId CreateUnit()
        {
            var e = _world.Create();
            _world.Set(e, ModifierStats.Identity);
            return e;
        }

        private void Stack(SimEntityId target, StackKind kind, byte countDelta, byte maxStack,
                           float perAppDuration)
            => _stackChannel.Enqueue(new StackModifierApplyEvent
            {
                target = target, kind = kind, countDelta = countDelta, maxStack = maxStack,
                perAppDuration = perAppDuration, source = SimEntityId.Null,
            });

        /// P2(Apply) → P7(StackTick). 구 오라클의 시스템 순서와 같다.
        private void Tick(float dt = 0.016f)
        {
            _world.SetDeltaTime(dt);
            _apply.Run(_world);
            _stackTick.Run(_world);
        }

        private static StackThresholdRule Stun(StackKind kind, byte atStack, float duration,
                                               ThresholdMode mode = ThresholdMode.Edge)
            => new StackThresholdRule
            {
                kind = kind, atStack = atStack, mode = mode,
                derivedKind = DerivedEffectKind.ApplyStun, magnitude = duration,
            };

        // ── 다중 임계 ─────────────────────────────────────────────────────────────

        [Test]
        public void StackModifier_MultiThreshold_FourToSeven_Fires_All_Crossed_Thresholds()
        {
            Build(Stun(StackKind.Fire, atStack: 5, duration: 0.5f),
                  Stun(StackKind.Fire, atStack: 6, duration: 0.5f));
            var e = CreateUnit();

            // 1차: 4스택 — 임계(5·6) 미도달.
            Stack(e, StackKind.Fire, countDelta: 4, maxStack: 10, perAppDuration: 100f);
            Tick();

            var slots = _world.GetBuffer<StackModifierSlot>(e);
            Assert.AreEqual(1, slots.Count, "Fire 슬롯 1개");
            Assert.AreEqual(4, slots[0].stackCount, "4스택 누적");
            Assert.AreEqual(4, slots[0].lastTriggeredStack,
                "임계 미도달이어도 lastTriggeredStack 은 stackCount 로 갱신된다");
            Assert.AreEqual(0, _ccChannel.Count, "임계 미도달 — CC 발화 없음");

            // 2차: +3 → 7스택. 4→7 점프가 5·6 을 **둘 다** 건너뛰므로 둘 다 발화.
            Stack(e, StackKind.Fire, countDelta: 3, maxStack: 10, perAppDuration: 100f);
            Tick();

            slots = _world.GetBuffer<StackModifierSlot>(e);
            Assert.AreEqual(7, slots[0].stackCount, "7스택 누적");
            Assert.AreEqual(7, slots[0].lastTriggeredStack, "엣지 캐시가 7로 전진");
            Assert.AreEqual(2, _ccChannel.Count,
                "4→7 점프는 건너뛴 임계(5·6)를 모두 발화한다 — 다중 임계 계약");
        }

        [Test]
        public void UnregisteredKind_ReturnsEmpty_AndFiresNothing()
        {
            Build(Stun(StackKind.Fire, atStack: 5, duration: 0.5f));

            Assert.IsNotNull(_world.Config.StackThresholdsFor(StackKind.Ice));
            Assert.AreEqual(0, _world.Config.StackThresholdsFor(StackKind.Ice).Count,
                "미등록 kind 는 빈 목록(예외·null 아님) — '규칙 없음' 은 정상 상태다");

            var e = CreateUnit();
            Stack(e, StackKind.Ice, countDelta: 9, maxStack: 10, perAppDuration: 100f);
            Tick();

            Assert.AreEqual(9, _world.GetBuffer<StackModifierSlot>(e)[0].stackCount);
            Assert.AreEqual(0, _ccChannel.Count, "규칙 미등록 — 파생 발화 없음");
            Assert.AreEqual(0, _dotChannel.Count, "규칙 미등록 — DoT 발화 없음");
        }

        [Test]
        public void NoRules_StillAdvancesEdgeCache()
        {
            // 캐시를 전진시키지 않으면 `stackCount > lastTriggeredStack` 이 영원히 참이라
            // 매 프레임 재판정이 돈다.
            Build();
            var e = CreateUnit();
            Stack(e, StackKind.Poison, countDelta: 3, maxStack: 10, perAppDuration: 100f);
            Tick();

            Assert.AreEqual(3, _world.GetBuffer<StackModifierSlot>(e)[0].lastTriggeredStack);
        }

        // ── 파생 3종의 페이로드 ───────────────────────────────────────────────────

        [Test]
        public void ApplyDot_CarriesOriginStack_AndElementFromKind()
        {
            Build(new StackThresholdRule
            {
                kind = StackKind.Bleed, atStack = 2, mode = ThresholdMode.Edge,
                derivedKind = DerivedEffectKind.ApplyDot,
                magnitude = 12f, duration = 3f, tickInterval = 0.5f,
            });
            var e = CreateUnit();
            Stack(e, StackKind.Bleed, countDelta: 2, maxStack: 9, perAppDuration: 100f);
            Tick();

            Assert.AreEqual(1, _dotChannel.Count);
            var ev = _dotChannel.Drain()[0];
            Assert.AreEqual(e, ev.target);
            Assert.AreEqual(DotOrigin.Stack, ev.effect.origin, "병합 키의 파이프라인 축.");
            Assert.AreEqual(DotElement.Bleed, ev.effect.element, "원소는 kind 에서 매핑된다.");
            Assert.AreEqual(12f, ev.effect.scalar, 1e-5f);
            Assert.AreEqual(0.5f, ev.effect.tickInterval, 1e-5f);
            Assert.AreEqual(0.5f, ev.effect.tickTimer, 1e-5f, "첫 틱 즉발 — tickTimer = tickInterval.");
            Assert.AreEqual(3f, ev.effect.remainingTime, 1e-5f);
        }

        [Test]
        public void ApplyStun_UsesMagnitudeAsDuration_NotDurationField()
        {
            Build(new StackThresholdRule
            {
                kind = StackKind.Ice, atStack = 5, mode = ThresholdMode.Edge,
                derivedKind = DerivedEffectKind.ApplyStun,
                magnitude = 0.75f, duration = 999f,   // duration 은 무시돼야 한다
            });
            var e = CreateUnit();
            Stack(e, StackKind.Ice, countDelta: 5, maxStack: 9, perAppDuration: 100f);
            Tick();

            var ev = _ccChannel.Drain()[0];
            Assert.AreEqual(CcKind.Stun, ev.effect.kind);
            Assert.AreEqual(0.75f, ev.effect.remainingTime, 1e-5f,
                "ApplyStun 은 magnitude 가 지속 시간이다(duration 필드는 무시).");
        }

        [Test]
        public void ApplyStat_NamespacesStackId_PerKind_AndSourcesToVictim()
        {
            // 이 파생은 source=피해자 자신이라, 배치/스킬 감속(source=target, stackId=0)과
            // 병합 키 4축이 전부 겹쳤던 이력이 있다 — kind 별 전용 id 로 갈라야 한다.
            Build(new StackThresholdRule
            {
                kind = StackKind.Fire, atStack = 1, mode = ThresholdMode.Edge,
                derivedKind = DerivedEffectKind.ApplyStat,
                stat = StatKind.MoveSpeedMul, op = CombineOp.Multiplicative,
                magnitude = 0.6f, duration = 2f,
            });
            var e = CreateUnit();
            Stack(e, StackKind.Fire, countDelta: 1, maxStack: 9, perAppDuration: 100f);
            Tick();

            var ev = _statChannel.Drain()[0];
            Assert.AreEqual(e, ev.target);
            Assert.AreEqual(e, ev.source, "source 는 피해자 자신.");
            Assert.AreEqual(StatKind.MoveSpeedMul, ev.stat);
            Assert.AreEqual(CombineOp.Multiplicative, ev.op);
            Assert.AreEqual(100 + (int)StackKind.Fire, ev.stackId,
                "stackId 는 kind 별 네임스페이스(base 100 + kind) — 배치 감속과 슬롯이 갈린다.");
            Assert.AreEqual(ModifierOrigin.Stack, ev.origin);
        }

        [Test]
        public void ApplyStat_FatigueGetsBurnoutOrigin()
        {
            Build(new StackThresholdRule
            {
                kind = StackKind.Fatigue, atStack = 1, mode = ThresholdMode.Edge,
                derivedKind = DerivedEffectKind.ApplyStat,
                stat = StatKind.AttackSpeedMul, op = CombineOp.Multiplicative,
                magnitude = 0.8f, duration = 5f,
            });
            var e = CreateUnit();
            Stack(e, StackKind.Fatigue, countDelta: 1, maxStack: 9, perAppDuration: 100f);
            Tick();

            Assert.AreEqual(ModifierOrigin.Burnout, _statChannel.Drain()[0].origin,
                "야근 번아웃만 전용 origin — 상태FX 가 다른 Stack 파생과 안 섞인다.");
        }

        // ── Consume 모드 ──────────────────────────────────────────────────────────

        [Test]
        public void ConsumeMode_SubtractsAtStack_AndEdgeCacheFollowsTheReducedCount()
        {
            Build(Stun(StackKind.Fire, atStack: 3, duration: 1f, mode: ThresholdMode.Consume));
            var e = CreateUnit();
            Stack(e, StackKind.Fire, countDelta: 5, maxStack: 9, perAppDuration: 100f);
            Tick();

            var slot = _world.GetBuffer<StackModifierSlot>(e)[0];
            Assert.AreEqual(1, _ccChannel.Count, "임계 1회 발화.");
            Assert.AreEqual(2, slot.stackCount, "Consume — 5 에서 atStack(3) 을 뺀 2.");
            Assert.AreEqual(2, slot.lastTriggeredStack,
                "엣지 캐시는 **차감이 끝난 뒤의** stackCount 를 따른다.");
        }

        // ── 쿼리 축 ───────────────────────────────────────────────────────────────

        [Test]
        public void TicksEntitiesWithoutModifierStats()
        {
            // 구 쿼리는 버퍼만 본다(`StatModifierTick` 이 ModifierStats 를 함께 요구하는 것과 다르다).
            // `With<ModifierStats>()` 로 좁히면 스탯 캐시 없는 대상이 통째로 빠진다.
            Build(Stun(StackKind.Fire, atStack: 1, duration: 1f));
            var e = _world.Create();                 // ModifierStats 없음
            Stack(e, StackKind.Fire, countDelta: 1, maxStack: 9, perAppDuration: 100f);
            Tick();

            Assert.AreEqual(1, _ccChannel.Count, "ModifierStats 가 없어도 임계는 돈다.");
            Assert.AreEqual(1, _world.GetBuffer<StackModifierSlot>(e)[0].lastTriggeredStack);
        }

        [Test]
        public void ExpiredStackSlot_IsRemoved()
        {
            Build();
            var e = CreateUnit();
            Stack(e, StackKind.Fire, countDelta: 1, maxStack: 9, perAppDuration: 1f);
            Tick(0.016f);
            Assert.AreEqual(1, _world.GetBuffer<StackModifierSlot>(e).Count);

            Tick(2f);
            Assert.AreEqual(0, _world.GetBuffer<StackModifierSlot>(e).Count, "만료 슬롯 제거.");
        }
    }
}
