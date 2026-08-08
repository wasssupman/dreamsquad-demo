using NUnit.Framework;
using Unity.Mathematics;
using Wassup.Battle.Movement;

namespace Wassup.Tests.EditMode
{
    public class GridMathTests
    {
        [Test]
        public void WorldToCell_Origin_ReturnsZero()
        {
            var cell = GridMath.WorldToCell(new float3(0, 0, 0), tileSize: 1f, gridSize: new int2(20, 10));
            Assert.AreEqual(new int2(0, 0), cell);
        }

        [Test]
        public void WorldToCell_ExactCellCenter_ReturnsCell()
        {
            var cell = GridMath.WorldToCell(new float3(5, 0, 3), tileSize: 1f, gridSize: new int2(20, 10));
            Assert.AreEqual(new int2(5, 3), cell);
        }

        [Test]
        public void WorldToCell_Rounds_NotFloors()
        {
            // 0.6 should round to 1, not floor to 0
            var cell = GridMath.WorldToCell(new float3(0.6f, 0, 0.4f), tileSize: 1f, gridSize: new int2(20, 10));
            Assert.AreEqual(new int2(1, 0), cell);
        }

        [Test]
        public void WorldToCell_OutOfBounds_ClampsToEdge()
        {
            var cellHigh = GridMath.WorldToCell(new float3(100, 0, 100), tileSize: 1f, gridSize: new int2(20, 10));
            Assert.AreEqual(new int2(19, 9), cellHigh);

            var cellLow = GridMath.WorldToCell(new float3(-10, 0, -10), tileSize: 1f, gridSize: new int2(20, 10));
            Assert.AreEqual(new int2(0, 0), cellLow);
        }

        // active-dreamcatcher-tile-aim rev — clamp 하는 변형만으로는 "보드 밖" 을 물을 수 없다.
        // 조준 커밋이 맵 밖 릴리즈를 거절하려면 접히지 않은 셀이 필요하다.
        [Test]
        public void WorldToCellUnclamped_OutOfBounds_KeepsCellOutside()
        {
            var high = GridMath.WorldToCellUnclamped(new float3(100, 0, 100), tileSize: 1f);
            Assert.AreEqual(new int2(100, 100), high);

            var low = GridMath.WorldToCellUnclamped(new float3(-10, 0, -10), tileSize: 1f);
            Assert.AreEqual(new int2(-10, -10), low);
        }

        [Test]
        public void WorldToCell_IsUnclampedThenClamped_SameRounding()
        {
            var grid = new int2(20, 10);
            var origin = new float3(3f, 0f, -2f);
            foreach (var p in new[] { new float3(0.6f, 0, 0.4f), new float3(7.5f, 0, 2.5f), new float3(-4f, 0, 30f) })
            {
                var raw = GridMath.WorldToCellUnclamped(p, tileSize: 2f, origin: origin);
                var clamped = GridMath.WorldToCell(p, tileSize: 2f, gridSize: grid, origin: origin);
                Assert.AreEqual(math.clamp(raw.x, 0, grid.x - 1), clamped.x, $"x @ {p}");
                Assert.AreEqual(math.clamp(raw.y, 0, grid.y - 1), clamped.y, $"y @ {p}");
            }
        }

        [Test]
        public void WorldToCell_DifferentTileSize_Scales()
        {
            var cell = GridMath.WorldToCell(new float3(10, 0, 5), tileSize: 2f, gridSize: new int2(20, 10));
            Assert.AreEqual(new int2(5, 3), cell);
        }

        [Test]
        public void CellToWorldCenter_MatchesWorldToCellInverse()
        {
            var world = GridMath.CellToWorldCenter(new int2(7, 4), tileSize: 1f);
            Assert.AreEqual(7f, world.x);
            Assert.AreEqual(0f, world.y);
            Assert.AreEqual(4f, world.z);
        }

        // --- map-origin-placement: board origin offset ---

        [Test]
        public void WorldToCell_DefaultOrigin_IdenticalToLegacy()
        {
            // origin defaults to zero -> behaviour matches the legacy 3-arg overload.
            var legacy = GridMath.WorldToCell(new float3(5, 0, 3), tileSize: 1f, gridSize: new int2(20, 10));
            var withOrigin = GridMath.WorldToCell(new float3(5, 0, 3), tileSize: 1f, gridSize: new int2(20, 10), origin: float3.zero);
            Assert.AreEqual(legacy, withOrigin);
        }

