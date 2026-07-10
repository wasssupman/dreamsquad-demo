using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using Wassup.Battle.Combat;

namespace Wassup.Tests.EditMode
{
    // nightmare-catcher unit 3 — SelfBlink destination math: degenerate
    // direction → hard constant axis (NaN 금지), ring walk capped at
    // maxRingRadius (비종료 금지), deterministic candidate order.
    public class BlinkMathTests
    {
        [Test]
        public void OffsetDest_NormalCase_LandsOneTileBeyondLeader()
        {
            // 보스 (0,0,0) → 리더 (2,0,0): 방향 +x, 목적지 = 리더 + 1타일(+x).
            var dest = BlinkMath.OffsetDest(new float3(2f, 0f, 0f), float3.zero, 1f);
            Assert.AreEqual(3f, dest.x, 1e-4f);
            Assert.AreEqual(0f, dest.z, 1e-4f);
        }

        [Test]
        public void OffsetDest_DegenerateDirection_FallsBackToConstantAxis_NoNaN()
        {
            // 보스가 리더와 동일 위치 — normalize(0) NaN 경로를 상수축(-Z)으로 차단.
            var leader = new float3(4f, 0f, 4f);
            var dest = BlinkMath.OffsetDest(leader, leader, 2f);
            Assert.IsFalse(math.any(math.isnan(dest)), "NaN 금지");
            Assert.AreEqual(leader.x, dest.x, 1e-4f);
            Assert.AreEqual(leader.z - 2f, dest.z, 1e-4f, "world -Z 하드 상수축");
        }

        // ── TryFindLandingCell ──────────────────────────────────────────────

        private static NativeArray<int> Grid3x3(params int2[] blocked)
        {
            var dist = new NativeArray<int>(9, Allocator.Temp);
            for (int i = 0; i < 9; i++) dist[i] = 1; // 전부 도달 가능
            foreach (var b in blocked) dist[b.y * 3 + b.x] = int.MaxValue;
            return dist;
        }

        [Test]
        public void Landing_DesiredWalkable_ReturnsDesiredItself()
        {
            using var dist = Grid3x3();
            Assert.IsTrue(BlinkMath.TryFindLandingCell(new int2(1, 1), dist, new int2(3, 3), 3, out var landing));
            Assert.AreEqual(new int2(1, 1), landing);
        }

        [Test]
        public void Landing_DesiredBlocked_PicksFirstRowMajorRingNeighbor()
        {
            using var dist = Grid3x3(new int2(1, 1));
            Assert.IsTrue(BlinkMath.TryFindLandingCell(new int2(1, 1), dist, new int2(3, 3), 3, out var landing));
            Assert.AreEqual(new int2(0, 0), landing, "링 r=1 의 row-major 첫 후보");
        }

        [Test]
        public void Landing_AllBlockedWithinCap_ReturnsFalse_Terminates()
        {
            using var dist = Grid3x3(
                new int2(0, 0), new int2(1, 0), new int2(2, 0),
                new int2(0, 1), new int2(1, 1), new int2(2, 1),
                new int2(0, 2), new int2(1, 2), new int2(2, 2));
            Assert.IsFalse(BlinkMath.TryFindLandingCell(new int2(1, 1), dist, new int2(3, 3), 5, out _),
                "상한 내 후보 없음 → skip (무한 확장 금지)");
        }

        [Test]
        public void Landing_OutOfBoundsDesired_StillFindsInGridCell()
        {
            // 목적지 셀이 그리드 밖(경계 바깥 오프셋)이어도 링이 안쪽 셀을 찾는다.
            using var dist = Grid3x3();
            Assert.IsTrue(BlinkMath.TryFindLandingCell(new int2(-1, 1), dist, new int2(3, 3), 2, out var landing));
            Assert.AreEqual(new int2(0, 0), landing, "링 r=1 에서 in-bounds row-major 첫 후보");
        }

        [Test]
        public void Landing_RingZeroCap_OnlyChecksDesired()
        {
            using var dist = Grid3x3(new int2(1, 1));
            Assert.IsFalse(BlinkMath.TryFindLandingCell(new int2(1, 1), dist, new int2(3, 3), 0, out _),
                "maxRing 0 = desired 셀만 검사");
        }
    }
}
