using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using Wassup.Battle.Combat;

namespace Wassup.Tests.EditMode
{
    // battle-sim-extraction unit 1 — 등거리 동률 tiebreak 신설의 회귀 핀.
    // 이전엔 후보 배열 순서(청크 스냅샷 순서)가 승자를 결정했다 — 배열 순서를
    // 뒤집어도 같은 대상(simId 낮은 쪽)이 뽑혀야 한다.
    public class AggroTargetingTests
    {
        private static AggroCandidate C(float x, int simId, bool aggroed = false) =>
            new AggroCandidate
            {
                cell = new int2((int)x, 0),
                pos = new float3(x, 0f, 0f),
                aggroed = aggroed,
                simId = simId,
            };

        private static int SelectOne(params AggroCandidate[] items)
        {
            using var cands = new NativeArray<AggroCandidate>(items, Allocator.Temp);
            using var outIdx = new NativeArray<int>(1, Allocator.Temp);
            int n = AggroTargeting.SelectTargets(
                new int2(0, 0), float3.zero, tileRange: 5, held: 0, capacity: 1, cands, outIdx);
            return n > 0 ? outIdx[0] : -1;
        }

        [Test]
        public void EquidistantTie_BreaksBySimId_RegardlessOfArrayOrder()
        {
            // 좌우 대칭 등거리 (x=+2 / x=-2, d² 정확히 동일). simId 12 가 77 을 이긴다.
            int first = SelectOne(C(2f, 77), C(-2f, 12));
            int second = SelectOne(C(-2f, 12), C(2f, 77));
            Assert.AreEqual(1, first, "배열 뒤쪽이라도 simId 낮은 쪽이 뽑힌다");
            Assert.AreEqual(0, second, "배열 순서를 뒤집어도 같은 대상");
        }
    }
}
