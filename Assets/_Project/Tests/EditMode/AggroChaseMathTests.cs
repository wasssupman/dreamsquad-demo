using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using Wassup.Battle.Combat;
using Wassup.Battle.Effects;
using Wassup.Battle.Movement;

namespace Wassup.Tests.EditMode
{
    // aggro-tile-chase unit 0 — 유효 사거리 해석 + chase field 조합의 회귀 고정.
    // 실제 고착 버그를 재현했던 기하(수선 pin·코너)를 synthetic mask 로 박제한다.
    public class AggroChaseMathTests
    {
        // ── ResolveTileRange ────────────────────────────────────────────────
        [Test]
        public void ResolveTileRange_AttackStateWins()
            => Assert.AreEqual(2, AggroChaseMath.ResolveTileRange(true, 2f, true, 1f));

        [Test]
        public void ResolveTileRange_ProfileFallback()
            => Assert.AreEqual(1, AggroChaseMath.ResolveTileRange(false, 0f, true, 1f));

        [Test]
        public void ResolveTileRange_Neither_NoAttack()
            => Assert.AreEqual(AggroChaseMath.NoAttack, AggroChaseMath.ResolveTileRange(false, 0f, false, 0f));

        // ── BuildChaseField ─────────────────────────────────────────────────
        // 8×5 그리드. '#'=walk. 좌표 (x,y), 인덱스 y*8+x.
        static NativeArray<byte> Mask(string[] rowsTopDown)
        {
            int h = rowsTopDown.Length, w = rowsTopDown[0].Length;
            var mask = new NativeArray<byte>(w * h, Allocator.Temp);
            for (int ry = 0; ry < h; ry++)
            {
                int y = h - 1 - ry; // 표기는 위→아래, 저장은 y0=아래
                for (int x = 0; x < w; x++)
                    mask[y * w + x] = rowsTopDown[ry][x] == '#' ? (byte)1 : (byte)0;
            }
            return mask;
        }

        static int Idx(int x, int y) => y * 8 + x;

        // 수선 pin 기하 (실버그): 통로 y3 (전폭), 가디언 (4,1) — 통로에서 2칸.
        //  range1: 가디언 Chebyshev≤1 에 walk 셀 없음 → 소스 0 → 거부 신호.
        //  range2: 통로 셀들이 소스 → 도달 가능.
        static readonly string[] PerpendicularPin =
        {
            "........",
            "########", // y3 통로
            "........", // y2
            "........", // y1 (가디언 (4,1) — Place)
            "........", // y0
        };

        [Test]
        public void PerpendicularPin_Range1_NoSources()
        {
            var mask = Mask(PerpendicularPin);
            var flow = new NativeArray<float2>(40, Allocator.Temp);
            var dist = new NativeArray<int>(40, Allocator.Temp);
            try
            {
                int sources = AggroChaseMath.BuildChaseField(mask, new int2(8, 5), new int2(4, 1), 1, flow, dist);
                Assert.AreEqual(0, sources);
                Assert.AreEqual(int.MaxValue, dist[Idx(6, 3)]); // 통로 위 적 — 전 셀 무한
            }
            finally { mask.Dispose(); flow.Dispose(); dist.Dispose(); }
        }

        [Test]
        public void PerpendicularPin_Range2_Reachable()
        {
            var mask = Mask(PerpendicularPin);
            var flow = new NativeArray<float2>(40, Allocator.Temp);
            var dist = new NativeArray<int>(40, Allocator.Temp);
            try
            {
                int sources = AggroChaseMath.BuildChaseField(mask, new int2(8, 5), new int2(4, 1), 2, flow, dist);
                Assert.Greater(sources, 0);            // y3 통로 중 |x-4|≤2 (x2..6) 셀들이 소스
                Assert.AreEqual(0, dist[Idx(4, 3)]);   // 수선 발 지점이 곧 목적지
                Assert.AreEqual(0, dist[Idx(6, 3)]);   // Chebyshev((6,3),(4,1))=2 → 소스 자신
                Assert.AreEqual(1 * FlowFieldBuilder.CostOrtho, dist[Idx(7, 3)]);   // 디스크 밖 적 — 1칸 걸어 도달 (unit 4: ×10 스케일)
            }
            finally { mask.Dispose(); flow.Dispose(); dist.Dispose(); }
        }

