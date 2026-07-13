using NUnit.Framework;
using Unity.Collections;
using Wassup.Battle.Combat;

namespace Wassup.Tests.EditMode
{
    // dreamcatcher-content-2 unit 1 — the frontmost ranking invariant. Flow-field
    // remaining distance dominates world distance, then squared distance, then a
    // deterministic entity tie-break; unreachable candidates are excluded.
    public class FrontmostTargetingTests
    {
        [Test]
        public void FlowDistance_BeatsCloserWorldDistance()
        {
            // Candidate A is closer in the world (sqDist 1) but farther along the path
            // (flowDist 10); B is farther in world (sqDist 100) but nearer the goal
            // (flowDist 2). Frontmost = B.
            var cands = new NativeArray<FrontmostTargeting.Candidate>(2, Allocator.Temp);
            cands[0] = new FrontmostTargeting.Candidate { flowDist = 10, sqDist = 1f, entityIndex = 5, entityVersion = 1 };
            cands[1] = new FrontmostTargeting.Candidate { flowDist = 2, sqDist = 100f, entityIndex = 9, entityVersion = 1 };
            Assert.AreEqual(1, FrontmostTargeting.SelectFrontmost(cands, 2));
            cands.Dispose();
        }

        [Test]
        public void EqualFlow_BreaksBySquaredDistance()
        {
            var cands = new NativeArray<FrontmostTargeting.Candidate>(2, Allocator.Temp);
            cands[0] = new FrontmostTargeting.Candidate { flowDist = 5, sqDist = 40f, entityIndex = 2, entityVersion = 1 };
            cands[1] = new FrontmostTargeting.Candidate { flowDist = 5, sqDist = 9f, entityIndex = 8, entityVersion = 1 };
            Assert.AreEqual(1, FrontmostTargeting.SelectFrontmost(cands, 2));
            cands.Dispose();
        }

        [Test]
        public void EqualFlowAndDistance_BreaksByEntityIndexThenVersion()
        {
            var a = new FrontmostTargeting.Candidate { flowDist = 3, sqDist = 4f, entityIndex = 7, entityVersion = 2 };
            var bLowerIdx = new FrontmostTargeting.Candidate { flowDist = 3, sqDist = 4f, entityIndex = 4, entityVersion = 9 };
            Assert.IsTrue(FrontmostTargeting.RanksBefore(bLowerIdx, a), "lower entityIndex ranks first");

            var sameIdxLowerVer = new FrontmostTargeting.Candidate { flowDist = 3, sqDist = 4f, entityIndex = 7, entityVersion = 1 };
            Assert.IsTrue(FrontmostTargeting.RanksBefore(sameIdxLowerVer, a), "same index, lower version ranks first");
        }

        [Test]
        public void UnreachableCandidates_AreExcluded()
        {
            var cands = new NativeArray<FrontmostTargeting.Candidate>(2, Allocator.Temp);
            cands[0] = new FrontmostTargeting.Candidate { flowDist = FrontmostTargeting.UnreachableDist, sqDist = 1f, entityIndex = 1, entityVersion = 1 };
            cands[1] = new FrontmostTargeting.Candidate { flowDist = 50, sqDist = 100f, entityIndex = 2, entityVersion = 1 };
            // The nearer-in-world candidate is unreachable → the reachable far one wins.
            Assert.AreEqual(1, FrontmostTargeting.SelectFrontmost(cands, 2));
            cands.Dispose();
        }

        [Test]
        public void AllUnreachable_ReturnsMinusOne()
        {
            var cands = new NativeArray<FrontmostTargeting.Candidate>(2, Allocator.Temp);
            cands[0] = new FrontmostTargeting.Candidate { flowDist = FrontmostTargeting.UnreachableDist, sqDist = 1f, entityIndex = 1, entityVersion = 1 };
            cands[1] = new FrontmostTargeting.Candidate { flowDist = FrontmostTargeting.UnreachableDist, sqDist = 2f, entityIndex = 2, entityVersion = 1 };
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
