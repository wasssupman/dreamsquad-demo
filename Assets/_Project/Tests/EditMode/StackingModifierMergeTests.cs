// dreamcatcher-berserker unit 0 — 스탯 모디파이어 병합의 «누적 상한» 축.
//
// 여기서 고정하는 것은 규칙 넷이다:
//   ① 상한이 있으면 같은 키의 재적용이 덮어쓰기가 아니라 누적이다
//   ② 상한에서 멈춘다
//   ③ **상한에 닿아도 지속은 계속 갱신된다** — 이게 깨지면 최대 중첩에서 버프가 스스로
//      꺼진다(스택 임계 방식을 안 쓴 이유와 같은 함정)
//   ④ **상한을 안 실은 이벤트는 여전히 덮어쓴다** — 이 엔진의 버프 회수가 「항등값으로
//      덮어쓰기」라서, 이게 깨지면 카드를 떼도 버프가 안 지워진다
using NUnit.Framework;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Wassup.Battle.Effects;

namespace Wassup.Tests.EditMode
{
    public class StackingModifierMergeTests
    {
        private World _world;
        private EntityManager _em;
        private SimulationSystemGroup _simGroup;
        private NativeQueue<StatModifierApplyEvent> _statQueue;
        private NativeQueue<StackModifierApplyEvent> _stackQueue;

        private const float PerStack = 0.08f;  // 배율 1.08 → 가산 버킷 0.08
        private const int   MaxStacks = 10;
        private const float Cap = PerStack * MaxStacks; // 0.8 = +80%
        private const float Ttl = 4f;

        [SetUp]
        public void SetUp()
        {
            _world    = new World("StackingModifierMergeTests");
            _em       = _world.EntityManager;
            _simGroup = _world.CreateSystemManaged<SimulationSystemGroup>();
            _simGroup.AddSystemToUpdateList(_world.CreateSystem<ModifierApplySystem>());
            _simGroup.AddSystemToUpdateList(_world.CreateSystem<StatModifierTickSystem>());

            _statQueue  = new NativeQueue<StatModifierApplyEvent>(Allocator.Persistent);
            _stackQueue = new NativeQueue<StackModifierApplyEvent>(Allocator.Persistent);
            _em.AddComponentData(_em.CreateEntity(),
                new StatModifierApplyEventsSingleton { queue = _statQueue });
            _em.AddComponentData(_em.CreateEntity(),
                new StackModifierApplyEventsSingleton { queue = _stackQueue });
        }

        [TearDown]
        public void TearDown()
        {
            if (_statQueue.IsCreated)  _statQueue.Dispose();
            if (_stackQueue.IsCreated) _stackQueue.Dispose();
            _world?.Dispose();
        }

        private void Tick(float dt = 0.016f)
        {
            _world.SetTime(new TimeData(_world.Time.ElapsedTime + dt, dt));
            _simGroup.Update();
        }

        // ⚠ ModifierStats 가 **필수**다 — StatModifierTickSystem 이 그 컴포넌트를 가진
        // 엔티티만 훑는다. 없으면 remaining 이 영원히 안 줄어서 「식는다」 계열 단언이
        // 전부 거짓 초록/거짓 빨강이 된다(실제로 이 픽스처의 첫 실패가 그것이었다).
        private Entity NewUnit()
        {
            var e = _em.CreateEntity();
            _em.AddComponentData(e, new ModifierStats
            {
                damageMul      = 1f,
                attackSpeedMul = 1f,
                dmgTakenMul    = 1f,
                regenPerSec    = 0f,
                moveSpeedMul   = 1f,
            });
            _em.AddComponent<ModifierStatsDirty>(e);
            _em.SetComponentEnabled<ModifierStatsDirty>(e, false);
            return e;
        }

        /// 광란 한 대분 — 가산 버킷에 PerStack 을 상한까지 누적.
        private void Hit(Entity e, float cap = Cap, float mag = PerStack, float ttl = Ttl)
        {
            _statQueue.Enqueue(new StatModifierApplyEvent
            {
                target       = e,
                stat         = StatKind.AttackSpeedMul,
                op           = CombineOp.Additive,
                magnitude    = mag,
                duration     = ttl,
                source       = e,
                stackId      = 7,
                magnitudeCap = cap,
            });
        }

        private StatModifierSlot Slot(Entity e)
        {
            var buf = _em.GetBuffer<StatModifierSlot>(e);
            Assert.AreEqual(1, buf.Length, "누적은 슬롯을 늘리지 않는다 — 항상 한 칸이다.");
            return buf[0];
        }

