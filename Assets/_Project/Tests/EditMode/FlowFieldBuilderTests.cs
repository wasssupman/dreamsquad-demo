using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using Wassup.Battle.Effects;

namespace Wassup.Tests.EditMode
{
    public class FlowFieldBuilderTests
    {
        // 각 테스트 NativeArray 는 try/finally 로 dispose — Assert 실패 시 leak 방지.

        [Test]
        public void Build_StraightLine_AllCellsPointToGoal()
        {
            var gridSize = new int2(5, 1);
            var walk = new NativeArray<byte>(5, Allocator.Temp);
            var flow = new NativeArray<float2>(5, Allocator.Temp);
            var dist = new NativeArray<int>(5, Allocator.Temp);
            try
            {
                for (int i = 0; i < 5; i++) walk[i] = 1;

                FlowFieldBuilder.Build(walk, gridSize, new int2(4, 0), flow, dist);

                Assert.AreEqual(0, dist[4], "goal cell dist must be 0");
                Assert.AreEqual(4, dist[0], "distance from start to goal");
                Assert.AreEqual(new float2(1, 0), flow[0], "cell 0 must point +x");
                Assert.AreEqual(new float2(1, 0), flow[3], "cell 3 must point +x");
                Assert.AreEqual(new float2(0, 0), flow[4], "goal flow must be zero");
            }
            finally { walk.Dispose(); flow.Dispose(); dist.Dispose(); }
        }

        [Test]
        public void Build_ObstacleDetour_RoutesAround()
        {
            // 3x3 grid with center obstacle:
            //  . . G     y=2
            //  . X .     y=1   X = obstacle at (1,1)
            //  S . .     y=0
            // 4-neighbor BFS from (2,2): center blocked, but two symmetric L-paths
            // exist through (1,0)→(2,0) or (0,1)→(0,2), both length 4. Path length
            // equals manhattan distance because the obstacle does not lie on the
            // L-path corners — it only blocks the illegal diagonal shortcut.
            var gridSize = new int2(3, 3);
            var walk = new NativeArray<byte>(9, Allocator.Temp);
            var flow = new NativeArray<float2>(9, Allocator.Temp);
            var dist = new NativeArray<int>(9, Allocator.Temp);
            try
            {
                for (int i = 0; i < 9; i++) walk[i] = 1;
                walk[1 * 3 + 1] = 0; // center obstacle

                FlowFieldBuilder.Build(walk, gridSize, new int2(2, 2), flow, dist);

                Assert.AreEqual(0, dist[2 * 3 + 2], "goal dist must be 0");
                Assert.AreEqual(4, dist[0], "start (0,0) routes around center obstacle via an L-path of length 4");
                Assert.AreEqual(int.MaxValue, dist[1 * 3 + 1], "obstacle cell must be unreachable");
                Assert.AreNotEqual(float2.zero, flow[0], "start cell flow must be non-zero (reachable)");
            }
            finally { walk.Dispose(); flow.Dispose(); dist.Dispose(); }
        }

        [Test]
        public void Build_Disconnected_UnreachableCellsHaveMaxDistAndZeroFlow()
        {
            // 3x1 grid, center obstacle splits left/right
            //  S X G
            var gridSize = new int2(3, 1);
            var walk = new NativeArray<byte>(3, Allocator.Temp);
            var flow = new NativeArray<float2>(3, Allocator.Temp);
            var dist = new NativeArray<int>(3, Allocator.Temp);
            try
            {
                walk[0] = 1; walk[1] = 0; walk[2] = 1;

                FlowFieldBuilder.Build(walk, gridSize, new int2(2, 0), flow, dist);

                Assert.AreEqual(0, dist[2]);
                Assert.AreEqual(int.MaxValue, dist[0], "left side unreachable from right goal");
                Assert.AreEqual(float2.zero, flow[0]);
            }
            finally { walk.Dispose(); flow.Dispose(); dist.Dispose(); }
        }

        // boss-defender-field unit 0 — multi-source BFS.

