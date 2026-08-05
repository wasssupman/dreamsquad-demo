using System.Collections.Generic;
using NUnit.Framework;
using Wassup.Sim;
using Wassup.Sim.Combat;

namespace Wassup.Tests.EditMode
{
    /// <summary>
    /// battle-sim-extraction unit 18-H/1 — 투사체 궤적·판정 수학 이식의 오라클.
    /// 레거시 `TileAoeTests`·`BallisticArcTests`·`SkyFallTests`·`SweepHitMathTests`·
    /// `Bezier3Tests`·`BounceRetargetTests` 의 어서션 **복제**다(재작성 아님).
    ///
    /// 이 계층은 원래부터 엔진을 몰랐다 — 옮긴 것은 벡터 타입뿐이다.
    /// </summary>
    public class SimProjectileMathTests
    {
        private const float Eps = 1e-4f;

        private static void AssertClose(SimVec3 expected, SimVec3 actual, string msg = "")
        {
            Assert.AreEqual(expected.x, actual.x, Eps, msg + " x");
            Assert.AreEqual(expected.y, actual.y, Eps, msg + " y");
            Assert.AreEqual(expected.z, actual.z, Eps, msg + " z");
        }

        // ═════ TileAoe ═══════════════════════════════════════════════════════

        [Test]
        public void Center_Is_In_Range_At_Zero()
            => Assert.IsTrue(TileAoe.IsInTileRange(new SimInt2(5, 5), new SimInt2(5, 5), 0));

        [Test]
        public void Chebyshev_Diagonal_Counts_As_One()
        {
            Assert.AreEqual(1, TileAoe.TileDistance(new SimInt2(0, 0), new SimInt2(1, 1)));
            Assert.IsTrue(TileAoe.IsInTileRange(new SimInt2(1, 1), new SimInt2(0, 0), 1));
        }

        [Test]
        public void Boundary_Is_Inclusive()
        {
            Assert.IsTrue(TileAoe.IsInTileRange(new SimInt2(3, 0), new SimInt2(0, 0), 3));
            Assert.IsTrue(TileAoe.IsInTileRange(new SimInt2(0, 3), new SimInt2(0, 0), 3));
            Assert.IsTrue(TileAoe.IsInTileRange(new SimInt2(3, 3), new SimInt2(0, 0), 3), "대각도 체비셰프 3");
        }

        [Test]
        public void Just_Outside_Is_Excluded()
        {
            Assert.IsFalse(TileAoe.IsInTileRange(new SimInt2(4, 0), new SimInt2(0, 0), 3));
            Assert.IsFalse(TileAoe.IsInTileRange(new SimInt2(3, 4), new SimInt2(0, 0), 3), "큰 축이 지배한다");
        }

        [Test]
        public void Negative_Offsets_Are_Symmetric()
        {
            Assert.AreEqual(2, TileAoe.TileDistance(new SimInt2(-2, 1), new SimInt2(0, 0)));
            Assert.IsTrue(TileAoe.IsInTileRange(new SimInt2(-2, -2), new SimInt2(0, 0), 2));
            Assert.IsFalse(TileAoe.IsInTileRange(new SimInt2(-3, 0), new SimInt2(0, 0), 2));
        }

        [Test]
        public void Range_Zero_Hits_Only_The_Center_Cell()
        {
            Assert.IsTrue(TileAoe.IsInTileRange(new SimInt2(7, 7), new SimInt2(7, 7), 0));
            Assert.IsFalse(TileAoe.IsInTileRange(new SimInt2(8, 7), new SimInt2(7, 7), 0));
        }

        // ═════ BallisticArc ══════════════════════════════════════════════════

