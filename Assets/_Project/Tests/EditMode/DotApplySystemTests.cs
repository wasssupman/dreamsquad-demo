using NUnit.Framework;
using Unity.Core;
using Unity.Entities;
using Wassup.Battle.Effects;
using Wassup.Battle.Units;

namespace Wassup.Tests.EditMode
{
    public class DotApplySystemTests
    {
        private World _world;
        private EntityManager _em;
        private SimulationSystemGroup _simGroup;

        [SetUp]
        public void SetUp()
        {
            _world = new World("DotApplyTestWorld");
            _em = _world.EntityManager;
            _simGroup = _world.CreateSystemManaged<SimulationSystemGroup>();
            var handle = _world.CreateSystem<DotApplySystem>();
            _simGroup.AddSystemToUpdateList(handle);
        }

        [TearDown]
        public void TearDown()
        {
            _world?.Dispose();
        }

        private void Tick(float dt)
        {
            _world.SetTime(new TimeData(_world.Time.ElapsedTime + dt, dt));
            _simGroup.Update();
        }

        [Test]
        public void Dot_Adds_Damage_Per_Second_Times_DeltaTime()
        {
            var e = _em.CreateEntity();
            _em.AddBuffer<DotEffect>(e);
            _em.AddBuffer<IncomingDamage>(e);
            var cc = _em.GetBuffer<DotEffect>(e);
            cc.Add(new DotEffect { scalar = 10f, remainingTime = 1f });

            Tick(0.25f);

            var damage = _em.GetBuffer<IncomingDamage>(e);
            Assert.AreEqual(1, damage.Length);
            Assert.AreEqual(2.5f, damage[0].amount, 1e-5f);
        }

        [Test]
        public void CcEffects_AreUntouched_ByDotPipeline()
        {
            // dot-effect-extraction unit 0 — 이관 전에는 "DoT 아닌 kind 는 무시한다"를 런타임
            // 분기로 지켰다. 이제 버퍼가 갈렸으므로 **타입이 그것을 보장한다** — CcEffect 는
            // 지속 피해 파이프라인에 들어갈 수조차 없다. 대신 여기서는 행동 제약 슬롯이
            // 지속 피해 처리에 아무 영향도 받지 않는지를 고정한다.
            var e = _em.CreateEntity();
            _em.AddBuffer<DotEffect>(e);
            _em.AddBuffer<CcEffect>(e);
            _em.AddBuffer<IncomingDamage>(e);
            var cc = _em.GetBuffer<CcEffect>(e);
            cc.Add(new CcEffect { kind = CcKind.Stun, remainingTime = 1f });
            cc.Add(new CcEffect { kind = CcKind.Impulse, remainingTime = 1f });

            Tick(0.25f);

            Assert.AreEqual(0, _em.GetBuffer<IncomingDamage>(e).Length, "CC 는 피해를 만들지 않는다");
            Assert.AreEqual(2, _em.GetBuffer<CcEffect>(e).Length, "CC 슬롯을 건드리지 않는다");
            Assert.AreEqual(1f, _em.GetBuffer<CcEffect>(e)[0].remainingTime, 1e-5f,
                "CC 감쇠는 CcDecaySystem 몫 — 지속 피해 파이프라인이 깎으면 안 된다");
        }

        [Test]
        public void Multiple_Dot_Entries_All_Contribute()
        {
            var e = _em.CreateEntity();
            _em.AddBuffer<DotEffect>(e);
            _em.AddBuffer<IncomingDamage>(e);
            var cc = _em.GetBuffer<DotEffect>(e);
            cc.Add(new DotEffect { scalar = 10f, remainingTime = 1f });
            cc.Add(new DotEffect { scalar = 20f, remainingTime = 1f });

            Tick(0.1f);

            var damage = _em.GetBuffer<IncomingDamage>(e);
            Assert.AreEqual(2, damage.Length);
            Assert.AreEqual(1f, damage[0].amount, 1e-5f);
            Assert.AreEqual(2f, damage[1].amount, 1e-5f);
        }

