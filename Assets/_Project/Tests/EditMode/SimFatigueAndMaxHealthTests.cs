// battle-sim-extraction unit 18-C/5 — 18-C/2 특성화의 **신 sim 복제**.
//
// 원본은 `FatigueAccrualSystemTests`(6) · `MaxHealthScaleSystemTests`(7) 이고, 그 둘은 구 sim 에
// 붙어 초록을 확인한 오라클이다(계획서 §증인 4). 여기는 **어서션을 그대로** 옮긴다 — 값도
// 문구도 같다. 구 버전은 unit 20 스왑 때 삭제한다.
//
// 이 복제가 곧 변이 검증이다: 이식이 골격을 어긋나게 옮겼다면(부착 조건·중간 Playback·
// 래치·가드 위치·while 루프) 여기서 빨간불이 난다. 구 sim 을 일부러 깨뜨릴 필요가 없다.
using NUnit.Framework;
using Wassup.Sim;
using Wassup.Sim.Effects;
using Wassup.Sim.Units;

namespace Wassup.Tests.EditMode
{
    public class SimFatigueAccrualTests
    {
        private SimWorld _world;
        private SimChannel<StackModifierApplyEvent> _stackChannel;
        private FatigueAccrualSystem _sys;

        [SetUp]
        public void SetUp()
        {
            _world = new SimWorld(new SimConfig(pickupSeed: 1u, bombSeedBase: 1u));
            _stackChannel = new SimChannel<StackModifierApplyEvent>();
            _sys = new FatigueAccrualSystem(_stackChannel);
        }

        private void Configure(float interval, byte amount = 1, byte maxStack = 5,
                               float perAppDuration = 30f)
        {
            var e = _world.Create();
            _world.Set(e, new BurnoutGimmickConfig
            {
                fatigueInterval = interval,
                fatigueAmount = amount,
                fatigueMaxStack = maxStack,
                fatiguePerAppDuration = perAppDuration,
            });
        }

        private SimEntityId CreateDefender()
        {
            var e = _world.Create();
            _world.Set(e, default(DefenderUnitTag));
            return e;
        }

        private void Tick(float dt)
        {
            _world.SetDeltaTime(dt);
            _sys.Run(_world);
        }

        [Test]
        public void NoGimmickConfig_SelfGate_NeitherAttachesNorEnqueues()
        {
            var d = CreateDefender();
            Tick(10f);

            Assert.IsFalse(_world.Has<FatigueAccrual>(d),
                "기믹 비활성이면 타이머 부착조차 일어나지 않는다(시스템 자체가 안 돈다).");
            Assert.AreEqual(0, _stackChannel.Count);
        }

        [Test]
        public void NonPositiveInterval_ReturnsBeforeLazyAttach()
        {
            Configure(interval: 0f);
            var d = CreateDefender();
            Tick(10f);

            Assert.IsFalse(_world.Has<FatigueAccrual>(d),
                "interval<=0 가드는 Pass 1(부착)보다 **앞**이다 — 부착도 하지 않는다.");
            Assert.AreEqual(0, _stackChannel.Count);
        }

        [Test]
        public void LazyAttach_AndAccrues_InTheSameFrame()
        {
            Configure(interval: 1f);
            var d = CreateDefender();
            Tick(0.25f);

            Assert.IsTrue(_world.Has<FatigueAccrual>(d), "배치된 defender 에 타이머가 붙는다.");
            Assert.AreEqual(0.25f, _world.Get<FatigueAccrual>(d).elapsed, 1e-5f,
                "중간 Playback — 부착된 그 프레임의 Pass 2 가 이미 누적한다(elapsed 가 0 이 아니다).");
            Assert.AreEqual(0, _stackChannel.Count, "주기 미도달 — 발행 없음.");
        }

        [Test]
        public void IntervalCrossing_EnqueuesSelfSourcedFatigueStack_FromConfig()
        {
            Configure(interval: 1f, amount: 2, maxStack: 7, perAppDuration: 30f);
            var d = CreateDefender();
            Tick(1f);

            Assert.AreEqual(1, _stackChannel.Count, "주기 1회 도달 → 1건.");
            var ev = _stackChannel.Drain()[0];
            Assert.AreEqual(d, ev.target);
            Assert.AreEqual(d, ev.source, "피로도의 source 는 **자기 자신**이다(병합 키 축).");
            Assert.AreEqual(StackKind.Fatigue, ev.kind);
            Assert.AreEqual(2, ev.countDelta, "countDelta = config.fatigueAmount");
            Assert.AreEqual(7, ev.maxStack, "maxStack = config.fatigueMaxStack");
            Assert.AreEqual(30f, ev.perAppDuration, 1e-5f, "perAppDuration = config 값");

            Assert.AreEqual(0f, _world.Get<FatigueAccrual>(d).elapsed, 1e-5f,
                "발행 후 elapsed 는 **차감**된다(0 대입이 아니다).");
        }