        [Test]
        public void ArcPosition_Endpoints_Are_Origin_And_Impact()
        {
            var origin = new SimVec3(1f, 0f, 2f);
            var impact = new SimVec3(5f, 0f, 8f);

            var p0 = BallisticArc.ArcPosition(origin, impact, 3f, 0f);
            var p1 = BallisticArc.ArcPosition(origin, impact, 3f, 1f);

            AssertClose(origin, p0);
            Assert.AreEqual(impact.x, p1.x, Eps);
            Assert.AreEqual(impact.y, p1.y, Eps, "t=1 에서 아치 항이 0 — 정확히 그 셀에 떨어진다");
            Assert.AreEqual(impact.z, p1.z, Eps);
        }

        [Test]
        public void ArcPosition_Apex_At_Half_Adds_ArcHeight()
        {
            var p = BallisticArc.ArcPosition(new SimVec3(0, 0, 0), new SimVec3(4, 0, 0), 2f, 0.5f);
            Assert.AreEqual(2f, p.x, Eps, "XZ 중점");
            Assert.AreEqual(0f, p.z, Eps);
            Assert.AreEqual(2f, p.y, Eps, "sin(pi/2) * arcHeight");
        }

        [Test]
        public void ArcPosition_Y_Is_Symmetric_About_Half()
        {
            float ya = BallisticArc.ArcPosition(new SimVec3(0, 0, 0), new SimVec3(10, 0, 0), 5f, 0.25f).y;
            float yb = BallisticArc.ArcPosition(new SimVec3(0, 0, 0), new SimVec3(10, 0, 0), 5f, 0.75f).y;
            Assert.AreEqual(ya, yb, Eps);
        }

        [Test]
        public void FlightTime_Is_Horizontal_Distance_Over_Speed()
        {
            // XZ 거리 10 (Y 무시), 속도 5 → 2s
            float t = BallisticArc.FlightTime(new SimVec3(0, 0, 0), new SimVec3(10, 99, 0), 5f, 0.3f);
            Assert.AreEqual(2f, t, Eps);
        }

        [Test]
        public void FlightTime_Floors_At_MinTime_For_PointBlank()
        {
            float t = BallisticArc.FlightTime(new SimVec3(0, 0, 0), new SimVec3(0.1f, 0, 0), 5f, 0.3f);
            Assert.AreEqual(0.3f, t, Eps);
        }

        [Test]
        public void FlightTime_Zero_Speed_Falls_Back_To_MinTime()
        {
            float t = BallisticArc.FlightTime(new SimVec3(0, 0, 0), new SimVec3(10, 0, 0), 0f, 0.3f);
            Assert.AreEqual(0.3f, t, Eps);
        }

        // ═════ SkyFall ═══════════════════════════════════════════════════════

        [Test] public void Progress_ZeroElapsed_IsZero() => Assert.AreEqual(0f, SkyFall.Progress(0f, 2f));
        [Test] public void Progress_Midpoint_IsHalf() => Assert.AreEqual(0.5f, SkyFall.Progress(1f, 2f), 1e-5f);
        [Test] public void Progress_AtFlightTime_IsOne() => Assert.AreEqual(1f, SkyFall.Progress(2f, 2f));
        [Test] public void Progress_PastFlightTime_ClampsToOne() => Assert.AreEqual(1f, SkyFall.Progress(5f, 2f));

        [Test]
        public void Progress_ZeroFlightTime_IsOne()
            => Assert.AreEqual(1f, SkyFall.Progress(0f, 0f), "경고 0 = 첫 틱 해결(레거시)");

        [Test] public void Progress_NegativeFlightTime_IsOne() => Assert.AreEqual(1f, SkyFall.Progress(0f, -1f));

        [Test] public void Arrived_BeforeFlightTime_False() => Assert.IsFalse(SkyFall.Arrived(1.9f, 2f));
        [Test] public void Arrived_AtFlightTime_True() => Assert.IsTrue(SkyFall.Arrived(2f, 2f));
        [Test] public void Arrived_PastFlightTime_True() => Assert.IsTrue(SkyFall.Arrived(2.1f, 2f));

        [Test]
        public void Arrived_ZeroFlightTime_TrueAtZeroElapsed()
            => Assert.IsTrue(SkyFall.Arrived(0f, 0f), "첫 이동 틱에 즉시 도착해야 한다");

