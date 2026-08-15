using NUnit.Framework;
using Unity.Mathematics;
using Wassup.Battle.Combat.Projectile;

namespace Wassup.Tests.EditMode
{
    // dreamcatcher-content-4 unit 1 — 궤도 궤적 수학. 아키텍처 없이 도는 순수
    // 계층(BallisticArc/Bezier3/SkyFall 과 같은 형태)이라 World 없이 검증된다.
    public class OrbitTests
    {
        private const float Eps = 1e-4f;

        private static void AssertClose(float3 expected, float3 actual, string msg = "")
        {
            Assert.AreEqual(expected.x, actual.x, Eps, msg + " x");
            Assert.AreEqual(expected.y, actual.y, Eps, msg + " y");
            Assert.AreEqual(expected.z, actual.z, Eps, msg + " z");
        }

        // 궤도의 정의 그 자체. 이게 깨지면 화염구가 원이 아니라 다른 도형을 그린다.
        [Test]
        public void Position_KeepsRadius_AtEveryElapsed()
        {
            float3 center = new float3(3f, 0.5f, -2f);
            const float radius = 2.5f;
            const float w = 4f;

            for (float t = 0f; t <= 3f; t += 0.137f)
            {
                var p = Orbit.Position(center, radius, w, t);
                float2 offset = (p - center).xz;
                Assert.AreEqual(radius, math.length(offset), Eps, $"t={t}");
                // sim 은 XZ 평면만 돈다 — 높이는 뷰가 얹는다.
                Assert.AreEqual(center.y, p.y, Eps, $"t={t} y");
            }
        }

        // 시작 각도 0 고정(결정론). 위상이 흔들리면 여기서 걸린다.
        [Test]
        public void Position_AtZeroElapsed_IsCenterPlusRadiusOnX()
        {
            float3 center = new float3(1f, 0f, 1f);
            AssertClose(new float3(4f, 0f, 1f), Orbit.Position(center, 3f, 7f, 0f));
        }

        [Test]
        public void Position_AfterFullTurn_ReturnsToStart()
        {
            float3 center = new float3(-4f, 0f, 6f);
            const float radius = 1.75f;
            const float w = 3f;
            float period = 2f * math.PI / w;

            AssertClose(Orbit.Position(center, radius, w, 0f),
                        Orbit.Position(center, radius, w, period), "한 바퀴");
            AssertClose(Orbit.Position(center, radius, w, 0.4f),
                        Orbit.Position(center, radius, w, 0.4f + period), "임의 위상 한 바퀴");
        }

        // 음수 각속도 = 역회전. 같은 시각에 X 는 같고 Z(중심 기준)만 뒤집힌다.
        [Test]
        public void Position_NegativeAngularSpeed_MirrorsRotation()
        {
            float3 center = new float3(2f, 0f, 2f);
            const float radius = 2f;

            for (float t = 0.1f; t <= 1.5f; t += 0.3f)
            {
                var cw = Orbit.Position(center, radius, -5f, t);
                var ccw = Orbit.Position(center, radius, 5f, t);
                Assert.AreEqual(ccw.x, cw.x, Eps, $"t={t} x");
                Assert.AreEqual(-(ccw.z - center.z), cw.z - center.z, Eps, $"t={t} z");
            }
        }

        // 퇴화 저작(반경 0)은 중심에 붙은 채로 도는 것이지 NaN 이 아니다.
        [Test]
        public void Position_ZeroRadius_IsAlwaysCenter()
        {
            float3 center = new float3(5f, 0f, -1f);
            for (float t = 0f; t <= 2f; t += 0.25f)
                AssertClose(center, Orbit.Position(center, 0f, 6f, t), $"t={t}");
        }

        // 접선은 실제 진행 방향이어야 정렬이 의미를 갖는다 — 위치의 수치 미분과 대조한다
        // (축 규약 (x,z) 와 회전 부호를 동시에 고정하는 핀).
        [Test]
        public void Tangent_MatchesFiniteDifferenceOfPosition()
        {
            float3 center = new float3(1f, 0f, -3f);
            const float radius = 2f;
            const float h = 1e-4f;

            foreach (float w in new[] { 4f, -4f })
                for (float t = 0f; t <= 1.2f; t += 0.3f)
                {
                    var p0 = Orbit.Position(center, radius, w, t);
                    var p1 = Orbit.Position(center, radius, w, t + h);
                    float2 fd = math.normalize((p1 - p0).xz);
                    float2 tan = Orbit.Tangent(w, t);
                    Assert.AreEqual(fd.x, tan.x, 1e-2f, $"w={w} t={t} x");
                    Assert.AreEqual(fd.y, tan.y, 1e-2f, $"w={w} t={t} z");
                }
        }

        [Test]
        public void Tangent_IsUnitAndPerpendicularToRadius()
        {
            float3 center = new float3(0f, 0f, 0f);
            const float radius = 3f;
            const float w = 2.5f;

            for (float t = 0f; t <= 2f; t += 0.31f)
            {
                float2 tan = Orbit.Tangent(w, t);
                Assert.AreEqual(1f, math.length(tan), Eps, $"t={t} 단위 길이");
                float2 spoke = (Orbit.Position(center, radius, w, t) - center).xz;
                Assert.AreEqual(0f, math.dot(math.normalize(spoke), tan), Eps, $"t={t} 반지름과 수직");
            }
        }

        // 역회전 궤도는 정회전 궤도의 X 축 대칭이다(위치가 Z 만 뒤집히듯이) — 접선도
        // Z 만 뒤집혀야 한다. 부호를 접선 계산에서 빠뜨리면 화염구가 도는 쪽과 정렬이
        // 보는 쪽이 반대가 된다.
        [Test]
        public void Tangent_MirrorsWithAngularSpeedSign()
        {
            for (float t = 0f; t <= 1f; t += 0.25f)
            {
                float2 ccw = Orbit.Tangent(3f, t);
                float2 cw = Orbit.Tangent(-3f, t);
                Assert.AreEqual(ccw.x, cw.x, Eps, $"t={t} x");
                Assert.AreEqual(-ccw.y, cw.y, Eps, $"t={t} z");
            }
        }

        // 정회전(각속도 > 0)은 +X 지점에서 +Z 로 나아간다 — Position 의 위상 진행과
        // 같은 손잡이(handedness). 회전 방향 자체를 고정하는 핀.
        [Test]
        public void Tangent_AtStart_PointsAlongIncreasingPhase()
        {
            float2 tan = Orbit.Tangent(2f, 0f);
            Assert.AreEqual(0f, tan.x, Eps);
            Assert.AreEqual(1f, tan.y, Eps);

            float3 center = float3.zero;
            var stepped = Orbit.Position(center, 2f, 2f, 0.01f);
            Assert.Greater(stepped.z, 0f, "위치도 같은 쪽으로 진행한다");
        }

        // 멈춘 궤도(저작 실수)에서도 0 벡터를 남기지 않는다 — front-most 정렬이
        // 조용히 무의미해지는 함정 방지(Orbit.Tangent 주석).
        [Test]
        public void Tangent_ZeroAngularSpeed_IsStillUnitVector()
        {
            float2 tan = Orbit.Tangent(0f, 1.234f);
            Assert.AreEqual(1f, math.length(tan), Eps);
            Assert.IsFalse(math.any(math.isnan(tan)));
        }
    }
}
