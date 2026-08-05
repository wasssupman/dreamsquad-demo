// battle-sim-extraction unit 18-D — CC/DoT 클러스터의 **오라클 복제**.
//
// 원본: `CcApplySystemTests`(8) · `DotApplySystemTests`(7) · `BossCcImmunityTests` ·
//       `DotEffectMergeTests` · `DotTickTests` · `CcActionLockTests`.
//
// **복제 불가 1건**: `EffectSpawner_ApplyCc_Uses_Same_Merge_Policy` — 두 번째 호출자가
// Bridge 의 `EffectSpawner` 라 신 sim 에 대응물이 없다. 그 테스트가 지키던 것("두 경로가 같은
// 병합 정책을 쓴다")은 신 sim 에서 **구조가 보증**한다: `CcEffectMerge` 호출자가 하나뿐이다.
using System.Collections.Generic;
using NUnit.Framework;
using Wassup.Sim;
using Wassup.Sim.Combat;
using Wassup.Sim.Effects;
using Wassup.Sim.Units;

namespace Wassup.Tests.EditMode
{
    public class SimCcApplyTests
    {
        private SimWorld _world;
        private SimChannels _ch;
        private CcApplySystem _sys;

        [SetUp]
        public void SetUp()
        {
            _world = new SimWorld(new SimConfig(1u, 1u));
            _ch = new SimChannels();
            _sys = new CcApplySystem(_ch.EnemyCc);
        }

        private SimEntityId WithCcBuffer()
        {
            var e = _world.Create();
            _world.AddBuffer<CcEffect>(e);   // 스폰 계약 재현: 버퍼 선부착
            return e;
        }

        private void Cc(SimEntityId target, CcEffect effect)
            => _ch.EnemyCc.Enqueue(new EnemyCcEvent { target = target, effect = effect });

        private List<CcEffect> Buf(SimEntityId e) => _world.GetBuffer<CcEffect>(e);

        [Test]
        public void Adds_Entry_To_Empty_Buffer()
        {
            var e = WithCcBuffer();
            Cc(e, new CcEffect { kind = CcKind.Slow, scalar = 0.5f, remainingTime = 2f });
            _sys.Run(_world);

            Assert.AreEqual(1, Buf(e).Count);
            Assert.AreEqual(CcKind.Slow, Buf(e)[0].kind);
        }

        [Test]
        public void Merges_Same_Kind_Taking_Max_RemainingTime_And_New_Scalar()
        {
            var e = WithCcBuffer();
            Buf(e).Add(new CcEffect { kind = CcKind.Slow, scalar = 0.5f, remainingTime = 3f });
            Cc(e, new CcEffect { kind = CcKind.Slow, scalar = 0.25f, remainingTime = 1f });
            _sys.Run(_world);

            Assert.AreEqual(1, Buf(e).Count);
            Assert.AreEqual(3f, Buf(e)[0].remainingTime, 1e-5f, "longer existing remaining should win");
            Assert.AreEqual(0.25f, Buf(e)[0].scalar, 1e-5f, "new scalar should overwrite");
        }

        [Test]
        public void Merge_Preserves_Existing_TickTimer_So_Ticks_Keep_Advancing()
        {
            // 가장 취약한 불변식 — incoming.tickTimer(0)로 덮으면 DoT 가 영원히 tick 못 한다.
            var e = WithCcBuffer();
            Buf(e).Add(new CcEffect { kind = CcKind.DoT, scalar = 10f, remainingTime = 0.2f, tickInterval = 0.5f, tickTimer = 0.3f });
            Cc(e, new CcEffect { kind = CcKind.DoT, scalar = 10f, remainingTime = 0.2f, tickInterval = 0.5f, tickTimer = 0f });
            _sys.Run(_world);

            Assert.AreEqual(1, Buf(e).Count);
            Assert.AreEqual(0.3f, Buf(e)[0].tickTimer, 1e-5f, "merge 는 기존 누적기를 보존(incoming 무시)");
            Assert.AreEqual(0.5f, Buf(e)[0].tickInterval, 1e-5f);
        }

