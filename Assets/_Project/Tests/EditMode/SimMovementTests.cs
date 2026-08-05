// battle-sim-extraction unit 18-F/4 — #17 Movement · #44 BlinkApply 이식 핀 + 클러스터 조립.
// 구 오라클 `MovementSystemTests` 는 unit 20 까지 계속 진다. 여기서는 이식이 실제로 갈리는
// 골격을 박는다: locked 의 계산 시점 · 외력/자기주도의 분리 · 방향 소스 4개 · 골 누수 게이트.
using System.Collections.Generic;
using NUnit.Framework;
using Wassup.Sim;
using Wassup.Sim.Combat;
using Wassup.Sim.Effects;
using Wassup.Sim.Movement;
using Wassup.Sim.Units;

namespace Wassup.Tests.EditMode
{
    public class SimMovementSystemTests
    {
        private static readonly SimInt2 Grid = new SimInt2(12, 12);
        private static readonly SimInt2 Goal = new SimInt2(11, 6);

        private SimWorld _world;
        private MovementSystem _sys;
        private FlowFieldSingleton _field;

        [SetUp]
        public void SetUp()
        {
            _world = new SimWorld(new SimConfig(1u, 1u));
            _sys = new MovementSystem();

            int n = Grid.x * Grid.y;
            _field = new FlowFieldSingleton
            {
                flow = new SimVec2[n], dist = new int[n],
                gridSize = Grid, tileSize = 1f, origin = SimVec3.Zero, goalCell = Goal,
            };
            var mask = new byte[n];
            for (int i = 0; i < n; i++) mask[i] = 1;
            FlowFieldBuilder.BuildFromSources(mask, Grid, new[] { Goal }, 1, _field.flow, _field.dist);
            _world.Set(_world.Create(), _field);
        }

        private SimEntityId Mover(SimInt2 cell, float speed = 1f)
        {
            var e = _world.Create();
            _world.Set(e, new PathFollowState { speed = speed });
            _world.Set(e, SimTransform.FromPosition(new SimVec3(cell.x, 0f, cell.y)));
            return e;
        }

        private SimVec3 Pos(SimEntityId e) => _world.Get<SimTransform>(e).Position;

        private void Tick(float dt = 1f)
        {
            _world.SetDeltaTime(dt);
            _sys.Run(_world);
        }

        // ── 방향 소스: goal flow ──────────────────────────────────────────────

        [Test]
        public void Marching_FollowsGoalFlow()
        {
            var e = Mover(new SimInt2(2, 6));
            Tick(0.5f);
            Assert.Greater(Pos(e).x, 2f, "골(+x) 방향으로 전진.");
        }

        [Test]
        public void ReachingGoal_TagsPastGoal_AndStopsMoving()
        {
            var e = Mover(Goal);
            Tick();
            Assert.IsTrue(_world.Has<PastGoalTag>(e), "골 셀 도달 = 누수 대기.");

            SimVec3 before = Pos(e);
            Tick();
            Assert.AreEqual(before.x, Pos(e).x, 1e-5f, "태그가 붙으면 이동 루프에서 빠진다.");
        }

        [Test]
        public void PatrollingUnit_DoesNotLeakAtGoal()
        {
            // 거점 박스 안에 골이 들어올 수 있다(맵은 매 판 랜덤). 태그가 붙으면 순찰병이
            // 영구 동결되고 #41 은 AttackUnitTag 를 요구해 파괴도 안 되며, 소환사가 남은 판
            // 내내 재소환하지 못한다.
            var e = Mover(Goal);
            _world.Set(e, new PatrolStep { dir = SimVec2.Zero });
            Tick();
            Assert.IsFalse(_world.Has<PastGoalTag>(e), "순찰병은 골에서 누수하지 않는다.");
        }

        // ── locked: 자기주도만 정지, 외력은 유지 ─────────────────────────────

