using NUnit.Framework;
using Unity.Mathematics;
using Wassup.Battle.Movement;

namespace Wassup.Tests.EditMode
{
    // enemy-spawn-positioning — 스폰 측면 분산(중앙 ± 연속 랜덤) 순수 수학 회귀.
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
    }
}
