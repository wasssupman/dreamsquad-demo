using NUnit.Framework;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Wassup.Battle.Combat;
using Wassup.Battle.Effects;
using Wassup.Battle.Movement;

namespace Wassup.Tests.EditMode
{
    // defender-knockback-on-impact unit 2 — 「넉백은 상태와 무관하다」.
    //
    // 사용자 증상: 「밀당맨이 제자리 공격 중인 킨들러를 못 민다」. 원인은 생산이 아니라
    // 소비였다 — impulse 소비는 `MovementSystem` 에 한 줄뿐인데(`CcKind.Impulse` 전수
    // 조사: 생산자 4 · 소비자 1), 자기주도 이동을 하지 않는 상태들이 **그 줄에 닿기 전에
    // `continue` 로 빠져나간다.** 못 쓴 impulse 는 `CcDecaySystem` 이 소비 여부와 무관하게
    // 만료시켜 조용히 증발한다.
    //
    // ★ 이 클래스는 **상태별 소비 커버리지**가 존재 이유다. `MovementCompositionTests` 는
    // 픽스처에 `EnemyAiState` 가 없어 **항상 Marching** 이라, 아래 상태들을 한 번도 지나지
    // 않는다 — 그래서 이 결함이 전 테스트 초록인 채로 살아 있었다.
    //
    // 불변식: **자기주도 이동이 0 인 것과 외력을 안 받는 것은 다르다.**
    // 「멈춤」은 self = 0 이지 「변위 계산을 건너뜀」이 아니다.
    public class MovementImpulseAcrossStatesTests
    {
        private World _world;
        private EntityManager _em;
        private SimulationSystemGroup _simGroup;
        private Entity _fieldEntity;

        // +z 로 미는 넉백. flow 는 +x 라 두 축이 안 섞여 단언이 명확하다.
        private const float ImpulseSpeed = 3f;
        private const float Dt = 0.2f;
        private const float ExpectedPush = ImpulseSpeed * Dt; // 0.6

        [SetUp]
        public void SetUp()
        {
            _world = new World("MovementImpulseStatesTestWorld");
            _em = _world.EntityManager;
            _simGroup = _world.CreateSystemManaged<SimulationSystemGroup>();
            _simGroup.AddSystemToUpdateList(_world.CreateSystem<MovementSystem>());

            // MovementCompositionTests 와 같은 2셀 +x 필드.
            var flow = new NativeArray<float2>(2, Allocator.Persistent);
            var dist = new NativeArray<int>(2, Allocator.Persistent);
            flow[0] = new float2(1, 0); dist[0] = 1;
            flow[1] = float2.zero;      dist[1] = 0;
            _fieldEntity = _em.CreateEntity();
            _em.AddComponentData(_fieldEntity, new FlowFieldSingleton
            {
                flow = flow, dist = dist,
                gridSize = new int2(2, 1),
                goalCell = new int2(1, 0),
                tileSize = 1f, version = 1,
            });
        }

        [TearDown]
        public void TearDown()
        {
            if (_em.Exists(_fieldEntity) && _em.HasComponent<FlowFieldSingleton>(_fieldEntity))
                _em.GetComponentData<FlowFieldSingleton>(_fieldEntity).Dispose();
            _world?.Dispose();
        }

        private Entity CreateUnit()
        {
            var e = _em.CreateEntity();
            _em.AddComponentData(e, LocalTransform.FromPosition(float3.zero));
            _em.AddComponentData(e, new PathFollowState { speed = 2f });
            var buf = _em.AddBuffer<CcEffect>(e);
            buf.Add(new CcEffect
            {
                kind = CcKind.Impulse,
                vector = new float3(0, 0, ImpulseSpeed),
                remainingTime = 5f,
            });
            return e;
        }

        private void SetState(Entity e, AiState state)
            => _em.AddComponentData(e, new EnemyAiState { value = state });

        // 잠금(Stun)을 추가로 건다 — CcActionLock.IsLocked 가 보는 것은 Stun/Sleep 이다.
        private void AddStun(Entity e)
        {
            var buf = _em.GetBuffer<CcEffect>(e);
            buf.Add(new CcEffect { kind = CcKind.Stun, remainingTime = 5f });
        }

