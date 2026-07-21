using NUnit.Framework;
using Unity.Collections;
using Wassup.Battle.Effects;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // shield-guardian-defender unit 1 — 필터별 대상 선별(순수) 검증.
    // World 불필요 — plain NativeArray/NativeList 입력.
    public class ShieldTargetingTests
    {
        private static NativeArray<ShieldCandidate> Candidates(params (float distSq, float effHp)[] items)
        {
            var arr = new NativeArray<ShieldCandidate>(items.Length, Allocator.Temp);
            for (int i = 0; i < items.Length; i++)
                arr[i] = new ShieldCandidate { distanceSq = items[i].distSq, effectiveHpRatio = items[i].effHp };
            return arr;
        }

        private static NativeList<int> Select(ShieldTargetFilter filter, int count, int selfIndex,
            NativeArray<ShieldCandidate> cands)
        {
            var results = new NativeList<int>(8, Allocator.Temp);
            ShieldTargeting.Select(filter, count, selfIndex, cands, ref results);
            return results;
        }

        [Test]
        public void Self_PicksSelfOnly_IgnoresCountAndKeys()
        {
            var cands = Candidates((0f, 1f), (1f, 0.1f), (4f, 0.2f));
            var results = Select(ShieldTargetFilter.Self, 3, 0, cands);
            Assert.AreEqual(1, results.Length);
            Assert.AreEqual(0, results[0]);
        }

        [Test]
        public void All_SortsByDistance_TakesC()
        {
            var cands = Candidates((9f, 0.1f), (1f, 1f), (4f, 0.5f));
            var results = Select(ShieldTargetFilter.All, 2, 0, cands);
            Assert.AreEqual(2, results.Length);
            Assert.AreEqual(1, results[0], "nearest first");
            Assert.AreEqual(2, results[1]);
        }

        [Test]
        public void MinHealth_SortsByEffectiveHp_TakesC()
        {
            var cands = Candidates((0f, 1f), (1f, 0.3f), (4f, 0.6f));
            var results = Select(ShieldTargetFilter.MinHealth, 2, 0, cands);
            Assert.AreEqual(2, results.Length);
            Assert.AreEqual(1, results[0], "lowest effective hp first");
            Assert.AreEqual(2, results[1]);
        }

        [Test]
        public void MinHealth_FullShieldLowHp_LosesTo_UnshieldedHigherHp()
        {
            // A: HP 50% + 실드 50% → 유효 1.0 / B: HP 60% 무실드 → 유효 0.6.
            // 실드 무시 정렬이면 A가 이기는 함정 — 유효HP 정렬로 B가 이겨야 한다(계약 6).
            var cands = Candidates((1f, 1.0f), (4f, 0.6f));
            var results = Select(ShieldTargetFilter.MinHealth, 1, 0, cands);
            Assert.AreEqual(1, results.Length);
            Assert.AreEqual(1, results[0], "unshielded 60% must outrank shield-full 50%");
        }

        [Test]
        public void CandidatesFewerThanC_TakesAll()
        {
            var cands = Candidates((1f, 0.5f), (2f, 0.7f));
            var results = Select(ShieldTargetFilter.All, 5, 0, cands);
            Assert.AreEqual(2, results.Length);
        }

        [Test]
        public void Self_IsRegularCandidate_InOtherFilters()
        {
            // self(index 2)가 최근접이면 All 에서 특별 취급 없이 그냥 1순위로 뽑힌다.
            var cands = Candidates((9f, 1f), (4f, 1f), (0f, 1f));
            var results = Select(ShieldTargetFilter.All, 1, 2, cands);
            Assert.AreEqual(2, results[0]);
        }

        [Test]
        public void TieBreak_IsDeterministic_ByIndex()
        {
            var cands = Candidates((1f, 0.5f), (1f, 0.5f), (1f, 0.5f));
            var all = Select(ShieldTargetFilter.All, 2, 0, cands);
            Assert.AreEqual(0, all[0]);
            Assert.AreEqual(1, all[1]);
            var min = Select(ShieldTargetFilter.MinHealth, 2, 0, cands);
            Assert.AreEqual(0, min[0]);
            Assert.AreEqual(1, min[1]);
        }

        [Test]
        public void SelfIndexMissing_SelfFilter_PicksNothing()
        {
            var cands = Candidates((1f, 0.5f));
            var results = Select(ShieldTargetFilter.Self, 1, -1, cands);
            Assert.AreEqual(0, results.Length, "self absent from candidates → no grant");
        }
    }
}