        [Test]
        public void FallProgress_PortionOne_IsIdentity()
            => Assert.AreEqual(0.4f, SkyFall.FallProgress(0.4f, 1f), 1e-5f);

        [Test]
        public void FallProgress_WaitWindow_IsZero()
        {
            Assert.AreEqual(0f, SkyFall.FallProgress(0.5f, 0.35f));
            Assert.AreEqual(0f, SkyFall.FallProgress(0.65f, 0.35f), 1e-5f);
        }

        [Test]
        public void FallProgress_FallWindow_Ramps()
            => Assert.AreEqual(0.5f, SkyFall.FallProgress(0.825f, 0.35f), Eps);

        [Test]
        public void FallProgress_AtImpact_IsOne()
            => Assert.AreEqual(1f, SkyFall.FallProgress(1f, 0.35f), 1e-5f);

        [Test]
        public void FallProgress_ZeroPortion_GuardsDivide()
        {
            // 저작이 막는 도달 불가 입력 — 순수함수 계약만 핀다(NaN/Inf 없음, 0 수렴).
            float v = SkyFall.FallProgress(1f, 0f);
            Assert.IsFalse(float.IsNaN(v) || float.IsInfinity(v));
            Assert.AreEqual(0f, v, 1e-3f);
        }

        [Test]
        public void FallProgress_MinAuthoredPortion_ReachesOneAtImpact()
            => Assert.AreEqual(1f, SkyFall.FallProgress(1f, 0.05f), Eps);

        // ═════ SweepHitMath ══════════════════════════════════════════════════

        [Test]
        public void TargetOnSegment_Hits()
            => Assert.IsTrue(SweepHitMath.SegmentHits(new SimVec2(0, 0), new SimVec2(2, 0), new SimVec2(1f, 0f), 0.1f));

        [Test]
        public void TargetNearMidpoint_WithinRadius_Hits()
        {
            Assert.IsTrue(SweepHitMath.SegmentHits(new SimVec2(0, 0), new SimVec2(2, 0), new SimVec2(1f, 0.25f), 0.3f));
            Assert.IsFalse(SweepHitMath.SegmentHits(new SimVec2(0, 0), new SimVec2(2, 0), new SimVec2(1f, 0.35f), 0.3f));
        }

        [Test]
        public void TargetPastEndpoint_ClampsToEndpointDistance()
        {
            Assert.IsTrue(SweepHitMath.SegmentHits(new SimVec2(0, 0), new SimVec2(2, 0), new SimVec2(2.2f, 0f), 0.3f),
                "끝점 바로 너머, 반경 안");
            Assert.IsFalse(SweepHitMath.SegmentHits(new SimVec2(0, 0), new SimVec2(2, 0), new SimVec2(2.5f, 0f), 0.3f),
                "선분은 연장되지 않는다");
            Assert.IsFalse(SweepHitMath.SegmentHits(new SimVec2(0, 0), new SimVec2(2, 0), new SimVec2(-0.5f, 0f), 0.3f),
                "시작점 뒤");
        }

        [Test]
        public void ZeroLengthSegment_DegradesToPointTest()
        {
            var p = new SimVec2(1f, 1f);
            Assert.IsTrue(SweepHitMath.SegmentHits(p, p, new SimVec2(1f, 1.2f), 0.3f));
            Assert.IsFalse(SweepHitMath.SegmentHits(p, p, new SimVec2(1f, 1.5f), 0.3f));
        }

        [Test]
        public void BoundaryDistance_ExactRadius_Hits()
            => Assert.IsTrue(SweepHitMath.SegmentHits(new SimVec2(0, 0), new SimVec2(2, 0), new SimVec2(1f, 0.3f), 0.3f));

        // ═════ Bezier3 ═══════════════════════════════════════════════════════

