using NUnit.Framework;
using Unity.Collections;
using Wassup.Battle.Combat;

namespace Wassup.Tests.EditMode
{
    // healer-lowest-health-targeting unit 0 — the most-hurt-ally ranking invariant.
    // HP ratio dominates world distance, then squared distance, then a deterministic
    // simId tie-break. Mirrors FrontmostTargetingTests' structure.
    public class LowestHealthTargetingTests
    {
        [Test]
        public void LowerHpRatio_BeatsCloserAlly()
        {
            // Candidate A is closer (sqDist 1) but healthier (ratio 0.9); B is farther
            // (sqDist 100) but more hurt (ratio 0.2). Most-hurt = B.
            var cands = new NativeArray<LowestHealthTargeting.Candidate>(2, Allocator.Temp);
            cands[0] = new LowestHealthTargeting.Candidate { hpRatio = 0.9f, sqDist = 1f, simId = 5 };
            cands[1] = new LowestHealthTargeting.Candidate { hpRatio = 0.2f, sqDist = 100f, simId = 9 };
            Assert.AreEqual(1, LowestHealthTargeting.SelectLowest(cands, 2));
            cands.Dispose();
        }

        [Test]
        public void EqualHpRatio_BreaksBySquaredDistance()
        {
            var cands = new NativeArray<LowestHealthTargeting.Candidate>(2, Allocator.Temp);
            cands[0] = new LowestHealthTargeting.Candidate { hpRatio = 0.5f, sqDist = 40f, simId = 2 };
            cands[1] = new LowestHealthTargeting.Candidate { hpRatio = 0.5f, sqDist = 9f, simId = 8 };
            Assert.AreEqual(1, LowestHealthTargeting.SelectLowest(cands, 2));
            cands.Dispose();
        }

        [Test]
        public void EqualRatioAndDistance_BreaksBySimId()
        {
            // battle-sim-extraction unit 1 — tiebreak 축이 Entity.Index/Version → simId 로 교체됨.
            var a = new LowestHealthTargeting.Candidate { hpRatio = 0.3f, sqDist = 4f, simId = 7 };
            var bLowerId = new LowestHealthTargeting.Candidate { hpRatio = 0.3f, sqDist = 4f, simId = 4 };
            Assert.IsTrue(LowestHealthTargeting.RanksBefore(bLowerId, a), "lower simId ranks first");
            Assert.IsFalse(LowestHealthTargeting.RanksBefore(a, bLowerId), "higher simId never ranks first on full tie");
        }

        [Test]
        public void FullHpAlly_IsStillSelectableWhenAlone()
        {
            // "그냥 재정렬" — no full-HP skip. A single full-HP ally is a valid target.
            var cands = new NativeArray<LowestHealthTargeting.Candidate>(1, Allocator.Temp);
            cands[0] = new LowestHealthTargeting.Candidate { hpRatio = 1f, sqDist = 25f, simId = 3 };
            Assert.AreEqual(0, LowestHealthTargeting.SelectLowest(cands, 1));
            cands.Dispose();
        }

        [Test]
        public void EmptyCandidates_ReturnsMinusOne()
        {
            var cands = new NativeArray<LowestHealthTargeting.Candidate>(1, Allocator.Temp);
            Assert.AreEqual(-1, LowestHealthTargeting.SelectLowest(cands, 0));
            cands.Dispose();
        }
    }
}
