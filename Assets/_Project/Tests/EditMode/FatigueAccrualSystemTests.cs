// battle-sim-extraction unit 18-C/2 — **특성화 테스트(구 sim)**.
//
// 왜 지금 구 sim 에 붙이나: `FatigueAccrualSystem` 은 오라클이 **0** 이다(계획서 §증인 3).
// 이식한 뒤 신 sim 에 테스트를 처음 붙이면 그건 오라클이 아니라 자기 확인이다 — 신 코드가
// 무엇을 하든 테스트가 그것을 따라 쓰게 된다. 그래서 **구 sim 에 먼저 붙여 초록을 확인**하고,
// 18-C 이식 후 어서션 그대로 신 sim 에 복제한다(계획서 §증인 4·5).
//
// 이 픽스처는 `FatigueAccrualSystem` **하나만** 월드에 올린다. 스택 채널의 소비측
// (`ModifierApplySystem`)은 일부러 뺐다 — 여기서 박제할 것은 이 시스템이 **무엇을 큐에 넣는가**
// 이고, 생산↔소비의 틱 지연은 채널 계약(18-B/18-C 의 26쌍)이 따로 소유한다. 소비자를 끼우면
// 큐가 비어 페이로드를 관측할 수 없다.
using NUnit.Framework;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Wassup.Battle.Effects;
using Wassup.Battle.Units;

namespace Wassup.Tests.EditMode
{
    public class FatigueAccrualSystemTests
    {
        private World _world;
        private EntityManager _em;
        private SimulationSystemGroup _simGroup;
        private NativeQueue<StackModifierApplyEvent> _stackQueue;

        [SetUp]
        public void SetUp()
        {
            _world    = new World("FatigueAccrualSystemTests");
            _em       = _world.EntityManager;
            _simGroup = _world.CreateSystemManaged<SimulationSystemGroup>();
            _simGroup.AddSystemToUpdateList(_world.CreateSystem<FatigueAccrualSystem>());

            // `RequireForUpdate` 2개 중 하나. 나머지(BurnoutGimmickConfig)는 테스트가 직접 만든다 —
            // 그 부재가 곧 self-gate 의 관측점이라 SetUp 에 두면 첫 테스트를 쓸 수 없다.
            _stackQueue = new NativeQueue<StackModifierApplyEvent>(Allocator.Persistent);
            var singleton = _em.CreateEntity();
            _em.AddComponentData(singleton,
                new StackModifierApplyEventsSingleton { queue = _stackQueue });
        }

