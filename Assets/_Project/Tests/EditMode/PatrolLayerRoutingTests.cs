using NUnit.Framework;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Wassup.Battle.Effects;
using Wassup.Battle.Movement;
using Wassup.Bridge;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // traversal-layers unit 4 — **시스템 경로** 검증.
    //
    // unit 3 은 `PatrolFieldSystem` 을 바꿨는데 테스트는 헬퍼(`MovementCellTrim.FillWalkMask`)
    // 까지만 있었다. 그건 1b 회귀 때 나를 문 것과 같은 구조다 — 헬퍼는 맞는데 경로는
    // 안 본 것. rev 3 계약 7("«행동 변화 0» 은 라이브 경로 테스트가 있을 때만")을 이행한다.
    //
    // 맵 6x1: 셀 0·1·2 = Walk(Path 층), 셀 3·4·5 = Place(Ground 층).
    // 두 층이 **가로로 이웃**하므로, 마스크를 틀리게 먹이면 유닛이 남의 층으로 넘어간다.
    public class PatrolLayerRoutingTests
    {
        private World _world;
        private EntityManager _em;
        private SimulationSystemGroup _simGroup;
        private SimFieldHandles _handles;
        private GeneratedMap _map;

        [SetUp]
        public void SetUp()
        {
            _world = new World("PatrolLayerRoutingTests");
            _em = _world.EntityManager;
            _simGroup = _world.CreateSystemManaged<SimulationSystemGroup>();
            _simGroup.AddSystemToUpdateList(_world.CreateSystem<PatrolFieldSystem>());
            _handles = default;
            _map = MakeMap();
            SimFieldInstaller.InstallNavFields(_em, in _map, 1f, float3.zero, ref _handles);
        }

        [TearDown]
        public void TearDown()
        {
            SimFieldInstaller.Teardown(_world, _em, ref _handles);
            _map.Dispose();
            _world?.Dispose();
        }

        private static GeneratedMap MakeMap()
        {
            const int n = 6;
            var tiles = new NativeArray<MapTileType>(n, Allocator.Persistent);
            for (int i = 0; i < 3; i++) tiles[i] = MapTileType.Walk;
            for (int i = 3; i < 6; i++) tiles[i] = MapTileType.Place;
            var goals = new NativeArray<int2>(1, Allocator.Persistent);
            goals[0] = new int2(0, 0);
            var spawns = new NativeArray<int2>(1, Allocator.Persistent);
            spawns[0] = new int2(2, 0);
            return new GeneratedMap
            {
                tiles = tiles, spawns = spawns, goals = goals,
                goal = new int2(0, 0), gridSize = new int2(n, 1), generatorVersion = 1,
            };
        }

        // 순찰 유닛 — 앵커에서 떨어진 자리에 세워야 dir 이 0 이 아니다.
        private Entity MakePatrol(int atX, int anchorX, PlacementLayer layers, int tileRadius = 2)
        {
            var e = _em.CreateEntity();
            _em.AddComponentData(e, LocalTransform.FromPosition(new float3(atX, 0f, 0f)));
            _em.AddComponentData(e, new PatrolAnchor { cell = new int2(anchorX, 0), tileRadius = tileRadius });
            _em.AddComponentData(e, new PatrolStep { dir = float2.zero });
            _em.AddComponentData(e, new PathFollowState
            {
                speed = 1f, radius = 0.25f, traversalLayers = (byte)layers,
            });
            return e;
        }

        private float2 Step(Entity e) => _em.GetComponentData<PatrolStep>(e).dir;

        private void Tick()
        {
            _world.SetTime(new TimeData(_world.Time.ElapsedTime + 0.016f, 0.016f));
            _simGroup.Update();
        }

        [Test]
        public void GroundUnit_MovesInsidePlaceRegion_TowardItsAnchor()
        {
            // 지면 유닛: 셀 5 에서 앵커 4 로. 자기 층(Place = 3·4·5) 안에서만 움직인다.
            var g = MakePatrol(atX: 5, anchorX: 4, PlacementLayer.Ground);
            Tick();
            Assert.Less(Step(g).x, 0f, "앵커(4) 쪽 = -x 로 향해야 한다");
        }

        [Test]
        public void PathUnit_MovesInsideWalkRegion_TowardItsAnchor()
        {
            var p = MakePatrol(atX: 0, anchorX: 1, PlacementLayer.Path);
            Tick();
            Assert.Greater(Step(p).x, 0f, "앵커(1) 쪽 = +x");
        }

        [Test]
        public void TwoLayers_Coexist_EachStaysInItsOwn()
        {
            // ★ 이 unit 의 핵심. 한 프레임에 층이 다른 두 유닛이 각자 자기 층을 본다.
            // 한 칸 메모(builtLayers)가 틀리면 뒤 유닛이 앞 유닛의 마스크를 물려받는다.
            var g = MakePatrol(atX: 5, anchorX: 4, PlacementLayer.Ground);
            var p = MakePatrol(atX: 0, anchorX: 1, PlacementLayer.Path);
            Tick();
            Assert.Less(Step(g).x, 0f, "지면 유닛");
            Assert.Greater(Step(p).x, 0f, "경로 유닛");
        }

        [Test]
        public void TwoLayers_ReversedCreationOrder_StillCorrect()
        {
            // 메모는 «직전 층과 같으면 재사용» 이다. 생성 순서가 뒤집혀도 같아야 한다 —
            // 순서에 의존하면 청크 배치(스폰·사망 이력)에 따라 조용히 갈린다.
            var p = MakePatrol(atX: 0, anchorX: 1, PlacementLayer.Path);
            var g = MakePatrol(atX: 5, anchorX: 4, PlacementLayer.Ground);
            Tick();
            Assert.Greater(Step(p).x, 0f, "경로 유닛");
            Assert.Less(Step(g).x, 0f, "지면 유닛");
        }

        [Test]
        public void WrongLayerForAnchor_UnitCannotMove_NegativeControl()
        {
            // ★ 음성 대조군 — 위 테스트들에 **이빨이 있음**을 증명한다.
            //
            // 지면 유닛의 앵커를 경로 구역(셀 1)에 두면 그 칸이 자기 층에 없다.
            // → 영역 마스크 전부 0 → 앵커가 BFS 소스에서 탈락 → 갈 곳이 없다.
            // 층 선택이 고장나 «아무 마스크나» 쓰고 있다면 이 유닛도 움직여서 여기가 빨개진다.
            //
            // 그리고 이건 README §5 의 위험이기도 하다 — 방어유닛에 Ground 를 저작해도
            // 앵커 스냅(TryGetNearestWalkCell)이 Walk 하드코딩이라 앵커가 자기 층 밖이 된다.
            // 그 상태가 «굳음»으로 나타난다는 것을 여기 고정해 둔다.
            var g = MakePatrol(atX: 0, anchorX: 1, PlacementLayer.Ground);
            Tick();
            Assert.AreEqual(0f, Step(g).x, 1e-6f, "자기 층 밖 앵커 → 갈 곳이 없다");
            Assert.AreEqual(0f, Step(g).y, 1e-6f);
        }

        [Test]
        public void UnauthoredLayers_FallBackToPath_CurrentBehavior()
        {
            // traversalLayers = 0(미주입) → Path 로 떨어져 현행을 재현한다.
            // 이게 이 spec 전체가 «행동 변화 0» 인 근거다.
            var e = _em.CreateEntity();
            _em.AddComponentData(e, LocalTransform.FromPosition(new float3(0f, 0f, 0f)));
            _em.AddComponentData(e, new PatrolAnchor { cell = new int2(1, 0), tileRadius = 2 });
            _em.AddComponentData(e, new PatrolStep { dir = float2.zero });
            _em.AddComponentData(e, new PathFollowState { speed = 1f, radius = 0.25f });  // layers 미주입

            var authored = MakePatrol(atX: 0, anchorX: 1, PlacementLayer.Path);
            Tick();
            Assert.AreEqual(Step(authored).x, Step(e).x, 1e-5f, "미주입 = Path 저작과 동일");
        }

        [Test]
        public void NoPathFollowState_DoesNotThrow_AndFallsBack()
        {
            // 레거시·픽스처 경로. PathFollowState 자체가 없어도 시스템이 죽지 않는다.
            var e = _em.CreateEntity();
            _em.AddComponentData(e, LocalTransform.FromPosition(new float3(0f, 0f, 0f)));
            _em.AddComponentData(e, new PatrolAnchor { cell = new int2(1, 0), tileRadius = 2 });
            _em.AddComponentData(e, new PatrolStep { dir = float2.zero });

            Assert.DoesNotThrow(() => Tick());
            Assert.Greater(Step(e).x, 0f, "Path 폴백으로 앵커를 향한다");
        }
    }
}