        // 코너 기하 (실버그): 위 통로 y3 와 세로 통로 x1, 가디언 (2,0) — 세로 통로 하단 옆.
        // 위 통로의 적 (6,3) 기준 직선은 벽이지만 경로(좌진→하강)로 도달 가능해야 한다.
        static readonly string[] CornerDetour =
        {
            "........",
            "########", // y3 통로
            ".#......", // y2 (x1 세로)
            ".#......", // y1
            ".#......", // y0  (가디언 (2,0))
        };

        [Test]
        public void CornerDetour_Range1_ReachableViaPath()
        {
            var mask = Mask(CornerDetour);
            var flow = new NativeArray<float2>(40, Allocator.Temp);
            var dist = new NativeArray<int>(40, Allocator.Temp);
            try
            {
                int sources = AggroChaseMath.BuildChaseField(mask, new int2(8, 5), new int2(2, 0), 1, flow, dist);
                Assert.Greater(sources, 0);                       // (1,0)·(1,1) 이 소스
                Assert.AreNotEqual(int.MaxValue, dist[Idx(6, 3)]); // 직선 불가여도 우회 도달
                Assert.AreEqual(7 * FlowFieldBuilder.CostOrtho, dist[Idx(6, 3)]);   // (6,3)→(1,3) 5 + 하강 2 → 소스 (1,1) (unit 4: ×10 스케일)
            }
            finally { mask.Dispose(); flow.Dispose(); dist.Dispose(); }
        }

        [Test]
        public void IsolatedIsland_Unreachable()
        {
            // y3 통로와 단절된 (6,0)~(7,0) 섬.
            var mask = Mask(new[]
            {
                "........",
                "########",
                "........",
                "........",
                "......##",
            });
            var flow = new NativeArray<float2>(40, Allocator.Temp);
            var dist = new NativeArray<int>(40, Allocator.Temp);
            try
            {
                AggroChaseMath.BuildChaseField(mask, new int2(8, 5), new int2(4, 2), 1, flow, dist);
                Assert.AreEqual(0, dist[Idx(4, 3)]);              // 통로 쪽 목적지 정상
                Assert.AreEqual(int.MaxValue, dist[Idx(6, 0)]);   // 섬의 적 — 도달 불가 → 거부
            }
            finally { mask.Dispose(); flow.Dispose(); dist.Dispose(); }
        }

        // 하강 재사용 검증: RecoveryDir 로 dist 를 따라가면 dist-0 셀에 도달하고 거기서 zero.
        [Test]
        public void RecoveryDirDescent_ReachesDestinationThenStops()
        {
            var mask = Mask(CornerDetour);
            var flow = new NativeArray<float2>(40, Allocator.Temp);
            var dist = new NativeArray<int>(40, Allocator.Temp);
            try
            {
                AggroChaseMath.BuildChaseField(mask, new int2(8, 5), new int2(2, 0), 1, flow, dist);
                int2 cell = new int2(6, 3);
                for (int step = 0; step < 32 && dist[Idx(cell.x, cell.y)] != 0; step++)
                {
                    float2 dir = FlowRecovery.RecoveryDir(cell, dist, new int2(8, 5));
                    Assert.AreNotEqual(0f, math.lengthsq(dir), $"조기 정지 at {cell}");
                    cell += new int2((int)dir.x, (int)dir.y);
                }
                Assert.AreEqual(0, dist[Idx(cell.x, cell.y)]);   // 목적지 도달
                Assert.AreEqual(0f, math.lengthsq(
                    FlowRecovery.RecoveryDir(cell, dist, new int2(8, 5)))); // 도달 후 자연 정지
            }
            finally { mask.Dispose(); flow.Dispose(); dist.Dispose(); }
        }
    }
}