        [Test]
        public void Stunned_StopsSelfDrivenMovement()
        {
            var e = Mover(new SimInt2(2, 6));
            _world.AddBuffer<CcEffect>(e).Add(new CcEffect { kind = CcKind.Stun, remainingTime = 5f });
            SimVec3 before = Pos(e);
            Tick(0.5f);
            Assert.AreEqual(before.x, Pos(e).x, 1e-5f, "잠금 중 flow-step 은 0.");
        }

        [Test]
        public void Stunned_StillTakesImpulse()
        {
            var e = Mover(new SimInt2(2, 6));
            var cc = _world.AddBuffer<CcEffect>(e);
            cc.Add(new CcEffect { kind = CcKind.Stun, remainingTime = 5f });
            cc.Add(new CcEffect { kind = CcKind.Impulse, vector = new SimVec3(0, 0, 0.5f), remainingTime = 5f });
            Tick(0.5f);
            Assert.Greater(Pos(e).z, 6f, "외력(넉백)은 잠금과 무관하게 먹는다.");
        }

        [Test]
        public void LeapFlight_LocksLikeCc()
        {
            var e = Mover(new SimInt2(2, 6));
            _world.Set(e, default(LeapFlight));
            SimVec3 before = Pos(e);
            Tick(0.5f);
            Assert.AreEqual(before.x, Pos(e).x, 1e-5f, "도약 비행 중도 자기주도 이동 정지.");
        }

        // ── FSM 상태 ─────────────────────────────────────────────────────────

        [Test]
        public void Standoff_IsAFullStop()
        {
            var e = Mover(new SimInt2(2, 6));
            _world.Set(e, new EnemyAiState { value = AiState.Standoff });
            SimVec3 before = Pos(e);
            Tick();
            Assert.AreEqual(before.x, Pos(e).x, 1e-5f);
        }

        [Test]
        public void EngagingHalt_StopsButStillTakesTornadoPull()
        {
            var e = Mover(new SimInt2(2, 6));
            _world.Set(e, new EnemyAiState { value = AiState.Engaging });
            _world.Set(e, new EnemyBehavior { engageMovement = EngageMovement.Halt });
            var t = _world.Create();
            _world.Set(t, new TornadoField
            { centerWorld = new SimVec3(2, 0, 9), tileRange = 5, pullSpeed = 1f, remaining = 9f });

            Tick(0.5f);
            Assert.Greater(Pos(e).z, 6f, "정지 중에도 외력은 당긴다(기존 거동 보존).");
            Assert.AreEqual(2f, Pos(e).x, 1e-3f, "자기주도 전진은 없다.");
        }

        [Test]
        public void EngagingAdvance_KeepsWalking()
        {
            var e = Mover(new SimInt2(2, 6));
            _world.Set(e, new EnemyAiState { value = AiState.Engaging });
            _world.Set(e, new EnemyBehavior { engageMovement = EngageMovement.Advance });
            Tick(0.5f);
            Assert.Greater(Pos(e).x, 2f);
        }

        [Test]
        public void EngagingPulse_StopsOnlyWhileTheSwingIsResolving()
        {
            var e = Mover(new SimInt2(2, 6));
            _world.Set(e, new EnemyAiState { value = AiState.Engaging });
            _world.Set(e, new EnemyBehavior { engageMovement = EngageMovement.Pulse });
            _world.Set(e, new AttackState { hitDelayRemaining = 0.2f });

            SimVec3 before = Pos(e);
            Tick(0.5f);
            Assert.AreEqual(before.x, Pos(e).x, 1e-5f, "타격 진행 중 = 정지.");

            _world.Set(e, new AttackState { hitDelayRemaining = 0f });
            Tick(0.5f);
            Assert.Greater(Pos(e).x, before.x, "타격이 끝나면 전진(진동).");
        }

        // ── 포털 · 토네이도 ──────────────────────────────────────────────────

