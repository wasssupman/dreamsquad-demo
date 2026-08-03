using NUnit.Framework;
using Unity.Collections;
using Wassup.Battle.Combat;

namespace Wassup.Tests.EditMode
{
    // dreamcatcher-content-2 unit 1 — the frontmost ranking invariant. Flow-field
    // remaining distance dominates world distance, then squared distance, then a
    // deterministic simId tie-break; unreachable candidates are excluded.
    public class FrontmostTargetingTests
    {
        [Test]
        public void FlowDistance_BeatsCloserWorldDistance()
        {
            // Candidate A is closer in the world (sqDist 1) but farther along the path
            // (flowDist 10); B is farther in world (sqDist 100) but nearer the goal
            // (flowDist 2). Frontmost = B.
            var cands = new NativeArray<FrontmostTargeting.Candidate>(2, Allocator.Temp);
            cands[0] = new FrontmostTargeting.Candidate { flowDist = 10, sqDist = 1f, simId = 5 };
            cands[1] = new FrontmostTargeting.Candidate { flowDist = 2, sqDist = 100f, simId = 9 };
            Assert.AreEqual(1, FrontmostTargeting.SelectFrontmost(cands, 2));
            cands.Dispose();
        }

        [Test]
        public void EqualFlow_BreaksBySquaredDistance()
        {
            var cands = new NativeArray<FrontmostTargeting.Candidate>(2, Allocator.Temp);
            cands[0] = new FrontmostTargeting.Candidate { flowDist = 5, sqDist = 40f, simId = 2 };
            cands[1] = new FrontmostTargeting.Candidate { flowDist = 5, sqDist = 9f, simId = 8 };
            Assert.AreEqual(1, FrontmostTargeting.SelectFrontmost(cands, 2));
            cands.Dispose();
        }

        [Test]
        public void EqualFlowAndDistance_BreaksBySimId()
        {
            // battle-sim-extraction unit 1 — tiebreak 축이 Entity.Index/Version → simId 로 교체됨.
            var a = new FrontmostTargeting.Candidate { flowDist = 3, sqDist = 4f, simId = 7 };
            var bLowerId = new FrontmostTargeting.Candidate { flowDist = 3, sqDist = 4f, simId = 4 };
            Assert.IsTrue(FrontmostTargeting.RanksBefore(bLowerId, a), "lower simId ranks first");
            Assert.IsFalse(FrontmostTargeting.RanksBefore(a, bLowerId), "higher simId never ranks first on full tie");
        }

        [Test]
        public void UnreachableCandidates_AreExcluded()
        {
            var cands = new NativeArray<FrontmostTargeting.Candidate>(2, Allocator.Temp);
            cands[0] = new FrontmostTargeting.Candidate { flowDist = FrontmostTargeting.UnreachableDist, sqDist = 1f, simId = 1 };
            cands[1] = new FrontmostTargeting.Candidate { flowDist = 50, sqDist = 100f, simId = 2 };
            // The nearer-in-world candidate is unreachable → the reachable far one wins.
            Assert.AreEqual(1, FrontmostTargeting.SelectFrontmost(cands, 2));
            cands.Dispose();
        }

        [Test]
        public void AllUnreachable_ReturnsMinusOne()
        {
            var cands = new NativeArray<FrontmostTargeting.Candidate>(2, Allocator.Temp);
            cands[0] = new FrontmostTargeting.Candidate { flowDist = FrontmostTargeting.UnreachableDist, sqDist = 1f, simId = 1 };
            cands[1] = new FrontmostTargeting.Candidate { flowDist = FrontmostTargeting.UnreachableDist, sqDist = 2f, simId = 2 };
            Assert.AreEqual(-1, FrontmostTargeting.SelectFrontmost(cands, 2));
            cands.Dispose();
        }

        [Test]
        public void EmptyCandidates_ReturnsMinusOne()
        {
            var cands = new NativeArray<FrontmostTargeting.Candidate>(1, Allocator.Temp);
            Assert.AreEqual(-1, FrontmostTargeting.SelectFrontmost(cands, 0));
            cands.Dispose();
        }
    }
}
