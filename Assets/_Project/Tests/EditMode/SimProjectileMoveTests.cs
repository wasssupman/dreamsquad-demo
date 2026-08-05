using NUnit.Framework;
using Wassup.Sim;
using Wassup.Sim.Combat;
using Wassup.Sim.Effects;
using Wassup.Sim.Movement;
using Wassup.Sim.Units;

namespace Wassup.Tests.EditMode
{
    /// <summary>
    /// battle-sim-extraction unit 18-H/2 — 궤적 축(#26) 이식의 오라클.
    ///
    /// 여섯 arm 각각의 **도착 조건**과 **위치 갱신**을 고정한다. 도착 조건이 궤적마다 다르다는
    /// 것이 이 시스템의 존재 이유이므로, arm 하나가 조용히 다른 arm 의 조건을 쓰기 시작하면
    /// 여기서 잡혀야 한다.
    /// </summary>
    public class SimProjectileMoveTests
    {
        private SimWorld _world;
        private ProjectileMoveSystem _sut;

        [SetUp]
        public void SetUp()
        {
            _world = new SimWorld(new SimConfig(1u, 1u));
            _sut = new ProjectileMoveSystem();
            _world.SetDeltaTime(0.1f);
        }

        private SimEntityId Enemy(SimVec3 pos)
        {
            var e = _world.Create();
            _world.Set(e, new AttackUnitTag());
            _world.Set(e, SimTransform.FromPosition(pos));
            return e;
        }

        private SimEntityId Shot(ProjectileState state, SimVec3 pos)
        {
            var e = _world.Create();
            _world.Set(e, new ProjectileTag());
            _world.Set(e, state);
            _world.Set(e, SimTransform.FromPosition(pos));
            return e;
        }

        private SimVec3 Pos(SimEntityId e) => _world.Get<SimTransform>(e).Position;
        private ProjectileState State(SimEntityId e) => _world.Get<ProjectileState>(e);

        private void Field(float tileSize = 1f)
        {
            var f = _world.Create();
            _world.Set(f, new FlowFieldSingleton
            {
                flow = new SimVec2[1], dist = new int[1],
                gridSize = new SimInt2(128, 128), tileSize = tileSize, origin = default,
            });
        }

        // ═════ HomingToEntity ════════════════════════════════════════════════

        [Test]
        public void Homing_StepsTowardTarget_AtSpeedTimesDt()
        {
            var target = Enemy(new SimVec3(10f, 0f, 0f));
            var shot = Shot(new ProjectileState
            {
                movement = MovementKind.HomingToEntity, target = target,
                speed = 20f, hitThreshold = 0.2f,
            }, new SimVec3(0f, 0f, 0f));

            _sut.Run(_world);

            Assert.AreEqual(2f, Pos(shot).x, 1e-4f, "speed 20 × dt 0.1");
            Assert.IsFalse(State(shot).impactReached);
        }

        [Test]
        public void Homing_SnapsToTarget_WhenStepOvershoots_AndArrives()
        {
            var target = Enemy(new SimVec3(1f, 0f, 0f));
            var shot = Shot(new ProjectileState
            {
                movement = MovementKind.HomingToEntity, target = target,
                speed = 100f, hitThreshold = 0.2f,
            }, new SimVec3(0f, 0f, 0f));

            _sut.Run(_world);

            Assert.AreEqual(1f, Pos(shot).x, 1e-4f, "넘어가지 않고 대상에 스냅");
            Assert.IsTrue(State(shot).impactReached);
        }

        [Test]
        public void Homing_ArrivalIsXZOnly()
        {
            // Y 로만 벌어진 대상은 XZ 판정상 도착이다(보드가 평면이라 Y 는 규칙이 아니다).
            var target = Enemy(new SimVec3(0f, 99f, 0f));
            var shot = Shot(new ProjectileState
            {
                movement = MovementKind.HomingToEntity, target = target,
                speed = 0.001f, hitThreshold = 0.2f,
            }, new SimVec3(0f, 0f, 0f));

            _sut.Run(_world);

            Assert.IsTrue(State(shot).impactReached);
        }

        [Test]
        public void Homing_DestroysWhenTargetGoneOrDead_WithoutRetarget()
        {
            var destroyed = Enemy(new SimVec3(5f, 0f, 0f));
            var a = Shot(new ProjectileState { movement = MovementKind.HomingToEntity, target = destroyed, speed = 1f },
                         new SimVec3(0, 0, 0));
            _world.Destroy(destroyed);

            var dead = Enemy(new SimVec3(5f, 0f, 0f));
            _world.Set(dead, new DeadTag());
            var b = Shot(new ProjectileState { movement = MovementKind.HomingToEntity, target = dead, speed = 1f },
                         new SimVec3(0, 0, 0));

            var nullTarget = Shot(new ProjectileState { movement = MovementKind.HomingToEntity, speed = 1f },
                                  new SimVec3(0, 0, 0));

            _sut.Run(_world);

            Assert.IsFalse(_world.Exists(a));
            Assert.IsFalse(_world.Exists(b), "⚠ 시체에 피해를 얹지 않는다 — DeadTag 도 소실로 친다");
            Assert.IsFalse(_world.Exists(nullTarget));
        }

