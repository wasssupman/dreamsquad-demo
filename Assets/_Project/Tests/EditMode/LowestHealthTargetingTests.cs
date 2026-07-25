using NUnit.Framework;
using Unity.Collections;
using Wassup.Battle.Combat;

namespace Wassup.Tests.EditMode
{
    // healer-lowest-health-targeting unit 0 — the most-hurt-ally ranking invariant.
    // HP ratio dominates world distance, then squared distance, then a deterministic
    // entity tie-break. Mirrors FrontmostTargetingTests' structure.
    public class LowestHealthTargetingTests
    {
        [Test]
        public void LowerHpRatio_BeatsCloserAlly()
        {
            // Candidate A is closer (sqDist 1) but healthier (ratio 0.9); B is farther
            // (sqDist 100) but more hurt (ratio 0.2). Most-hurt = B.
            var cands = new NativeArray<LowestHealthTargeting.Candidate>(2, Allocator.Temp);
            cands[0] = new LowestHealthTargeting.Candidate { hpRatio = 0.9f, sqDist = 1f, entityIndex = 5, entityVersion = 1 };
            cands[1] = new LowestHealthTargeting.Candidate { hpRatio = 0.2f, sqDist = 100f, entityIndex = 9, entityVersion = 1 };
            Assert.AreEqual(1, LowestHealthTargeting.SelectLowest(cands, 2));
            cands.Dispose();
        }

        [Test]
        public void EqualHpRatio_BreaksBySquaredDistance()
        {
            var cands = new NativeArray<LowestHealthTargeting.Candidate>(2, Allocator.Temp);
            cands[0] = new LowestHealthTargeting.Candidate { hpRatio = 0.5f, sqDist = 40f, entityIndex = 2, entityVersion = 1 };
            cands[1] = new LowestHealthTargeting.Candidate { hpRatio = 0.5f, sqDist = 9f, entityIndex = 8, entityVersion = 1 };
            Assert.AreEqual(1, LowestHealthTargeting.SelectLowest(cands, 2));
            cands.Dispose();
        }

        [Test]
        public void EqualRatioAndDistance_BreaksByEntityIndexThenVersion()
        {
            var a = new LowestHealthTargeting.Candidate { hpRatio = 0.3f, sqDist = 4f, entityIndex = 7, entityVersion = 2 };
            var bLowerIdx = new LowestHealthTargeting.Candidate { hpRatio = 0.3f, sqDist = 4f, entityIndex = 4, entityVersion = 9 };
            Assert.IsTrue(LowestHealthTargeting.RanksBefore(bLowerIdx, a), "lower entityIndex ranks first");

            var sameIdxLowerVer = new LowestHealthTargeting.Candidate { hpRatio = 0.3f, sqDist = 4f, entityIndex = 7, entityVersion = 1 };
            Assert.IsTrue(LowestHealthTargeting.RanksBefore(sameIdxLowerVer, a), "same index, lower version ranks first");
        }

        [Test]
        public void FullHpAlly_IsStillSelectableWhenAlone()
        {
            // "그냥 재정렬" — no full-HP skip. A single full-HP ally is a valid target.
            var cands = new NativeArray<LowestHealthTargeting.Candidate>(1, Allocator.Temp);
            cands[0] = new LowestHealthTargeting.Candidate { hpRatio = 1f, sqDist = 25f, entityIndex = 3, entityVersion = 1 };
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
