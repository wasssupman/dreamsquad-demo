// 적응: Assets/_Project/Tests/EditMode/GridMathTests.cs (battle-core-rebuild unit 2)
using NUnit.Framework;
using Unity.Mathematics;
using Somnia.Battle.BattleCore.Map;

namespace Somnia.Battle.Tests.EditMode.Core
{
    public class MapGridMathTests
    {
        [Test]
        public void 월드에서_칸은_반칸에서_위로_붙는다()
        {
            // `math.round` 는 banker's rounding 이라 2.5 → 2 다. 격자 조회는 예측 가능한
            // 쪽(위로)이어야 해서 `floor(v + 0.5)` 를 쓴다.
            var grid = new int2(8, 8);
            Assert.AreEqual(3, GridMath.WorldToCell(new float3(2.5f, 0f, 0f), 1f, grid).x);
            Assert.AreEqual(2, GridMath.WorldToCell(new float3(2.49f, 0f, 0f), 1f, grid).x);
        }

        [Test]
        public void 격자_밖은_클램프되지만_무클램프는_밖을_말한다()
        {
            var grid = new int2(4, 4);
            Assert.AreEqual(3, GridMath.WorldToCell(new float3(99f, 0f, 0f), 1f, grid).x);
            Assert.AreEqual(99, GridMath.WorldToCellUnclamped(new float3(99f, 0f, 0f), 1f).x);
        }

        [Test]
        public void 흐름_단위벡터의_대각_성분을_버리면_스텝이_사라진다()
        {
            // 8-이웃이 되면서 대각 성분이 ±0.7071 이 됐다. `(int)` 버림 캐스트면 0 이 되어
            // 스텝이 사라지고 루프가 같은 칸에 갇힌다(예고 라인이 첫 대각에서 끊긴 실제 사고).
            var step = GridMath.FlowStep(new float2(0.7071f, 0.7071f));
            Assert.AreEqual(new int2(1, 1), step);
            Assert.AreEqual(int2.zero, GridMath.FlowStep(float2.zero));
        }

        [Test]
        public void 사거리를_칸으로_바꿀_때는_올림이다()
        {
            // 소스 디스크는 사격 가능 칸을 **덮어야** 한다 — 반올림이면 2.4 가 2 로 줄어
            // 쏠 수 있는 칸이 소스에서 빠지고 적이 더 멀리서 멈춘다.
            Assert.AreEqual(3, GridMath.RangeToTiles(2.4f));
            Assert.AreEqual(1, GridMath.RangeToTiles(0.1f));
            Assert.AreEqual(2, GridMath.RangeToTiles(2f));
        }

        [Test]
        public void 칸_인덱스는_행우선이다()
        {
            var grid = new int2(5, 3);
            Assert.AreEqual(2 * 5 + 1, GridMath.CellIndex(new int2(1, 2), grid));
        }
    }
}