        [Test]
        public void Position_AtZero_IsStart_AtOne_IsEnd()
        {
            var p0 = new SimVec3(0f, 0f, 0f);
            var p1 = new SimVec3(1f, 0f, 3f);
            var p2 = new SimVec3(4f, 0f, -2f);
            var p3 = new SimVec3(6f, 0f, 1f);

            AssertClose(p0, Bezier3.Position(p0, p1, p2, p3, 0f), "t=0");
            AssertClose(p3, Bezier3.Position(p0, p1, p2, p3, 1f), "t=1");
        }

        [Test]
        public void Position_CollinearControls_EqualsStraightLerp()
        {
            // 제어점이 직선의 1/3·2/3 지점이면 3차 베지어 = 등속 lerp — 다항식 구현의 해석적 기준.
            var p0 = new SimVec3(-2f, 0f, 1f);
            var p3 = new SimVec3(8f, 0f, 5f);
            var p1 = SimMath.Lerp(p0, p3, 1f / 3f);
            var p2 = SimMath.Lerp(p0, p3, 2f / 3f);

            for (float t = 0f; t <= 1.0001f; t += 0.125f)
                AssertClose(SimMath.Lerp(p0, p3, t), Bezier3.Position(p0, p1, p2, p3, t), $"t={t}");
        }

        [Test]
        public void Position_IsSymmetricUnderReversal()
        {
            var p0 = new SimVec3(0f, 0f, 0f);
            var p1 = new SimVec3(1f, 0f, 2f);
            var p2 = new SimVec3(3f, 0f, 2f);
            var p3 = new SimVec3(4f, 0f, 0f);

            AssertClose(Bezier3.Position(p0, p1, p2, p3, 0.3f),
                        Bezier3.Position(p3, p2, p1, p0, 0.7f));
        }

        [Test]
        public void ControlPoints_AlternateSides_ByShotIndex()
        {
            var origin = new SimVec3(0f, 0f, 0f);
            var dest = new SimVec3(10f, 0f, 0f); // +X → perp = +Z

            Bezier3.ControlPoints(origin, dest, 0, lateral: 2f, forwardBias: 0.35f, out var a1, out _);
            Bezier3.ControlPoints(origin, dest, 1, lateral: 2f, forwardBias: 0.35f, out var b1, out _);

            Assert.Greater(a1.z, 0f, "0번 발은 한쪽으로");
            Assert.Less(b1.z, 0f, "1번 발은 반대쪽으로");
            Assert.AreEqual(SimMath.Abs(a1.z), SimMath.Abs(b1.z), Eps, "같은 쌍은 대칭 크기");
        }

        [Test]
        public void ControlPoints_SwingWidensWithIndex()
        {
            var origin = new SimVec3(0f, 0f, 0f);
            var dest = new SimVec3(10f, 0f, 0f);

            Bezier3.ControlPoints(origin, dest, 0, 2f, 0.35f, out var c0, out _);
            Bezier3.ControlPoints(origin, dest, 2, 2f, 0.35f, out var c2, out _);
            Bezier3.ControlPoints(origin, dest, 4, 2f, 0.35f, out var c4, out _);

            Assert.Less(SimMath.Abs(c0.z), SimMath.Abs(c2.z));
            Assert.Less(SimMath.Abs(c2.z), SimMath.Abs(c4.z));
        }

        [Test]
        public void ControlPoints_ZeroLateral_StaysOnAxis()
        {
            Bezier3.ControlPoints(new SimVec3(0, 0, 0), new SimVec3(10f, 0f, 0f), 3,
                                  lateral: 0f, forwardBias: 0.35f, out var c1, out var c2);
            Assert.AreEqual(0f, c1.z, Eps);
            Assert.AreEqual(0f, c2.z, Eps);
        }

        [Test]
        public void ControlPoints_DegenerateDirection_IsNaNFree()
        {
            // 발사점 == 목표면 수직축이 없다. 파생 축으로 대체하면 NaN 이 재유입되므로 직선 붕괴.
            var same = new SimVec3(3f, 0f, 3f);
            Bezier3.ControlPoints(same, same, 0, 2f, 0.35f, out var c1, out var c2);

            Assert.IsFalse(IsNaN(c1));
            Assert.IsFalse(IsNaN(c2));
            AssertClose(same, c1);
            AssertClose(same, c2);

            var mid = Bezier3.Position(same, c1, c2, same, 0.5f);
            Assert.IsFalse(IsNaN(mid), "그 제어점으로 만든 곡선도 NaN-free");
        }

