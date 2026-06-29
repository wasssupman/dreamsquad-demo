using NUnit.Framework;
using Unity.Mathematics;
using Wassup.Battle.Movement;

namespace Wassup.Tests.EditMode
{
    // enemy-tile-movement-integrity unit 1 — 측면 복원(target=0 + dead-band) 순수 수학 회귀.
    // 셀 (0,0), tile=1, origin=0 → 중심 (0,*,0). flowDir=+X → perp 축 = Z.
    public class LateralRecenterTests
    {
        private const float Eps = 1e-4f;
        private static readonly float2 FlowX = new float2(1f, 0f);
        private const float Tile = 1f;
        private const float Deadband = LateralRecenter.DeadbandFraction; // 0.25 (tile=1)

        private static float3 At(float perpZ) => new float3(0f, 0f, perpZ);

        [Test]
        public void InsideDeadband_NoMove()
        {
            // |perp| 0.1 < deadband → 분산 보존(이동 0).
            var d = LateralRecenter.Compute(At(0.1f), int2.zero, FlowX, 3f, 0.016f, Tile, float3.zero);
            Assert.Less(math.length(d), Eps);
        }

        [Test]
        public void AtDeadbandEdge_NoMove()
        {
            // 경계는 보존(<=). 코너에 정착한 적이 떨림 없이 머문다.
            var d = LateralRecenter.Compute(At(Deadband), int2.zero, FlowX, 3f, 0.016f, Tile, float3.zero);
            Assert.Less(math.length(d), Eps);
        }

        [Test]
        public void BeyondDeadband_PullsTowardCenter()
        {
            // 코너 엣지(0.49) → 중심(−Z) 쪽으로 rate·dt 만큼.
            var d = LateralRecenter.Compute(At(0.49f), int2.zero, FlowX, 3f, 0.016f, Tile, float3.zero);
            float expected = math.min(LateralRecenter.RateK * 3f * 0.016f, 0.49f - Deadband);
            Assert.Less(d.z, 0f);                              // 중심 쪽(−)
            Assert.AreEqual(expected, math.abs(d.z), Eps);     // 크기 = min(rate·dt, adp−deadband)
            Assert.AreEqual(0f, d.x, Eps);                     // 진행축 성분 0
            Assert.AreEqual(0f, d.y, Eps);                     // 평면
        }

        [Test]
        public void CapsAtDeadbandEdge_NoOvershoot()
        {
            // 큰 rate·dt 라도 밴드 가장자리까지만 → 정확히 deadband 에 정착(0 관통 없음).
            var d = LateralRecenter.Compute(At(0.30f), int2.zero, FlowX, 100f, 1f, Tile, float3.zero);
            Assert.AreEqual(0.30f - Deadband, math.abs(d.z), Eps);
            Assert.AreEqual(0.30f - math.abs(d.z), Deadband, Eps); // newPerp == deadband
        }

        [Test]
        public void ForwardOffset_NotRecentered()
        {
            // 진행축(X) 방향 오프셋은 perp 성분 0 → 이동 0(전방 위치 보존).
            var d = LateralRecenter.Compute(new float3(0.4f, 0f, 0f), int2.zero, FlowX, 3f, 0.016f, Tile, float3.zero);
            Assert.Less(math.length(d), Eps);
        }

        [Test]
        public void SignSymmetric()
        {
            var pos = LateralRecenter.Compute(At( 0.4f), int2.zero, FlowX, 3f, 0.016f, Tile, float3.zero);
            var neg = LateralRecenter.Compute(At(-0.4f), int2.zero, FlowX, 3f, 0.016f, Tile, float3.zero);
            Assert.AreEqual(pos.z, -neg.z, Eps);
            Assert.Greater(neg.z, 0f);   // −0.4 는 +Z(중심) 쪽으로
        }
    }
}