        [Test]
        public void Portal_TeleportsAndKeepsY()
        {
            var e = Mover(new SimInt2(2, 6));
            _world.Set(e, SimTransform.FromPosition(new SimVec3(2f, 3.5f, 6f)));
            var p = _world.Create();
            _world.Set(p, new PortalLink
            {
                entryWorld = new SimVec3(2, 0, 6), exitWorld = new SimVec3(9, 0, 6),
                entryRadius = 0.5f, remaining = 9f,
            });

            Tick(0.016f);
            Assert.Greater(Pos(e).x, 8f, "출구로 텔레포트.");
            Assert.AreEqual(3.5f, Pos(e).y, 1e-5f, "y 는 이동체 것을 유지한다.");
        }

        [Test]
        public void Tornado_OutsideRange_DoesNotPull()
        {
            var e = Mover(new SimInt2(2, 6));
            var t = _world.Create();
            _world.Set(t, new TornadoField
            { centerWorld = new SimVec3(10, 0, 6), tileRange = 1, pullSpeed = 5f, remaining = 9f });

            Tick(0.5f);
            Assert.Less(Pos(e).z, 6.0001f, "사거리 밖 — z 변위 없음.");
        }

        // ── 어그로 추격 ──────────────────────────────────────────────────────

        [Test]
        public void Chasing_WithoutChaseField_StandsStill()
        {
            var e = Mover(new SimInt2(2, 6));
            _world.Set(e, new EnemyAiState { value = AiState.Chasing });
            SimVec3 before = Pos(e);
            Tick(0.5f);
            Assert.AreEqual(before.x, Pos(e).x, 1e-5f, "필드가 없으면 정지(합성 월드 계약).");
        }

        [Test]
        public void Chasing_DescendsTheChaseField_AndSkipsGoalLeak()
        {
            var e = Mover(Goal);   // 골 위에 서 있다
            _world.Set(e, new EnemyAiState { value = AiState.Chasing });

            // 가디언이 (6,6) 에 있다고 가정한 chase field.
            int n = Grid.x * Grid.y;
            var mask = new byte[n];
            for (int i = 0; i < n; i++) mask[i] = 1;
            var flow = new SimVec2[n];
            var dist = new int[n];
            FlowFieldBuilder.BuildFromSources(mask, Grid, new[] { new SimInt2(6, 6) }, 1, flow, dist);
            var buf = _world.AddBuffer<AggroChaseCell>(e);
            for (int i = 0; i < n; i++) buf.Add(new AggroChaseCell { dist = dist[i] });

            Tick(0.5f);

            Assert.IsFalse(_world.Has<PastGoalTag>(e), "Chasing 은 골 판정 앞에서 continue 한다.");
            Assert.Less(Pos(e).x, 11f, "가디언(-x) 쪽으로 하강.");
        }

        [Test]
        public void Chasing_WhileLocked_StandsStill()
        {
            var e = Mover(new SimInt2(2, 6));
            _world.Set(e, new EnemyAiState { value = AiState.Chasing });
            _world.AddBuffer<CcEffect>(e).Add(new CcEffect { kind = CcKind.Sleep, remainingTime = 5f });
            SimVec3 before = Pos(e);
            Tick(0.5f);
            Assert.AreEqual(before.x, Pos(e).x, 1e-5f,
                "locked 가 Chasing 분기보다 **앞에서** 계산되기 때문에 성립한다.");
        }

        // ── 속도 모디파이어 ──────────────────────────────────────────────────

        [Test]
        public void MoveSpeedMultiplier_ScalesTheStep()
        {
            var slow = Mover(new SimInt2(2, 6));
            var stats = ModifierStats.Identity; stats.moveSpeedMul = 0.5f;
            _world.Set(slow, stats);
            var fast = Mover(new SimInt2(2, 8));

            Tick(0.5f);
            Assert.Less(Pos(slow).x - 2f, Pos(fast).x - 2f, "×0.5 는 절반만 간다.");
        }
    }

