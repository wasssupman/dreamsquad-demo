using NUnit.Framework;
using Wassup.Skills;

namespace Wassup.Tests.EditMode
{
    // shield-guardian-defender unit 1 — 필터별 대상 선별(순수) 검증.
    //
    // skill-layer-migration unit 5b — 규칙이 `Wassup.Skills.SkillShieldSelect` 로 이사했다
    // (`SkillAim` 과 같은 이사: 도메인은 `NativeArray` 를 모른다). **단언 여덟은 그대로다** —
    // 바뀐 것은 그릇뿐이고, 그 여덟이 이 규칙의 정본이다.
    public class ShieldTargetingTests
    {
        private readonly struct Cands
        {
            public readonly float[] DistSq;
            public readonly float[] Hp;
            public readonly int N;
            public Cands((float distSq, float effHp)[] items)
            {
                N = items.Length;
                DistSq = new float[N];
                Hp = new float[N];
                for (int i = 0; i < N; i++) { DistSq[i] = items[i].distSq; Hp[i] = items[i].effHp; }
            }
        }

        private static Cands Candidates(params (float distSq, float effHp)[] items) => new Cands(items);

        private static int[] Select(SkillShieldFilter filter, int count, int selfIndex, Cands c)
        {
            var into = new int[16];
            int n = SkillShieldSelect.Select(filter, count, selfIndex, c.DistSq, c.Hp, c.N, into);
            var result = new int[n];
            System.Array.Copy(into, result, n);
            return result;
        }

        [Test]
        public void Self_PicksSelfOnly_IgnoresCountAndKeys()
        {
            var cands = Candidates((0f, 1f), (1f, 0.1f), (4f, 0.2f));
            var results = Select(SkillShieldFilter.Self, 3, 0, cands);
            Assert.AreEqual(1, results.Length);
            Assert.AreEqual(0, results[0]);
        }

        [Test]
        public void All_SortsByDistance_TakesC()
        {
            var cands = Candidates((9f, 0.1f), (1f, 1f), (4f, 0.5f));
            var results = Select(SkillShieldFilter.Nearest, 2, 0, cands);
            Assert.AreEqual(2, results.Length);
            Assert.AreEqual(1, results[0], "nearest first");
            Assert.AreEqual(2, results[1]);
        }

        [Test]
        public void MinHealth_SortsByEffectiveHp_TakesC()
        {
            var cands = Candidates((0f, 1f), (1f, 0.3f), (4f, 0.6f));
            var results = Select(SkillShieldFilter.MostHurt, 2, 0, cands);
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
            var results = Select(SkillShieldFilter.MostHurt, 1, 0, cands);
            Assert.AreEqual(1, results.Length);
            Assert.AreEqual(1, results[0], "unshielded 60% must outrank shield-full 50%");
        }

        [Test]
        public void CandidatesFewerThanC_TakesAll()
        {
            var cands = Candidates((1f, 0.5f), (2f, 0.7f));
            var results = Select(SkillShieldFilter.Nearest, 5, 0, cands);
            Assert.AreEqual(2, results.Length);
        }

        [Test]
        public void Self_IsRegularCandidate_InOtherFilters()
        {
            // self(index 2)가 최근접이면 All 에서 특별 취급 없이 그냥 1순위로 뽑힌다.
            var cands = Candidates((9f, 1f), (4f, 1f), (0f, 1f));
            var results = Select(SkillShieldFilter.Nearest, 1, 2, cands);
            Assert.AreEqual(2, results[0]);
        }

        [Test]
        public void TieBreak_IsDeterministic_ByIndex()
        {
            var cands = Candidates((1f, 0.5f), (1f, 0.5f), (1f, 0.5f));
            var all = Select(SkillShieldFilter.Nearest, 2, 0, cands);
            Assert.AreEqual(0, all[0]);
            Assert.AreEqual(1, all[1]);
            var min = Select(SkillShieldFilter.MostHurt, 2, 0, cands);
            Assert.AreEqual(0, min[0]);
            Assert.AreEqual(1, min[1]);
        }

        [Test]
        public void SelfIndexMissing_SelfFilter_PicksNothing()
        {
            var cands = Candidates((1f, 0.5f));
            var results = Select(SkillShieldFilter.Self, 1, -1, cands);
            Assert.AreEqual(0, results.Length, "self absent from candidates → no grant");
        }
    }
}
