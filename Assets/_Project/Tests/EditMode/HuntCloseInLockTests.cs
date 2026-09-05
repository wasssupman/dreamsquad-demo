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
        NativeArray<float2> _gFlow, _hFlow;
        NativeArray<int> _gDist, _hDist;

        // 3×3. 방어유닛은 셀 (0,0). 소스 = 체비셰프 ≤1 ∩ walkable − 자기 셀 = (1,0)·(0,1)·(1,1).
        // 헌터는 **대각 소스 칸 (1,1) 의 바깥 모서리**에 선다 — 이게 unit 4c 가 고친 동결 기하다.
        //   실거리 = √(1.45² + 1.45²) = 2.05칸  vs  도달 = 사거리1 + 0.5 = 1.5칸
        // 즉 「필드는 도착이라 하고 사거리는 밖이라 한다」가 **이 픽스처에서 실제로 참**이다.
        // (초기 판은 3×1 · x=1.45 였는데 거리 1.45 ≤ 1.5 로 **사거리 안**이었다. 그때
        //  Marching 이던 진짜 이유는 `targetMask = 0` 이었고, 그러면 나중에 마스크를 채우는
        //  사람이 기준선을 깨뜨리고 「보정이 망가졌다」로 오진한다.)
        //
        // ⚠ **이 픽스처에 벽은 없다.** `FlowFieldSingleton.walkMask` 를 안 채우므로
        // `NavGrid` 는 전 칸을 평지로 본다(`NavGrid:56-59` — 마스크 미생성 = 평지, EditMode
        // 픽스처 보호 규약). `dist = MaxValue` 는 「사냥 BFS 가 안 닿았다」일 뿐 통행과 무관하다
        // (그 둘이 갈릴 수 있다는 게 이 spec 이 다루는 문제의 절반이다).
        // 따라서 **막힘·폴백축 경로는 여기서 한 번도 안 돈다.**
        const float EnemyXZ = 1.45f;
        const int   N = 3;

        [SetUp]
        public void SetUp()
        {
            _world = new World("HuntCloseInTestWorld");
            _em = _world.EntityManager;
            _sim = _world.CreateSystemManaged<SimulationSystemGroup>();
            _sim.AddSystemToUpdateList(_world.CreateSystem<MovementSystem>());

            _gFlow = new NativeArray<float2>(N * N, Allocator.Persistent);
            _gDist = new NativeArray<int>(N * N, Allocator.Persistent);
            for (int i = 0; i < N * N; i++) { _gFlow[i] = float2.zero; _gDist[i] = int.MaxValue; }
            _goalField = _em.CreateEntity();
            _em.AddComponentData(_goalField, new FlowFieldSingleton
            {
                flow = _gFlow, dist = _gDist, gridSize = new int2(N, N),
                goalCell = new int2(2, 2), tileSize = 1f, version = 1,
            });

            _hFlow = new NativeArray<float2>(N * N, Allocator.Persistent);
            _hDist = new NativeArray<int>(N * N, Allocator.Persistent);
            for (int i = 0; i < N * N; i++) { _hFlow[i] = float2.zero; _hDist[i] = 1; }
            _hDist[Idx(0, 0)] = int.MaxValue;   // 방어유닛 자기 셀 — 소스에서 제외된다
            _hDist[Idx(1, 0)] = 0;              // 사격 칸(소스)
            _hDist[Idx(0, 1)] = 0;
            _hDist[Idx(1, 1)] = 0;              // 헌터가 서는 대각 소스 칸
            _huntField = _em.CreateEntity();
            _em.AddComponentData(_huntField, new DefenderFieldSingleton
            {
                flow = _hFlow, dist = _hDist, gridSize = new int2(N, N), tileSize = 1f,
            });

            // 다가갈 대상 — `DefenderFieldSystem` 스냅샷과 같은 조건.
            // `PathFollowState` 가 없어 이동 루프에는 안 잡힌다.
            var d = _em.CreateEntity();
            _em.AddComponentData(d, LocalTransform.FromPosition(float3.zero));
            _em.AddComponentData(d, new FactionTag { value = Faction.DefenderUnit });
            _em.AddComponentData(d, new Health { value = 10f, max = 10f });
        }

        static int Idx(int x, int y) => y * N + x;

        [TearDown]
        public void TearDown()
        {
            // 핸들로 직접 지운다 — `SetUp` 이 중도에 던지면 엔티티가 없어도 배열은 살아 있다.
            if (_gFlow.IsCreated) _gFlow.Dispose();
            if (_gDist.IsCreated) _gDist.Dispose();
            if (_hFlow.IsCreated) _hFlow.Dispose();
            if (_hDist.IsCreated) _hDist.Dispose();
            _world?.Dispose();
        }

        Entity Hunter(bool withAttack = true)
        {
            var e = _em.CreateEntity();
            _em.AddComponentData(e, LocalTransform.FromPosition(new float3(EnemyXZ, 0f, EnemyXZ)));
            // ⚠ `radius` 를 **프로덕션 값**으로 준다(`BattleBridge:2313` agentRadiusTiles).
            // 0 이면 `AgentCollision:28` 이 레거시 **점 충돌**로 빠져 셀-경계 clamp 를 타고,
            // 그러면 「실제 ECS 월드」인데 충돌 경로만 프로덕션과 다른 테스트가 된다.
            _em.AddComponentData(e, new PathFollowState { speed = 2f, radius = 0.25f });
            _em.AddComponent<DefenderHunterTag>(e);
            // enemy-detection-range unit 3 — **사냥 게이트가 태그에서 감지로 옮겨갔다.**
            // 태그만으로는 더 이상 `hunting` 이 서지 않는다(`MovementSystem` 은 이제
            // `DetectedTarget.hunting` 을 읽는다). 이 픽스처가 고정하려는 것은 **이동 보정의
            // 게이트**이지 감지 판정이 아니므로, 「지금 사냥 중」을 새 어휘로 그대로 표현한다.
            // `DetectionRange` 는 무제한(-1) — 그래야 leak-proof 가 옛 헌터와 같이 걸린다.
            _em.AddComponentData(e, new DetectionRange { tiles = -1f });
            _em.AddComponentData(e, new DetectedTarget { target = Entity.Null, hunting = 1 });
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

        // 보정은 지배축 cardinal 이고 (1.45, 1.45) 는 동률이라 x 가 이긴다(`CloseInCardinals`).

        // ── 기준선: 보정이 실제로 돈다 ──────────────────────────────────────
        [Test]
        public void Hunter_AtFiringCell_ClosesIn()
        {
            var e = Hunter();
            Tick();
            Assert.Less(X(e), EnemyXZ - 1e-4f,
                "대각 소스 칸(실거리 2.05칸 > 도달 1.5칸)에 섰는데 안 움직인다. " +
                "필드는 도착이라 하고 사거리는 밖이라 한다 — 보정이 없으면 발사 0 + 이동 0 = 영구 동결.");
        }

        // ── C-1 회귀: 잠긴 헌터는 안 움직인다 ────────────────────────────────
        [TestCase(CcKind.Sleep, TestName = "수면")]
        [TestCase(CcKind.Stun,  TestName = "스턴")]
        public void LockedHunter_DoesNotCloseIn(CcKind kind)
        {
            var e = Hunter();
            var cc = _em.AddBuffer<CcEffect>(e);
            // `remainingTime` 은 판정에 안 든다 — `CcActionLock.IsLocked` 는 `kind` 만 본다.
            cc.Add(new CcEffect { kind = kind, remainingTime = 5f });
            Tick();
            Assert.AreEqual(EnemyXZ, X(e), 1e-5f,
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
            Assert.AreEqual(EnemyXZ, X(e), 1e-5f,
                "도약 비행 중인데 보정이 위치를 덮어썼다 — 그 위치의 소유자는 도약 시스템이다");
        }

        // ── M-2 회귀: 술어를 안 지난 Marching 은 보정 근거가 못 된다 ──────────
        [Test]
        public void HunterWithoutAttackState_DoesNotCloseIn()
        {
            var e = Hunter(withAttack: false);
            Tick();
            Assert.AreEqual(EnemyXZ, X(e), 1e-5f,
                "`AttackState` 가 없으면 EnemyAiStateSystem 은 사거리 술어를 **한 번도 안 부른다** — " +
                "그 Marching 은 「사거리 밖」이 아니라 「물어보지도 않았다」다");
        }

        // ── 게이트: 헌터 태그가 없으면 사냥 분기 자체가 아니다 ────────────────
        [Test]
        public void NonHunter_DoesNotCloseIn()   // ⚠ 격리 안 됨 — 아래 참조
        {
            var e = Hunter();
            _em.RemoveComponent<DefenderHunterTag>(e);
            Tick();
            Assert.AreEqual(EnemyXZ, X(e), 1e-5f,
                "헌터가 아닌데 사냥 보정이 돌았다. ⚠ 이 케이스는 **두 항이 동시에** 깨진다 — " +
                "태그를 떼면 `hunting` 이 false 가 되고, 헌터가 0기라 `huntTargets` 도 빈다. " +
                "`hunting` 항 단독 격리는 아니다.");
        }
    }
}
