using NUnit.Framework;
using Unity.Mathematics;
using Wassup.Battle.Movement;

namespace Wassup.Tests.EditMode
{
    public class WaypointProgressTests
    {
        // waypoint-routing unit 9 — 도달 판정이 셀 일치에서 체비셰프 1 이내로
        // 완화됐다(스웜이 축분리 스윕에 밀려 한 칸에 수렴 못 함). 1칸 떨어진
        // (2,0)→(1,0)은 이제 도달로 인정되므로, "아직 안 닿음"은 2칸 이상으로 검증한다.
        [Test]
        public void NotReached_KeepsCurrentIndex()
        {
            Step(new int2(0, 0), new int2(2, 0), reachable: true,
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

        // waypoint-routing unit 9 — 대각 인접(체비셰프 1)도 도달로 인정된다.
        [Test]
        public void DiagonallyAdjacentCell_Advances()
        {
            Step(new int2(1, 1), new int2(2, 2), reachable: true,
                index: 0, count: 2,
                expectedIndex: 1, expectedAdvanced: true, expectedDone: false);
        }

        // waypoint-routing unit 9 — 대각으로도 체비셰프 2 이상은 아직 도달이 아니다
        // (직선 2칸 케이스는 NotReached_KeepsCurrentIndex 가 이미 커버).
        [Test]
        public void TwoCellsAwayDiagonally_DoesNotAdvance()
        {
            Step(new int2(0, 0), new int2(2, 2), reachable: true,
                index: 0, count: 2,
                expectedIndex: 0, expectedAdvanced: false, expectedDone: false);
        }

        // waypoint-routing unit 9 회귀 가드 — 축분리 스윕에 밀려 목표 칸을 스치고
        // 지나간 개체(대각으로 한 칸 넘어감)도 advanced 가 서서 되돌아오지 않는다.
        [Test]
        public void OvershotAdjacentCell_StillAdvances_DoesNotBounceBack()
        {
            Step(new int2(3, 1), new int2(2, 0), reachable: true,
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

    // waypoint-routing unit 9 — 레인 경로 해석 우선순위.
    // duel-route-tours unit 1 — 컨셉 축이 가운데로 들어와 3축이 됐다.
    // 좁은 순서: 적 SO(종의 정체성) > 컨셉(이번 편성) > 레인 기본(맵의 성질).
    public class WaypointRoutingTests
    {
        [Test]
        public void AuthoredPathIndex_Wins()
        {
            Assert.AreEqual(3, WaypointRouting.ResolvePathIndex(
                authoredPathIndex: 3, conceptPathIndex: 4, laneDefaultPathIndex: 5));
        }

        [Test]
        public void NoAuthoredIndex_ConceptWinsOverLaneDefault()
        {
            Assert.AreEqual(4, WaypointRouting.ResolvePathIndex(
                authoredPathIndex: -1, conceptPathIndex: 4, laneDefaultPathIndex: 5));
        }

        [Test]
        public void NoAuthoredIndex_FallsBackToLaneDefault()
        {
            Assert.AreEqual(5, WaypointRouting.ResolvePathIndex(
                authoredPathIndex: -1, conceptPathIndex: -1, laneDefaultPathIndex: 5));
        }

        [Test]
        public void NeitherAxisSet_ReturnsMinusOne_GoesStraightToGoal()
        {
            Assert.AreEqual(-1, WaypointRouting.ResolvePathIndex(
                authoredPathIndex: -1, conceptPathIndex: -1, laneDefaultPathIndex: -1));
        }

        // 비행 적이 컨셉에 실려도 자기 경로를 잃지 않는다 — 컨셉이 덮으면 강을 못 건넌다.
        // 「좁은 쪽이 이긴다」가 여기서 안전 규칙으로 작동한다는 것을 고정한다.
        [Test]
        public void ConceptCannotOverrideSpeciesRoute()
        {
            Assert.AreEqual(0, WaypointRouting.ResolvePathIndex(
                authoredPathIndex: 0, conceptPathIndex: 2, laneDefaultPathIndex: -1),
                "Skimmer 의 공중 경로(0)는 어떤 컨셉에 실려도 유지되어야 한다");
        }
    }
}
