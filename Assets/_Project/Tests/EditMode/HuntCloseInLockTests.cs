using NUnit.Framework;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Wassup.Battle.Combat;
using Wassup.Battle.Effects;
using Wassup.Battle.Movement;
using Wassup.Battle.Units;

namespace Wassup.Tests.EditMode
{
    // distance-based-range unit 4c — 사냥 레인 접근 보정의 **게이트**를 실제 ECS 월드로 고정한다.
    //
    // ⚠ 순수 하네스(`AggroChaseFreezeTests`)로는 이걸 못 잡는다. 그쪽은 기하를 재구성하므로
    // 게이트를 **복제**하게 되고, 복제한 게이트는 프로덕션이 바뀌어도 안 빨개진다
    // (이 spec 에서 이미 한 번 그렇게 거짓 초록을 만들었다). 그래서 여기서는 `MovementSystem`
    // 자체를 돌린다 — `MovementCompositionTests` 와 같은 형태.
    //
    // 고정하는 계약:
    //   · 잠긴(수면·스턴·도약) 헌터는 보정이 **안 돈다** — `combat-action-lock` 은 하드 계약이고
    //     CC 면역은 `BossTag` 전용이라(`CcApplySystem:37`) 비-보스 헌터로 오늘 재현된다.
    //   · `AttackState` 없는 헌터도 안 돈다 — 그 `Marching` 은 「사거리 밖」이 아니라
    //     「물어보지도 않았다」다(`EnemyAiStateSystem` 이 `hasAttack` 안에서만 술어를 부른다).
    public class HuntCloseInLockTests
    {
        World _world;
        EntityManager _em;
        SimulationSystemGroup _sim;
        Entity _goalField, _huntField;

        // 3×1. 방어유닛 셀 0(도달 불가=벽), 소스 셀 1(dist 0), 셀 2(dist 1).
        // 헌터는 셀 1 의 **바깥 가장자리**에 선다 — 필드는 「도착」이라 하고 flow 는 zero 다.
        const float EnemyX = 1.45f;

        [SetUp]
        public void SetUp()
        {
            _world = new World("HuntCloseInTestWorld");
            _em = _world.EntityManager;
            _sim = _world.CreateSystemManaged<SimulationSystemGroup>();
            _sim.AddSystemToUpdateList(_world.CreateSystem<MovementSystem>());

            var gFlow = new NativeArray<float2>(3, Allocator.Persistent);
            var gDist = new NativeArray<int>(3, Allocator.Persistent);
            for (int i = 0; i < 3; i++) { gFlow[i] = float2.zero; gDist[i] = int.MaxValue; }
            _goalField = _em.CreateEntity();
            _em.AddComponentData(_goalField, new FlowFieldSingleton
            {
                flow = gFlow, dist = gDist, gridSize = new int2(3, 1),
                goalCell = new int2(2, 0), tileSize = 1f, version = 1,
            });

            var hFlow = new NativeArray<float2>(3, Allocator.Persistent);
            var hDist = new NativeArray<int>(3, Allocator.Persistent);
            hFlow[0] = float2.zero; hDist[0] = int.MaxValue;   // 방어유닛 칸 — 벽
            hFlow[1] = float2.zero; hDist[1] = 0;              // 사격 칸(소스)
            hFlow[2] = float2.zero; hDist[2] = 1;
            _huntField = _em.CreateEntity();
            _em.AddComponentData(_huntField, new DefenderFieldSingleton
            {
                flow = hFlow, dist = hDist, gridSize = new int2(3, 1), tileSize = 1f,
            });

            // 다가갈 대상 — `DefenderFieldSystem` 스냅샷과 같은 조건.
            var d = _em.CreateEntity();
            _em.AddComponentData(d, LocalTransform.FromPosition(float3.zero));
            _em.AddComponentData(d, new FactionTag { value = Faction.DefenderUnit });
            _em.AddComponentData(d, new Health { value = 10f, max = 10f });
        }

