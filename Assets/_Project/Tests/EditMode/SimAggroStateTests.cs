// battle-sim-extraction unit 18-F/2 — #8 AggroState 이식 핀.
// 구 오라클은 PlayMode 어그로 군이 진다(EditMode 대응물 없음) — 여기서 시스템 골격을 박는다:
// OR 게이트 · 사망 3중 판정 · full recompute · 게이트 순서 · **지연 구조 변경의 관측 가능성**.
using System.Collections.Generic;
using NUnit.Framework;
using Wassup.Sim;
using Wassup.Sim.Combat;
using Wassup.Sim.Effects;
using Wassup.Sim.Movement;
using Wassup.Sim.Units;

namespace Wassup.Tests.EditMode
{
    public class SimAggroStateTests
    {
        private static readonly SimInt2 Grid = new SimInt2(9, 9);

        private SimWorld _world;
        private SimChannels _ch;
        private AggroStateSystem _sys;

        [SetUp]
        public void SetUp()
        {
            _world = new SimWorld(new SimConfig(1u, 1u));
            _ch = new SimChannels();
            _sys = new AggroStateSystem(_ch.AggroHit);
        }

        private void WithFlowField()
        {
            int n = Grid.x * Grid.y;
            var f = new FlowFieldSingleton
            {
                flow = new SimVec2[n], dist = new int[n],
                gridSize = Grid, tileSize = 1f, origin = SimVec3.Zero, goalCell = new SimInt2(8, 8),
            };
            // 전 셀 walkable 로 만들려면 flow 가 비-0 이어야 한다(`IsWallCell` 이 zero-flow=벽).
            var mask = new byte[n];
            for (int i = 0; i < n; i++) mask[i] = 1;
            FlowFieldBuilder.BuildFromSources(mask, Grid, new[] { new SimInt2(8, 8) }, 1, f.flow, f.dist);
            _world.Set(_world.Create(), f);
        }

        private SimEntityId Guardian(int capacity, SimInt2 cell, float hp = 10f)
        {
            var e = _world.Create();
            _world.Set(e, new AggroCapacity { max = capacity, held = 0 });
            _world.Set(e, new Health { value = hp, max = 10f });
            _world.Set(e, SimTransform.FromPosition(new SimVec3(cell.x, 0f, cell.y)));
            return e;
        }

        private SimEntityId Enemy(SimInt2 cell, float range = 1f, bool boss = false)
        {
            var e = _world.Create();
            _world.Set(e, new AttackState { range = range });
            _world.Set(e, SimTransform.FromPosition(new SimVec3(cell.x, 0f, cell.y)));
            if (boss) _world.Set(e, default(BossTag));
            return e;
        }

        private void Hit(SimEntityId guardian, SimEntityId enemy)
            => _ch.AggroHit.Enqueue(new AggroHitEvent { guardian = guardian, enemy = enemy });

        private void Tick() => _sys.Run(_world);

        // ── 게이트 (RequireAnyForUpdate = OR) ─────────────────────────────────

        [Test]
        public void NoProviderAndNoAggroed_DoesNotRun()
        {
            var e = Enemy(new SimInt2(1, 1));
            Hit(e, e);
            Tick();
            Assert.AreEqual(1, _ch.AggroHit.Count, "시스템이 안 돌았으므로 채널도 안 비워진다.");
        }

        [Test]
        public void AggroedWithoutAnyProvider_StillRuns_SoOrphansGetReleased()
        {
            // 마지막 가디언이 소멸한 뒤에도 해제 패스가 살아 있어야 한다(OR 게이트의 존재 이유).
            // AND 로 오번역하면 적이 **영원히 어그로된 채**로 남는다.
            var enemy = Enemy(new SimInt2(1, 1));
            _world.Set(enemy, new Aggroed { guardian = SimEntityId.Null });
            Tick();

            Assert.IsFalse(_world.Has<Aggroed>(enemy), "가디언 없음 = 해제.");
        }

        // ── Pass 1: 사망 3중 판정 ─────────────────────────────────────────────

        [Test]
        public void Release_WhenGuardianHasDeadTag()
        {
            var g = Guardian(1, new SimInt2(4, 4));
            var e = Enemy(new SimInt2(5, 4));
            _world.Set(e, new Aggroed { guardian = g });
            _world.Set(g, default(DeadTag));
            Tick();
            Assert.IsFalse(_world.Has<Aggroed>(e), "DeadTag 는 파괴 전 프레임의 사망 신호다.");
        }

        [Test]
        public void Release_WhenGuardianHpIsZero_EvenWithoutDeadTag()
        {
            var g = Guardian(1, new SimInt2(4, 4), hp: 0f);
            var e = Enemy(new SimInt2(5, 4));
            _world.Set(e, new Aggroed { guardian = g });
            Tick();
            Assert.IsFalse(_world.Has<Aggroed>(e), "HP<=0 도 판정에 든다(마킹 전 프레임).");
        }