        [Test]
        public void Add_New_Slot_Preloads_TickTimer_To_TickInterval_For_Immediate_First_Tick()
        {
            var e = WithCcBuffer();
            Cc(e, new CcEffect { kind = CcKind.DoT, scalar = 10f, remainingTime = 0.2f, tickInterval = 0.5f, tickTimer = 0f });
            _sys.Run(_world);

            Assert.AreEqual(0.5f, Buf(e)[0].tickTimer, 1e-5f, "add-path 는 tickTimer=tickInterval 로 초기화");
        }

        [Test]
        public void Merge_Different_Interval_Rescales_TickTimer_Progress()
        {
            var e = WithCcBuffer();
            Buf(e).Add(new CcEffect { kind = CcKind.DoT, scalar = 10f, remainingTime = 0.2f, tickInterval = 0.5f, tickTimer = 0.3f });
            Cc(e, new CcEffect { kind = CcKind.DoT, scalar = 20f, remainingTime = 0.2f, tickInterval = 1.0f, tickTimer = 0f });
            _sys.Run(_world);

            Assert.AreEqual(1.0f, Buf(e)[0].tickInterval, 1e-5f, "interval 은 incoming 으로 갱신");
            Assert.AreEqual(0.6f, Buf(e)[0].tickTimer, 1e-5f, "진행률 60% 보존: 0.3/0.5*1.0");
            Assert.AreEqual(20f, Buf(e)[0].scalar, 1e-5f, "scalar 는 last-writer");
        }

        [Test]
        public void Merge_Same_Interval_Does_Not_Rescale_Timer()
        {
            var e = WithCcBuffer();
            Buf(e).Add(new CcEffect { kind = CcKind.DoT, scalar = 10f, remainingTime = 0.2f, tickInterval = 0.5f, tickTimer = 0.3f });
            Cc(e, new CcEffect { kind = CcKind.DoT, scalar = 10f, remainingTime = 0.2f, tickInterval = 0.5f, tickTimer = 0f });
            _sys.Run(_world);

            Assert.AreEqual(0.3f, Buf(e)[0].tickTimer, 1e-5f, "동일 interval → 환산 없음");
        }

        [Test]
        public void Different_Kinds_Accumulate_As_Separate_Entries()
        {
            var e = WithCcBuffer();
            Cc(e, new CcEffect { kind = CcKind.Slow, scalar = 0.5f, remainingTime = 2f });
            Cc(e, new CcEffect { kind = CcKind.Impulse, vector = new SimVec3(1, 0, 0), remainingTime = 0.5f });
            _sys.Run(_world);

            Assert.AreEqual(2, Buf(e).Count);
        }

        [Test]
        public void Ignores_Event_When_Target_Was_Destroyed_Before_Apply()
        {
            var e = WithCcBuffer();
            Cc(e, new CcEffect { kind = CcKind.Slow, scalar = 0.5f, remainingTime = 2f });
            _world.Destroy(e);

            Assert.DoesNotThrow(() => _sys.Run(_world));
            Assert.AreEqual(0, _ch.EnemyCc.Count);
        }

        // ── 보스 면역 (BossCcImmunityTests 복제) ──────────────────────────────────

        [Test]
        public void Boss_IsImmuneTo_ActionLocks_AndImpulse_ButNotSlow()
        {
            var boss = WithCcBuffer();
            _world.Set(boss, default(BossTag));

            Cc(boss, new CcEffect { kind = CcKind.Stun, remainingTime = 1f });
            Cc(boss, new CcEffect { kind = CcKind.Sleep, remainingTime = 1f });
            Cc(boss, new CcEffect { kind = CcKind.Impulse, remainingTime = 1f });
            Cc(boss, new CcEffect { kind = CcKind.Slow, scalar = 0.5f, remainingTime = 1f });
            _sys.Run(_world);

            Assert.AreEqual(1, Buf(boss).Count, "Slow 하나만 통과한다.");
            Assert.AreEqual(CcKind.Slow, Buf(boss)[0].kind);
        }

