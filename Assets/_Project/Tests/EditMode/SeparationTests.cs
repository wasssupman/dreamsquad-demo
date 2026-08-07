using NUnit.Framework;
using Unity.Mathematics;
using Wassup.Battle.Movement;

namespace Wassup.Tests.EditMode
{
    // continuous-agent-movement unit 8 — 겹침 해소의 순수 계산.
    // 시스템의 몫은 이웃 수집뿐이고, 규칙은 전부 여기 있다.
    public class SeparationTests
    {
        private const float S = Separation.DefaultStrength;

        [Test]
        public void NoOverlap_NoPush()
        {
            var p = Separation.PairPush(
                new float3(0f, 0f, 0f), new float3(2f, 0f, 0f), radiusSum: 0.7f, strength: S);
            Assert.AreEqual(0f, math.lengthsq(p), 1e-8f);
        }

        [Test]
        public void Overlap_PushesApartAlongCenterLine()
        {
            var p = Separation.PairPush(
                new float3(0.3f, 0f, 0f), new float3(0f, 0f, 0f), radiusSum: 0.7f, strength: S);

            Assert.Greater(p.x, 0f, "자기 쪽(+x)으로 밀린다");
            Assert.AreEqual(0f, p.y, 1e-6f, "중심선 방향뿐");
        }

        [Test]
        public void DeeperOverlap_PushesHarder()
        {
            var shallow = Separation.PairPush(
                new float3(0.6f, 0f, 0f), float3.zero, 0.7f, S);
            var deep = Separation.PairPush(
                new float3(0.2f, 0f, 0f), float3.zero, 0.7f, S);

            Assert.Greater(math.length(deep), math.length(shallow));
        }

        [Test]
        public void ExactOverlap_ReturnsZero_NotRandom()
        {
            // 같은 좌표에 스폰된 두 유닛. 임의 방향을 주려면 난수가 필요하고 그러면
            // 결정론이 깨진다 — zero 를 주고 다음 프레임에 다른 요인이 벌리게 둔다.
            var p = Separation.PairPush(float3.zero, float3.zero, 0.7f, S);
            Assert.AreEqual(0f, math.lengthsq(p), 1e-8f);
        }

        [Test]
        public void PairPush_IsAntisymmetric()
        {
            // 작용-반작용. 시스템이 한 쌍을 한 번만 보고 `pushes[j] -= push` 하는 근거다.
            var a = new float3(0.3f, 0f, 0.1f);
            var b = new float3(0f, 0f, 0f);
            var ab = Separation.PairPush(a, b, 0.7f, S);
            var ba = Separation.PairPush(b, a, 0.7f, S);

            Assert.AreEqual(-ab.x, ba.x, 1e-5f);
            Assert.AreEqual(-ab.y, ba.y, 1e-5f);
        }

        [Test]
        public void AccumulationIsOrderIndependent()
        {
            // 순서 무관이 이 unit 의 결정론 계약이다. 누적은 덧셈이라 순서와 무관해야 한다.
            var self = new float3(1f, 0f, 1f);
            var n1 = new float3(0.8f, 0f, 1f);
            var n2 = new float3(1f, 0f, 0.8f);

            var forward = Separation.PairPush(self, n1, 0.7f, S) + Separation.PairPush(self, n2, 0.7f, S);
            var reverse = Separation.PairPush(self, n2, 0.7f, S) + Separation.PairPush(self, n1, 0.7f, S);

            Assert.AreEqual(forward.x, reverse.x, 1e-6f);
            Assert.AreEqual(forward.y, reverse.y, 1e-6f);
        }

        [Test]
        public void ApplyAccumulated_ClampsToMaxPush()
        {
            // 밀집 대열에서 누적이 커지면 튕겨 나간다. 한 프레임 상한이 그걸 막는다.
            var r = Separation.ApplyAccumulated(
                new float3(0f, 5f, 0f), new float2(10f, 0f), maxPush: 0.35f);

            Assert.AreEqual(0.35f, r.x, 1e-4f, "상한으로 잘린다");
            Assert.AreEqual(5f, r.y, 1e-6f, "y 는 건드리지 않는다");
        }

        [Test]
        public void ApplyAccumulated_PreservesDirection()
        {
            var r = Separation.ApplyAccumulated(
                float3.zero, new float2(3f, 4f), maxPush: 1f);

            Assert.AreEqual(0.6f, r.x, 1e-4f, "3/5 비율 보존");
            Assert.AreEqual(0.8f, r.z, 1e-4f, "4/5 비율 보존");
        }

        [Test]
        public void ApplyAccumulated_ZeroPush_IsNoOp()
        {
            var p = new float3(1f, 2f, 3f);
            var r = Separation.ApplyAccumulated(p, float2.zero, 0.35f);
            Assert.AreEqual(p, r);
        }
    }
}
