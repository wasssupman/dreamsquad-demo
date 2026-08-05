// battle-sim-extraction unit 18-C/4 — 신 sim P7 틱 계열의 **오라클 복제**.
//
// 원본 대응(`ModifierFrameworkTests`):
//   Test 2  ModifierStats_Combines_Multiplicative_And_Additive_Then_Override_Wins
//   —       ModifierStats_Combines_MoveSpeedMul_As_Multiplicative_Stat
//   —       DamageVsCcMul_AggregatesToBaseOne_WhenNoVsCcSlot
//   —       DamageVsCcMul_Combines_Multiplicatively
//   —       Clamp_DamageAndMove_UseTheirOwnFloor
//   —       AdditiveBuffs_Sum_ThenCeil
//   Test 5  StatModifier_ExpiresAfterDuration_Even_When_ModifierStatsDirty_Is_False  ← 핫픽스 회귀 가드
//
// 픽스처는 구 오라클과 **같은 실행 순서**를 재현한다: Apply(P2) → StatTick(#29) → Aggregate(#30).
// 구 sim 에선 `UpdateBefore`/`UpdateAfter` 가 그 순서를 만들었고, 신 sim 에선 phase 안의
// 등록 순서가 만든다.
using NUnit.Framework;
using Wassup.Sim;
using Wassup.Sim.Effects;

namespace Wassup.Tests.EditMode
{
    public class SimModifierTickAggregateTests
    {
        private SimWorld _world;
        private SimChannel<StatModifierApplyEvent> _statChannel;
        private SimChannel<StackModifierApplyEvent> _stackChannel;
        private ModifierApplySystem _apply;
        private StatModifierTickSystem _tick;
        private ModifierStatsAggregateSystem _aggregate;

        [SetUp]
        public void SetUp()
        {
            _world = new SimWorld(new SimConfig(pickupSeed: 1u, bombSeedBase: 1u));
            _statChannel = new SimChannel<StatModifierApplyEvent>();
            _stackChannel = new SimChannel<StackModifierApplyEvent>();
            _apply = new ModifierApplySystem(_statChannel, _stackChannel);
            _tick = new StatModifierTickSystem();
            _aggregate = new ModifierStatsAggregateSystem();
        }

        private SimEntityId CreateUnit()
        {
            var e = _world.Create();
            _world.Set(e, ModifierStats.Identity);
            return e;
        }

        private void Tick(float dt = 0.016f)
        {
            _world.SetDeltaTime(dt);
            _apply.Run(_world);
            _tick.Run(_world);
            _aggregate.Run(_world);
        }

        private void Stat(SimEntityId target, StatKind stat, CombineOp op, float magnitude,
                          float duration, SimEntityId source, ushort stackId = 0)
            => _statChannel.Enqueue(new StatModifierApplyEvent
            {
                target = target, stat = stat, op = op, magnitude = magnitude,
                duration = duration, source = source, stackId = stackId,
            });

        private ModifierStats Stats(SimEntityId e) => _world.Get<ModifierStats>(e);

        /// 슬롯을 직접 심고 dirty 를 세운다(구 오라클이 Apply 채널을 거치지 않고 산식만 볼 때 쓴 방식).
        private void PushSlot(SimEntityId e, StatKind stat, CombineOp op, float magnitude, ushort stackId)
        {
            _world.AddBuffer<StatModifierSlot>(e).Add(new StatModifierSlot
            {
                header = new ModifierHeader { remaining = 100f, source = e, stackId = stackId },
                stat = stat, op = op, magnitude = magnitude,
            });
            _world.Set(e, default(ModifierStatsDirty));
        }

        // ── 결합식: (1 + Σadd) * Πmul, override 가 이긴다 ────────────────────────

        [Test]
        public void ModifierStats_Combines_Multiplicative_And_Additive_Then_Override_Wins()
        {
            var e = CreateUnit();

            PushSlot(e, StatKind.DamageMul, CombineOp.Multiplicative, 1.5f, stackId: 0);
            PushSlot(e, StatKind.DamageMul, CombineOp.Additive, 0.2f, stackId: 1);
            Tick();

            Assert.AreEqual(1.8f, Stats(e).damageMul, 1e-4f,
                "damageMul = (1 + Σadd) * Πmul = (1+0.2)*1.5 = 1.8");

            PushSlot(e, StatKind.DamageMul, CombineOp.Override, 3.0f, stackId: 2);
            Tick();

            Assert.AreEqual(3.0f, Stats(e).damageMul, 1e-4f,
                "Override 가 있으면 mul/add 를 무시하고 max(override) = 3.0.");
        }

