using NUnit.Framework;
using Unity.Mathematics;
using Wassup.Battle.Combat.Projectile;

namespace Wassup.Tests.EditMode
{
    // projectile-emission-pattern unit 1 — 베지어 궤적 수학. 아키텍처 없이 도는
    // 순수 계층(BallisticArc/SkyFall 과 같은 형태)이라 World 없이 검증된다.
    public class Bezier3Tests
    {
        private const float Eps = 1e-4f;

        private static void AssertClose(float3 expected, float3 actual, string msg = "")
        {
            Assert.AreEqual(expected.x, actual.x, Eps, msg + " x");
            Assert.AreEqual(expected.y, actual.y, Eps, msg + " y");
            Assert.AreEqual(expected.z, actual.z, Eps, msg + " z");
        }

        [Test]
        public void Position_AtZero_IsStart_AtOne_IsEnd()
        {
            float3 p0 = new float3(0f, 0f, 0f);
            float3 p1 = new float3(1f, 0f, 3f);
            float3 p2 = new float3(4f, 0f, -2f);
            float3 p3 = new float3(6f, 0f, 1f);

            AssertClose(p0, Bezier3.Position(p0, p1, p2, p3, 0f), "t=0");
            AssertClose(p3, Bezier3.Position(p0, p1, p2, p3, 1f), "t=1");
        }

        // 제어점이 시작~끝 직선의 1/3·2/3 지점이면 3차 베지어는 직선 등속 lerp 와
        // 동일하다 — 다항식 구현이 맞는지 보는 해석적 기준.
        [Test]
        public void Position_CollinearControls_EqualsStraightLerp()
        {
            float3 p0 = new float3(-2f, 0f, 1f);
            float3 p3 = new float3(8f, 0f, 5f);
            float3 p1 = math.lerp(p0, p3, 1f / 3f);
            float3 p2 = math.lerp(p0, p3, 2f / 3f);

            for (float t = 0f; t <= 1.0001f; t += 0.125f)
                AssertClose(math.lerp(p0, p3, t), Bezier3.Position(p0, p1, p2, p3, t), $"t={t}");
        }

        [Test]
        public void Position_IsSymmetricUnderReversal()
        {
            float3 p0 = new float3(0f, 0f, 0f);
            float3 p1 = new float3(1f, 0f, 2f);
            float3 p2 = new float3(3f, 0f, 2f);
            float3 p3 = new float3(4f, 0f, 0f);

            // 곡선을 뒤집어 반대로 걸으면 같은 지점을 지난다.
            AssertClose(Bezier3.Position(p0, p1, p2, p3, 0.3f),
                        Bezier3.Position(p3, p2, p1, p0, 0.7f));
        }

        [Test]
        public void ControlPoints_AlternateSides_ByShotIndex()
        {
            float3 origin = new float3(0f, 0f, 0f);
            float3 dest = new float3(10f, 0f, 0f); // +X 방향 → perp = +Z

            Bezier3.ControlPoints(origin, dest, 0, lateral: 2f, forwardBias: 0.35f, out var a1, out _);
            Bezier3.ControlPoints(origin, dest, 1, lateral: 2f, forwardBias: 0.35f, out var b1, out _);

            Assert.Greater(a1.z, 0f, "0번 발은 한쪽으로");
            Assert.Less(b1.z, 0f, "1번 발은 반대쪽으로");
            Assert.AreEqual(math.abs(a1.z), math.abs(b1.z), Eps, "같은 쌍은 대칭 크기");
        }

        [Test]
        public void ControlPoints_SwingWidensWithIndex()
        {
            float3 origin = float3.zero;
            float3 dest = new float3(10f, 0f, 0f);

            Bezier3.ControlPoints(origin, dest, 0, 2f, 0.35f, out var c0, out _);
            Bezier3.ControlPoints(origin, dest, 2, 2f, 0.35f, out var c2, out _);
            Bezier3.ControlPoints(origin, dest, 4, 2f, 0.35f, out var c4, out _);

            // 같은 쪽(짝수 index)끼리는 index 가 커질수록 더 크게 벌어진다.
            Assert.Less(math.abs(c0.z), math.abs(c2.z));
            Assert.Less(math.abs(c2.z), math.abs(c4.z));
        }

        [Test]
        public void ControlPoints_ZeroLateral_StaysOnAxis()
        {
            Bezier3.ControlPoints(float3.zero, new float3(10f, 0f, 0f), 3, lateral: 0f,
                                  forwardBias: 0.35f, out var c1, out var c2);
            Assert.AreEqual(0f, c1.z, Eps);
            Assert.AreEqual(0f, c2.z, Eps);
        }

        // 퇴화 입력(발사점 == 목표)은 수직축이 정의되지 않는다. 런타임 파생 축으로
        // 대체하면 NaN 이 재유입되므로 직선으로 붕괴시킨다(BlinkMath 선례).
        [Test]
        public void ControlPoints_DegenerateDirection_IsNaNFree()
        {
            float3 same = new float3(3f, 0f, 3f);
            Bezier3.ControlPoints(same, same, 0, 2f, 0.35f, out var c1, out var c2);

            Assert.IsFalse(math.any(math.isnan(c1)));
            Assert.IsFalse(math.any(math.isnan(c2)));
            AssertClose(same, c1);
            AssertClose(same, c2);

            // 그 제어점으로 만든 곡선도 NaN-free (전 구간 제자리).
            var mid = Bezier3.Position(same, c1, c2, same, 0.5f);
            Assert.IsFalse(math.any(math.isnan(mid)));
        }

        [Test]
        public void ControlPoints_NegativeSwingIndex_IsHandled()
        {
            Bezier3.ControlPoints(float3.zero, new float3(0f, 0f, 10f), -3, 2f, 0.35f,
                                  out var c1, out var c2);
            Assert.IsFalse(math.any(math.isnan(c1)));
            Assert.IsFalse(math.any(math.isnan(c2)));
        }
    }
}
