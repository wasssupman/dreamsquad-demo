// 적응: Assets/_Project/Tests/EditMode/Battle/SeparationTests.cs (battle-core-rebuild unit 2)
// ⚠ 옛 파일에는 1 ULP 순서 의존을 보존한 `[Ignore]` 실패 사례가 있었다. **옮기지 않았다** —
//   새 코어가 그 축을 `SimEntityId` 오름차순으로 닫았기 때문이다(M27). 닫힌 것을 증언하는
//   테스트는 `MovementRulesTests.분리_누적은_id_오름차순이다` 다.
using NUnit.Framework;
using Unity.Mathematics;
using Somnia.Battle.BattleCore.Move;

namespace Somnia.Battle.Tests.EditMode.Core
{
    public class SeparationTests
    {
        private const float S = Separation.DefaultStrength;

        [Test]
        public void 안_겹치면_밀지_않는다()
        {
            var p = Separation.PairPush(float3.zero, new float3(2f, 0f, 0f), 0.7f, S);
            Assert.AreEqual(0f, math.lengthsq(p), 1e-8f);
        }

        [Test]
        public void 중심선_방향으로_밀어낸다()
        {
            var p = Separation.PairPush(new float3(0.3f, 0f, 0f), float3.zero, 0.7f, S);
            Assert.Greater(p.x, 0f);
            Assert.AreEqual(0f, p.y, 1e-6f);
        }

        [Test]
        public void 깊게_겹칠수록_세게_민다()
        {
            var shallow = Separation.PairPush(new float3(0.6f, 0f, 0f), float3.zero, 0.7f, S);
            var deep = Separation.PairPush(new float3(0.2f, 0f, 0f), float3.zero, 0.7f, S);
            Assert.Greater(math.length(deep), math.length(shallow));
        }

        [Test]
        public void 정확히_겹치면_zero_다_난수가_아니다()
        {
            // 임의 방향을 주려면 난수가 필요하고 그러면 결정론이 깨진다.
            Assert.AreEqual(0f, math.lengthsq(Separation.PairPush(float3.zero, float3.zero, 0.7f, S)), 1e-8f);
        }

        [Test]
        public void 정지한_유닛은_전진_성분만_거부한다()
        {
            // 뒤 무리가 교전 중인 유닛을 경로를 따라 밀어 나르는 것을 막는다. 옆·뒤 성분은
            // 남긴다 — 전면 차단하면 폭1 복도에서 마개가 진짜 벽이 되어 뒤가 못 지난다.
            var forward = new float2(1f, 0f);
            var kept = Separation.RejectForwardPush(new float2(1f, 1f), forward);
            Assert.AreEqual(0f, kept.x, 1e-6f, "전진 성분은 제거");
            Assert.AreEqual(1f, kept.y, 1e-6f, "측면은 유지");

            var backward = Separation.RejectForwardPush(new float2(-1f, 0f), forward);
            Assert.AreEqual(-1f, backward.x, 1e-6f, "뒤로 밀리는 건 그대로 받는다");
        }

        [Test]
        public void 정규화하지_않은_전진벡터도_같은_답을_준다()
        {
            var a = Separation.RejectForwardPush(new float2(1f, 1f), new float2(1f, 0f));
            var b = Separation.RejectForwardPush(new float2(1f, 1f), new float2(5f, 0f));
            Assert.AreEqual(a.x, b.x, 1e-6f);
            Assert.AreEqual(a.y, b.y, 1e-6f);
        }

        [Test]
        public void 누적_적용은_상한을_넘지_않는다()
        {
            var moved = Separation.ApplyAccumulated(float3.zero, new float2(10f, 0f), maxPush: 0.25f);
            Assert.AreEqual(0.25f, moved.x, 1e-5f);
        }
    }
}
