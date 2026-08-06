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
    /// battle-sim-extraction unit 18-J/3 — 캡처 #39(호접몽) · #43(궁극기 도약).
    ///
    /// 둘 다 **시퀀스를 sim 이 소유하는** 이유가 핵심이다: 호접몽은 파탄/완주가 규칙이고,
    /// 도약의 예고 창은 회피 창이자 피해 게이트다.
    /// </summary>
    public class SimCocoonAndLeapTests
    {
        private SimWorld _world;
        private SimChannels _channels;

        [SetUp]
        public void SetUp()
        {
            _world = new SimWorld(new SimConfig(1u, 1u));
            _channels = new SimChannels();
            _world.SetDeltaTime(0.1f);
        }

        // ── #39 호접몽 ────────────────────────────────────────────────────────

        private SimEntityId Sleeper(float remaining, bool asleep = true, float mult = 1.35f)
        {
            var e = _world.Create();
            _world.Set(e, new DreamCocoon
            {
                remaining = remaining, stat = StatKind.DamageMul, mult = mult, stackId = 42,
            });
            var cc = _world.AddBuffer<CcEffect>(e);
            if (asleep) cc.Add(new CcEffect { kind = CcKind.Sleep, remainingTime = 9f });
            return e;
        }

        [Test]
        public void Cocoon_TicksDownWhileAsleep()
        {
            var e = Sleeper(1f);

            new DreamCocoonSystem(_channels).Run(_world);

            Assert.AreEqual(0.9f, _world.Get<DreamCocoon>(e).remaining, 1e-4f);
            Assert.AreEqual(0, _channels.StatApply.Count, "아직 완주가 아니다");
        }

        [Test]
        public void Cocoon_Completes_GrantsAPermanentSelfBuff()
        {
            var e = Sleeper(0.05f);

            new DreamCocoonSystem(_channels).Run(_world);

            var mods = _channels.StatApply.Drain();
            Assert.AreEqual(1, mods.Count);
            Assert.AreEqual(e, mods[0].target);
            Assert.AreEqual(e, mods[0].source, "self 버프");
            Assert.AreEqual(StatKind.DamageMul, mods[0].stat);
            Assert.AreEqual(float.PositiveInfinity, mods[0].duration, "완주 버프는 영구다");
            Assert.AreEqual(42, mods[0].stackId);
            Assert.AreEqual(ModifierOrigin.Dreamcatcher, mods[0].origin);
            Assert.IsFalse(_world.Has<DreamCocoon>(e), "완주 후 감시자는 사라진다");
        }

        [Test]
        public void Cocoon_UsesFromMultiplier_SoPercentGoesToTheAdditiveBucket()
        {
            var e = Sleeper(0.05f, mult: 1.35f);

            new DreamCocoonSystem(_channels).Run(_world);

            SimModifierAuthoring.FromMultiplier(1.35f, out var op, out float mag);
            var mods = _channels.StatApply.Drain();
            Assert.AreEqual(op, mods[0].op);
            Assert.AreEqual(mag, mods[0].magnitude, 1e-4f);
        }

        [Test]
        public void Cocoon_Shatters_WhenSleepIsGoneBeforeCompletion()
        {
            // ⚠ 파탄 = 피격 wake. **버프 없이** 감시자만 사라진다.
            var e = Sleeper(1f, asleep: false);

            new DreamCocoonSystem(_channels).Run(_world);

            Assert.IsFalse(_world.Has<DreamCocoon>(e));
            Assert.AreEqual(0, _channels.StatApply.Count, "파탄에는 보상이 없다");
        }

        [Test]
        public void Cocoon_SimultaneousHitAndExpiry_CountsAsShatter()
        {
            // ⚠ ①파탄 체크가 ②감산보다 **먼저**다 — `remaining > 0` 가드가 그 disambiguator 다.
            var e = Sleeper(0.05f, asleep: false); // 이번 틱에 만료할 값 + 잠 없음

            new DreamCocoonSystem(_channels).Run(_world);

            Assert.IsFalse(_world.Has<DreamCocoon>(e));
            Assert.AreEqual(0, _channels.StatApply.Count, "동시면 파탄이 이긴다");
        }

        [Test]
        public void Cocoon_SkipsDeadAndBufferlessEntities()
        {
            var dead = Sleeper(1f);
            _world.Set(dead, new DeadTag());
            var noBuffer = _world.Create();
            _world.Set(noBuffer, new DreamCocoon { remaining = 1f, mult = 1.2f });

            new DreamCocoonSystem(_channels).Run(_world);

            Assert.AreEqual(1f, _world.Get<DreamCocoon>(dead).remaining, 1e-4f, "죽으면 참여하지 않는다");
            Assert.AreEqual(1f, _world.Get<DreamCocoon>(noBuffer).remaining, 1e-4f,
                "구 쿼리가 `CcEffect` 버퍼를 요구한다");
        }

        // ── #43 궁극기 도약 ───────────────────────────────────────────────────

        private SimEntityId Leaper(float remaining, float slamDamage = 50f, int slamRange = 2)
        {
            var e = _world.Create();
            _world.Set(e, SimTransform.FromPosition(new SimVec3(9f, 0f, 9f)));
            _world.Set(e, new LeapFlight());
            _world.Set(e, new UltimateLeapState
            {
                remaining = remaining,
                landingCell = new SimInt2(3, 4),
                landingWorld = new SimVec3(3f, 0f, 4f),
                slamDamage = slamDamage,
                slamTileRange = slamRange,
                projectileDataIndex = 7,
            });
            return e;
        }

        private int CarrierCount()
        {
            int n = 0;
            foreach (var _ in _world.With<ProjectileRequestCarrier>()) n++;
            return n;
        }

        [Test]
        public void Leap_CountsDown_WithoutLandingEarly()
        {
            var e = Leaper(1f);

            new UltimateLeapSystem(_channels).Run(_world);

            Assert.AreEqual(0.9f, _world.Get<UltimateLeapState>(e).remaining, 1e-4f);
            Assert.AreEqual(0, _channels.BlinkRequest.Count, "예고 창은 회피 창이다 — 아직 착지 아님");
            Assert.IsTrue(_world.Has<LeapFlight>(e));
        }

        [Test]
        public void Leap_Lands_InFourSteps()
        {
            var e = Leaper(0.05f);

            new UltimateLeapSystem(_channels).Run(_world);

            // 1. 텔레포트 요청
            var blinks = _channels.BlinkRequest.Drain();
            Assert.AreEqual(1, blinks.Count);
            Assert.AreEqual(e, blinks[0].entity);
            Assert.AreEqual(new SimVec3(3f, 0f, 4f), blinks[0].destWorld);

            // 2. 슬램 캐리어
            Assert.AreEqual(1, CarrierCount());
            foreach (var c in _world.With<ProjectileRequestCarrier>())
            {
                var req = _world.Get<ProjectileSpawnRequest>(c);
                Assert.AreEqual(MovementKind.SkyFall, req.movement);
                Assert.AreEqual(PayloadKind.TileAoe, req.payload);
                Assert.AreEqual(50f, req.damage, 1e-4f, "shooter 스냅샷 없이 고정 피해");
                Assert.AreEqual(2, req.impactTileRange);
                Assert.AreEqual(0f, req.flightTime, 1e-4f, "즉발 — 예고가 이미 창을 벌었다");
                Assert.AreEqual(7, req.dataIndex);
                Assert.AreEqual(e, req.owner);
                Assert.AreEqual(ProjectileTargetFaction.Defender, req.targetFaction);
            }

            // 3. 강하 연출
            var vis = _channels.UltimateLeapVisual.Drain();
            Assert.AreEqual(1, vis.Count);
            Assert.AreEqual(UltimateLeapVisualKind.Descend, vis[0].kind);
            Assert.AreEqual(new SimVec3(3f, 0f, 4f), vis[0].world);
            Assert.AreEqual(7, vis[0].dataIndex);

            // 4. 상태 해제 — 무적과 잠금이 **함께** 떨어진다
            Assert.IsFalse(_world.Has<UltimateLeapState>(e));
            Assert.IsFalse(_world.Has<LeapFlight>(e), "붙을 때와 대칭");
        }

        [Test]
        public void Leap_WithoutSlam_StillTeleportsAndReleases()
        {
            var e = Leaper(0.05f, slamDamage: 0f);

            new UltimateLeapSystem(_channels).Run(_world);

            Assert.AreEqual(0, CarrierCount(), "피해 0 이면 슬램 캐리어가 없다");
            Assert.AreEqual(1, _channels.BlinkRequest.Count);
            Assert.IsFalse(_world.Has<UltimateLeapState>(e));
        }

        [Test]
        public void Leap_DeadMidAir_ReleasesTheStateWithoutLanding()
        {
            // ⚠ 방어적 가드 — 정상 경로엔 없다. 시체가 잠긴 채 남지 않게 한다.
            var e = Leaper(0.05f);
            _world.Set(e, new DeadTag());

            new UltimateLeapSystem(_channels).Run(_world);

            Assert.AreEqual(0, _channels.BlinkRequest.Count, "착지하지 않는다");
            Assert.AreEqual(0, CarrierCount());
        }

        [Test]
        public void Leap_LandsInTheSameTickAsBlinkApply()
        {
            // ⚠ #43 → #44 가 같은 phase 안에서 앞뒤 — 요청이 그 틱에 적용된다.
            var steps = new GimmickCluster(new SimChannels()).Steps().ToList();
            var leap = steps.Single(s => s.Order == 43);
            Assert.AreEqual(SimPhase.Destruction, leap.Phase);
            Assert.AreEqual(SimPipeline.PhaseForOrder(44), leap.Phase, "#44 와 같은 phase 여야 한다");
        }

        [Test]
        public void Cocoon_SitsBetweenCcClearAndCcDecay()
        {
            // ⚠ 그 사이가 아니면 자연만료를 피격 파탄으로 오인한다.
            var steps = new GimmickCluster(new SimChannels()).Steps().ToList();
            var cocoon = steps.Single(s => s.Order == 39);
            Assert.AreEqual(SimPhase.PostProcess, cocoon.Phase);
            Assert.Greater(cocoon.Order, 37, "#37 CcClear 뒤");
            Assert.Less(cocoon.Order, 40, "#40 CcDecay 앞");
        }
    }
}