        [Test]
        public void SingleTick_SpanningMultipleIntervals_EnqueuesOnePerCrossing_AndCarriesRemainder()
        {
            Configure(interval: 1f);
            var d = CreateDefender();
            Tick(2.5f);

            Assert.AreEqual(2, _stackChannel.Count,
                "while 루프 — 한 틱이 건너뛴 주기마다 1건씩(if 면 1건으로 유실).");
            Assert.AreEqual(0.5f, _world.Get<FatigueAccrual>(d).elapsed, 1e-5f,
                "나머지는 이월된다(0 대입이 아니다).");
        }

        [Test]
        public void NonDefender_NeverAccrues()
        {
            Configure(interval: 1f);
            var plain = _world.Create();
            Tick(5f);

            Assert.IsFalse(_world.Has<FatigueAccrual>(plain),
                "DefenderUnitTag 가 없으면 부착 대상이 아니다(적·해저드·싱글턴 엔티티 포함).");
            Assert.AreEqual(0, _stackChannel.Count);
        }
    }

    public class SimMaxHealthScaleTests
    {
        private SimWorld _world;
        private MaxHealthScaleSystem _sys;

        [SetUp]
        public void SetUp()
        {
            _world = new SimWorld(new SimConfig(pickupSeed: 1u, bombSeedBase: 1u));
            _sys = new MaxHealthScaleSystem();
        }

        private SimEntityId CreateUnit(float value, float max, float maxHealthMul)
        {
            var e = _world.Create();
            _world.Set(e, new Health { value = value, max = max });
            var stats = ModifierStats.Identity;
            stats.maxHealthMul = maxHealthMul;
            _world.Set(e, stats);
            return e;
        }

        private void SetMul(SimEntityId e, float mul)
        {
            var s = _world.Get<ModifierStats>(e);
            s.maxHealthMul = mul;
            _world.Set(e, s);
        }

        private void SetHp(SimEntityId e, float value)
        {
            var h = _world.Get<Health>(e);
            h.value = value;
            _world.Set(e, h);
        }

        private void Tick()
        {
            _world.SetDeltaTime(0.016f);
            _sys.Run(_world);
        }

        [Test]
        public void MulExactlyOne_NeverAttachesState()
        {
            var e = CreateUnit(value: 70f, max: 100f, maxHealthMul: 1f);
            Tick();

            Assert.IsFalse(_world.Has<MaxHealthScaleState>(e),
                "배율이 1 이면 상태를 붙이지 않는다 — 대다수 유닛이 이 경로다.");
            var h = _world.Get<Health>(e);
            Assert.AreEqual(70f, h.value, 1e-5f);
            Assert.AreEqual(100f, h.max, 1e-5f);
        }

        [Test]
        public void MulZero_UninitializedGuard_NeverAttaches()
        {
            var e = CreateUnit(value: 100f, max: 100f, maxHealthMul: 0f);
            Tick();

            Assert.IsFalse(_world.Has<MaxHealthScaleState>(e),
                "mul<=0 은 미초기화로 보고 부착하지 않는다.");
            Assert.AreEqual(100f, _world.Get<Health>(e).max, 1e-5f);
        }

        [Test]
        public void Attach_AndApply_InTheSameFrame()
        {
            var e = CreateUnit(value: 100f, max: 100f, maxHealthMul: 0.8f);
            Tick();

            Assert.IsTrue(_world.Has<MaxHealthScaleState>(e));
            var st = _world.Get<MaxHealthScaleState>(e);
            Assert.AreEqual(100f, st.baseMax, 1e-5f, "baseMax 는 **부착 시점의 Health.max** 다.");
            Assert.AreEqual(0.8f, st.appliedMul, 1e-5f);

            var h = _world.Get<Health>(e);
            Assert.AreEqual(80f, h.max, 1e-5f,
                "중간 Playback — 부착된 그 프레임의 Pass 2 가 이미 적용한다(다음 틱이 아니다).");
            Assert.AreEqual(80f, h.value, 1e-5f, "축소는 value 를 새 max 로 클램프.");
        }