        private void Tick()
        {
            _world.SetTime(new TimeData(_world.Time.ElapsedTime + Dt, Dt));
            _simGroup.Update();
        }

        private void AssertPushed(Entity e, string state)
        {
            var pos = _em.GetComponentData<LocalTransform>(e).Position;
            Assert.AreEqual(ExpectedPush, pos.z, 1e-4f,
                $"{state} 상태에서도 넉백은 먹어야 한다 — 「멈춤」은 self=0 이지 외력 무시가 아니다");
        }

        // ── 사이트 1: Standoff (도발돼 가디언과 대치) ─────────────────────────
        [Test]
        public void Standoff_StillTakesKnockback()
        {
            var e = CreateUnit();
            SetState(e, AiState.Standoff);
            Tick();
            AssertPushed(e, "Standoff");
        }

        // ── 사이트 2: Chasing + 잠금 ────────────────────────────────────────
        [Test]
        public void ChasingLocked_StillTakesKnockback()
        {
            var e = CreateUnit();
            SetState(e, AiState.Chasing);
            AddStun(e);
            Tick();
            AssertPushed(e, "Chasing+Stun");
        }

        // ── 사이트 3: Chasing (추격 필드 없음 = 자기 이동 0) ──────────────────
        [Test]
        public void Chasing_StillTakesKnockback()
        {
            var e = CreateUnit();
            SetState(e, AiState.Chasing);
            Tick();
            AssertPushed(e, "Chasing");
        }

        // ── 사이트 4: Engaging + Halt (사용자가 실제로 본 킨들러) ─────────────
        // EnemyBehavior 부재 시 기본값이 Halt 라(MovementSystem:213-214) 그대로 둔다.
        [Test]
        public void EngagingHalt_StillTakesKnockback()
        {
            var e = CreateUnit();
            SetState(e, AiState.Engaging);
            Tick();
            AssertPushed(e, "Engaging+Halt");
        }

        // 저작으로 Halt 를 명시한 경우도 같다(킨들러 = engageMovement 0).
        [Test]
        public void EngagingHalt_Authored_StillTakesKnockback()
        {
            var e = CreateUnit();
            SetState(e, AiState.Engaging);
            _em.AddComponentData(e, new EnemyBehavior
            {
                engageMovement = Wassup.Data.EngageMovement.Halt,
            });
            Tick();
            AssertPushed(e, "Engaging+Halt(저작)");
        }

        // ── 사이트 5: 순찰 dir == 0 (거점 도착한 순찰 아군) ───────────────────
        // 오늘 Impulse 생산자는 전부 적을 겨눠 실전 no-op 이지만, 소비 규칙은
        // 상태와 무관해야 한다 — 규칙의 구멍을 메우는 것이지 콘텐츠가 아니다.
        [Test]
        public void PatrolIdle_StillTakesKnockback()
        {
            var e = CreateUnit();
            _em.AddComponentData(e, new PatrolStep { dir = float2.zero });
            Tick();
            AssertPushed(e, "Patrol(dir 0)");
        }

        // ── 사이트 7 (대조군): Marching — 원래도 먹던 경로. 회귀 감시용 ────────
        [Test]
        public void Marching_TakesKnockback_AndKeepsFlowStep()
        {
            var e = CreateUnit();
            Tick();
            var pos = _em.GetComponentData<LocalTransform>(e).Position;
            Assert.AreEqual(0.4f, pos.x, 1e-4f, "자기 이동은 그대로 (speed 2 × dt 0.2)");
            Assert.AreEqual(ExpectedPush, pos.z, 1e-4f, "Marching 넉백은 원래 먹었다");
        }

        // 잠긴 Marching — 자기 이동만 0, 넉백은 유지(MovementSystem:375 계약).
        // 이 계약이 다른 상태에서 깨져 있었다는 것이 이 클래스의 요점이다.
        [Test]
        public void MarchingLocked_KeepsKnockback_ButNoSelfMove()
        {
            var e = CreateUnit();
            AddStun(e);
            Tick();
            var pos = _em.GetComponentData<LocalTransform>(e).Position;
            Assert.AreEqual(0f, pos.x, 1e-4f, "잠기면 자기주도 이동은 0");
            Assert.AreEqual(ExpectedPush, pos.z, 1e-4f, "잠겨도 넉백은 유지");
        }
    }
}