        [Test]
        public void BossImmunity_IsAppliedAtGrantTime_NotAtJudgementTime()
        {
            // 부여 시점 1곳 차단이 계약이다 — 버퍼에 아예 안 들어간다.
            var boss = WithCcBuffer();
            _world.Set(boss, default(BossTag));
            Cc(boss, new CcEffect { kind = CcKind.Stun, remainingTime = 1f });
            _sys.Run(_world);

            Assert.AreEqual(0, Buf(boss).Count);
            Assert.IsFalse(CcActionLock.IsLocked(Buf(boss)));
        }

        [Test]
        public void CcActionLock_LockSet_IsStunAndSleep()
        {
            Assert.IsTrue(CcActionLock.IsLock(CcKind.Stun));
            Assert.IsTrue(CcActionLock.IsLock(CcKind.Sleep));
            Assert.IsFalse(CcActionLock.IsLock(CcKind.Slow));
            Assert.IsFalse(CcActionLock.IsLock(CcKind.Impulse));
            Assert.IsFalse(CcActionLock.IsLock(CcKind.DoT));

            // 면역 = lock ∪ {Impulse} — lock-set 을 조회하므로 새 lock 이 자동 동행한다.
            Assert.IsTrue(CcActionLock.IsBossImmune(CcKind.Impulse));
            Assert.IsFalse(CcActionLock.IsBossImmune(CcKind.Slow));
        }

        [Test]
        public void CcApply_CreatesBufferWhenAbsent_UnlikeDotWhichSkips()
        {
            // 구 sim 의 비대칭: CcApply 는 HasBuffer 를 확인하지 않고(부재면 던진다),
            // DotApply 는 명시적으로 건너뛴다. 신 sim 은 전자를 흡수(생성), 후자를 보존(스킵).
            var e = _world.Create();
            Assert.IsFalse(_world.HasBuffer<CcEffect>(e));
            Cc(e, new CcEffect { kind = CcKind.Slow, scalar = 0.5f, remainingTime = 1f });
            _sys.Run(_world);

            Assert.AreEqual(1, Buf(e).Count, "CC 는 버퍼를 만들어서라도 적용한다.");
        }
    }

    public class SimDotApplyTests
    {
        private SimWorld _world;
        private SimChannels _ch;
        private DotApplySystem _sys;

        [SetUp]
        public void SetUp()
        {
            _world = new SimWorld(new SimConfig(1u, 1u));
            _ch = new SimChannels();
            _sys = new DotApplySystem(_ch.DotApply, _ch.HazardRuntime);
        }

        /// 구 job 이 두 버퍼를 모두 요구한다 — 그 전제를 픽스처가 재현한다.
        private SimEntityId Victim()
        {
            var e = _world.Create();
            _world.AddBuffer<DotEffect>(e);
            _world.AddBuffer<IncomingDamage>(e);
            return e;
        }

        private List<DotEffect> Dots(SimEntityId e) => _world.GetBuffer<DotEffect>(e);
        private List<IncomingDamage> Dmg(SimEntityId e) => _world.GetBuffer<IncomingDamage>(e);

        private void Tick(float dt)
        {
            _world.SetDeltaTime(dt);
            _sys.Run(_world);
        }

        [Test]
        public void Dot_Adds_Damage_Per_Second_Times_DeltaTime()
        {
            var e = Victim();
            Dots(e).Add(new DotEffect { scalar = 10f, remainingTime = 1f });
            Tick(0.25f);

            Assert.AreEqual(1, Dmg(e).Count);
            Assert.AreEqual(2.5f, Dmg(e)[0].amount, 1e-5f);
        }

