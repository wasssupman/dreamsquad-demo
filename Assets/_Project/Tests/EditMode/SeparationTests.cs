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
        public void PairPush_SumOfTwo_IsCommutative()
        {
            // ⚠ 이 테스트는 **2항 교환법칙만** 증명한다. IEEE754 에서 a+b 와 b+a 는 항상
            // 비트까지 같으므로 자명하게 통과한다 — 즉 이건 결정론 증거가 아니다.
            // 실제 위험한 축(3항 이상 결합법칙)은 아래 [Ignore] 테스트가 다룬다.
            var self = new float3(1f, 0f, 1f);
            var n1 = new float3(0.8f, 0f, 1f);
            var n2 = new float3(1f, 0f, 0.8f);

            var forward = Separation.PairPush(self, n1, 0.7f, S) + Separation.PairPush(self, n2, 0.7f, S);
            var reverse = Separation.PairPush(self, n2, 0.7f, S) + Separation.PairPush(self, n1, 0.7f, S);

            Assert.AreEqual(forward.x, reverse.x, 1e-6f);
            Assert.AreEqual(forward.y, reverse.y, 1e-6f);
        }

        [Test]
        [Ignore("현재 실패한다 — 그게 사실이다. float 덧셈에 결합법칙이 없어 3항 이상 누적은 " +
                "더한 순서에 따라 결과가 1 ULP 갈린다(아래 값이 실측 사례). AgentSeparationSystem 의 " +
                "`pushes[i] += push` 누적 순서는 ToComponentDataArray 의 청크 순서 = 스폰·사망 이력에서 " +
                "온다. 전체 리플레이는 안전하지만 스냅샷 부분 재시뮬은 갈릴 수 있다. 해결은 float 를 " +
                "결합적으로 만드는 게 아니라 누적 순서를 stable id 로 고정하는 것이고, 그 축(SimEntityId)은 " +
                "docs/spec/battle-sim-extraction/ unit 1 소관이다. unit 1 이 정렬 누적을 세우면 이 테스트를 " +
                "그 API 로 겨눠 다시 켠다 — 그때 통과해야 한다.")]
        public void Accumulation_OfThreeOrMore_IsOrderIndependent()
        {
            // 세 이웃에게 동시에 밀리는 한 에이전트. 시스템의 누적 루프를 그대로 재현한다.
            var self = new float3(1f, 0f, 1f);
            var p1 = Separation.PairPush(self, new float3(0.98f, 0f, 0.63f), 0.7f, S);
            var p2 = Separation.PairPush(self, new float3(1.14f, 0f, 0.60f), 0.7f, S);
            var p3 = Separation.PairPush(self, new float3(1.18f, 0f, 0.84f), 0.7f, S);

            var a = (p1 + p2) + p3;
            var b = (p3 + p2) + p1;   // 역순 — x 가 1 ULP 어긋난다
            var c = (p1 + p3) + p2;   // 회전 — y 가 1 ULP 어긋난다

            // 비트로 비교한다. 허용오차를 주면 이 결함이 통째로 숨는다 — 1 ULP 가 요점이다.
            Assert.AreEqual(math.asuint(a.x), math.asuint(b.x), "역순 누적이 x 를 바꾼다");
            Assert.AreEqual(math.asuint(a.y), math.asuint(c.y), "회전 누적이 y 를 바꾼다");
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
