using NUnit.Framework;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Wassup.Battle.Effects;

namespace Wassup.Tests.EditMode
{
    public class CcApplySystemTests
    {
        private World _world;
        private EntityManager _em;
        private SimulationSystemGroup _simGroup;
        private NativeQueue<EnemyCcEvent> _queue;

        [SetUp]
        public void SetUp()
        {
            _world = new World("CcApplyTestWorld");
            _em = _world.EntityManager;
            _simGroup = _world.CreateSystemManaged<SimulationSystemGroup>();
            var handle = _world.CreateSystem<CcApplySystem>();
            _simGroup.AddSystemToUpdateList(handle);

            _queue = new NativeQueue<EnemyCcEvent>(Allocator.Persistent);
            var singletonEntity = _em.CreateEntity();
            _em.AddComponentData(singletonEntity, new EnemyCcEventsSingleton { queue = _queue });
        }

        [TearDown]
        public void TearDown()
        {
            if (_queue.IsCreated) _queue.Dispose();
            _world?.Dispose();
        }

        private void Tick(float dt = 0.016f)
        {
            _world.SetTime(new TimeData(_world.Time.ElapsedTime + dt, dt));
            _simGroup.Update();
        }

        [Test]
        public void Adds_Entry_To_Empty_Buffer()
        {
            var e = _em.CreateEntity();
            _em.AddBuffer<CcEffect>(e); // mirrors attacker spawn contract: buffer pre-attached
            _queue.Enqueue(new EnemyCcEvent
            {
                target = e,
                effect = new CcEffect { kind = CcKind.Slow, scalar = 0.5f, remainingTime = 2f }
            });

            Tick();

            var buf = _em.GetBuffer<CcEffect>(e);
            Assert.AreEqual(1, buf.Length);
            Assert.AreEqual(CcKind.Slow, buf[0].kind);
        }

        [Test]
        public void Merges_Same_Kind_Taking_Max_RemainingTime_And_New_Scalar()
        {
            var e = _em.CreateEntity();
            var buf = _em.AddBuffer<CcEffect>(e);
            buf.Add(new CcEffect { kind = CcKind.Slow, scalar = 0.5f, remainingTime = 3f });

            _queue.Enqueue(new EnemyCcEvent
            {
                target = e,
                effect = new CcEffect { kind = CcKind.Slow, scalar = 0.25f, remainingTime = 1f }
            });

            Tick();

            var result = _em.GetBuffer<CcEffect>(e);
            Assert.AreEqual(1, result.Length);
            Assert.AreEqual(3f, result[0].remainingTime, 1e-5f, "longer existing remaining should win");
            Assert.AreEqual(0.25f, result[0].scalar, 1e-5f, "new scalar should overwrite");
        }

        // dot-tick-cadence unit 1 — DoT 이산 tick 누적기 불변식 (contract #4)

        [Test]
        public void Merge_Preserves_Existing_TickTimer_So_Ticks_Keep_Advancing()
        {
            // 가장 취약한 불변식: 매 프레임 존 refresh(merge)가 tickTimer 를 보존해야
            // 누적이 진행된다. incoming.tickTimer(0)로 덮으면 DoT 가 영원히 tick 못 함.
            var e = _em.CreateEntity();
            var buf = _em.AddBuffer<CcEffect>(e);
            buf.Add(new CcEffect { kind = CcKind.DoT, scalar = 10f, remainingTime = 0.2f, tickInterval = 0.5f, tickTimer = 0.3f });

            _queue.Enqueue(new EnemyCcEvent
            {
                target = e,
                effect = new CcEffect { kind = CcKind.DoT, scalar = 10f, remainingTime = 0.2f, tickInterval = 0.5f, tickTimer = 0f }
            });

            Tick();

            var result = _em.GetBuffer<CcEffect>(e);
            Assert.AreEqual(1, result.Length);
            Assert.AreEqual(0.3f, result[0].tickTimer, 1e-5f, "merge 는 기존 누적기를 보존(incoming 무시)");
            Assert.AreEqual(0.5f, result[0].tickInterval, 1e-5f);
        }

        [Test]
        public void Add_New_Slot_Preloads_TickTimer_To_TickInterval_For_Immediate_First_Tick()
        {
            // add-path 컨벤션: 빈 버퍼에 첫 DoT 진입 시 tickTimer=tickInterval → 첫 프레임 즉발.
            var e = _em.CreateEntity();
            _em.AddBuffer<CcEffect>(e);
            _queue.Enqueue(new EnemyCcEvent
            {
                target = e,
                effect = new CcEffect { kind = CcKind.DoT, scalar = 10f, remainingTime = 0.2f, tickInterval = 0.5f, tickTimer = 0f }
            });

            Tick();

            var result = _em.GetBuffer<CcEffect>(e);
            Assert.AreEqual(1, result.Length);
            Assert.AreEqual(0.5f, result[0].tickTimer, 1e-5f, "add-path 는 tickTimer=tickInterval 로 초기화");
        }

