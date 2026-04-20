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
    }
}