        [Test]
        public void AppliedMulLatch_DoesNotRecompute_WhileMulUnchanged()
        {
            var e = CreateUnit(value: 100f, max: 100f, maxHealthMul: 0.8f);
            Tick();

            SetHp(e, 40f);
            Tick();

            var h = _world.Get<Health>(e);
            Assert.AreEqual(40f, h.value, 1e-5f, "배율 무변 → 재계산 없음(피해가 보존된다).");
            Assert.AreEqual(80f, h.max, 1e-5f);
        }

        [Test]
        public void RestoreToOne_RestoresMaxFromBaseMax_WithoutFreeHeal()
        {
            var e = CreateUnit(value: 100f, max: 100f, maxHealthMul: 0.8f);
            Tick();

            SetMul(e, 1f);
            Tick();

            var h = _world.Get<Health>(e);
            Assert.AreEqual(100f, h.max, 1e-5f, "max 는 baseMax 로 복원된다.");
            Assert.AreEqual(80f, h.value, 1e-5f, "value 는 오르지 않는다 — 무료 힐 없음.");
            Assert.AreEqual(1f, _world.Get<MaxHealthScaleState>(e).appliedMul, 1e-5f,
                "복원도 래치를 갱신한다(mul==1 이 continue 대상이 아니다).");
        }

        [Test]
        public void MulDropsToZeroAfterAttach_Pass2Skips()
        {
            var e = CreateUnit(value: 100f, max: 100f, maxHealthMul: 0.8f);
            Tick();

            SetMul(e, 0f);
            Tick();

            var h = _world.Get<Health>(e);
            Assert.AreEqual(80f, h.max, 1e-5f, "mul<=0 은 Pass 2 도 건너뛴다.");
            Assert.AreEqual(80f, h.value, 1e-5f);
            Assert.AreEqual(0.8f, _world.Get<MaxHealthScaleState>(e).appliedMul, 1e-5f,
                "건너뛰었으므로 래치도 갱신되지 않는다.");
        }

        [Test]
        public void BaseMaxCapturedOnce_LaterMulAppliesToOriginal_NotCurrentMax()
        {
            var e = CreateUnit(value: 100f, max: 100f, maxHealthMul: 0.8f);
            Tick();

            SetMul(e, 1.5f);
            Tick();

            Assert.AreEqual(150f, _world.Get<Health>(e).max, 1e-5f,
                "baseMax(100)×1.5 = 150. 현재 max(80)에 곱했다면 120 — 누적 오염이다.");
            Assert.AreEqual(100f, _world.Get<MaxHealthScaleState>(e).baseMax, 1e-5f,
                "baseMax 는 재캡처되지 않는다.");
            Assert.AreEqual(80f, _world.Get<Health>(e).value, 1e-5f,
                "확대는 value 를 올리지 않는다.");
        }
    }

    /// 순수 산식 복제 — 구 `HealthScaleMaxTests` 6건과 같은 어서션.
    public class SimHealthScaleMaxTests
    {
        [Test]
        public void Identity_MulOne_ReturnsUnchanged()
        {
            var r = Health.ScaleMax(value: 70f, baseMax: 100f, mul: 1f);
            Assert.AreEqual(70f, r.x, 1e-5f);
            Assert.AreEqual(100f, r.y, 1e-5f);
        }

        [Test]
        public void Shrink_ClampsValueToNewMax()
        {
            var r = Health.ScaleMax(value: 90f, baseMax: 100f, mul: 0.8f);
            Assert.AreEqual(80f, r.x, 1e-5f);
            Assert.AreEqual(80f, r.y, 1e-5f);
        }

        [Test]
        public void Shrink_ValueBelowNewMax_Unchanged()
        {
            var r = Health.ScaleMax(value: 30f, baseMax: 100f, mul: 0.8f);
            Assert.AreEqual(30f, r.x, 1e-5f);
            Assert.AreEqual(80f, r.y, 1e-5f);
        }

        [Test]
        public void Restore_NoFreeHeal()
        {
            var r = Health.ScaleMax(value: 55f, baseMax: 100f, mul: 1f);
            Assert.AreEqual(55f, r.x, 1e-5f);
            Assert.AreEqual(100f, r.y, 1e-5f);
        }

        [Test]
        public void LastRun_NinetyPercentCut()
        {
            var r = Health.ScaleMax(value: 200f, baseMax: 200f, mul: 0.1f);
            Assert.AreEqual(20f, r.x, 1e-5f);
            Assert.AreEqual(20f, r.y, 1e-5f);
        }

        [Test]
        public void TinyBase_FlooredAtOneHp()
        {
            var r = Health.ScaleMax(value: 5f, baseMax: 5f, mul: 0.1f);
            Assert.AreEqual(1f, r.y, 1e-5f);
            Assert.AreEqual(1f, r.x, 1e-5f);
        }
    }
}