        [Test]
        public void CcEffects_AreUntouched_ByDotPipeline()
        {
            var e = Victim();
            var cc = _world.AddBuffer<CcEffect>(e);
            cc.Add(new CcEffect { kind = CcKind.Stun, remainingTime = 1f });
            cc.Add(new CcEffect { kind = CcKind.Impulse, remainingTime = 1f });

            Tick(0.25f);

            Assert.AreEqual(0, Dmg(e).Count, "CC 는 피해를 만들지 않는다");
            Assert.AreEqual(2, cc.Count, "CC 슬롯을 건드리지 않는다");
            Assert.AreEqual(1f, cc[0].remainingTime, 1e-5f,
                "CC 감쇠는 CcDecaySystem 몫 — 지속 피해 파이프라인이 깎으면 안 된다");
        }

        [Test]
        public void Multiple_Dot_Entries_All_Contribute_InSlotOrder()
        {
            var e = Victim();
            Dots(e).Add(new DotEffect { scalar = 10f, remainingTime = 1f });
            Dots(e).Add(new DotEffect { scalar = 20f, remainingTime = 1f });
            Tick(0.1f);

            Assert.AreEqual(2, Dmg(e).Count);
            Assert.AreEqual(1f, Dmg(e)[0].amount, 1e-5f);
            Assert.AreEqual(2f, Dmg(e)[1].amount, 1e-5f);
            // 지급이 **정방향**이라는 관측점 — 역순이면 표시 순서가 뒤집힌다.
        }

        [Test]
        public void Tick_Dot_First_Tick_Is_Immediate()
        {
            var e = Victim();
            Dots(e).Add(new DotEffect { scalar = 10f, remainingTime = 0.2f, tickInterval = 0.5f, tickTimer = 0.5f });
            Tick(0.016f);

            Assert.AreEqual(1, Dmg(e).Count, "진입 즉시 1 tick");
            Assert.AreEqual(10f, Dmg(e)[0].amount, 1e-5f, "청크 = scalar(=tick당 데미지), dt 무관");
        }

        [Test]
        public void Tick_Dot_Fires_Once_Per_Interval_Not_Per_Frame()
        {
            var e = Victim();
            Dots(e).Add(new DotEffect { scalar = 10f, remainingTime = 1f, tickInterval = 0.5f, tickTimer = 0f });

            Tick(0.3f);
            Assert.AreEqual(0, Dmg(e).Count, "0.3<0.5 → 0 tick");

            Tick(0.3f);
            Assert.AreEqual(1, Dmg(e).Count, "누적 0.6s → 정확히 1 tick");
            Assert.AreEqual(10f, Dmg(e)[0].amount, 1e-5f);
            Assert.AreEqual(0.1f, Dots(e)[0].tickTimer, 1e-5f, "잔여 누적 보존");
        }

        [Test]
        public void Tick_Dot_Large_Dt_Fires_Multiple_Chunks()
        {
            var e = Victim();
            Dots(e).Add(new DotEffect { scalar = 10f, remainingTime = 5f, tickInterval = 0.5f, tickTimer = 0f });
            Tick(1.2f);

            Assert.AreEqual(2, Dmg(e).Count, "1.2/0.5 = 2 청크");
            Assert.AreEqual(10f, Dmg(e)[0].amount, 1e-5f);
            Assert.AreEqual(10f, Dmg(e)[1].amount, 1e-5f);
            Assert.AreEqual(0.2f, Dots(e)[0].tickTimer, 1e-5f);
        }

        [Test]
        public void Continuous_Dot_Unchanged_When_TickInterval_Zero()
        {
            var e = Victim();
            Dots(e).Add(new DotEffect { scalar = 20f, remainingTime = 1f, tickInterval = 0f });
            Tick(0.1f);

            Assert.AreEqual(1, Dmg(e).Count);
            Assert.AreEqual(2f, Dmg(e)[0].amount, 1e-5f, "20 DPS * 0.1s");
        }