        [TearDown]
        public void TearDown()
        {
            if (_stackQueue.IsCreated) _stackQueue.Dispose();
            _world?.Dispose();
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private void Configure(float interval, byte amount = 1, byte maxStack = 5,
                               float perAppDuration = 30f)
        {
            var e = _em.CreateEntity();
            _em.AddComponentData(e, new BurnoutGimmickConfig
            {
                fatigueInterval       = interval,
                fatigueAmount         = amount,
                fatigueMaxStack       = maxStack,
                fatiguePerAppDuration = perAppDuration,
            });
        }

        private Entity CreateDefender()
        {
            var e = _em.CreateEntity();
            _em.AddComponent<DefenderUnitTag>(e);
            return e;
        }

        private void Tick(float deltaTime)
        {
            _world.SetTime(new TimeData(_world.Time.ElapsedTime + deltaTime, deltaTime));
            _simGroup.Update();
        }

        // ── 1. self-gate ──────────────────────────────────────────────────────────
        // 기믹 비활성(BurnoutGimmickConfig 부재) = 시스템이 **한 번도 돌지 않는다**.
        // 신 sim 에는 RequireForUpdate 가 없으므로 이 게이트는 phase early-return 으로 옮겨진다
        // (18-B 의 53 게이트 중 1건) — 그때 이 어서션이 "게이트가 실제로 존재하는가" 의 증인이다.

        [Test]
        public void NoGimmickConfig_SelfGate_NeitherAttachesNorEnqueues()
        {
            var d = CreateDefender();

            Tick(10f);

            Assert.IsFalse(_em.HasComponent<FatigueAccrual>(d),
                "기믹 비활성이면 타이머 부착조차 일어나지 않는다(시스템 자체가 안 돈다).");
            Assert.AreEqual(0, _stackQueue.Count);
        }

        // ── 2. 저작 방어 가드의 **위치** ──────────────────────────────────────────
        // `fatigueInterval <= 0` 은 무한 루프 방어인데, 그 return 이 Pass 1(lazy attach)보다
        // **앞**이다. 이식할 때 가드를 누적 루프 안으로 옮기면 부착이 일어나 상태가 갈린다
        // (엔티티가 FatigueAccrual 을 갖느냐 마느냐는 상태 해시에 그대로 나간다).

        [Test]
        public void NonPositiveInterval_ReturnsBeforeLazyAttach()
        {
            Configure(interval: 0f);
            var d = CreateDefender();

            Tick(10f);

            Assert.IsFalse(_em.HasComponent<FatigueAccrual>(d),
                "interval<=0 가드는 Pass 1(부착)보다 **앞**이다 — 부착도 하지 않는다.");
            Assert.AreEqual(0, _stackQueue.Count);
        }

        // ── 3. 중간 Playback — 부착과 누적이 같은 프레임 ──────────────────────────
        // Pass 1 의 ECB 를 Pass 2 **전에** Playback 하므로, 부착된 그 프레임에 이미 dt 가 쌓인다.
        // 이식이 부착을 다음 틱으로 미루면 모든 defender 의 피로도가 영구히 1틱씩 밀린다.

        [Test]
        public void LazyAttach_AndAccrues_InTheSameFrame()
        {
            Configure(interval: 1f);
            var d = CreateDefender();

            Tick(0.25f);

            Assert.IsTrue(_em.HasComponent<FatigueAccrual>(d), "배치된 defender 에 타이머가 붙는다.");
            Assert.AreEqual(0.25f, _em.GetComponentData<FatigueAccrual>(d).elapsed, 1e-5f,
                "중간 Playback — 부착된 그 프레임의 Pass 2 가 이미 누적한다(elapsed 가 0 이 아니다).");
            Assert.AreEqual(0, _stackQueue.Count, "주기 미도달 — 발행 없음.");
        }

        // ── 4. 주기 도달 시 페이로드 ──────────────────────────────────────────────
        // 6필드 전부를 박제한다. 특히 `source = target`(자기 자신)은 병합 키의 한 축이라
        // 다른 값으로 이식하면 스택이 합쳐지지 않고 슬롯이 갈린다.

        [Test]
        public void IntervalCrossing_EnqueuesSelfSourcedFatigueStack_FromConfig()
        {
            Configure(interval: 1f, amount: 2, maxStack: 7, perAppDuration: 30f);
            var d = CreateDefender();

            Tick(1f);

            Assert.AreEqual(1, _stackQueue.Count, "주기 1회 도달 → 1건.");
            var ev = _stackQueue.Dequeue();
            Assert.AreEqual(d, ev.target);
            Assert.AreEqual(d, ev.source, "피로도의 source 는 **자기 자신**이다(병합 키 축).");
            Assert.AreEqual(StackKind.Fatigue, ev.kind);
            Assert.AreEqual(2, ev.countDelta,        "countDelta = config.fatigueAmount");
            Assert.AreEqual(7, ev.maxStack,          "maxStack = config.fatigueMaxStack");
            Assert.AreEqual(30f, ev.perAppDuration, 1e-5f, "perAppDuration = config 값");

            Assert.AreEqual(0f, _em.GetComponentData<FatigueAccrual>(d).elapsed, 1e-5f,
                "발행 후 elapsed 는 **차감**된다(0 대입이 아니다 — 4의 나머지 이월과 같은 산식).");
        }

        // ── 5. while 루프 — 한 틱이 여러 주기를 건너뛰면 전부 발행 ────────────────
        // `if` 로 이식하면 저프레임/슬로우모 복귀 구간에서 피로도가 조용히 유실된다.
        // 나머지 이월(2.5 → 0.5)이 `elapsed = 0` 대입과 갈리는 지점이다.

        [Test]
        public void SingleTick_SpanningMultipleIntervals_EnqueuesOnePerCrossing_AndCarriesRemainder()
        {
            Configure(interval: 1f);
            var d = CreateDefender();

            Tick(2.5f);

            Assert.AreEqual(2, _stackQueue.Count,
                "while 루프 — 한 틱이 건너뛴 주기마다 1건씩(if 면 1건으로 유실).");
            Assert.AreEqual(0.5f, _em.GetComponentData<FatigueAccrual>(d).elapsed, 1e-5f,
                "나머지는 이월된다(0 대입이 아니다).");
        }

        // ── 6. 대상 한정 ──────────────────────────────────────────────────────────

        [Test]
        public void NonDefender_NeverAccrues()
        {
            Configure(interval: 1f);
            var plain = _em.CreateEntity();

            Tick(5f);

            Assert.IsFalse(_em.HasComponent<FatigueAccrual>(plain),
                "DefenderUnitTag 가 없으면 부착 대상이 아니다(적·해저드·싱글턴 엔티티 포함).");
            Assert.AreEqual(0, _stackQueue.Count);
        }
    }
}
