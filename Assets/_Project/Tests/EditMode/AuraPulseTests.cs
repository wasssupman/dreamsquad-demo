using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using Wassup.Battle.Combat;

namespace Wassup.Tests.EditMode
{
    // nightmare-whip-aura unit 0 — pulse target pick: Chebyshev boundary
    // inclusive, diagonal = Chebyshev (not Euclidean), same-cell included,
    // results cleared on entry, empty/negative-range degenerate.
    public class AuraPulseTests
    {
        private static int[] Select(int2[] cells, int2 host, int range)
        {
            using var na = new NativeArray<int2>(cells, Allocator.Temp);
            using var results = new NativeList<int>(Allocator.Temp);
            var list = results;
            AuraPulse.SelectTargets(na, host, range, ref list);
            return list.AsArray().ToArray();
        }

        [Test]
        public void Boundary_ExactRangeIncluded_BeyondExcluded()
        {
            var host = new int2(5, 5);
            var cells = new[] { new int2(8, 5), new int2(9, 5), new int2(5, 2), new int2(5, 1) };
            // Chebyshev 3(경계) 포함, 4 제외 — x/y 축 모두.
            CollectionAssert.AreEqual(new[] { 0, 2 }, Select(cells, host, 3));
        }

        [Test]
        public void Diagonal_UsesChebyshev_NotEuclidean()
        {
            var host = new int2(5, 5);
            // (8,8) = Chebyshev 3 (유클리드 ~4.24) → 포함. (8,9) = Chebyshev 4 → 제외.
            var cells = new[] { new int2(8, 8), new int2(8, 9) };
            CollectionAssert.AreEqual(new[] { 0 }, Select(cells, host, 3));
        }

        [Test]
        public void HostCell_SameCellCandidate_Included()
        {
            // self 제외는 arm 의 entity 비교 책임 — 셀 판정은 같은 셀도 대상.
            var host = new int2(4, 4);
            var cells = new[] { new int2(4, 4) };
            CollectionAssert.AreEqual(new[] { 0 }, Select(cells, host, 3));
        }

        [Test]
        public void EmptyPool_NoResults()
        {
            Assert.AreEqual(0, Select(new int2[0], new int2(0, 0), 3).Length);
        }

        [Test]
        public void NegativeRange_SelectsNothing()
        {
            var cells = new[] { new int2(1, 1) };
            Assert.AreEqual(0, Select(cells, new int2(1, 1), -1).Length);
        }

        [Test]
        public void Results_ClearedOnEntry()
        {
            using var na = new NativeArray<int2>(new[] { new int2(2, 2) }, Allocator.Temp);
            using var results = new NativeList<int>(Allocator.Temp);
            var list = results;
            list.Add(99); // 이전 펄스 잔여를 가정
            AuraPulse.SelectTargets(na, new int2(0, 0), 5, ref list);
            CollectionAssert.AreEqual(new[] { 0 }, list.AsArray().ToArray());
        }

        // ── boss-mamemo unit 1 — 도넛(annulus) 선택 ──────────────────────────────

        private static int[] SelectRing(int2[] cells, int2 host, int min, int max)
        {
            using var na = new NativeArray<int2>(cells, Allocator.Temp);
            using var results = new NativeList<int>(Allocator.Temp);
            var list = results;
            AuraPulse.SelectRing(na, host, min, max, ref list);
            return list.AsArray().ToArray();
        }

        // 자장가의 핵심 규칙: 사거리 **안**(minRange 미만)은 재우지 않는다. 안 그러면
        // 마메모가 자기 평타로 자기가 재운 유닛을 깨우는 자기무효화가 된다.
        [Test]
        public void Ring_ExcludesInsideMinRange_KeepsOutside()
        {
            var host = new int2(5, 5);
            var cells = new[]
            {
                new int2(5, 5), // 0: 같은 셀 (Chebyshev 0) — 사거리 안 → 제외
                new int2(7, 5), // 1: 2 — 경계 안쪽(min=3 미만) → 제외
                new int2(8, 5), // 2: 3 — 경계 = 포함
                new int2(9, 5), // 3: 4 → 포함
                new int2(11, 5),// 4: 6 → max 5 초과 제외
            };
            CollectionAssert.AreEqual(new[] { 2, 3 }, SelectRing(cells, host, 3, 5));
        }

        // minRange 경계는 **포함**이다(>=). max 와 같은 컨벤션이라 둘이 갈리지 않는다.
        [Test]
        public void Ring_MinBoundaryIsInclusive()
        {
            var host = new int2(0, 0);
            var cells = new[] { new int2(2, 0), new int2(1, 0) };
            CollectionAssert.AreEqual(new[] { 0 }, SelectRing(cells, host, 2, 4));
        }

        // min<=0 이면 기존 전범위 선택과 동치 — whip 오라 무회귀의 근거다.
        [Test]
        public void Ring_ZeroMin_MatchesSelectTargets()
        {
            var host = new int2(3, 3);
            var cells = new[] { new int2(3, 3), new int2(5, 4), new int2(9, 9) };
            CollectionAssert.AreEqual(Select(cells, host, 2), SelectRing(cells, host, 0, 2));
            CollectionAssert.AreEqual(Select(cells, host, 2), SelectRing(cells, host, -5, 2));
        }

        // min > max = 저작 실수. 조용히 전부 고르지 말고 아무도 안 고른다.
        [Test]
        public void Ring_MinGreaterThanMax_SelectsNothing()
        {
            var cells = new[] { new int2(1, 0), new int2(4, 0), new int2(7, 0) };
            Assert.AreEqual(0, SelectRing(cells, new int2(0, 0), 5, 3).Length);
        }

        [Test]
        public void Ring_NegativeMax_SelectsNothing()
        {
            var cells = new[] { new int2(1, 1) };
            Assert.AreEqual(0, SelectRing(cells, new int2(1, 1), 0, -1).Length);
        }

        // 리뷰 H1 회귀 가드 — **사거리 링과 수면 링은 겹치면 안 된다.**
        // 공격 판정이 `tileDist > tileRange` 면 skip 이라 **경계 링 자체가 사거리 안**이고,
        // SelectRing 의 min 은 inclusive 다. 그래서 호출처는 `attackTiles + 1` 을 넘겨야 한다.
        // 여기서 고정하는 것은 그 산수다: min=attackTiles 로 부르면 사거리 링이 딸려온다.
        [Test]
        public void Ring_MinMustBeAttackTilesPlusOne_ToExcludeAttackableRing()
        {
            var host = new int2(0, 0);
            const int attackTiles = 2;
            var cells = new[]
            {
                new int2(attackTiles, 0),     // 0: 사거리 링 — 때릴 수 있다 → 재우면 안 된다
                new int2(attackTiles + 1, 0), // 1: 첫 안전 링
            };
            // 잘못된 호출(min = attackTiles): 사거리 링이 딸려온다 — 이게 H1 이었다.
            CollectionAssert.AreEqual(new[] { 0, 1 }, SelectRing(cells, host, attackTiles, 4));
            // 올바른 호출(min = attackTiles + 1): 사거리 링 제외.
            CollectionAssert.AreEqual(new[] { 1 }, SelectRing(cells, host, attackTiles + 1, 4));
        }
    }
}