        [Test]
        public void TickIsPaid_BeforeRemainingTimeIsDecremented()
        {
            // 순서 계약 — 만료되는 프레임에도 이번 몫은 받고 나서 사라진다.
            // dt 는 주기 하나만 넘기게 잡는다(크게 잡으면 청크가 여러 개 나와 순서가 아니라
            // 개수를 재게 된다 — 초판이 dt=1.0 으로 3청크를 세고 있었다).
            var e = Victim();
            Dots(e).Add(new DotEffect { scalar = 10f, remainingTime = 0.01f, tickInterval = 0.5f, tickTimer = 0.5f });
            Tick(0.05f);

            Assert.AreEqual(1, Dmg(e).Count, "만료되는 프레임에도 지급이 먼저다.");
            Assert.AreEqual(10f, Dmg(e)[0].amount, 1e-5f);
            Assert.AreEqual(0, Dots(e).Count, "그 뒤 만료 제거.");
        }

        // ── 병합 키 (origin, element) 2축 ─────────────────────────────────────────

        [Test]
        public void Merge_SplitsOnOrigin_And_OnElement()
        {
            var e = Victim();
            void Send(DotOrigin o, DotElement el, float scalar)
                => _ch.DotApply.Enqueue(new DotApplyEvent
                {
                    target = e,
                    effect = new DotEffect { origin = o, element = el, scalar = scalar, remainingTime = 10f },
                });

            Send(DotOrigin.Stack, DotElement.Bleed, 1f);
            Send(DotOrigin.Zone, DotElement.Fire, 2f);
            Send(DotOrigin.Stack, DotElement.Fire, 3f);   // 같은 원소·다른 파이프라인
            Tick(0f);

            Assert.AreEqual(3, Dots(e).Count,
                "origin·element 중 하나만 달라도 슬롯이 갈린다 — 장판 화염이 스택 화염을 덮으면 과피해.");
        }

        [Test]
        public void Merge_SameKey_KeepsMaxRemaining_AndTakesNewScalar()
        {
            var e = Victim();
            Dots(e).Add(new DotEffect { origin = DotOrigin.Zone, element = DotElement.Fire, scalar = 5f, remainingTime = 9f });
            _ch.DotApply.Enqueue(new DotApplyEvent
            {
                target = e,
                effect = new DotEffect { origin = DotOrigin.Zone, element = DotElement.Fire, scalar = 7f, remainingTime = 2f },
            });
            Tick(0f);

            Assert.AreEqual(1, Dots(e).Count);
            Assert.AreEqual(9f, Dots(e)[0].remainingTime, 1e-5f);
            Assert.AreEqual(7f, Dots(e)[0].scalar, 1e-5f);
        }

        // ── 버퍼 부재 게이트 ──────────────────────────────────────────────────────

        [Test]
        public void Apply_SkipsTarget_WithoutDotBuffer()
        {
            var e = _world.Create();
            _world.AddBuffer<IncomingDamage>(e);
            _ch.DotApply.Enqueue(new DotApplyEvent
            {
                target = e,
                effect = new DotEffect { origin = DotOrigin.Stack, element = DotElement.Bleed, scalar = 5f, remainingTime = 1f },
            });
            Tick(0.1f);

            Assert.IsFalse(_world.HasBuffer<DotEffect>(e), "DoT 는 버퍼를 만들지 않는다(CC 와 비대칭).");
            Assert.AreEqual(0, Dmg(e).Count);
        }

        [Test]
        public void Tick_SkipsEntity_WithoutIncomingDamageBuffer()
        {
            var e = _world.Create();
            _world.AddBuffer<DotEffect>(e).Add(new DotEffect { scalar = 10f, remainingTime = 5f });
            Tick(1f);

            Assert.AreEqual(5f, _world.GetBuffer<DotEffect>(e)[0].remainingTime, 1e-5f,
                "피해 버퍼가 없으면 도트가 **틱조차 하지 않는다**(구 job 이 두 버퍼를 모두 요구).");
        }

