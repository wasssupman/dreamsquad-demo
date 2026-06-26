using NUnit.Framework;
using Unity.Mathematics;
using Wassup.Battle.Movement;

namespace Wassup.Tests.EditMode
{
    // enemy-spawn-positioning 1 — 스폰 측면 분산 순수 수학 회귀.
    public class SpawnSpreadTests
    {
        private const float Eps = 1e-4f;

        [Test]
        public void SlotFraction_ThreeSlots_SpansNegToPos()
        {
            Assert.AreEqual(-0.33f, SpawnSpread.SlotFraction(0, 3, 0.33f), Eps);
            Assert.AreEqual( 0.00f, SpawnSpread.SlotFraction(1, 3, 0.33f), Eps);
            Assert.AreEqual( 0.33f, SpawnSpread.SlotFraction(2, 3, 0.33f), Eps);
        }

        [Test]
        public void SlotFraction_SingleSlot_IsCentered()
        {
            Assert.AreEqual(0f, SpawnSpread.SlotFraction(0, 1, 0.33f), Eps);
        }

        [Test]
        public void SlotFraction_ClampsToHalfTileMax()
        {
            // 0.9 를 요청해도 셀 침범 방지를 위해 MaxHalfFraction(0.49) 로 클램프.
            Assert.AreEqual( SpawnSpread.MaxHalfFraction, SpawnSpread.SlotFraction(2, 3, 0.9f), Eps);
            Assert.AreEqual(-SpawnSpread.MaxHalfFraction, SpawnSpread.SlotFraction(0, 3, 0.9f), Eps);
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
        public void LateralOffset_StaysInsideCell()
        {
            // 어떤 슬롯이든 |XZ| < 0.5·tileSize (셀 불변식).
            const float tile = 1f;
            for (int s = 0; s < 3; s++)
            {
                var o = SpawnSpread.LateralOffset(s, 3, 0.49f, tile, new float2(1f, 0f));
                Assert.AreEqual(0f, o.y, Eps);                               // 평면 오프셋
                Assert.Less(math.length(new float2(o.x, o.z)), 0.5f * tile); // 셀 안
            }
        }

        [Test]
        public void LateralOffset_PerpendicularToFlow_AndSymmetric()
        {
            float2 flow = new float2(1f, 0f);
            var bottom = SpawnSpread.LateralOffset(0, 3, 0.33f, 1f, flow);
            var top    = SpawnSpread.LateralOffset(2, 3, 0.33f, 1f, flow);
            Assert.AreEqual(0f, bottom.x, Eps); // 진행축(X) 성분 0
            Assert.AreEqual(0f, top.x, Eps);
            Assert.AreEqual(-bottom.z, top.z, Eps); // 상/하 대칭
            Assert.AreNotEqual(0f, top.z);
        }
    }
}