        [Test]
        public void Release_WhenGuardianWasDestroyed()
        {
            var g = Guardian(1, new SimInt2(4, 4));
            var e = Enemy(new SimInt2(5, 4));
            _world.Set(e, new Aggroed { guardian = g });
            _world.Destroy(g);
            Tick();
            Assert.IsFalse(_world.Has<Aggroed>(e));
        }

        [Test]
        public void Release_RemovesChaseBuffer_NotJustClearsIt()
        {
            // 소비자가 `HasBuffer` 로 분기한다 — 빈 버퍼는 "전부 dist 0" 이라는 없는 상태다.
            var g = Guardian(1, new SimInt2(4, 4));
            var e = Enemy(new SimInt2(5, 4));
            _world.Set(e, new Aggroed { guardian = g });
            _world.AddBuffer<AggroChaseCell>(e).Add(new AggroChaseCell { dist = 3 });
            _world.Destroy(g);
            Tick();

            Assert.IsFalse(_world.HasBuffer<AggroChaseCell>(e), "버퍼 자체가 없어야 한다.");
        }

        // ── Pass 2: full recompute ────────────────────────────────────────────

        [Test]
        public void Held_IsFullyRecomputed_NotIncremented()
        {
            var g = Guardian(3, new SimInt2(4, 4));
            var cap = _world.Get<AggroCapacity>(g);
            cap.held = 99;                      // 드리프트를 심는다
            _world.Set(g, cap);

            var a = Enemy(new SimInt2(5, 4)); _world.Set(a, new Aggroed { guardian = g });
            var b = Enemy(new SimInt2(3, 4)); _world.Set(b, new Aggroed { guardian = g });
            Tick();

            Assert.AreEqual(2, _world.Get<AggroCapacity>(g).held, "매 틱 전량 재계산 — 드리프트가 지워진다.");
        }

        [Test]
        public void DyingEnemies_AreExcludedFromHeldCount()
        {
            var g = Guardian(3, new SimInt2(4, 4));
            var a = Enemy(new SimInt2(5, 4)); _world.Set(a, new Aggroed { guardian = g });
            var b = Enemy(new SimInt2(3, 4)); _world.Set(b, new Aggroed { guardian = g });
            _world.Set(b, default(DeadTag));
            Tick();

            Assert.AreEqual(1, _world.Get<AggroCapacity>(g).held);
        }

        // ── Pass 3: 획득 게이트 ───────────────────────────────────────────────

        [Test]
        public void Acquires_WhenAllGatesPass()
        {
            var g = Guardian(1, new SimInt2(4, 4));
            var e = Enemy(new SimInt2(5, 4));
            Hit(g, e);
            Tick();

            Assert.IsTrue(_world.Has<Aggroed>(e));
            Assert.AreEqual(g, _world.Get<Aggroed>(e).guardian);
            Assert.AreEqual(0, _ch.AggroHit.Count, "채널은 통째로 비운다.");
        }

        [Test]
        public void Refuses_WhenGuardianIsNotACapacityProvider()
        {
            var notGuardian = Enemy(new SimInt2(4, 4));
            var e = Enemy(new SimInt2(5, 4));
            _world.Set(e, new Aggroed { guardian = SimEntityId.Null });   // OR 게이트 통과용
            Hit(notGuardian, e);
            Tick();
            Assert.IsFalse(_world.Has<Aggroed>(e));
        }

        [Test]
        public void Refuses_WhenCapacityIsFull()
        {
            var g = Guardian(1, new SimInt2(4, 4));
            var held = Enemy(new SimInt2(3, 4)); _world.Set(held, new Aggroed { guardian = g });
            var e = Enemy(new SimInt2(5, 4));
            Hit(g, e);
            Tick();
            Assert.IsFalse(_world.Has<Aggroed>(e), "상한 도달.");
        }

        [Test]
        public void Refuses_Boss_BecauseImmunityIsAppliedAtAttachment()
        {
            // 소비 지점이 6곳이라 "붙은 것을 무시" 는 비싸다 — 부착 1곳에서 막는다.
            var g = Guardian(4, new SimInt2(4, 4));
            var boss = Enemy(new SimInt2(5, 4), boss: true);
            Hit(g, boss);
            Tick();

            Assert.IsFalse(_world.Has<Aggroed>(boss));
            Assert.AreEqual(0, _world.Get<AggroCapacity>(g).held, "부착이 없으면 회계에도 안 들어온다.");
        }

        [Test]
        public void Refuses_EnemyWithNoAttackMeans()
        {
            var g = Guardian(4, new SimInt2(4, 4));
            var e = _world.Create();   // AttackState 도 도발 프로파일도 없다
            _world.Set(e, SimTransform.FromPosition(new SimVec3(5, 0, 4)));
            Hit(g, e);
            Tick();

            Assert.IsFalse(_world.Has<Aggroed>(e), "때릴 수단이 없으면 Chasing 고착이 된다 — 거부.");
        }