        [Test]
        public void ModifierStats_Combines_MoveSpeedMul_As_Multiplicative_Stat()
        {
            var e = CreateUnit();
            PushSlot(e, StatKind.MoveSpeedMul, CombineOp.Multiplicative, 0.5f, stackId: 0);
            PushSlot(e, StatKind.MoveSpeedMul, CombineOp.Multiplicative, 0.8f, stackId: 1);
            Tick();

            Assert.AreEqual(0.4f, Stats(e).moveSpeedMul, 1e-4f,
                "MoveSpeedMul 도 다른 배율 스탯과 같이 곱해진다.");
        }

        [Test]
        public void DamageVsCcMul_AggregatesToBaseOne_WhenNoVsCcSlot()
        {
            // 이게 깨지면(0 유지) shatter 미보유 유닛이 CC 걸린 적에게 데미지 0 = 적 무적(critic HIGH).
            var e = _world.Create();
            _world.Set(e, default(ModifierStats));   // 전 필드 0 — vsCc 도 0 에서 출발
            PushSlot(e, StatKind.DamageMul, CombineOp.Multiplicative, 2f, stackId: 0);
            Tick();

            Assert.AreEqual(1f, Stats(e).damageVsCcMul, 1e-5f,
                "집계는 vsCc 슬롯이 없어도 base 1 을 써야 한다(0 이면 CC 적 무적).");
        }

        [Test]
        public void DamageVsCcMul_Combines_Multiplicatively()
        {
            var e = CreateUnit();
            PushSlot(e, StatKind.DamageVsCcMul, CombineOp.Multiplicative, 1.25f, stackId: 0);
            Tick();

            Assert.AreEqual(1.25f, Stats(e).damageVsCcMul, 1e-5f, "+25% vsCc → 1.25.");
        }

        // ── clamp 는 스탯마다 자기 경계를 쓴다 ────────────────────────────────────

        [Test]
        public void Clamp_DamageAndMove_UseTheirOwnFloor()
        {
            var e = CreateUnit();

            // 서로 다른 stackId 5개씩 → 별개 슬롯 → 0.6^5≈0.078 · 0.5^5≈0.031, 둘 다 바닥 아래.
            for (ushort i = 0; i < 5; i++)
            {
                Stat(e, StatKind.DamageMul, CombineOp.Multiplicative, 0.6f, 100f, e, stackId: i);
                Stat(e, StatKind.MoveSpeedMul, CombineOp.Multiplicative, 0.5f, 100f, e, stackId: i);
            }
            Tick();

            var s = Stats(e);
            Assert.AreEqual(0.2f, s.damageMul, 1e-5f, "damageMul 은 자기 바닥 0.2.");
            Assert.AreEqual(0.15f, s.moveSpeedMul, 1e-5f,
                "moveSpeedMul 은 자기 바닥 0.15 — damage 의 0.2 가 아니다.");
        }

        [Test]
        public void MaxHealthMul_UsesItsOwnFloor_SoLastRunTimesPointOneSurvives()
        {
            // 라스트런 ×0.1 은 일반 floor(0.2)에 걸리면 안 된다 — 전용 floor 0.05.
            var e = CreateUnit();
            PushSlot(e, StatKind.MaxHealthMul, CombineOp.Multiplicative, 0.1f, stackId: 0);
            Tick();

            Assert.AreEqual(0.1f, Stats(e).maxHealthMul, 1e-5f,
                "maxHealthMul 전용 floor 0.05 — 일반 floor 였다면 0.2 로 잘렸다.");
        }

        [Test]
        public void AdditiveBuffs_Sum_ThenCeil()
        {
            var e = CreateUnit();
            Stat(e, StatKind.DamageMul, CombineOp.Additive, 3f, 100f, e, stackId: 0);
            Stat(e, StatKind.DamageMul, CombineOp.Additive, 3f, 100f, e, stackId: 1);
            Tick();

            Assert.AreEqual(2, _world.GetBuffer<StatModifierSlot>(e).Count, "stackId 가 다르면 슬롯 2개.");
            Assert.AreEqual(5f, Stats(e).damageMul, 1e-5f,
                "1 + Σadd(3+3) = 7 이 천장 5.0 으로 잘린다(버프는 합산이지 복리가 아니다).");
        }

