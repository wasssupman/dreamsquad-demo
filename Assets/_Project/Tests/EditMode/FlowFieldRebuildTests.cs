using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using Wassup.Battle.Effects;
using Wassup.Battle.Movement;

namespace Wassup.Tests.EditMode
{
    // continuous-agent-movement unit 5 (D1-b) — "막으면 돌아간다" 의 계산 계약.
    //
    // 시스템 배선(업데이트 순서·싱글턴 쓰기)이 아니라 **재빌드가 무엇을 산출하는가**를 본다.
    // 시스템은 이 계산을 dirty 판정 뒤에서 호출할 뿐이다.
    public class FlowFieldRebuildTests
    {
        private const int W = 5, H = 3;
        private static readonly int2 Grid = new int2(W, H);
        private static readonly int2 Goal = new int2(0, 1);

        // y=1 행만 통로인 5x3 맵.
        private static NativeArray<byte> Corridor()
        {
            var walk = new NativeArray<byte>(W * H, Allocator.Temp);
            for (int x = 0; x < W; x++) walk[1 * W + x] = 1;
            return walk;
        }

        private static NativeArray<byte> Combine(NativeArray<byte> walk, params int2[] blocked)
        {
            var m = new NativeArray<byte>(W * H, Allocator.Temp);
            m.CopyFrom(walk);
            foreach (var c in blocked) m[GridMath.CellIndex(c, Grid)] = 0;
            return m;
        }

        private static void Build(NativeArray<byte> mask, out NativeArray<int> dist)
        {
            var flow = new NativeArray<float2>(W * H, Allocator.Temp);
            dist = new NativeArray<int>(W * H, Allocator.Temp);
            FlowFieldBuilder.Build(mask, Grid, Goal, flow, dist);
            flow.Dispose();
        }

        [Test]
        public void Obstacle_MakesDownstreamUnreachable_InCorridor()
        {
            // 1타일 복도 한가운데를 막으면 그 뒤는 도달 불가가 된다 — 우회로가 없기 때문이다.
            var walk = Corridor();
            var blockedMask = Combine(walk, new int2(2, 1));
            Build(blockedMask, out var dist);

            Assert.AreEqual(int.MaxValue, dist[GridMath.CellIndex(new int2(4, 1), Grid)],
                "차단 뒤쪽은 도달 불가");
            Assert.AreNotEqual(int.MaxValue, dist[GridMath.CellIndex(new int2(1, 1), Grid)],
                "차단 앞쪽은 여전히 도달 가능");
        }

        [Test]
        public void RemovingObstacle_RestoresOriginalField()
        {
            // 해저드가 부서지면 필드가 원래대로 돌아와야 한다.
            var walk = Corridor();
            Build(walk, out var before);

            var blockedMask = Combine(walk, new int2(2, 1));
            Build(blockedMask, out var during);

            Build(walk, out var after);

            int far = GridMath.CellIndex(new int2(4, 1), Grid);
            Assert.AreNotEqual(before[far], during[far], "차단 중엔 달라야 한다");
            Assert.AreEqual(before[far], after[far], "제거 후 원복");
        }

        [Test]
        public void Detour_IsTakenWhenAlternativeExists()
        {
            // 통로를 2행으로 넓히고 한 셀만 막으면, 막힌 셀을 우회해 여전히 도달한다.
            var walk = new NativeArray<byte>(W * H, Allocator.Temp);
            for (int x = 0; x < W; x++) { walk[1 * W + x] = 1; walk[2 * W + x] = 1; }

            var blockedMask = Combine(walk, new int2(2, 1));
            Build(blockedMask, out var dist);

            Assert.AreNotEqual(int.MaxValue, dist[GridMath.CellIndex(new int2(4, 1), Grid)],
                "우회로가 있으면 도달 가능 — 막으면 돌아간다");
        }

        [Test]
        public void FullBlockade_LeavesCutRegionUnreachable_ButNotWall()
        {
            // 완전 봉쇄. 차단 구역은 도달 불가(dist=MaxValue)가 되지만 **지형은 여전히 Walk** 다.
            // unit 2 가 술어를 지형 기반으로 바꾼 이유가 이것 — 예전 zero-flow 술어였다면
            // 이 구역 전체가 "벽"이 되어 그 안의 적이 자기가 선 칸을 벽으로 인식했다.
            var walk = Corridor();
            var blockedMask = Combine(walk, new int2(2, 1));
            Build(blockedMask, out var dist);

            int cut = GridMath.CellIndex(new int2(4, 1), Grid);
            Assert.AreEqual(int.MaxValue, dist[cut], "도달 불가");

            var nav = new NavGrid(
                staticWalk: walk, blockedCells: default, hasObstacles: false,
                gridSize: Grid, tileSize: 1f, origin: float3.zero);
            Assert.IsFalse(nav.IsBlocked(new int2(4, 1)),
                "도달 불가와 벽은 다르다 — 적은 그 칸에 서 있을 수 있어야 한다");
        }
    }
}
