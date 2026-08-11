using NUnit.Framework;
using Unity.Mathematics;
using Wassup.Battle.Movement;

namespace Wassup.Tests.EditMode
{
    public class WaypointProgressTests
    {
        [Test]
        public void NotReached_KeepsCurrentIndex()
        {
            Step(new int2(1, 0), new int2(2, 0), reachable: true,
                index: 0, count: 2,
                expectedIndex: 0, expectedAdvanced: false, expectedDone: false);
        }

        [Test]
        public void MatchingCell_AdvancesToNextWaypoint()
        {
            Step(new int2(2, 0), new int2(2, 0), reachable: true,
                index: 0, count: 2,
                expectedIndex: 1, expectedAdvanced: true, expectedDone: false);
        }

        [Test]
        public void ReachingLastWaypoint_CompletesPath()
        {
            Step(new int2(3, 0), new int2(3, 0), reachable: true,
                index: 1, count: 2,
                expectedIndex: 2, expectedAdvanced: true, expectedDone: true);
        }

        [Test]
        public void UnreachableWaypoint_IsSkipped()
        {
            Step(new int2(0, 0), new int2(3, 0), reachable: false,
                index: 0, count: 2,
                expectedIndex: 1, expectedAdvanced: true, expectedDone: false);
        }

        [Test]
        public void EmptyPath_IsImmediatelyDone()
        {
            Step(new int2(0, 0), int2.zero, reachable: false,
                index: 0, count: 0,
                expectedIndex: 0, expectedAdvanced: false, expectedDone: true);
        }

        private static void Step(
            int2 currentCell,
            int2 waypointCell,
            bool reachable,
            int index,
            int count,
            int expectedIndex,
            bool expectedAdvanced,
            bool expectedDone)
        {
            WaypointProgress.Step(
                currentCell, waypointCell, reachable, index, count,
                out int nextIndex, out bool advanced, out bool done);

            Assert.AreEqual(expectedIndex, nextIndex);
            Assert.AreEqual(expectedAdvanced, advanced);
            Assert.AreEqual(expectedDone, done);
        }
    }
}