        // dot-tick-cadence unit 1 — 이산 tick DoT

        [Test]
        public void Tick_Dot_First_Tick_Is_Immediate()
        {
            // CcApply add-path 가 tickTimer=tickInterval 로 초기화한 상태를 재현 → 첫 프레임 즉발.
            var e = _em.CreateEntity();
            _em.AddBuffer<DotEffect>(e);
            _em.AddBuffer<IncomingDamage>(e);
            var cc = _em.GetBuffer<DotEffect>(e);
            cc.Add(new DotEffect { scalar = 10f, remainingTime = 0.2f, tickInterval = 0.5f, tickTimer = 0.5f });

            Tick(0.016f);

            var damage = _em.GetBuffer<IncomingDamage>(e);
            Assert.AreEqual(1, damage.Length, "진입 즉시 1 tick");
            Assert.AreEqual(10f, damage[0].amount, 1e-5f, "청크 = scalar(=tick당 데미지), dt 무관");
        }

        [Test]
        public void Tick_Dot_Fires_Once_Per_Interval_Not_Per_Frame()
        {
            var e = _em.CreateEntity();
            _em.AddBuffer<DotEffect>(e);
            _em.AddBuffer<IncomingDamage>(e);
            var cc = _em.GetBuffer<DotEffect>(e);
            cc.Add(new DotEffect { scalar = 10f, remainingTime = 1f, tickInterval = 0.5f, tickTimer = 0f });

            // 0.3s 경과: 아직 0.5s 미도달 → 지급 없음
            Tick(0.3f);
            Assert.AreEqual(0, _em.GetBuffer<IncomingDamage>(e).Length, "0.3<0.5 → 0 tick");

            // 누적 0.6s: 0.5s 도달 → 1회 지급, timer 0.1 잔여
            Tick(0.3f);
            var damage = _em.GetBuffer<IncomingDamage>(e);
            Assert.AreEqual(1, damage.Length, "누적 0.6s → 정확히 1 tick");
            Assert.AreEqual(10f, damage[0].amount, 1e-5f);
            Assert.AreEqual(0.1f, _em.GetBuffer<DotEffect>(e)[0].tickTimer, 1e-5f, "잔여 누적 보존");
        }

        [Test]
        public void Tick_Dot_Large_Dt_Fires_Multiple_Chunks()
        {
            var e = _em.CreateEntity();
            _em.AddBuffer<DotEffect>(e);
            _em.AddBuffer<IncomingDamage>(e);
            var cc = _em.GetBuffer<DotEffect>(e);
            cc.Add(new DotEffect { scalar = 10f, remainingTime = 5f, tickInterval = 0.5f, tickTimer = 0f });

            // dt=1.2, interval=0.5 → 2 tick (1.0 소진), timer 0.2 잔여
            Tick(1.2f);

            var damage = _em.GetBuffer<IncomingDamage>(e);
            Assert.AreEqual(2, damage.Length, "1.2/0.5 = 2 청크");
            Assert.AreEqual(10f, damage[0].amount, 1e-5f);
            Assert.AreEqual(10f, damage[1].amount, 1e-5f);
            Assert.AreEqual(0.2f, _em.GetBuffer<DotEffect>(e)[0].tickTimer, 1e-5f);
        }

        [Test]
        public void Continuous_Dot_Unchanged_When_TickInterval_Zero()
        {
            // 레거시 하위호환: tickInterval=0 이면 scalar*dt 연속(기존 행동).
            var e = _em.CreateEntity();
            _em.AddBuffer<DotEffect>(e);
            _em.AddBuffer<IncomingDamage>(e);
            var cc = _em.GetBuffer<DotEffect>(e);
            cc.Add(new DotEffect { scalar = 20f, remainingTime = 1f, tickInterval = 0f });

            Tick(0.1f);

            var damage = _em.GetBuffer<IncomingDamage>(e);
            Assert.AreEqual(1, damage.Length);
            Assert.AreEqual(2f, damage[0].amount, 1e-5f, "20 DPS * 0.1s");
        }
    }
}
