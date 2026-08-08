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
        private NativeArray<byte> _mask;
        private NativeHashSet<int2> _blocked;

        [SetUp]
        public void SetUp()
        {
            int n = W * H;
            _flow = new NativeArray<float2>(n, Allocator.Persistent);
            _dist = new NativeArray<int>(n, Allocator.Persistent);
            _mask = new NativeArray<byte>(n, Allocator.Persistent);
            _blocked = new NativeHashSet<int2>(8, Allocator.Persistent);
            // 기본: 전 셀에 non-zero flow → 벽 아님
            for (int i = 0; i < n; i++) _flow[i] = new float2(1f, 0f);
        }

        [TearDown]
        public void TearDown()
        {
            if (_flow.IsCreated) _flow.Dispose();
            if (_dist.IsCreated) _dist.Dispose();
            if (_mask.IsCreated) _mask.Dispose();
            if (_blocked.IsCreated) _blocked.Dispose();
        }

        private FlowFieldSingleton Field() => new FlowFieldSingleton
        {
            flow = _flow,
            dist = _dist,
            gridSize = Grid,
            goalCell = Goal,
            tileSize = 1f,
            version = 1,
        };

        private byte At(int2 cell) => _mask[GridMath.CellIndex(cell, Grid)];

        [Test]
        public void All_Flowing_Cells_Are_Walkable()
        {
            MovementCellTrim.FillWalkMask(Field(), false, default, _mask);

            for (int i = 0; i < _mask.Length; i++)
                Assert.AreEqual((byte)1, _mask[i], $"index {i} 는 walkable 이어야 한다");
        }

        [Test]
        public void Zero_Flow_Cell_Is_Wall()
        {
            var wall = new int2(2, 3);
            _flow[GridMath.CellIndex(wall, Grid)] = float2.zero;

            MovementCellTrim.FillWalkMask(Field(), false, default, _mask);

            Assert.AreEqual((byte)0, At(wall));
            Assert.AreEqual((byte)1, At(new int2(2, 2)), "이웃은 영향 없어야 한다");
        }

        [Test]
        public void Goal_Cell_Is_Walkable_Even_With_Zero_Flow()
        {
            // multi-goal-map — 골은 flow=0 이지만 벽이 아니다. 이 예외가 마스크에도 살아야
            // 적/순찰병이 골 셀에서 clamp 되지 않는다.
            _flow[GridMath.CellIndex(Goal, Grid)] = float2.zero;

            MovementCellTrim.FillWalkMask(Field(), false, default, _mask);

            Assert.AreEqual((byte)1, At(Goal));
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
        public void Matches_IsWallCell_Composition_Cell_By_Cell()
        {
            // 추출이 기존 술어와 어긋나지 않음을 전 셀에서 확인 — 이 테스트가 추출의 계약이다.
            _flow[GridMath.CellIndex(new int2(1, 1), Grid)] = float2.zero;
            _flow[GridMath.CellIndex(new int2(0, 4), Grid)] = float2.zero;
            _blocked.Add(new int2(3, 3));
            var field = Field();
            var obstacles = new ObstacleSingleton { blockedCells = _blocked };

            MovementCellTrim.FillWalkMask(field, true, obstacles, _mask);

            for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                var cell = new int2(x, y);
                bool expectedWall = MovementCellTrim.IsWallCell(cell, in field)
                                    || _blocked.Contains(cell);
                Assert.AreEqual(expectedWall ? (byte)0 : (byte)1, At(cell), $"cell {cell}");
            }
        }
    }
}