        [Test]
        public void BuildFromSources_TwoSources_EachCellPointsTowardNearest()
        {
            // 7x1 lane, sources at both ends. Midpoint (3) ties; sides drain to nearer source.
            //  A . . ? . . B
            var gridSize = new int2(7, 1);
            var walk = new NativeArray<byte>(7, Allocator.Temp);
            var flow = new NativeArray<float2>(7, Allocator.Temp);
            var dist = new NativeArray<int>(7, Allocator.Temp);
            var sources = new NativeArray<int2>(2, Allocator.Temp);
            try
            {
                for (int i = 0; i < 7; i++) walk[i] = 1;
                sources[0] = new int2(0, 0);
                sources[1] = new int2(6, 0);

                FlowFieldBuilder.BuildFromSources(walk, gridSize, sources, flow, dist);

                Assert.AreEqual(0, dist[0]);
                Assert.AreEqual(0, dist[6]);
                Assert.AreEqual(1, dist[1]);
                Assert.AreEqual(1, dist[5]);
                Assert.AreEqual(3, dist[3], "midpoint is 3 from both sources");
                Assert.AreEqual(new float2(-1, 0), flow[1], "left of midpoint drains to left source");
                Assert.AreEqual(new float2(1, 0), flow[5], "right of midpoint drains to right source");
                Assert.AreEqual(float2.zero, flow[0], "source cell flow is zero");
            }
            finally { walk.Dispose(); flow.Dispose(); dist.Dispose(); sources.Dispose(); }
        }

        [Test]
        public void BuildFromSources_NoValidSources_AllMaxDistZeroFlow()
        {
            var gridSize = new int2(3, 1);
            var walk = new NativeArray<byte>(3, Allocator.Temp);
            var flow = new NativeArray<float2>(3, Allocator.Temp);
            var dist = new NativeArray<int>(3, Allocator.Temp);
            var empty = new NativeArray<int2>(0, Allocator.Temp);
            var invalid = new NativeArray<int2>(2, Allocator.Temp);
            try
            {
                for (int i = 0; i < 3; i++) walk[i] = 1;
                walk[1] = 0;
                invalid[0] = new int2(-1, 0); // out of bounds
                invalid[1] = new int2(1, 0);  // wall cell

                FlowFieldBuilder.BuildFromSources(walk, gridSize, empty, flow, dist);
                for (int i = 0; i < 3; i++) Assert.AreEqual(int.MaxValue, dist[i], $"empty sources: dist[{i}]");

                FlowFieldBuilder.BuildFromSources(walk, gridSize, invalid, flow, dist);
                for (int i = 0; i < 3; i++)
                {
                    Assert.AreEqual(int.MaxValue, dist[i], $"invalid sources: dist[{i}]");
                    Assert.AreEqual(float2.zero, flow[i], $"invalid sources: flow[{i}]");
                }
            }
            finally { walk.Dispose(); flow.Dispose(); dist.Dispose(); empty.Dispose(); invalid.Dispose(); }
        }

        [Test]
        public void BuildFromSources_DuplicateSources_SameAsSingle()
        {
            var gridSize = new int2(4, 1);
            var walk = new NativeArray<byte>(4, Allocator.Temp);
            var flow = new NativeArray<float2>(4, Allocator.Temp);
            var dist = new NativeArray<int>(4, Allocator.Temp);
            var sources = new NativeArray<int2>(3, Allocator.Temp);
            try
            {
                for (int i = 0; i < 4; i++) walk[i] = 1;
                sources[0] = new int2(0, 0);
                sources[1] = new int2(0, 0);
                sources[2] = new int2(0, 0);

                FlowFieldBuilder.BuildFromSources(walk, gridSize, sources, flow, dist);

                Assert.AreEqual(0, dist[0]);
                Assert.AreEqual(3, dist[3]);
                Assert.AreEqual(new float2(-1, 0), flow[3]);
            }
            finally { walk.Dispose(); flow.Dispose(); dist.Dispose(); sources.Dispose(); }
        }

