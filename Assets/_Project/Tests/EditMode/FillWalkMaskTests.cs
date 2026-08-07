using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using Wassup.Battle.Effects;
using Wassup.Battle.Movement;

namespace Wassup.Tests.EditMode
{
    // summon-patrol-defender — walk 마스크 대량 생성. AggroStateSystem 과 PatrolFieldSystem 이
    // 문자 그대로 같은 루프를 복제하고 있던 것을 MovementCellTrim 으로 모았다.
    // 마스크가 틀리면 **이동 전체가 깨지므로**(BFS 목적지·도달 판정의 입력) sim-critical 이다.
    public class FillWalkMaskTests
    {
        private const int W = 6;
        private const int H = 6;
        private static readonly int2 Grid = new int2(W, H);
        private static readonly int2 Goal = new int2(5, 5);

        private NativeArray<float2> _flow;
        private NativeArray<int> _dist;
        private NativeArray<byte> _walk;   // unit 2 — 벽의 정본
        private NativeArray<byte> _mask;
        private NativeHashSet<int2> _blocked;

        [SetUp]
        public void SetUp()
        {
            int n = W * H;
            _flow = new NativeArray<float2>(n, Allocator.Persistent);
            _dist = new NativeArray<int>(n, Allocator.Persistent);
            _walk = new NativeArray<byte>(n, Allocator.Persistent);
            _mask = new NativeArray<byte>(n, Allocator.Persistent);
            _blocked = new NativeHashSet<int2>(8, Allocator.Persistent);
            // 기본: 전 셀 Walk 타일 → 벽 아님.
            // continuous-agent-movement unit 2 — 벽의 근거가 flow 에서 walkMask 로 바뀌었다.
            // flow 는 벽 판정에 더 이상 쓰이지 않지만, "flow 가 있어도 마스크가 이긴다"를
            // 보이기 위해 채워둔 채로 둔다.
            for (int i = 0; i < n; i++) { _flow[i] = new float2(1f, 0f); _walk[i] = 1; }
        }

        [TearDown]
        public void TearDown()
        {
            if (_flow.IsCreated) _flow.Dispose();
            if (_dist.IsCreated) _dist.Dispose();
            if (_walk.IsCreated) _walk.Dispose();
            if (_mask.IsCreated) _mask.Dispose();
            if (_blocked.IsCreated) _blocked.Dispose();
        }

        private FlowFieldSingleton Field() => new FlowFieldSingleton
        {
            flow = _flow,
            dist = _dist,
            walkMask = _walk,
            gridSize = Grid,
            goalCell = Goal,
            tileSize = 1f,
            version = 1,
        };

        private byte At(int2 cell) => _mask[GridMath.CellIndex(cell, Grid)];

        [Test]
        public void All_Walk_Tiles_Are_Walkable()
        {
            MovementCellTrim.FillWalkMask(Field(), false, default, _mask);

            for (int i = 0; i < _mask.Length; i++)
                Assert.AreEqual((byte)1, _mask[i], $"index {i} 는 walkable 이어야 한다");
        }

        [Test]
        public void NonWalk_Tile_Is_Wall()
        {
            var wall = new int2(2, 3);
            _walk[GridMath.CellIndex(wall, Grid)] = 0;

            MovementCellTrim.FillWalkMask(Field(), false, default, _mask);

            Assert.AreEqual((byte)0, At(wall));
            Assert.AreEqual((byte)1, At(new int2(2, 2)), "이웃은 영향 없어야 한다");
        }

        [Test]
        public void Goal_Cell_Is_Walkable()
        {
            // 골은 Walk 타일이므로 마스크에서 이미 통행 가능이다. unit 1 까지는 flow=0 을
            // 우회하는 명시적 골 예외가 이 결과를 만들었고, unit 2 부터는 예외가 불필요하다.
            // 적/순찰병이 골 셀에서 clamp 되지 않아야 한다는 계약은 그대로다.
            _flow[GridMath.CellIndex(Goal, Grid)] = float2.zero;

            MovementCellTrim.FillWalkMask(Field(), false, default, _mask);

            Assert.AreEqual((byte)1, At(Goal));
        }

        [Test]
        public void Isolated_Walk_Tile_Is_Walkable()
        {
            // unit 2 의 유일한 의미 변화: 도달 불가(flow=0)여도 Walk 타일이면 통행 가능.
            // D1-b 에서 봉쇄로 끊긴 구역 전체가 벽이 되는 사고를 막는 성질이다.
            var isolated = new int2(1, 1);
            _flow[GridMath.CellIndex(isolated, Grid)] = float2.zero;

            MovementCellTrim.FillWalkMask(Field(), false, default, _mask);

            Assert.AreEqual((byte)1, At(isolated), "flow 는 더 이상 벽을 정하지 않는다");
        }

        [Test]
        public void Obstacle_Cell_Is_Wall_When_Obstacles_Present()
        {
            var blocked = new int2(4, 1);
            _blocked.Add(blocked);

            MovementCellTrim.FillWalkMask(Field(), true, new ObstacleSingleton { blockedCells = _blocked }, _mask);

            Assert.AreEqual((byte)0, At(blocked));
            Assert.AreEqual((byte)1, At(new int2(3, 1)));
        }

        [Test]
        public void Obstacles_Ignored_When_Flag_Is_False()
        {
            var blocked = new int2(4, 1);
            _blocked.Add(blocked);

            MovementCellTrim.FillWalkMask(Field(), false, new ObstacleSingleton { blockedCells = _blocked }, _mask);

            Assert.AreEqual((byte)1, At(blocked), "hasObstacles=false 면 장애물을 보지 않는다");
        }

        [Test]
        public void Composition_Is_Mask_And_Obstacles_Cell_By_Cell()
        {
            // 마스크 생성이 술어와 어긋나지 않음을 전 셀에서 확인 — 인덱싱/스트라이드 사고를
            // 잡는 계약이다. 기대값은 NavGrid 를 다시 부르지 않고 **독립적으로** 계산한다.
            _walk[GridMath.CellIndex(new int2(1, 1), Grid)] = 0;
            _walk[GridMath.CellIndex(new int2(0, 4), Grid)] = 0;
            _flow[GridMath.CellIndex(new int2(5, 0), Grid)] = float2.zero;  // flow 는 무관해야 한다
            _blocked.Add(new int2(3, 3));
            var obstacles = new ObstacleSingleton { blockedCells = _blocked };

            MovementCellTrim.FillWalkMask(Field(), true, obstacles, _mask);

            for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                var cell = new int2(x, y);
                bool expectedWall = _walk[GridMath.CellIndex(cell, Grid)] == 0
                                    || _blocked.Contains(cell);
                Assert.AreEqual(expectedWall ? (byte)0 : (byte)1, At(cell), $"cell {cell}");
            }
        }
    }
}