        [TearDown]
        public void TearDown()
        {
            if (_em.Exists(_goalField)) _em.GetComponentData<FlowFieldSingleton>(_goalField).Dispose();
            if (_em.Exists(_huntField)) _em.GetComponentData<DefenderFieldSingleton>(_huntField).Dispose();
            _world?.Dispose();
        }

        Entity Hunter(bool withAttack = true)
        {
            var e = _em.CreateEntity();
            _em.AddComponentData(e, LocalTransform.FromPosition(new float3(EnemyX, 0f, 0f)));
            _em.AddComponentData(e, new PathFollowState { speed = 2f });
            _em.AddComponent<DefenderHunterTag>(e);
            // FSM 이 「사거리 안 대상 없음」이라고 방금 말한 상태.
            _em.AddComponentData(e, new EnemyAiState { value = AiState.Marching });
            if (withAttack) _em.AddComponentData(e, new AttackState { range = 1f });
            return e;
        }

        void Tick(float dt = 0.2f)
        {
            _world.SetTime(new TimeData(_world.Time.ElapsedTime + dt, dt));
            _sim.Update();
        }

        float X(Entity e) => _em.GetComponentData<LocalTransform>(e).Position.x;

        // ── 기준선: 보정이 실제로 돈다 ──────────────────────────────────────
        [Test]
        public void Hunter_AtFiringCell_ClosesIn()
        {
            var e = Hunter();
            Tick();
            Assert.Less(X(e), EnemyX - 1e-4f,
                "사격 칸에 섰는데 안 움직인다 — 보정이 안 돌면 이게 영구 동결이다");
        }

        // ── C-1 회귀: 잠긴 헌터는 안 움직인다 ────────────────────────────────
        [TestCase(CcKind.Sleep, TestName = "수면")]
        [TestCase(CcKind.Stun,  TestName = "스턴")]
        public void LockedHunter_DoesNotCloseIn(CcKind kind)
        {
            var e = Hunter();
            var cc = _em.AddBuffer<CcEffect>(e);
            cc.Add(new CcEffect { kind = kind, remainingTime = 5f });
            Tick();
            Assert.AreEqual(EnemyX, X(e), 1e-5f,
                $"{kind} 에 걸렸는데 자기주도로 걸었다 — combat-action-lock 위반. " +
                "이 분기는 원래 자기 이동이 0 이라 잠금 게이트보다 앞에 있어도 안전했다. " +
                "보정이 자기 이동을 넣었으므로 `!locked` 도 같이 와야 한다.");
        }

        [Test]
        public void LeapingHunter_DoesNotCloseIn()
        {
            var e = Hunter();
            _em.AddComponent<LeapFlight>(e);
            Tick();
            Assert.AreEqual(EnemyX, X(e), 1e-5f,
                "도약 비행 중인데 보정이 위치를 덮어썼다 — 그 위치의 소유자는 도약 시스템이다");
        }

        // ── M-2 회귀: 술어를 안 지난 Marching 은 보정 근거가 못 된다 ──────────
        [Test]
        public void HunterWithoutAttackState_DoesNotCloseIn()
        {
            var e = Hunter(withAttack: false);
            Tick();
            Assert.AreEqual(EnemyX, X(e), 1e-5f,
                "`AttackState` 가 없으면 EnemyAiStateSystem 은 사거리 술어를 **한 번도 안 부른다** — " +
                "그 Marching 은 「사거리 밖」이 아니라 「물어보지도 않았다」다");
        }

        // ── 게이트: 헌터 태그가 없으면 사냥 분기 자체가 아니다 ────────────────
        [Test]
        public void NonHunter_DoesNotCloseIn()
        {
            var e = Hunter();
            _em.RemoveComponent<DefenderHunterTag>(e);
            Tick();
            Assert.AreEqual(EnemyX, X(e), 1e-5f, "헌터가 아닌데 사냥 보정이 돌았다");
        }
    }
}