        [Test]
        public void Homing_RetargetsInsteadOfDying_WhenRadiusIsSet()
        {
            Field();
            var dead = Enemy(new SimVec3(5f, 0f, 0f));
            _world.Set(dead, new DeadTag());
            var alive = Enemy(new SimVec3(2f, 0f, 0f));
            var shot = Shot(new ProjectileState
            {
                movement = MovementKind.HomingToEntity, target = dead,
                speed = 1f, hitThreshold = 0.1f, retargetTileRange = 4,
            }, new SimVec3(1f, 0f, 0f));

            _sut.Run(_world);

            Assert.IsTrue(_world.Exists(shot));
            Assert.AreEqual(alive, State(shot).target, "현재 위치 기준 반경 안의 산 적으로 다시 겨눈다");
        }

        [Test]
        public void Homing_RetargetPool_ExcludesDeadLeakedAndOutOfPlay()
        {
            Field();
            var dead = Enemy(new SimVec3(5f, 0f, 0f));
            _world.Set(dead, new DeadTag());

            var leaked = Enemy(new SimVec3(2f, 0f, 0f));
            _world.Set(leaked, new PastGoalTag());
            var outOfPlay = Enemy(new SimVec3(2f, 0f, 1f));
            _world.Set(outOfPlay, new UltimateLeapState { remaining = 2f });

            var shot = Shot(new ProjectileState
            {
                movement = MovementKind.HomingToEntity, target = dead,
                speed = 1f, retargetTileRange = 4,
            }, new SimVec3(1f, 0f, 0f));

            _sut.Run(_world);

            Assert.IsFalse(_world.Exists(shot), "후보가 없으면 결국 파괴된다");
        }

        // ═════ BezierHomingToEntity ══════════════════════════════════════════

        [Test]
        public void Bezier_WalksTheCurve_AndArrivesAtCompletion()
        {
            var target = Enemy(new SimVec3(10f, 0f, 0f));
            var shot = Shot(new ProjectileState
            {
                movement = MovementKind.BezierHomingToEntity, target = target,
                origin = new SimVec3(0, 0, 0),
                control1 = new SimVec3(3.33333f, 0f, 0f),
                control2 = new SimVec3(6.66667f, 0f, 0f),
                flightTime = 1f, hitThreshold = 0.01f,
            }, new SimVec3(0, 0, 0));

            for (int i = 0; i < 9; i++) _sut.Run(_world);
            Assert.IsFalse(State(shot).impactReached, "t=0.9 — 아직");

            _sut.Run(_world);
            Assert.IsTrue(State(shot).impactReached, "t=1 완주");
            Assert.AreEqual(10f, Pos(shot).x, 1e-3f);
        }

        [Test]
        public void Bezier_DiesOnTargetLoss_RetargetIsIntentionallyClosed()
        {
            // ⚠ 재조준이 열려 있으면 t≈1 에서 새 대상으로 순간이동 후 즉시 착탄한다.
            Field();
            var dead = Enemy(new SimVec3(5f, 0f, 0f));
            _world.Set(dead, new DeadTag());
            Enemy(new SimVec3(2f, 0f, 0f)); // 살아 있는 후보가 있어도

            var shot = Shot(new ProjectileState
            {
                movement = MovementKind.BezierHomingToEntity, target = dead,
                flightTime = 1f, retargetTileRange = 4, // 값이 실려 있어도 무시된다
            }, new SimVec3(1f, 0f, 0f));

            _sut.Run(_world);

            Assert.IsFalse(_world.Exists(shot));
        }

        // ═════ BallisticArcToPoint ═══════════════════════════════════════════

        [Test]
        public void Ballistic_IgnoresTargetFate_AndArrivesOnFlightTime()
        {
            var shot = Shot(new ProjectileState
            {
                movement = MovementKind.BallisticArcToPoint,
                origin = new SimVec3(0, 0, 0), impact = new SimVec3(4f, 0f, 0f),
                arcHeight = 2f, flightTime = 0.4f,
            }, new SimVec3(0, 0, 0));

            _sut.Run(_world); _sut.Run(_world);
            Assert.AreEqual(2f, Pos(shot).x, 1e-4f, "t=0.5 → XZ 중점");
            Assert.AreEqual(2f, Pos(shot).y, 1e-4f, "정점");
            Assert.IsFalse(State(shot).impactReached);

            _sut.Run(_world); _sut.Run(_world);
            Assert.IsTrue(State(shot).impactReached);
            Assert.AreEqual(4f, Pos(shot).x, 1e-4f);
            Assert.AreEqual(0f, Pos(shot).y, 1e-4f, "착탄에서 아치 항은 0");
        }