    public class SimBlinkApplyTests
    {
        [Test]
        public void Blink_AssignsXZ_AndKeepsY()
        {
            var world = new SimWorld(new SimConfig(1u, 1u));
            var ch = new SimChannels();
            var sys = new BlinkApplySystem(ch.BlinkRequest);

            var e = world.Create();
            world.Set(e, SimTransform.FromPosition(new SimVec3(1f, 2.5f, 3f)));
            ch.BlinkRequest.Enqueue(new BlinkRequestEvent { entity = e, destWorld = new SimVec3(9, 0, 9) });

            sys.Run(world);

            SimVec3 p = world.Get<SimTransform>(e).Position;
            Assert.AreEqual(9f, p.x, 1e-5f);
            Assert.AreEqual(9f, p.z, 1e-5f);
            Assert.AreEqual(2.5f, p.y, 1e-5f, "y 는 소비자가 현재값을 유지한다.");
        }

        [Test]
        public void Blink_DropsRequestsForDestroyedTargets()
        {
            var world = new SimWorld(new SimConfig(1u, 1u));
            var ch = new SimChannels();
            var sys = new BlinkApplySystem(ch.BlinkRequest);

            var e = world.Create();
            world.Set(e, SimTransform.FromPosition(SimVec3.Zero));
            ch.BlinkRequest.Enqueue(new BlinkRequestEvent { entity = e, destWorld = new SimVec3(9, 0, 9) });
            world.Destroy(e);

            Assert.DoesNotThrow(() => sys.Run(world));
            Assert.AreEqual(0, ch.BlinkRequest.Count);
        }
    }

    public class SimMovementClusterTests
    {
        [Test]
        public void StepsLandInTheirCapturePhases()
        {
            foreach (var s in new MovementCluster(new SimChannels()).Steps())
                Assert.AreEqual(SimPipeline.PhaseForOrder(s.Order), s.Phase,
                    $"#{s.Order} {s.Name}");
        }

        [Test]
        public void OwnsFiveCaptureNumbers_AcrossFourPhases_WithOnlyOneAdjacency()
        {
            var orders = new List<int>();
            var phases = new HashSet<SimPhase>();
            foreach (var s in new MovementCluster(new SimChannels()).Steps())
            { orders.Add(s.Order); phases.Add(s.Phase); }

            CollectionAssert.AreEqual(new[] { 8, 13, 14, 17, 44 }, orders);
            Assert.AreEqual(4, phases.Count,
                "P2·P3·P4·P12 — 다섯 시스템이 네 phase 에 흩어진다.");

            // **유일한 인접이 #13 → #14(둘 다 P3)이고 그 인접이 계약이다** — 도발로 부여된
            // `AttackState.range` 를 FSM 이 같은 프레임에 봐야 Standoff 판정이 맞는다.
            Assert.AreEqual(SimPhase.PreCombat, PhaseOf(13));
            Assert.AreEqual(SimPhase.PreCombat, PhaseOf(14));
            Assert.AreNotEqual(PhaseOf(8), PhaseOf(13), "나머지는 전부 서로 다른 phase 다.");
            Assert.AreNotEqual(PhaseOf(14), PhaseOf(17));
            Assert.AreNotEqual(PhaseOf(17), PhaseOf(44));
        }

        private static SimPhase PhaseOf(int order)
        {
            foreach (var s in new MovementCluster(new SimChannels()).Steps())
                if (s.Order == order) return s.Phase;
            throw new System.InvalidOperationException($"#{order} 가 클러스터에 없다.");
        }

        [Test]
        public void ThreeClusters_ComposeWithoutNumberCollisions()
        {
            var ch = new SimChannels();
            SimTick tick = new SimPipeline()
                .Add(new MovementCluster(ch).Steps())
                .Add(new ModifierCluster(ch).Steps())
                .Add(new EnvironmentCluster(ch).Steps())
                .Build();

            Assert.AreEqual(6, tick.StepCount(SimPhase.FieldsAndPeriodic));
            Assert.AreEqual(2, tick.StepCount(SimPhase.Intake), "#8 · #9");
            Assert.AreEqual(3, tick.StepCount(SimPhase.PreCombat), "#13 · #14 · #16");
            Assert.AreEqual(1, tick.StepCount(SimPhase.Movement), "#17");
            Assert.AreEqual(5, tick.StepCount(SimPhase.ModifierTick));
            Assert.AreEqual(1, tick.StepCount(SimPhase.Destruction), "#44");
        }
    }
}