        [Test]
        public void ControlPoints_NegativeSwingIndex_IsHandled()
        {
            Bezier3.ControlPoints(new SimVec3(0, 0, 0), new SimVec3(0f, 0f, 10f), -3, 2f, 0.35f,
                                  out var c1, out var c2);
            Assert.IsFalse(IsNaN(c1));
            Assert.IsFalse(IsNaN(c2));
        }

        private static bool IsNaN(SimVec3 v) => float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z);

        // ═════ BounceRetarget ════════════════════════════════════════════════

        private const float TileSize = 1f;
        private static readonly SimInt2 Grid = new SimInt2(128, 128);
        private static readonly SimVec3 Origin = default;

        private static List<SimVec3> Make(params SimVec3[] xs) => new List<SimVec3>(xs);

        [Test]
        public void PicksNearest_WithinRange()
        {
            var pos = Make(new SimVec3(1, 0, 0), new SimVec3(2, 0, 0), new SimVec3(0, 0, 3));
            Assert.AreEqual(0, BounceRetarget.FindNext(Origin, -1, pos, 3, TileSize, Grid, Origin));
        }

        [Test]
        public void SkipsExcludeIndex_PicksNextNearest()
        {
            var pos = Make(new SimVec3(1, 0, 0), new SimVec3(2, 0, 0), new SimVec3(0, 0, 3));
            Assert.AreEqual(1, BounceRetarget.FindNext(Origin, 0, pos, 3, TileSize, Grid, Origin));
        }

        [Test]
        public void OutOfRange_ReturnsMinusOne()
        {
            var pos = Make(new SimVec3(5, 0, 0)); // 체비셰프 5 > 3
            Assert.AreEqual(-1, BounceRetarget.FindNext(Origin, -1, pos, 3, TileSize, Grid, Origin));
        }

        [Test]
        public void EmptyOrAllExcluded_ReturnsMinusOne()
        {
            Assert.AreEqual(-1, BounceRetarget.FindNext(Origin, -1, new List<SimVec3>(), 3, TileSize, Grid, Origin));
            Assert.AreEqual(-1, BounceRetarget.FindNext(Origin, 0, Make(new SimVec3(1, 0, 0)), 3, TileSize, Grid, Origin));
        }

        [Test]
        public void DistanceTie_ResolvesToLowerIndex()
        {
            // 둘 다 셀 거리 2, XZ 제곱거리 4 → 낮은 인덱스 승.
            var pos = Make(new SimVec3(0, 0, 2), new SimVec3(2, 0, 0));
            Assert.AreEqual(0, BounceRetarget.FindNext(Origin, -1, pos, 3, TileSize, Grid, Origin));
        }

        [Test]
        public void ZeroTileRange_ReturnsMinusOne()
            => Assert.AreEqual(-1, BounceRetarget.FindNext(Origin, -1, Make(new SimVec3(1, 0, 0)), 0, TileSize, Grid, Origin));

        // ═════ PathHitRecord ═════════════════════════════════════════════════

        [Test]
        public void PathHitRecord_ContainsFindsVictim_AndTreatsNullBufferAsEmpty()
        {
            var world = new SimWorld(new SimConfig(1u, 1u));
            var a = world.Create();
            var b = world.Create();
            var records = new List<PathHitRecord> { new PathHitRecord { value = a } };

            Assert.IsTrue(PathHitRecord.Contains(records, a));
            Assert.IsFalse(PathHitRecord.Contains(records, b));
            Assert.IsFalse(PathHitRecord.Contains(null, a), "버퍼 부재 = 아직 아무도 안 맞았다");
        }
    }
}
