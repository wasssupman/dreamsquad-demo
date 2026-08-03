using NUnit.Framework;
using Unity.Collections;
using Wassup.Battle.Combat;

namespace Wassup.Tests.EditMode
{
    // 반경 내 최근접 선정의 결정론 핀. 이 유틸은 특정 카드/효과에 속하지 않으므로
    // 테스트도 호출 맥락과 무관하게 규칙 자체만 고정한다.
    public class NearestTargetingTests
    {
        private static NearestTargeting.Candidate C(float sqDist, int tileDist,
            int simId, bool eligible = true) =>
            new NearestTargeting.Candidate
            {
                eligible = eligible,
                tileDist = tileDist,
                sqDist = sqDist,
                simId = simId,
            };

        private static int Select(int tileRange, params NearestTargeting.Candidate[] items)
        {
            using var arr = new NativeArray<NearestTargeting.Candidate>(items, Allocator.Temp);
            return NearestTargeting.SelectNearest(arr, tileRange);
        }

        [Test]
        public void PicksNearestWithinRange()
        {
            int i = Select(4, C(25f, 5, 10), C(4f, 2, 11), C(9f, 3, 12));
            Assert.AreEqual(1, i, "반경 안 최근접(sqDist 4)");
        }

        [Test]
        public void ExcludesOutOfRange()
        {
            // 가장 가까운 후보가 반경 밖이면 그 다음이 뽑힌다.
            int i = Select(2, C(1f, 9, 10), C(16f, 2, 11));
            Assert.AreEqual(1, i);
        }

        [Test]
        public void ExcludesIneligible()
        {
            // eligible=false = 호출부의 진영/PastGoal/사망 필터 탈락.
            int i = Select(5, C(1f, 1, 10, eligible: false), C(9f, 3, 11));
            Assert.AreEqual(1, i, "부적격 후보는 더 가까워도 뽑히지 않는다");
        }

        [Test]
        public void ReturnsMinusOneWhenNoCandidate()
        {
            Assert.AreEqual(-1, Select(4), "후보 배열이 비었을 때");
            Assert.AreEqual(-1, Select(4, C(1f, 9, 10)), "전부 반경 밖");
            Assert.AreEqual(-1, Select(4, C(1f, 1, 10, eligible: false)), "전부 부적격");
        }

        [Test]
        public void TieBreakIsDeterministic_BySimId()
        {
            // 같은 거리 → 배열 순서와 무관하게 낮은 simId(먼저 스폰된 쪽)가 이긴다.
            // battle-sim-extraction unit 1 — 축이 Entity.Index/Version 에서 교체됨.
            Assert.AreEqual(1, Select(4, C(4f, 2, 77), C(4f, 2, 12)));
            Assert.AreEqual(0, Select(4, C(4f, 2, 12), C(4f, 2, 77)));
        }

        [Test]
        public void NonPositiveRange_SelectsNothing()
        {
            // 반경 0 을 "자기 셀만 검색"으로 읽지 않는다 — 계약은 '선정 없음'이다.
            Assert.AreEqual(-1, Select(0, C(0f, 0, 10)));
            Assert.AreEqual(-1, Select(-1, C(0f, 0, 10)));
        }
    }
}