        [Test]
        public void RegenPerSec_HasBaseZero_NotOne()
        {
            // 유일하게 base 가 0 인 스탯. `(0 + Σadd) * Πmul` 이고 clamp 대신 음수만 막는다.
            var e = CreateUnit();
            PushSlot(e, StatKind.RegenPerSec, CombineOp.Additive, 4f, stackId: 0);
            Tick();
            Assert.AreEqual(4f, Stats(e).regenPerSec, 1e-5f, "base 1 이었다면 5 가 나온다.");

            var e2 = CreateUnit();
            PushSlot(e2, StatKind.RegenPerSec, CombineOp.Additive, -9f, stackId: 0);
            Tick();
            Assert.AreEqual(0f, Stats(e2).regenPerSec, 1e-5f, "음수 회복률은 0 으로 막는다.");
        }

        // ── 만료 — 핫픽스 회귀 가드 ──────────────────────────────────────────────
        // 구 sim 의 사고: TickSystem 이 dirty=true 인 엔티티만 쿼리해, 집계가 dirty 를 끈 뒤엔
        // remaining 이 영영 안 줄어 모디파이어가 **영구 지속**됐다.
        // 신 sim 은 집계가 마커를 **제거**하므로 같은 함정이 다른 모양으로 재발할 수 있다 —
        // 그래서 이 테스트가 구 sim 에서보다 오히려 더 중요하다.

        [Test]
        public void StatModifier_ExpiresAfterDuration_Even_When_NotDirty()
        {
            var e = CreateUnit();
            Stat(e, StatKind.DamageMul, CombineOp.Multiplicative, 1.5f, duration: 2f, source: e);

            // 프레임 1: Apply → Tick(remaining 2-1=1) → Aggregate(damageMul=1.5, dirty 해제)
            Tick(1.0f);
            Assert.AreEqual(1.5f, Stats(e).damageMul, 1e-4f, "모디파이어 활성.");
            Assert.IsFalse(_world.Has<ModifierStatsDirty>(e), "집계가 dirty 를 해제했다.");

            // 프레임 2: dirty 가 없는데도 remaining 이 줄어야 한다(1-1=0 → 만료 → dirty → 집계).
            Tick(1.0f);
            Assert.AreEqual(0, _world.GetBuffer<StatModifierSlot>(e).Count, "만료 슬롯은 제거된다.");
            Assert.AreEqual(1.0f, Stats(e).damageMul, 1e-4f,
                "만료가 집계를 깨워 같은 프레임에 1.0 으로 복귀한다(#29 가 #30 보다 앞).");

            // 프레임 3: 원본 오라클의 관측 시점. 값이 유지된다.
            Tick(1.0f);
            Assert.AreEqual(1.0f, Stats(e).damageMul, 1e-4f);
            Assert.IsTrue(_world.HasBuffer<StatModifierSlot>(e),
                "버퍼 자체는 남는다(빈 버퍼 ≠ 부재).");
        }

        [Test]
        public void ExpiredSlot_RemovalIsSwapBack_NotStable()
        {
            // 순서가 계약이다 — 안정 제거로 바꾸면 집계의 곱셈 누적 순서가 달라진다.
            var e = CreateUnit();
            Stat(e, StatKind.DamageMul, CombineOp.Additive, 1f, duration: 1f, source: e, stackId: 0);
            Stat(e, StatKind.DamageMul, CombineOp.Additive, 2f, duration: 100f, source: e, stackId: 1);
            Stat(e, StatKind.DamageMul, CombineOp.Additive, 3f, duration: 100f, source: e, stackId: 2);
            Tick(0.016f);
            Assert.AreEqual(3, _world.GetBuffer<StatModifierSlot>(e).Count);

            Tick(2f);   // 0번 슬롯만 만료

            var slots = _world.GetBuffer<StatModifierSlot>(e);
            Assert.AreEqual(2, slots.Count);
            Assert.AreEqual(3f, slots[0].magnitude, 1e-5f,
                "swap-back — 마지막(3)이 만료된 0번 자리로 끌려온다. 안정 제거였다면 2 가 온다.");
            Assert.AreEqual(2f, slots[1].magnitude, 1e-5f);
        }

        [Test]
        public void EntityWithoutModifierStats_IsNotTicked()
        {
            // 구 쿼리가 `RefRO<ModifierStats>` 를 요구한다 — 없으면 슬롯이 있어도 틱하지 않는다.
            var e = _world.Create();
            _world.AddBuffer<StatModifierSlot>(e).Add(new StatModifierSlot
            {
                header = new ModifierHeader { remaining = 1f, source = e },
                stat = StatKind.DamageMul, op = CombineOp.Additive, magnitude = 1f,
            });

            _world.SetDeltaTime(5f);
            _tick.Run(_world);

            Assert.AreEqual(1, _world.GetBuffer<StatModifierSlot>(e).Count,
                "ModifierStats 부재 = 쿼리 불일치 — remaining 이 줄지 않는다.");
        }
    }
}
