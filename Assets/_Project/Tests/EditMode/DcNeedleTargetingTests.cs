using NUnit.Framework;
using Unity.Collections;
using Wassup.Battle.Combat;

namespace Wassup.Tests.EditMode
{
    // dreamcatcher-attack-decoupling unit 2 — 니들 폴백 선정의 결정론 핀.
    // 실제 호출은 unit 3·4(폭탄맨/캐스터 사건 지점)에서 붙는다.
    public class DcNeedleTargetingTests
    {
        private static DcNeedleTargeting.Candidate C(float sqDist, int tileDist,
            int index, int version = 1, bool eligible = true) =>
            new DcNeedleTargeting.Candidate
            {
                eligible = eligible,
                tileDist = tileDist,
                sqDist = sqDist,
                entityIndex = index,
                entityVersion = version,
            };

        private static int Select(int tileRange, params DcNeedleTargeting.Candidate[] items)
        {
            using var arr = new NativeArray<DcNeedleTargeting.Candidate>(items, Allocator.Temp);
            return DcNeedleTargeting.SelectNearest(arr, tileRange);
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
            // eligible=false = caller 의 진영/PastGoal/사망 필터 탈락(아군·해저드 포함).
            int i = Select(5, C(1f, 1, 10, eligible: false), C(9f, 3, 11));
            Assert.AreEqual(1, i, "아군을 겨누지 않는다 — 진영은 caller 가 Enemy 로 고정");
        }

        [Test]
        public void ReturnsMinusOneWhenNoCandidate()
        {
            Assert.AreEqual(-1, Select(4), "후보 배열이 비었을 때");
            Assert.AreEqual(-1, Select(4, C(1f, 9, 10)), "전부 반경 밖");
            Assert.AreEqual(-1, Select(4, C(1f, 1, 10, eligible: false)), "전부 부적격");
        }

        [Test]
        public void TieBreakIsDeterministic_ByEntityIndexThenVersion()
        {
            // 같은 거리 → 배열 순서와 무관하게 낮은 index 가 이긴다.
            Assert.AreEqual(1, Select(4, C(4f, 2, 77), C(4f, 2, 12)));
            Assert.AreEqual(0, Select(4, C(4f, 2, 12), C(4f, 2, 77)));
            // index 도 같으면 version.
            Assert.AreEqual(1, Select(4, C(4f, 2, 12, version: 9), C(4f, 2, 12, version: 3)));
        }

        [Test]
        public void NonPositiveRange_DisablesFallback()
        {
            // B안에서 tileRange 0 은 "발동 불가"가 아니라 "폴백 없음"이다.
            // 자기 셀만 뒤지는 대신 아무것도 고르지 않는다.
            Assert.AreEqual(-1, Select(0, C(0f, 0, 10)));
            Assert.AreEqual(-1, Select(-1, C(0f, 0, 10)));
        }
    }
}