        // ── ① 누적 ────────────────────────────────────────────────────────────────

        [Test]
        public void RepeatedApply_WithCap_Accumulates_InOneSlot()
        {
            var e = NewUnit();

            Hit(e); Tick();
            Assert.AreEqual(PerStack, Slot(e).magnitude, 1e-5f, "1중첩");

            Hit(e); Tick();
            Assert.AreEqual(PerStack * 2f, Slot(e).magnitude, 1e-5f, "2중첩");

            Hit(e); Tick();
            Assert.AreEqual(PerStack * 3f, Slot(e).magnitude, 1e-5f, "3중첩");
        }

        [Test]
        public void FirstApply_WithCap_UsesIncomingValue()
        {
            var e = NewUnit();
            Hit(e); Tick();
            Assert.AreEqual(PerStack, Slot(e).magnitude, 1e-5f,
                "첫 적용은 기존값이 0 이라 들어온 값 그대로여야 한다.");
        }

        // ── ② 상한 ────────────────────────────────────────────────────────────────

        [Test]
        public void Accumulation_ClampsAtCap()
        {
            var e = NewUnit();
            for (int i = 0; i < MaxStacks + 5; i++) { Hit(e); Tick(); }
            Assert.AreEqual(Cap, Slot(e).magnitude, 1e-5f,
                "최대 중첩을 넘겨 때려도 상한에서 멈춘다.");
        }

        // ── ③ 상한에 닿아도 지속은 갱신된다 (가장 중요한 회귀 핀) ──────────────────

        [Test]
        public void AtCap_Duration_Still_Refreshes()
        {
            var e = NewUnit();
            for (int i = 0; i < MaxStacks + 2; i++) { Hit(e); Tick(); }
            Assert.AreEqual(Cap, Slot(e).magnitude, 1e-5f);

            // 상한에 머무른 채 3초를 흘려보낸다.
            for (int i = 0; i < 30; i++) Tick(0.1f);
            Assert.Less(Slot(e).header.remaining, 1.5f, "실제로 시간이 흘렀어야 한다.");

            // 상한이라 magnitude 는 안 자라지만 지속은 되살아나야 한다.
            Hit(e); Tick();
            Assert.AreEqual(Cap, Slot(e).magnitude, 1e-5f);
            Assert.Greater(Slot(e).header.remaining, 3f,
                "상한에 닿았다고 지속 갱신까지 막으면 최대 중첩에서 버프가 스스로 꺼진다.");
        }

        [Test]
        public void StopHitting_WholeBuff_Disappears_AtOnce()
        {
            var e = NewUnit();
            for (int i = 0; i < 5; i++) { Hit(e); Tick(); }
            Assert.AreEqual(1, _em.GetBuffer<StatModifierSlot>(e).Length);

            for (int i = 0; i < 60; i++) Tick(0.1f); // 6초 > 지속 4초
            Assert.AreEqual(0, _em.GetBuffer<StatModifierSlot>(e).Length,
                "중첩은 하나씩 빠지지 않는다 — 지속이 끝나면 통째로 사라진다.");
        }

        // ── ④ 상한 0 = 기존 덮어쓰기 ──────────────────────────────────────────────

        [Test]
        public void NoCap_Overwrites_LikeBefore()
        {
            var e = NewUnit();
            Hit(e, cap: 0f, mag: 0.5f); Tick();
            Hit(e, cap: 0f, mag: 0.5f); Tick();
            Assert.AreEqual(0.5f, Slot(e).magnitude, 1e-5f,
                "상한을 안 실은 이벤트는 예전처럼 덮어쓴다(기존 생산자 전부가 이 경로다).");
        }

        [Test]
        public void Neutralize_WithoutCap_Resets_AccumulatedSlot()
        {
            var e = NewUnit();
            for (int i = 0; i < 5; i++) { Hit(e); Tick(); }
            Assert.Greater(Slot(e).magnitude, PerStack, "먼저 실제로 쌓여 있어야 한다.");

            // 이 엔진의 회수는 슬롯 삭제가 아니라 «항등값 덮어쓰기» 다
            // (BattleBridge.RevokeDreamcatcherEffects). 그 이벤트는 상한을 싣지 않는다.
            Hit(e, cap: 0f, mag: 0f);
            Tick();
            Assert.AreEqual(0f, Slot(e).magnitude, 1e-5f,
                "지우는 이벤트가 상한을 실으면 min(cap, 기존+0)=기존 이 되어 버프가 안 지워진다.");
        }
    }
}
