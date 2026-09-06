using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Wassup.Battle.Combat;
using Wassup.Battle.Effects;
using Wassup.Battle.Movement;
using Wassup.Battle.Units;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // enemy-detection-range unit 8 — **규칙 2단계**: 「그 적을 향해 갈 수 있는 이동 경로가 있나」.
    //
    // 감지의 규칙은 적의 타입과 무관하다:
    //
    //     내 감지 반경 안에 적이 있고, 그 적을 향해 갈 수 있는 이동 경로가 있으면 그쪽으로 간다.
    //     없으면 원래 가던 길로 간다.
    //
    // units 1~6 은 1·3단계만 구현하고 2단계를 **공용 사냥판**에 위임했다. 그 필드는 다른 질문에
    // 답한다 — 「**아무** 방어유닛의 사격 칸까지, **지상** 통행으로」. 그래서 ⑴ 도착지가 감지
    // 대상과 실측 5.0% 갈렸고 ⑵ **비행이 벽 위에서 조용히 죽었다**(그게 「비행은 감지 대상 밖」
    // 으로 오독됐다). 이 파일이 그 두 결함의 회귀를 막는다.
    //
    // ★ **`비행은_벽_너머_방어유닛을_감지한다`가 이 unit 의 본체다.** 그 짝인 지상 테스트와
    // 같이 읽어야 한다 — 둘의 차이는 `traversalLayers` **한 바이트뿐**이고, 그것이
    // 「비행에 분기가 없다」의 증거다.
    public class DetectionChaseFieldTests
    {
        private const int W = 9;
        private const int H = 5;
        private const int WallX = 4;      // 이 열은 지상이 못 지난다(Air 비트만 연다)
        private const float Dt = 1f / 60f;

        private World _world;
        private EntityManager _em;
        private SimulationSystemGroup _sim;
        private FlowFieldSingleton _field;

        [SetUp]
        public void SetUp()
        {
            _world = new World("DetectionChaseFieldTestWorld");
            _em = _world.EntityManager;
            _sim = _world.CreateSystemManaged<SimulationSystemGroup>();
            _sim.AddSystemToUpdateList(_world.CreateSystem<DetectionSystem>());

            int n = W * H;
            var walkMask = new NativeArray<byte>(n, Allocator.Persistent);
            var cellLayers = new NativeArray<byte>(n, Allocator.Persistent);
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int i = y * W + x;
                    // 벽 열은 **비행만** 연다. 「지상 벽 = 비행 통로」가 이 맵의 전부다.
                    cellLayers[i] = x == WallX
                        ? (byte)PlacementLayer.Air
                        : (byte)(PlacementLayer.Path | PlacementLayer.Air);
                    walkMask[i] = (byte)(x == WallX ? 0 : 1);
                }

            _field = new FlowFieldSingleton
            {
                flow = new NativeArray<float2>(n, Allocator.Persistent),
                dist = new NativeArray<int>(n, Allocator.Persistent),
                walkMask = walkMask,
                cellLayers = cellLayers,
                gridSize = new int2(W, H),
                tileSize = 1f,
                origin = float3.zero,
            };

            var fieldEntity = _em.CreateEntity();
            _em.AddComponentData(fieldEntity, _field);
        }

        [TearDown]
        public void TearDown()
        {
            _field.flow.Dispose();
            _field.dist.Dispose();
            _field.walkMask.Dispose();
            _field.cellLayers.Dispose();
            if (_world != null && _world.IsCreated) _world.Dispose();
        }

        private void Step(int frames = 1)
        {
            for (int i = 0; i < frames; i++)
            {
                _world.SetTime(new Unity.Core.TimeData(_world.Time.ElapsedTime + Dt, Dt));
                _sim.Update();
            }
        }

        // ⚠ 셀 중심은 **정수 좌표**다(`GridMath.WorldToCell` 이 `floor(x + 0.5)`).
        private Entity Defender(int x, int y, int simId)
        {
            var e = _em.CreateEntity();
            _em.AddComponentData(e, new Health { value = 100f, max = 100f });
            _em.AddComponentData(e, new FactionTag { value = Faction.DefenderUnit });
            _em.AddComponentData(e, LocalTransform.FromPosition(new float3(x, 0f, y)));
            _em.AddComponentData(e, new HitRadius { value = 0f });
            _em.AddComponentData(e, new SimEntityId { value = simId });
            return e;
        }

        // `layers` 하나만 다른 적 둘을 만드는 것이 이 파일의 요점이다.
        private Entity Enemy(int x, int y, float detectionRange, PlacementLayer layers)
        {
            var e = _em.CreateEntity();
            _em.AddComponentData(e, new Health { value = 50f, max = 50f });
            _em.AddComponentData(e, new FactionTag { value = Faction.EnemyUnit });
            _em.AddComponentData(e, LocalTransform.FromPosition(new float3(x, 0f, y)));
            _em.AddComponentData(e, new HitRadius { value = 0f });
            _em.AddComponentData(e, new SimEntityId { value = 100 });
            _em.AddComponentData(e, new EnemyAiState { value = AiState.Marching });
            _em.AddComponentData(e, new PathFollowState
            {
                speed = 1f, radius = 0.25f, traversalLayers = (byte)layers,
            });
            _em.AddComponentData(e, new DetectionRange { tiles = detectionRange });
            _em.AddComponentData(e, new DetectedTarget { target = Entity.Null });
            _em.AddComponentData(e, new EnemyTargetFilter
            {
                classMask = -1, priorityClass = -1, factionMask = (int)Faction.DefenderUnit,
            });
            _em.AddComponentData(e, new AttackState
            {
                range = 1f, targetMask = (int)Faction.DefenderUnit,
            });
            return e;
        }

        private DetectedTarget Detected(Entity e) => _em.GetComponentData<DetectedTarget>(e);
        private bool HasChase(Entity e) => _em.HasBuffer<DetectionChaseDist>(e);

        // ── 층 무관성 — 이 unit 의 본체 ──────────────────────────────────────────

        [Test]
        public void 비행은_벽_너머_방어유닛을_감지한다()
        {
            var d = Defender(6, 2, simId: 1);
            var e = Enemy(2, 2, detectionRange: 6f, layers: PlacementLayer.Air);
            Step();

            var got = Detected(e);
            Assert.AreEqual(1, got.hunting,
                "비행은 벽 열을 지날 수 있으므로 「갈 수 있는 경로」가 있다 — 감지가 성립해야 한다. " +
                "빨간색이면 추격판이 다시 지상 마스크로 구워지고 있다(계약 13 위반).");
            Assert.AreEqual(d, got.target, "발견한 대상이 그 방어유닛이어야 한다");
            Assert.IsTrue(HasChase(e), "대상 지향 추격판이 부착돼야 한다");
        }

        [Test]
        public void 지상은_벽_너머_방어유닛에게_가지_않는다()
        {
            Defender(6, 2, simId: 1);
            var e = Enemy(2, 2, detectionRange: 6f, layers: PlacementLayer.Path);
            Step();

            var got = Detected(e);
            Assert.AreEqual(0, got.hunting,
                "지상은 벽 열을 못 지나므로 「갈 수 있는 경로」가 없다 — 원래 가던 길로 가야 한다");
            Assert.IsFalse(HasChase(e), "경로가 없으면 추격판을 붙이지 않는다");
        }

        // 위 두 테스트의 차이가 `traversalLayers` 한 바이트뿐임을 못박는다. 「비행 전용 분기」가
        // 생기면(예: `if (layers == Air) …`) 이 단언은 통과해도 계약 13 은 깨지므로, 여기서는
        // **반경 안에 있다는 사실 자체는 둘 다 같다**는 것만 고정한다.
        [Test]
        public void 지상도_비행도_반경_판정_자체는_같다()
        {
            Defender(6, 2, simId: 1);
            var air = Enemy(2, 2, detectionRange: 6f, layers: PlacementLayer.Air);
            var ground = Enemy(2, 3, detectionRange: 6f, layers: PlacementLayer.Path);
            Step();

            Assert.AreEqual(1, Detected(air).hunting, "비행: 반경 안 + 경로 있음");
            Assert.AreEqual(0, Detected(ground).hunting, "지상: 반경 안이지만 경로 없음");
            // 반경을 0.5 로 줄이면 **둘 다** 안 걸린다 — 반경 판정이 층을 안 본다는 뜻.
            var airNear = Enemy(2, 1, detectionRange: 0.5f, layers: PlacementLayer.Air);
            Step();
            Assert.AreEqual(0, Detected(airNear).hunting,
                "반경 밖이면 비행이어도 안 걸린다 — 반경 판정은 층과 무관하다");
        }

        // ── 후보 탐침 — 「최근접이 못 가면 다음」 ────────────────────────────────

        [Test]
        public void 최근접이_도달_불가면_다음_후보를_잡는다()
        {
            var far = Defender(0, 2, simId: 1);     // 3칸, 같은 쪽 — 갈 수 있다
            var near = Defender(5, 2, simId: 2);    // 2칸, 벽 너머 — 못 간다
            var e = Enemy(3, 2, detectionRange: 6f, layers: PlacementLayer.Path);
            Step();

            var got = Detected(e);
            Assert.AreEqual(1, got.hunting, "갈 수 있는 후보가 하나라도 있으면 감지가 성립한다");
            Assert.AreEqual(far, got.target,
                "최근접(벽 너머)은 못 가므로 그 다음 후보를 잡아야 한다. " +
                $"near={near.Index} 를 잡았다면 탐침이 경로를 안 물어본 것이다.");
        }

        [Test]
        public void 후보_전부_도달_불가면_원래_가던_길로_간다()
        {
            Defender(5, 2, simId: 1);
            Defender(6, 1, simId: 2);
            var e = Enemy(2, 2, detectionRange: 8f, layers: PlacementLayer.Path);
            Step();

            Assert.AreEqual(0, Detected(e).hunting, "전부 벽 너머 — 규칙 3단계로 내려가야 한다");
        }

        // ── 무제한은 이 레인에 오지 않는다(무회귀) ──────────────────────────────

        [Test]
        public void 무제한_감지는_추격판을_굽지_않는다()
        {
            Defender(6, 2, simId: 1);
            var e = Enemy(2, 2, detectionRange: -1f, layers: PlacementLayer.Path);
            Step();

            var got = Detected(e);
            Assert.AreEqual(1, got.hunting, "무제한은 반경도 경로도 안 묻는다(계약 12)");
            Assert.IsFalse(HasChase(e),
                "무제한의 진짜 질문은 「아무 방어유닛이나」라 공용 사냥판이 정확한 답이다 — " +
                "대상 지향 추격판을 붙이면 보스 거동이 바뀐다");
        }

        // ── 캐시 무효화 ──────────────────────────────────────────────────────────

        [Test]
        public void 장애물_변경은_추격판을_다시_굽게_한다()
        {
            Defender(6, 2, simId: 1);
            var e = Enemy(5, 2, detectionRange: 3f, layers: PlacementLayer.Path);
            Step();
            Assert.AreEqual(1, Detected(e).hunting, "사전 조건: 같은 쪽이라 감지가 걸린다");

            // 필드 시그니처를 바꾼다 = 「장애물 배치가 달라졌다」.
            var q = _em.CreateEntityQuery(typeof(FlowFieldSingleton));
            var fe = q.GetSingletonEntity();
            var f = _em.GetComponentData<FlowFieldSingleton>(fe);
            f.blockedSignature = 12345u;
            _em.SetComponentData(fe, f);
            Step();

            Assert.AreEqual(12345u, Detected(e).chaseSignature,
                "시그니처가 바뀌면 다시 구워야 한다 — 안 그러면 낡은 경로를 따라 얼어붙는다");
        }

        [Test]
        public void 대상이_바뀌면_추격판을_다시_굽는다()
        {
            var first = Defender(5, 2, simId: 1);
            var e = Enemy(6, 2, detectionRange: 2f, layers: PlacementLayer.Path);
            Step();
            Assert.AreEqual(first, Detected(e).chaseBuiltFor, "첫 대상으로 구웠다");

            // 첫 대상을 없애고 다른 대상을 둔다.
            _em.DestroyEntity(first);
            var second = Defender(7, 2, simId: 2);
            Step();

            Assert.AreEqual(second, Detected(e).target, "새 대상을 잡아야 한다");
            Assert.AreEqual(second, Detected(e).chaseBuiltFor,
                "추격판이 새 대상 기준으로 다시 구워져야 한다 — 옛 대상 자리로 계속 가면 안 된다");
        }
    }
}
