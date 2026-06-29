using NUnit.Framework;
using Unity.Mathematics;
using Wassup.Battle.Movement;

namespace Wassup.Tests.EditMode
{
    // enemy-spawn-positioning / enemy-tile-movement-integrity u0 — 스폰 측면 분산 순수 수학 회귀(결정론 분율 포함).
    public class SpawnSpreadTests
    {
        private const float Eps = 1e-4f;

        [Test]
        public void FractionRange_Symmetric_WhenTopScaleOne()
        {
            var r = SpawnSpread.FractionRange(0.2f, 1f);
            Assert.AreEqual(-0.2f, r.x, Eps);
            Assert.AreEqual( 0.2f, r.y, Eps);
        }

        [Test]
        public void FractionRange_TopScale_CompressesTopOnly()
        {
            var r = SpawnSpread.FractionRange(0.2f, 0.5f);
            Assert.AreEqual(-0.2f, r.x, Eps);  // 하단 불변
            Assert.AreEqual( 0.1f, r.y, Eps);  // 상단만 절반
        }

        [Test]
        public void FractionRange_ClampsFractionToMaxHalf()
        {
            var r = SpawnSpread.FractionRange(0.9f, 1f);
            Assert.AreEqual(-SpawnSpread.MaxHalfFraction, r.x, Eps);
            Assert.AreEqual( SpawnSpread.MaxHalfFraction, r.y, Eps);
        }

        [Test]
        public void FractionRange_SaturatesTopScale()
        {
            Assert.AreEqual( 0.2f, SpawnSpread.FractionRange(0.2f, 2f).y, Eps);  // >1 → 1
            Assert.AreEqual( 0.0f, SpawnSpread.FractionRange(0.2f, 0f).y, Eps);  // 0 → 상단 0
            Assert.AreEqual(-0.2f, SpawnSpread.FractionRange(0.2f, 0f).x, Eps);  // 하단 그대로
        }

        [Test]
        public void Perpendicular_OfXAxis_IsZAxisUnit()
        {
            var p = SpawnSpread.Perpendicular(new float2(1f, 0f));
            Assert.AreEqual(0f, p.x, Eps);
            Assert.AreEqual(1f, math.abs(p.y), Eps);
            Assert.AreEqual(1f, math.length(p), Eps);
        }

        [Test]
        public void Perpendicular_OfZAxis_IsXAxisUnit()
        {
            var p = SpawnSpread.Perpendicular(new float2(0f, 1f));
            Assert.AreEqual(1f, math.abs(p.x), Eps);
            Assert.AreEqual(0f, p.y, Eps);
        }

        [Test]
        public void Perpendicular_OfZero_FallsBackToUnit()
        {
            var p = SpawnSpread.Perpendicular(float2.zero);
            Assert.AreEqual(1f, math.length(p), Eps); // 폴백도 단위벡터
        }

        [Test]
        public void LateralOffset_AlongPerpendicular_PlanarOnly()
        {
            var o = SpawnSpread.LateralOffset(0.2f, 1f, new float2(1f, 0f));
            Assert.AreEqual(0f, o.x, Eps);            // 진행축(X) 성분 0
            Assert.AreEqual(0f, o.y, Eps);            // 평면
            Assert.AreEqual(0.2f, math.abs(o.z), Eps);
        }

        [Test]
        public void LateralOffset_ZeroFrac_IsZero()
        {
            var o = SpawnSpread.LateralOffset(0f, 1f, new float2(1f, 0f));
            Assert.Less(math.length(o), Eps);
        }

        [Test]
        public void LateralOffset_ClampsToCell()
        {
            const float tile = 1f;
            // |frac| 가 범위를 넘어도 셀 불변식 보장(< 0.5·tile).
            var hi = SpawnSpread.LateralOffset( 0.9f, tile, new float2(1f, 0f));
            var lo = SpawnSpread.LateralOffset(-0.9f, tile, new float2(1f, 0f));
            Assert.Less(math.length(new float2(hi.x, hi.z)), 0.5f * tile);
            Assert.Less(math.length(new float2(lo.x, lo.z)), 0.5f * tile);
            Assert.AreEqual(SpawnSpread.MaxHalfFraction, math.abs(hi.z), Eps);
        }

        // enemy-tile-movement-integrity unit 0(rev) — 이산 N-레인 분율 회귀.
        [Test]
        public void LaneFraction_LaneCount1_AlwaysCenter()
        {
            for (int i = 0; i < 5; i++)
                Assert.AreEqual(0f, SpawnSpread.LaneFraction(i, 1, 0.2f, 1f), Eps);
        }

        [Test]
        public void LaneFraction_ThreeLanes_TopScaleOne_Symmetric()
        {
            Assert.AreEqual(-0.2f, SpawnSpread.LaneFraction(0, 3, 0.2f, 1f), Eps); // 하
            Assert.AreEqual( 0.0f, SpawnSpread.LaneFraction(1, 3, 0.2f, 1f), Eps); // 중
            Assert.AreEqual( 0.2f, SpawnSpread.LaneFraction(2, 3, 0.2f, 1f), Eps); // 상
        }

        [Test]
        public void LaneFraction_ThreeLanes_TopScale_CompressesTopOnly()
        {
            Assert.AreEqual(-0.2f, SpawnSpread.LaneFraction(0, 3, 0.2f, 0.5f), Eps); // 하 불변
            Assert.AreEqual( 0.0f, SpawnSpread.LaneFraction(1, 3, 0.2f, 0.5f), Eps); // 중
            Assert.AreEqual( 0.1f, SpawnSpread.LaneFraction(2, 3, 0.2f, 0.5f), Eps); // 상만 절반
        }

        [Test]
        public void LaneFraction_RoundRobin_PeriodicEveryN()
        {
            for (int i = 0; i < 6; i++)
                Assert.AreEqual(
                    SpawnSpread.LaneFraction(i, 3, 0.2f, 1f),
                    SpawnSpread.LaneFraction(i + 3, 3, 0.2f, 1f), Eps);
        }

        [Test]
        public void LaneFraction_ConsecutiveIndices_DifferentLanes()
        {
            // round-robin: 연속 스폰은 서로 다른 레인 → 한 점 겹침 회피.
            for (int i = 0; i < 6; i++)
                Assert.AreNotEqual(
                    SpawnSpread.LaneFraction(i,     3, 0.2f, 1f),
                    SpawnSpread.LaneFraction(i + 1, 3, 0.2f, 1f));
        }

        [Test]
        public void LaneFraction_WithinCellInvariant()
        {
            // spreadFraction 가 범위를 넘어도 |분율| < 0.5(셀 불변식).
            for (int n = 1; n <= 7; n++)
                for (int i = 0; i < n; i++)
                    Assert.Less(math.abs(SpawnSpread.LaneFraction(i, n, 0.9f, 1f)), 0.5f);
        }
    }
}