        [Test]
        public void RuntimeLog_IsEmitted_ForEveryChunk()
        {
            var e = Victim();
            Dots(e).Add(new DotEffect { scalar = 10f, remainingTime = 5f, tickInterval = 0.5f, tickTimer = 0f });
            Tick(1.2f);

            Assert.AreEqual(2, _ch.HazardRuntime.Count, "청크마다 로그 1건(구 sim 의 with-events 변형).");
            var log = _ch.HazardRuntime.Drain()[0];
            Assert.AreEqual(HazardRuntimeEventType.DotDamage, log.eventType);
            Assert.AreEqual(CcKind.DoT, log.kind, "로그 태그로만 남은 값.");
            Assert.AreEqual(e, log.target);
        }
    }

    public class SimCcDecayAndClearTests
    {
        private SimWorld _world;
        private SimChannels _ch;

        [SetUp]
        public void SetUp()
        {
            _world = new SimWorld(new SimConfig(1u, 1u));
            _ch = new SimChannels();
        }

        [Test]
        public void Decay_RemovesExpired_AndKeepsTheRest()
        {
            var sys = new CcDecaySystem();
            var e = _world.Create();
            var buf = _world.AddBuffer<CcEffect>(e);
            buf.Add(new CcEffect { kind = CcKind.Slow, remainingTime = 0.5f });
            buf.Add(new CcEffect { kind = CcKind.Stun, remainingTime = 5f });

            _world.SetDeltaTime(1f);
            sys.Run(_world);

            Assert.AreEqual(1, buf.Count);
            Assert.AreEqual(CcKind.Stun, buf[0].kind);
            Assert.AreEqual(4f, buf[0].remainingTime, 1e-5f);
        }

        [Test]
        public void Clear_RemovesOnlyRequestedKind()
        {
            var sys = new CcClearSystem(_ch.CcClear);
            var e = _world.Create();
            var buf = _world.AddBuffer<CcEffect>(e);
            buf.Add(new CcEffect { kind = CcKind.Sleep, remainingTime = 99f });
            buf.Add(new CcEffect { kind = CcKind.Slow, remainingTime = 99f });

            _ch.CcClear.Enqueue(new CcClearRequest { entity = e, kind = CcKind.Sleep });
            sys.Run(_world);

            Assert.AreEqual(1, buf.Count, "wake-on-hit 은 그 kind 만 벗긴다.");
            Assert.AreEqual(CcKind.Slow, buf[0].kind);
        }

        [Test]
        public void Clear_IgnoresDestroyedTarget()
        {
            var sys = new CcClearSystem(_ch.CcClear);
            var e = _world.Create();
            _world.AddBuffer<CcEffect>(e);
            _ch.CcClear.Enqueue(new CcClearRequest { entity = e, kind = CcKind.Sleep });
            _world.Destroy(e);   // 치명타로 죽었을 수 있다

            Assert.DoesNotThrow(() => sys.Run(_world));
            Assert.AreEqual(0, _ch.CcClear.Count);
        }
    }

    public class SimDotTickTests
    {
        [Test]
        public void Advance_ReturnsZero_AndLeavesTimer_WhenIntervalNonPositive()
        {
            float timer = 3f;
            Assert.AreEqual(0, DotTick.Advance(ref timer, 0f, 1f));
            Assert.AreEqual(3f, timer, 1e-5f, "연속 DoT 전제 — timer 불변.");
        }

        [Test]
        public void Advance_CountsChunks_AndCarriesRemainder()
        {
            float timer = 0f;
            Assert.AreEqual(2, DotTick.Advance(ref timer, 0.5f, 1.2f));
            Assert.AreEqual(0.2f, timer, 1e-5f);
        }

        [Test]
        public void Advance_IsBoundedBy_MaxTicksPerFrame()
        {
            float timer = 0f;
            int ticks = DotTick.Advance(ref timer, 1e-6f, 1000f);
            Assert.AreEqual(DotTick.MaxTicksPerFrame, ticks, "무한 루프 방어 상한.");
        }
    }
}