        [Test]
        public void Merge_Different_Interval_Rescales_TickTimer_Progress()
        {
            // Fire(0.5)↔Poison(1.0) 겹침 전환: 큰/작은 주기 사이에서 timer 를 raw 로 넘기면
            // 조기 발동. 진행률(비례) 환산으로 "다음 tick 까지 %" 를 보존해야 한다.
            var e = _em.CreateEntity();
            var buf = _em.AddBuffer<CcEffect>(e);
            // 기존 Fire 슬롯: interval 0.5, timer 0.3 → 진행률 60%
            buf.Add(new CcEffect { kind = CcKind.DoT, scalar = 10f, remainingTime = 0.2f, tickInterval = 0.5f, tickTimer = 0.3f });

            // Poison(interval 1.0) merge
            _queue.Enqueue(new EnemyCcEvent
            {
                target = e,
                effect = new CcEffect { kind = CcKind.DoT, scalar = 20f, remainingTime = 0.2f, tickInterval = 1.0f, tickTimer = 0f }
            });

            Tick();

            var r = _em.GetBuffer<CcEffect>(e);
            Assert.AreEqual(1, r.Length);
            Assert.AreEqual(1.0f, r[0].tickInterval, 1e-5f, "interval 은 incoming 으로 갱신");
            Assert.AreEqual(0.6f, r[0].tickTimer, 1e-5f, "진행률 60% 보존: 0.3/0.5*1.0");
            Assert.AreEqual(20f, r[0].scalar, 1e-5f, "scalar 는 last-writer");
        }

        [Test]
        public void Merge_Same_Interval_Does_Not_Rescale_Timer()
        {
            // 흔한 케이스(동일 Fire 존 매 프레임 refresh): interval 불변 → timer 그대로.
            var e = _em.CreateEntity();
            var buf = _em.AddBuffer<CcEffect>(e);
            buf.Add(new CcEffect { kind = CcKind.DoT, scalar = 10f, remainingTime = 0.2f, tickInterval = 0.5f, tickTimer = 0.3f });

            _queue.Enqueue(new EnemyCcEvent
            {
                target = e,
                effect = new CcEffect { kind = CcKind.DoT, scalar = 10f, remainingTime = 0.2f, tickInterval = 0.5f, tickTimer = 0f }
            });

            Tick();

            Assert.AreEqual(0.3f, _em.GetBuffer<CcEffect>(e)[0].tickTimer, 1e-5f, "동일 interval → 환산 없음");
        }

        [Test]
        public void EffectSpawner_ApplyCc_Uses_Same_Merge_Policy_Preserving_Tick_Fields()
        {
            // 정책 통합 회귀: EffectSpawner 도 CcEffectMerge 위임 → tick 필드 보존 + 즉발 초기화.
            var e = _em.CreateEntity();
            _em.AddBuffer<CcEffect>(e);

            // 신규: add-path 가 tickTimer=tickInterval 로 즉발 초기화
            EffectSpawner.ApplyCc(_em, e, new CcEffect { kind = CcKind.DoT, scalar = 10f, remainingTime = 0.2f, tickInterval = 0.5f });
            Assert.AreEqual(0.5f, _em.GetBuffer<CcEffect>(e)[0].tickTimer, 1e-5f, "즉발 초기화");

            // 이후 진행시킨 timer 를 흉내: 0.2 로 세팅 후 재적용 → 보존
            var b = _em.GetBuffer<CcEffect>(e);
            b[0] = new CcEffect { kind = CcKind.DoT, scalar = 10f, remainingTime = 0.2f, tickInterval = 0.5f, tickTimer = 0.2f };
            EffectSpawner.ApplyCc(_em, e, new CcEffect { kind = CcKind.DoT, scalar = 10f, remainingTime = 0.2f, tickInterval = 0.5f });
            Assert.AreEqual(0.2f, _em.GetBuffer<CcEffect>(e)[0].tickTimer, 1e-5f, "merge 시 누적기 보존");
        }

        [Test]
        public void Different_Kinds_Accumulate_As_Separate_Entries()
        {
            var e = _em.CreateEntity();
            _em.AddBuffer<CcEffect>(e);

            _queue.Enqueue(new EnemyCcEvent
            {
                target = e,
                effect = new CcEffect { kind = CcKind.Slow, scalar = 0.5f, remainingTime = 2f }
            });
            _queue.Enqueue(new EnemyCcEvent
            {
                target = e,
                effect = new CcEffect { kind = CcKind.Impulse, vector = new float3(1, 0, 0), remainingTime = 0.5f }
            });

            Tick();

            var result = _em.GetBuffer<CcEffect>(e);
            Assert.AreEqual(2, result.Length);
        }

        [Test]
        public void Ignores_Event_When_Target_Was_Destroyed_Before_Apply()
        {
            var e = _em.CreateEntity();
            _queue.Enqueue(new EnemyCcEvent
            {
                target = e,
                effect = new CcEffect { kind = CcKind.Slow, scalar = 0.5f, remainingTime = 2f }
            });
            _em.DestroyEntity(e);

            Assert.DoesNotThrow(() => Tick());
            Assert.IsTrue(_queue.IsEmpty());
        }
    }
}