        [Test]
        public void BuildFromSources_SourceBehindWall_OnlyItsComponentReachable()
        {
            // 5x1: A . X . B — wall at 2 splits components. All finite except wall,
            // each side drains to its own source.
            var gridSize = new int2(5, 1);
            var walk = new NativeArray<byte>(5, Allocator.Temp);
            var flow = new NativeArray<float2>(5, Allocator.Temp);
            var dist = new NativeArray<int>(5, Allocator.Temp);
            var sources = new NativeArray<int2>(1, Allocator.Temp);
            try
            {
                for (int i = 0; i < 5; i++) walk[i] = 1;
                walk[2] = 0;
                sources[0] = new int2(0, 0);

                FlowFieldBuilder.BuildFromSources(walk, gridSize, sources, flow, dist);

                Assert.AreEqual(1, dist[1], "same component reachable");
                Assert.AreEqual(int.MaxValue, dist[2], "wall");
                Assert.AreEqual(int.MaxValue, dist[3], "other component unreachable");
                Assert.AreEqual(int.MaxValue, dist[4], "other component unreachable");
                Assert.AreEqual(float2.zero, flow[4]);
            }
            finally { walk.Dispose(); flow.Dispose(); dist.Dispose(); sources.Dispose(); }
        }

        [Test]
        public void CollectDefenderSources_WallCellDefender_YieldsWalkableNeighborsOnly()
        {
            // 3x3: lane on y=1 only. Defender at (1,0) (wall) → single walkable
            // neighbor (1,1). Defender at (0,0) fully wall-locked except (0,1).
            //  . . .   y=2 (wall)
            //  W W W   y=1 (walk)
            //  D d .   y=0 (wall) — D=(0,0), d=(1,0)
            var gridSize = new int2(3, 3);
            var walk = new NativeArray<byte>(9, Allocator.Temp);
            var defenders = new NativeArray<int2>(2, Allocator.Temp);
            var outSources = new NativeList<int2>(Allocator.Temp);
            try
            {
                for (int x = 0; x < 3; x++) walk[1 * 3 + x] = 1;
                defenders[0] = new int2(0, 0);
                defenders[1] = new int2(1, 0);

                int count = FlowFieldBuilder.CollectDefenderSources(walk, gridSize, defenders, outSources);

                Assert.AreEqual(2, count);
                Assert.AreEqual(new int2(0, 1), outSources[0], "defender (0,0) → walkable neighbor above");
                Assert.AreEqual(new int2(1, 1), outSources[1], "defender (1,0) → walkable neighbor above");
            }
            finally { walk.Dispose(); defenders.Dispose(); outSources.Dispose(); }
        }

        [Test]
        public void CollectDefenderSources_NoWalkableNeighbors_ContributesNothing()
        {
            var gridSize = new int2(3, 3);
            var walk = new NativeArray<byte>(9, Allocator.Temp); // all wall
            var defenders = new NativeArray<int2>(1, Allocator.Temp);
            var outSources = new NativeList<int2>(Allocator.Temp);
            try
            {
                defenders[0] = new int2(1, 1);

                int count = FlowFieldBuilder.CollectDefenderSources(walk, gridSize, defenders, outSources);

                Assert.AreEqual(0, count);
                Assert.AreEqual(0, outSources.Length);
            }
            finally { walk.Dispose(); defenders.Dispose(); outSources.Dispose(); }
        }

        [Test]
        public void Build_TwentyByTwentyStraightPath_ProducesExpectedArrayLengthAndDistances()
        {
            var gridSize = new int2(20, 20);
            int n = gridSize.x * gridSize.y;
            var walk = new NativeArray<byte>(n, Allocator.Temp);
            var flow = new NativeArray<float2>(n, Allocator.Temp);
            var dist = new NativeArray<int>(n, Allocator.Temp);
            try
            {
                int midY = gridSize.y / 2;
                for (int x = 0; x < gridSize.x; x++)
                    walk[midY * gridSize.x + x] = 1;

                FlowFieldBuilder.Build(walk, gridSize, new int2(gridSize.x - 1, midY), flow, dist);

                Assert.AreEqual(400, flow.Length);
                Assert.AreEqual(400, dist.Length);
                Assert.AreEqual(0, dist[midY * gridSize.x + gridSize.x - 1]);
                Assert.AreEqual(19, dist[midY * gridSize.x]);
                Assert.AreEqual(float2.zero, flow[midY * gridSize.x + gridSize.x - 1]);
                Assert.AreEqual(new float2(1, 0), flow[midY * gridSize.x]);
            }
            finally { walk.Dispose(); flow.Dispose(); dist.Dispose(); }
        }
    }
}
