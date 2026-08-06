using System.Linq;
using NUnit.Framework;
using Wassup.Sim;
using Wassup.Sim.Combat;
using Wassup.Sim.Effects;
using Wassup.Sim.Movement;
using Wassup.Sim.Units;

namespace Wassup.Tests.EditMode
{
    /// <summary>
    /// battle-sim-extraction unit 18-J/1 — 캡처 #20(사직서 임계) · #24(피격 플래시) · #25(캐리어 수명).
    ///
    /// 셋 다 **자기 상태만** 굴리는 시스템이라 오라클이 좁다. 지키는 것:
    /// **① 임계는 소모하고 재누적한다**(래치가 아니다) **② 캐리어는 만료하면 엔티티째 사라진다**
    /// **③ 플래시는 반드시 원본 스케일로 복원한 뒤 태그를 뗀다**.
    /// </summary>
    public class SimGimmickTickTests
    {
        private SimWorld _world;
        private SimChannels _channels;

        private static SimConfig ConfigWithClockOut(byte threshold = 3, byte meteorCount = 5)
            => new SimConfig(1u, 1u, clockOut: new ClockOutConfig(
                resignationThreshold: threshold, meteorCount: meteorCount, meteorDamage: 10f,
                meteorTileRange: 2, meteorWarningSec: 1f, meteorStaggerSec: 0.2f));

        [SetUp]
        public void SetUp()
        {
            _channels = new SimChannels();
            _world = new SimWorld(ConfigWithClockOut());
            _world.SetDeltaTime(0.1f);
        }

        private SimEntityId Resignation()
        {
            var e = _world.Create();
            _world.Set(e, new Resignation());
            return e;
        }

        private int AliveWith<T>() where T : struct
        {
            int n = 0;
            foreach (var _ in _world.With<T>()) n++;
            return n;
        }

        // ── #20 사직서 임계 ───────────────────────────────────────────────────

        [Test]
        public void Threshold_ConsumesExactlyTheThreshold_AndRequestsOneBarrage()
        {
            var sut = new ResignationThresholdSystem(_channels);
            for (int i = 0; i < 4; i++) Resignation();

            sut.Run(_world);

            Assert.AreEqual(1, AliveWith<Resignation>(), "임계 3 을 소모하고 1 장이 남는다");
            var reqs = _channels.MeteorBarrageRequest.Drain();
            Assert.AreEqual(1, reqs.Count);
            Assert.AreEqual(5, reqs[0].meteorCount);
        }

        [Test]
        public void Threshold_BelowThreshold_DoesNothing()
        {
            var sut = new ResignationThresholdSystem(_channels);
            for (int i = 0; i < 2; i++) Resignation();

            sut.Run(_world);

            Assert.AreEqual(2, AliveWith<Resignation>());
            Assert.AreEqual(0, _channels.MeteorBarrageRequest.Count);
        }

        [Test]
        public void Threshold_MultipleCrossingsInOneFrame_FireThatManyBarrages()
        {
            var sut = new ResignationThresholdSystem(_channels);
            for (int i = 0; i < 7; i++) Resignation();

            sut.Run(_world);

            Assert.AreEqual(1, AliveWith<Resignation>(), "7 / 3 = 2 회분 6 장 소모");
            Assert.AreEqual(2, _channels.MeteorBarrageRequest.Count);
        }

        [Test]
        public void Threshold_ConsumesTheOldestFirst()
        {
            // ⚠ 파괴 순서가 순회 순서(= 생성 순서)다 — "가장 오래된 사직서부터".
            var sut = new ResignationThresholdSystem(_channels);
            var first = Resignation();
            Resignation();
            Resignation();
            var last = Resignation();

            sut.Run(_world);

            Assert.IsFalse(_world.Exists(first));
            Assert.IsTrue(_world.Exists(last), "가장 나중 것이 남는다");
        }

        [Test]
        public void Threshold_IsInert_WhenTheGimmickIsOff()
        {
            // 구 `RequireForUpdate<ClockOutGimmickConfig>`(분류 B)가 저작면으로 이사한 자리.
            _world = new SimWorld(new SimConfig(1u, 1u));
            var sut = new ResignationThresholdSystem(_channels);
            for (int i = 0; i < 9; i++) Resignation();

            sut.Run(_world);

            Assert.AreEqual(9, AliveWith<Resignation>());
            Assert.AreEqual(0, _channels.MeteorBarrageRequest.Count);
        }

        [Test]
        public void Threshold_Zero_IsRejected_NotAnInfiniteTrigger()
        {
            _world = new SimWorld(ConfigWithClockOut(threshold: 0));
            var sut = new ResignationThresholdSystem(_channels);
            Resignation();

            sut.Run(_world);

            Assert.AreEqual(1, AliveWith<Resignation>(), "0 이면 매 프레임 무한 트리거가 된다");
            Assert.AreEqual(0, _channels.MeteorBarrageRequest.Count);
        }

        // ── #25 캐리어 수명 ───────────────────────────────────────────────────

