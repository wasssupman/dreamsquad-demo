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
    // enemy-detection-range 계약 5·9 — **감지는 유출/공성 전환을 건드리지 않는다.**
    //
    // ★이 파일이 지키는 것이 이 spec 에서 가장 위험한 계약이다. 골 도달은
    // `GoalReachedEvent` → 마음 HP → `StressMath` → 스트레스 100 = 판 종료로 이어지는
    // **이 게임의 유일한 패배 통로**라, 감지가 그 통로의 조절기가 되면 안 된다.
    //
    // ⚠ 실제로 두 번 틀렸다 — 그래서 `DetectedTarget` 수준 테스트로는 부족하다:
    //   ① 초판은 `leakProof` 를 아예 안 만들고 `hunting` 을 그대로 골 게이트에 썼다
    //      → **유한 반경 감지 적이 골 칸을 밟아도 공성 전환을 안 했다.**
    //   ② 2판은 `leakProof = hunting && Unlimited` 로 썼다 → `hunting` 이 감지 타이머
    //      (관성·막힘 해제·억제)로 꺼지는 틈에 **무제한 사냥꾼이 골을 유출**했다.
    //      `Enemy_DreamShard` 는 비보스라 CC 면역이 없어 자장가 한 번으로 그 틈이 열린다.
    //
    // 그래서 여기서는 `MovementSystem` 자체를 돌리고 `PastGoalTag` 부착을 직접 본다.
    public class DetectionLeakProofTests
    {
        private const int W = 8;
        private const int H = 6;
        private const int GoalX = 4;
        private const int GoalY = 3;

        private World _world;
        private EntityManager _em;
        private SimulationSystemGroup _sim;
        private Entity _fieldEntity;
        private FlowFieldSingleton _goalField;
        private DefenderFieldSingleton _huntField;

        [SetUp]
        public void SetUp()
        {
            _world = new World("DetectionLeakProofTestWorld");
            _em = _world.EntityManager;
            _sim = _world.CreateSystemManaged<SimulationSystemGroup>();
            _sim.AddSystemToUpdateList(_world.CreateSystem<MovementSystem>());

            int n = W * H;
            var walkMask = new NativeArray<byte>(n, Allocator.Persistent);
            for (int i = 0; i < n; i++) walkMask[i] = 1;

            _goalField = new FlowFieldSingleton
            {
                flow = new NativeArray<float2>(n, Allocator.Persistent),
                dist = new NativeArray<int>(n, Allocator.Persistent),
                walkMask = walkMask,
                gridSize = new int2(W, H),
                // goals 를 안 채우면 `IsGoalCell` 이 이 폴백을 본다(픽스처 관용구).
                goalCell = new int2(GoalX, GoalY),
                tileSize = 1f,
                origin = float3.zero,
            };

            // 사냥 필드는 **전 셀 도달 가능**으로 둔다 — leak-proof 술어의
            // `dist != int.MaxValue` 항이 참이어야 「무제한은 안 샌다」를 실제로 시험한다.
            _huntField = new DefenderFieldSingleton
            {
                flow = new NativeArray<float2>(n, Allocator.Persistent),
                dist = new NativeArray<int>(n, Allocator.Persistent),
                gridSize = new int2(W, H),
                tileSize = 1f,
                origin = float3.zero,
            };
            for (int i = 0; i < n; i++) _huntField.dist[i] = 0;

            _fieldEntity = _em.CreateEntity();
            _em.AddComponentData(_fieldEntity, _goalField);
            _em.AddComponentData(_fieldEntity, _huntField);
        }

        [TearDown]
        public void TearDown()
        {
            _goalField.flow.Dispose();
            _goalField.dist.Dispose();
            _goalField.walkMask.Dispose();
            _huntField.Dispose();
            if (_world != null && _world.IsCreated) _world.Dispose();
        }

        // 골 칸 위에 선 적. `hunting` 은 손으로 세운다 — 이 파일의 관심사는 감지 «판정» 이
        // 아니라 그 결과가 골 게이트를 어떻게 통과하느냐다.
        private Entity EnemyOnGoal(float detectionRange, byte hunting)
        {
            var e = _em.CreateEntity();
            // ⚠ 셀 중심은 **정수 좌표**다 — `GridMath.WorldToCell` 이 `floor(x + 0.5)` 를 쓴다.
            // `+0.5f` 를 더하면 한 칸 밀려서 골 칸을 안 밟는다(이 픽스처가 처음에 그렇게 틀렸다).
            _em.AddComponentData(e, LocalTransform.FromPosition(new float3(GoalX, 0f, GoalY)));
            _em.AddComponentData(e, new PathFollowState { speed = 1f, radius = 0.25f });
            _em.AddComponentData(e, new EnemyAiState { value = AiState.Marching });
            _em.AddComponentData(e, new DetectionRange { tiles = detectionRange });
            _em.AddComponentData(e, new DetectedTarget { target = Entity.Null, hunting = hunting });
            _em.AddComponent<DefenderHunterTag>(e);
            return e;
        }

        private void Tick()
        {
            _world.SetTime(new TimeData(_world.Time.ElapsedTime + 0.2f, 0.2f));
            _sim.Update();
        }

        private bool PastGoal(Entity e) => _em.HasComponent<PastGoalTag>(e);

        // ── 유한 반경 = 오늘과 같이 공성 전환한다 ────────────────────────────────

        [Test]
        public void 유한_감지_적은_사냥_중에도_골에서_공성_전환한다()
        {
            var e = EnemyOnGoal(detectionRange: 3f, hunting: 1);
            Tick();
            Assert.IsTrue(PastGoal(e),
                "유한 반경 감지가 골 전환을 막았다 — 감지가 이 게임의 유일한 패배 통로의 " +
                "조절기가 된다(계약 5·9)");
        }

        [Test]
        public void 감지가_꺼진_적도_골에서_공성_전환한다()
        {
            var e = EnemyOnGoal(detectionRange: 3f, hunting: 0);
            Tick();
            Assert.IsTrue(PastGoal(e), "감지와 무관하게 골 전환은 일어나야 한다");
        }

        // ── 무제한 = 오늘 보스 거동(leak-proof) ──────────────────────────────────

        [Test]
        public void 무제한_사냥꾼은_골을_밟아도_공성_전환하지_않는다()
        {
            var e = EnemyOnGoal(detectionRange: -1f, hunting: 1);
            Tick();
            Assert.IsFalse(PastGoal(e),
                "무제한 사냥은 「전멸시켜야 골에 간다」가 저작된 성질이다(boss-defender-field)");
        }

        // ★ H2 회귀 가드 — `leakProof` 를 `hunting` 에 묶으면 여기가 빨개진다.
        //   감지 타이머(관성 만료·막힘 해제·억제)로 `hunting` 이 0 이 되는 틈은 실제로 열리고,
        //   `Enemy_DreamShard` 는 비보스라 CC 면역이 없어 자장가 한 번이면 충분하다.
        [Test]
        public void 무제한_사냥꾼은_감지가_꺼진_틈에도_유출하지_않는다()
        {
            var e = EnemyOnGoal(detectionRange: -1f, hunting: 0);
            Tick();
            Assert.IsFalse(PastGoal(e),
                "leak-proof 가 `hunting` 에 묶여 있다 — 감지 타이머가 꺼지는 틈에 " +
                "무제한 사냥꾼이 골을 유출한다(리뷰 H2)");
        }

        // ── 계약 13 · 이동 소스가 실제로 갈아타나 ────────────────────────────────
        //
        // ★ 리뷰 H4 — `DetectionLeakProofTests` 의 나머지와 `HuntCloseInLockTests` 는 **게이트의
        // 부작용**(골 전환 · 접근 보정)만 본다. 「게이트가 흐름장 **소스를 갈아탄다**」는 아무도
        // 안 봤고, 그러면 `MovementSystem` 의 `if (hunting) … else if (waypoint) …` 순서가
        // 뒤집혀도 전부 초록이다. 여기서 두 흐름장을 **반대 방향**으로 깔고 실제 변위 부호를 본다.

        private Entity EnemyAt(int x, int y, float detectionRange, byte hunting, AiState ai = AiState.Marching)
        {
            var e = _em.CreateEntity();
            _em.AddComponentData(e, LocalTransform.FromPosition(new float3(x, 0f, y)));
            _em.AddComponentData(e, new PathFollowState { speed = 2f, radius = 0.25f });
            _em.AddComponentData(e, new EnemyAiState { value = ai });
            _em.AddComponentData(e, new DetectionRange { tiles = detectionRange });
            _em.AddComponentData(e, new DetectedTarget { target = Entity.Null, hunting = hunting });
            _em.AddComponent<DefenderHunterTag>(e);
            return e;
        }

        // 골 흐름장 = +X, 사냥판 = −X. 부호 하나로 「어느 소스를 탔나」가 읽힌다.
        private void SetOpposingFlows()
        {
            for (int i = 0; i < _goalField.flow.Length; i++) { _goalField.flow[i] = new float2(1f, 0f); _goalField.dist[i] = 5; }
            for (int i = 0; i < _huntField.flow.Length; i++) { _huntField.flow[i] = new float2(-1f, 0f); _huntField.dist[i] = 5; }
        }

        private float X(Entity e) => _em.GetComponentData<LocalTransform>(e).Position.x;

        [Test]
        public void 감지가_꺼진_적은_골_흐름장을_따른다()
        {
            SetOpposingFlows();
            var e = EnemyAt(2, 2, detectionRange: 3f, hunting: 0);
            Tick();
            Assert.Greater(X(e), 2f, "감지가 꺼졌으면 골 흐름장(+X)을 따라야 한다");
        }

        [Test]
        public void 감지가_켜진_적은_사냥판을_따른다()
        {
            SetOpposingFlows();
            var e = EnemyAt(2, 2, detectionRange: 3f, hunting: 1);
            Tick();
            Assert.Less(X(e), 2f, "감지가 켜졌으면 사냥판(−X)을 따라야 한다 — 게이트가 소스를 안 갈아탔다");
        }

        // 계약 2 의 **이동 쪽 절반** — 어그로가 감지를 이긴다. `Chasing` 분기가 사냥 분기보다
        // 위에 있어 먼저 `continue` 하므로, 추격판이 없는 이 픽스처에서는 **움직이지 않는다.**
        // 순서가 뒤집히면 사냥판(−X)을 타고 움직여 여기가 빨개진다.
        [Test]
        public void 어그로가_감지를_이긴다_이동도()
        {
            SetOpposingFlows();
            var e = EnemyAt(2, 2, detectionRange: 3f, hunting: 1, ai: AiState.Chasing);
            Tick();
            Assert.AreEqual(2f, X(e), 1e-5f,
                "어그로(Chasing) 가 사냥 분기보다 먼저 continue 해야 한다(계약 2·13)");
        }

        // 사냥 필드가 도달 불가면 무제한이어도 옛 술어대로 유출한다 — 옛 동작 보존.
        [Test]
        public void 사냥_필드가_도달_불가면_무제한도_공성_전환한다()
        {
            for (int i = 0; i < _huntField.dist.Length; i++) _huntField.dist[i] = int.MaxValue;
            var e = EnemyOnGoal(detectionRange: -1f, hunting: 1);
            Tick();
            Assert.IsTrue(PastGoal(e),
                "방어유닛이 전멸(도달 불가)하면 사냥꾼도 골로 간다 — DefenderFieldSystem 계약 5");
        }
    }
}