        // ═════ SkyFall ═══════════════════════════════════════════════════════

        [Test]
        public void SkyFall_HoldsPositionAndOnlyTicksElapsed()
        {
            var impact = new SimVec3(3f, 0f, 7f);
            var shot = Shot(new ProjectileState
            {
                movement = MovementKind.SkyFall, impact = impact, flightTime = 0.25f,
            }, impact);

            _sut.Run(_world);
            Assert.AreEqual(impact, Pos(shot), "sim 위치는 움직이지 않는다 — 낙하는 뷰 전용");
            Assert.AreEqual(0.1f, State(shot).elapsed, 1e-4f);
            Assert.IsFalse(State(shot).impactReached);

            _sut.Run(_world); _sut.Run(_world);
            Assert.IsTrue(State(shot).impactReached);
        }

        [Test]
        public void SkyFall_ZeroFlightTime_ArrivesOnFirstTick()
        {
            var shot = Shot(new ProjectileState { movement = MovementKind.SkyFall, flightTime = 0f },
                            new SimVec3(1, 0, 1));
            _sut.Run(_world);
            Assert.IsTrue(State(shot).impactReached, "경고 0 = 첫 틱 해결(레거시)");
        }

        // ═════ DirectionalLinear ═════════════════════════════════════════════

        [Test]
        public void Directional_RecordsPrevPosBeforeStepping()
        {
            var shot = Shot(new ProjectileState
            {
                movement = MovementKind.DirectionalLinear,
                origin = new SimVec3(0, 0, 0), direction = new SimVec2(1f, 0f),
                speed = 10f, maxDistance = 100f,
            }, new SimVec3(0, 0, 0));

            _sut.Run(_world);
            Assert.AreEqual(0f, State(shot).prevPos.x, 1e-4f);
            Assert.AreEqual(1f, Pos(shot).x, 1e-4f);

            _sut.Run(_world);
            Assert.AreEqual(1f, State(shot).prevPos.x, 1e-4f, "직전 프레임 위치 — 스윕 선분의 시작");
            Assert.AreEqual(2f, Pos(shot).x, 1e-4f);
        }

        [Test]
        public void Directional_LandsExactlyOnRangeLimit_AndFlagsFlightEnd()
        {
            // ⚠ 넘어가면 마지막 스윕이 저작 사거리 밖 타일까지 때린다.
            var shot = Shot(new ProjectileState
            {
                movement = MovementKind.DirectionalLinear,
                origin = new SimVec3(0, 0, 0), direction = new SimVec2(1f, 0f),
                speed = 100f, maxDistance = 3f,
            }, new SimVec3(0, 0, 0));

            _sut.Run(_world);

            Assert.AreEqual(3f, Pos(shot).x, 1e-4f, "정확히 사거리 위");
            Assert.IsTrue(State(shot).impactReached, "여기서의 도착은 '비행 종료' 다");
            Assert.IsTrue(_world.Exists(shot), "소멸은 착탄 시스템의 몫");
        }

        // ═════ GrenadeToCell ═════════════════════════════════════════════════

        [Test]
        public void Grenade_ArrivesAtTravelPlusFuse_HoldingAtTheCell()
        {
            var impact = new SimVec3(2f, 0f, 0f);
            var shot = Shot(new ProjectileState
            {
                movement = MovementKind.GrenadeToCell,
                origin = new SimVec3(0, 0, 0), impact = impact,
                arcHeight = 0f, flightTime = 0.2f, fuseSec = 0.2f,
            }, new SimVec3(0, 0, 0));

            _sut.Run(_world); _sut.Run(_world);
            Assert.AreEqual(impact, Pos(shot), "이동 완료 — 셀 위");
            Assert.IsFalse(State(shot).impactReached, "신관이 남았다");

            _sut.Run(_world);
            Assert.AreEqual(impact, Pos(shot), "신관 동안 위치 고정");
            Assert.IsFalse(State(shot).impactReached);

            _sut.Run(_world);
            Assert.IsTrue(State(shot).impactReached, "이동 + 신관 = 도착");
        }

        // ═════ 모르는 궤적 ════════════════════════════════════════════════════

        [Test]
        public void UnknownMovementKind_IsDestroyed_NotLeaked()
        {
            var shot = Shot(new ProjectileState { movement = (MovementKind)99 }, new SimVec3(0, 0, 0));
            _sut.Run(_world);
            Assert.IsFalse(_world.Exists(shot), "보이는 증상이 조용한 누수보다 낫다");
        }
    }
}