        [Test]
        public void Carriers_TickDown_AndAreDestroyedOnExpiry()
        {
            var sut = new EffectTickSystem();
            var tornado = _world.Create();
            _world.Set(tornado, new TornadoField { remaining = 0.25f, tileRange = 2, pullSpeed = 1f });
            var portal = _world.Create();
            _world.Set(portal, new PortalLink { remaining = 0.05f, entryRadius = 1f });
            var buff = _world.Create();
            _world.Set(buff, new AllyBuffField { remaining = 0.05f });

            sut.Run(_world); // dt 0.1

            Assert.IsTrue(_world.Exists(tornado));
            Assert.AreEqual(0.15f, _world.Get<TornadoField>(tornado).remaining, 1e-4f);
            Assert.IsFalse(_world.Exists(portal), "만료한 캐리어는 엔티티째 사라진다");
            Assert.IsFalse(_world.Exists(buff));
        }

        [Test]
        public void Carrier_ExpiresExactlyAtZero()
        {
            var sut = new EffectTickSystem();
            var tornado = _world.Create();
            _world.Set(tornado, new TornadoField { remaining = 0.1f });

            sut.Run(_world);

            Assert.IsFalse(_world.Exists(tornado), "가드가 `<= 0` 이라 정확히 0 이면 만료다");
        }

        [Test]
        public void Carriers_OfOtherKinds_AreUntouched()
        {
            // 세 타입만 이 시스템의 소관이다.
            var sut = new EffectTickSystem();
            var hazard = _world.Create();
            _world.Set(hazard, new HitFlashTag { remaining = 0.05f, duration = 1f, originalScale = 1f });

            sut.Run(_world);

            Assert.IsTrue(_world.Exists(hazard));
            Assert.AreEqual(0.05f, _world.Get<HitFlashTag>(hazard).remaining, 1e-4f);
        }

        // ── #24 피격 플래시 ───────────────────────────────────────────────────

        [Test]
        public void Flash_ScalesUp_ProportionallyToTheRemainingFraction()
        {
            var sut = new HitFlashSystem();
            var e = _world.Create();
            _world.Set(e, SimTransform.FromPosition(default));
            _world.Set(e, new HitFlashTag { remaining = 0.5f, duration = 0.5f, originalScale = 2f });

            sut.Run(_world); // remaining 0.4 / duration 0.5 = t 0.8

            Assert.AreEqual(2f * (1f + 0.2f * 0.8f), _world.Get<SimTransform>(e).Scale, 1e-4f);
            Assert.IsTrue(_world.Has<HitFlashTag>(e));
        }

        [Test]
        public void Flash_RestoresTheOriginalScale_BeforeRemovingTheTag()
        {
            // ⚠ 복원 없이 떼면 유닛이 부푼 채로 남는다.
            var sut = new HitFlashSystem();
            var e = _world.Create();
            _world.Set(e, SimTransform.FromPosition(default));
            _world.Set(e, new HitFlashTag { remaining = 0.05f, duration = 0.5f, originalScale = 2f });

            sut.Run(_world);

            Assert.AreEqual(2f, _world.Get<SimTransform>(e).Scale, 1e-4f);
            Assert.IsFalse(_world.Has<HitFlashTag>(e));
        }

        [Test]
        public void Flash_WithZeroDuration_RestoresImmediately()
        {
            // 저작 오류 방어 — 0 나눗셈을 만들지 않고 그 프레임에 정리한다.
            var sut = new HitFlashSystem();
            var e = _world.Create();
            _world.Set(e, SimTransform.FromPosition(default));
            _world.Set(e, new HitFlashTag { remaining = 5f, duration = 0f, originalScale = 1.5f });

            sut.Run(_world);

            Assert.AreEqual(1.5f, _world.Get<SimTransform>(e).Scale, 1e-4f);
            Assert.IsFalse(_world.Has<HitFlashTag>(e));
        }

        [Test]
        public void Flash_WithoutATransform_IsSkipped_WithoutThrowing()
        {
            var sut = new HitFlashSystem();
            var e = _world.Create();
            _world.Set(e, new HitFlashTag { remaining = 0.05f, duration = 0.5f, originalScale = 1f });

            Assert.DoesNotThrow(() => sut.Run(_world));
            Assert.IsTrue(_world.Has<HitFlashTag>(e), "위치가 없으면 손대지 않는다");
        }

        // ── 클러스터 등록 ─────────────────────────────────────────────────────

        [Test]
        public void Cluster_DeclaresItsStepsWithMatchingPhases()
        {
            // ⚠ 이 클러스터는 조각마다 자란다 — **포함**만 본다. {1..44} 전수 단정은 18-K 몫이다.
            var steps = new GimmickCluster(new SimChannels()).Steps().ToList();

            CollectionAssert.IsSubsetOf(new[] { 20, 24, 25 }, steps.Select(s => s.Order).ToArray());
            foreach (var s in steps)
                Assert.AreEqual(SimPipeline.PhaseForOrder(s.Order), s.Phase,
                    $"#{s.Order}({s.Name}) 의 phase 가 캡처 번호 구간과 어긋난다");
        }

        [Test]
        public void Cluster_DoesNotCollideWithTheOtherClusters()
        {
            // `SimPipeline` 은 번호 중복을 던진다 — 조립이 늘어날 때 그 검사가 실제로 통과하는지 본다.
            var ch = new SimChannels();
            Assert.DoesNotThrow(() => new SimPipeline()
                .Add(new GimmickCluster(ch).Steps())
                .Add(new AttackCluster(ch).Steps())
                .Add(new ModifierCluster(ch).Steps())
                .Add(new EnvironmentCluster(ch).Steps())
                .Add(new MovementCluster(ch).Steps())
                .Add(new DamageCluster(ch).Steps())
                .Add(new ProjectileCluster(ch).Steps())
                .Build());
        }
    }
}