        [Test]
        public void TauntProfile_IsAValidFallback_WhenAttackStateIsAbsent()
        {
            var g = Guardian(4, new SimInt2(4, 4));
            var e = _world.Create();
            _world.Set(e, new AggroAttackProfile { range = 1f, damage = 1f, cooldown = 1f });
            _world.Set(e, SimTransform.FromPosition(new SimVec3(5, 0, 4)));
            Hit(g, e);
            Tick();

            Assert.IsTrue(_world.Has<Aggroed>(e));
        }

        [Test]
        public void Preemption_FirstHitWins_WithinTheSameTick()
        {
            var g1 = Guardian(4, new SimInt2(4, 4));
            var g2 = Guardian(4, new SimInt2(6, 4));
            var e = Enemy(new SimInt2(5, 4));
            Hit(g1, e);
            Hit(g2, e);
            Tick();

            Assert.AreEqual(g1, _world.Get<Aggroed>(e).guardian, "first-come, sticky.");

            // ⚠ `held` 는 Pass 2(부착 **전**)에서 계산되므로 이번 틱엔 아직 0 이다 —
            // 회계는 **커밋된 상태**에서만 나온다(틱 내 임시 카운터 `runningHeld` 는 컴포넌트에
            // 쓰지 않는다). 다음 틱의 full recompute 가 1 을 만든다.
            Assert.AreEqual(0, _world.Get<AggroCapacity>(g1).held, "부착은 Pass 2 뒤라 이번 틱엔 미반영.");
            Tick();
            Assert.AreEqual(1, _world.Get<AggroCapacity>(g1).held);
            Assert.AreEqual(0, _world.Get<AggroCapacity>(g2).held, "두 번 세지 않는다.");
        }

        [Test]
        public void ReleasedEnemy_CannotBeReacquired_InTheSameTick()
        {
            // 구조 변경이 **지연**이라 Pass 3 에서 그 적은 여전히 Aggroed 로 보인다.
            // 즉시 제거로 바꾸면 해제된 적이 같은 틱에 다시 끌려간다.
            var dyingGuardian = Guardian(1, new SimInt2(2, 2));
            var freshGuardian = Guardian(1, new SimInt2(6, 6));
            var e = Enemy(new SimInt2(5, 4));
            _world.Set(e, new Aggroed { guardian = dyingGuardian });
            _world.Set(dyingGuardian, default(DeadTag));
            Hit(freshGuardian, e);
            Tick();

            Assert.IsFalse(_world.Has<Aggroed>(e),
                "이번 틱엔 해제만 된다 — 재획득은 다음 틱 몫이다.");
        }

        // ── 기하 게이트 ───────────────────────────────────────────────────────

        [Test]
        public void WithoutFlowField_AttachesWithoutChaseBuffer()
        {
            // 합성 테스트 월드 계약 — 기하를 생략하고 부착만 한다.
            var g = Guardian(1, new SimInt2(4, 4));
            var e = Enemy(new SimInt2(5, 4));
            Hit(g, e);
            Tick();

            Assert.IsTrue(_world.Has<Aggroed>(e));
            Assert.IsFalse(_world.HasBuffer<AggroChaseCell>(e), "필드가 없으면 chase 버퍼도 없다.");
        }

        [Test]
        public void WithFlowField_AttachesChaseBuffer_SizedToTheGrid()
        {
            WithFlowField();
            var g = Guardian(1, new SimInt2(4, 4));
            var e = Enemy(new SimInt2(6, 4));
            Hit(g, e);
            Tick();

            Assert.IsTrue(_world.Has<Aggroed>(e));
            var chase = _world.GetBuffer<AggroChaseCell>(e);
            Assert.IsNotNull(chase);
            Assert.AreEqual(Grid.x * Grid.y, chase.Count, "chase field 는 그리드 전체다.");
        }

        [Test]
        public void UnreachableEnemy_IsRefused_NoZombieChase()
        {
            WithFlowField();
            var g = Guardian(1, new SimInt2(4, 4));
            var e = Enemy(new SimInt2(6, 4));

            // 가디언 주변을 전부 막아 목적지 후보를 0 으로 만든다.
            var obstacles = new ObstacleSingleton { blockedCells = new HashSet<SimInt2>() };
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
                obstacles.blockedCells.Add(new SimInt2(4 + dx, 4 + dy));
            _world.Set(_world.Create(), obstacles);

            Hit(g, e);
            Tick();

            Assert.IsFalse(_world.Has<Aggroed>(e), "목적지 후보 0 = 거부(좀비 추격 금지).");
        }
    }
}