        [Test]
        public void WorldToCell_NonZeroOrigin_SubtractsBeforeCellifying()
        {
            // Board shifted to (10,0,5). A world point at origin + (5,3) tiles must map to cell (5,3).
            var origin = new float3(10, 0, 5);
            var cell = GridMath.WorldToCell(new float3(15, 0, 8), tileSize: 1f, gridSize: new int2(20, 10), origin: origin);
            Assert.AreEqual(new int2(5, 3), cell);
        }

        [Test]
        public void CellToWorldCenter_NonZeroOrigin_AddsOrigin()
        {
            var origin = new float3(10, 0, 5);
            var world = GridMath.CellToWorldCenter(new int2(7, 4), tileSize: 1f, y: 0f, origin: origin);
            Assert.AreEqual(17f, world.x);
            Assert.AreEqual(0f, world.y);
            Assert.AreEqual(9f, world.z);
        }

        [Test]
        public void RoundTrip_NonZeroOrigin_PreservesCell()
        {
            var origin = new float3(12.5f, 0, -3.5f);
            var grid = new int2(20, 10);
            for (int x = 0; x < 8; x++)
            for (int z = 0; z < 6; z++)
            {
                var cell = new int2(x, z);
                var world = GridMath.CellToWorldCenter(cell, tileSize: 2f, y: 0f, origin: origin);
                var back = GridMath.WorldToCell(world, tileSize: 2f, gridSize: grid, origin: origin);
                Assert.AreEqual(cell, back, $"round-trip failed for cell ({x},{z})");
            }
        }

        // continuous-agent-movement unit 4 후속 회귀 — 경로 예고 라인이 첫 대각에서 끊긴 사고.
        // 8-이웃 flow 의 대각 성분은 ±0.7071 이라 버림 캐스트로는 0 이 되고, 필드를 따라가는
        // 루프가 같은 셀에 갇힌다. 이 변환의 단일 정의가 그 사고를 막는다.
        [Test]
        public void FlowStep_DiagonalUnitVector_YieldsDiagonalStep()
        {
            float inv = 1f / Unity.Mathematics.math.sqrt(2f);   // 0.7071
            Assert.AreEqual(new int2(1, 1),   GridMath.FlowStep(new float2( inv,  inv)));
            Assert.AreEqual(new int2(1, -1),  GridMath.FlowStep(new float2( inv, -inv)));
            Assert.AreEqual(new int2(-1, 1),  GridMath.FlowStep(new float2(-inv,  inv)));
            Assert.AreEqual(new int2(-1, -1), GridMath.FlowStep(new float2(-inv, -inv)));
        }

        [Test]
        public void FlowStep_TruncationWouldHaveFailed()
        {
            // 사고의 실체를 못박는다: 버림이면 0 이 나온다.
            float inv = 1f / Unity.Mathematics.math.sqrt(2f);
            Assert.AreEqual(0, (int)inv, "버림 캐스트는 대각을 0 으로 만든다 — 이래서 끊겼다");
            Assert.AreNotEqual(0, GridMath.FlowStep(new float2(inv, inv)).x);
        }

        [Test]
        public void FlowStep_OrthogonalUnitVector_Unchanged()
        {
            Assert.AreEqual(new int2(1, 0),  GridMath.FlowStep(new float2(1f, 0f)));
            Assert.AreEqual(new int2(-1, 0), GridMath.FlowStep(new float2(-1f, 0f)));
            Assert.AreEqual(new int2(0, 1),  GridMath.FlowStep(new float2(0f, 1f)));
            Assert.AreEqual(new int2(0, -1), GridMath.FlowStep(new float2(0f, -1f)));
        }

        [Test]
        public void FlowStep_ZeroFlow_YieldsZeroStep()
        {
            // 호출자는 이걸 루프 종료 신호로 쓴다(골 도착·고립·미도달).
            Assert.AreEqual(int2.zero, GridMath.FlowStep(float2.zero));
            Assert.AreEqual(int2.zero, GridMath.FlowStep(new float2(1e-4f, -1e-4f)));
        }

    }
}
