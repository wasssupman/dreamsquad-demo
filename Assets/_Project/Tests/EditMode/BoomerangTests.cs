using NUnit.Framework;
using Unity.Mathematics;
using Wassup.Battle.Combat.Projectile;

namespace Wassup.Tests.EditMode
{
    // dreamcatcher-content-5 unit 1 — 왕복 궤적 수학. 아키텍처 없이 도는 순수 계층
    // (Orbit/BallisticArc/Bezier3/SkyFall 과 같은 형태)이라 World 없이 검증된다.
    public class BoomerangTests
    {
        private const float Eps = 1e-4f;

        private static readonly float3 Origin = new float3(3f, 0.5f, -2f);
        private static readonly float2 Axis = new float2(1f, 0f);
        private const float MaxD = 4f;
        private const float Speed = 8f;

        private static float3 At(float t, out bool returning)
            => Boomerang.Position(Origin, Axis, MaxD, Speed, t, out returning);

        // 왕복의 정의 그 자체 — 편도 끝에서 되짚는다.
        [Test]
        public void Position_ReachesExactlyMaxDistance_AtHalfTime()
        {
            float half = MaxD / Speed;
            var p = At(half, out bool returning);
            Assert.AreEqual(MaxD, math.length((p - Origin).xz), Eps, "편도 끝 = maxDistance");
            Assert.IsFalse(returning, "정확히 끝점은 아직 나가는 다리다");
        }

        [Test]
        public void Position_ReturnsToOrigin_AtTotalTime()
        {
            float total = Boomerang.TotalTime(MaxD, Speed);
            Assert.AreEqual(2f * MaxD / Speed, total, Eps);
            var p = At(total, out bool returning);
            Assert.AreEqual(0f, math.length((p - Origin).xz), Eps, "발사점 복귀");
            Assert.IsTrue(returning);
        }

        // 궤적은 **축 위**를 벗어나지 않는다(직선 왕복). sim 은 XZ 만 간다.
        [Test]
        public void Position_StaysOnAxis_AndKeepsHeight()
        {
            for (float t = 0f; t <= Boomerang.TotalTime(MaxD, Speed); t += 0.037f)
            {
                var p = At(t, out _);
                float2 off = (p - Origin).xz;
                Assert.AreEqual(0f, off.y, Eps, $"t={t} — 축을 벗어남");
                Assert.AreEqual(Origin.y, p.y, Eps, $"t={t} — sim 높이는 불변");
            }
        }

        // 「어느 다리인가」의 전환 시각을 고정한다.
        [Test]
        public void Returning_FlipsAt_MaxDistanceOverSpeed()
        {
            float half = MaxD / Speed;
            At(half - 0.01f, out bool before);
            At(half + 0.01f, out bool after);
            Assert.IsFalse(before);
            Assert.IsTrue(after);
        }

        // 이 궤적의 핵심 계약 — **두 다리의 진행 방향이 정확히 반대**다. 넉백이
        // 「밀었다 당김」이 되는 것이 이 성질 하나의 결과이므로 여기서 못박는다.
        // 상태(direction)를 읽지 않고 위치의 차분으로만 확인한다 — 소비 쪽(히트 시스템)이
        // 실제로 쓰는 값이 그 차분이기 때문.
        [Test]
        public void SweepDirection_IsExactlyOpposite_OnTheTwoLegs()
        {
            const float h = 1e-3f;
            float half = MaxD / Speed;

            float2 outLeg = math.normalize(
                (At(half * 0.5f + h, out _) - At(half * 0.5f, out _)).xz);
            float2 backLeg = math.normalize(
                (At(half * 1.5f + h, out _) - At(half * 1.5f, out _)).xz);

            Assert.AreEqual(-outLeg.x, backLeg.x, 1e-3f);
            Assert.AreEqual(-outLeg.y, backLeg.y, 1e-3f);
            Assert.AreEqual(-1f, math.dot(outLeg, backLeg), 1e-3f, "정확히 반대");
        }

        // 축은 **입력이고 불변**이다. 같은 축으로 계속 호출해도 궤적이 발사점 뒤로 가지
        // 않는다 — arm 이 축을 뒤집었을 때 나던 결함(발사점 뒤로 날아감)의 회귀 핀.
        // ⚠ 왕복 시간을 **넘겨서도** 확인한다. 완료 프레임은 정확히 total 에 떨어지지 않아
        // (한 프레임에 speed*dt 만큼 오버슛) 클램프가 없으면 **마지막 스윕이 발사점 뒤로
        // 뻗어 뒤에 선 적을 때린다** — 계약에 없는 피해 사건이다(ECS 리뷰 L1).
        [Test]
        public void Position_NeverGoesBehindOrigin_AlongTheAxis()
        {
            for (float t = 0f; t <= Boomerang.TotalTime(MaxD, Speed) + 0.5f; t += 0.01f)
            {
                float along = math.dot((At(t, out _) - Origin).xz, Axis);
                Assert.GreaterOrEqual(along, -Eps, $"t={t} — 발사점 뒤로 갔다");
                Assert.LessOrEqual(along, MaxD + Eps, $"t={t} — 편도 거리를 넘었다");
            }
        }

        // 클램프가 수명을 건드리지 않는다 — 위치는 발사점에 머물러도 완료 판정은 시간이 한다.
        [Test]
        public void Position_ClampAtOrigin_DoesNotAffectCompletion()
        {
            float total = Boomerang.TotalTime(MaxD, Speed);
            var p = At(total + 0.4f, out bool returning);
            Assert.AreEqual(0f, math.length((p - Origin).xz), Eps, "뒤로 나가지 않고 발사점에 접힌다");
            Assert.IsTrue(returning);
            Assert.IsTrue(Boomerang.IsComplete(MaxD, Speed, total + 0.4f), "완료 판정은 그대로");
        }

        [Test]
        public void IsComplete_TrueOnlyAfterFullRoundTrip()
        {
            float total = Boomerang.TotalTime(MaxD, Speed);
            Assert.IsFalse(Boomerang.IsComplete(MaxD, Speed, total - 0.01f));
            Assert.IsTrue(Boomerang.IsComplete(MaxD, Speed, total));
            Assert.IsTrue(Boomerang.IsComplete(MaxD, Speed, total + 1f));
        }

        // 퇴화 저작은 여기서 «안전한 값» 을 지어내지 않는다 — NaN 만 안 나오면 되고,
        // 실제 차단은 스폰 드레인의 loud 거절이다(불멸 투사체 방지).
        [Test]
        public void Degenerate_Authoring_ProducesNoNaN()
        {
            for (float t = 0f; t <= 2f; t += 0.25f)
            {
                var zeroSpeed = Boomerang.Position(Origin, Axis, MaxD, 0f, t, out _);
                var zeroDist = Boomerang.Position(Origin, Axis, 0f, Speed, t, out _);
                Assert.IsFalse(math.any(math.isnan(zeroSpeed)), $"t={t} speed=0");
                Assert.IsFalse(math.any(math.isnan(zeroDist)), $"t={t} dist=0");
            }
            // 속도 0 은 «영원히 안 끝난다» — 그래서 드레인이 거절해야 한다는 근거.
            Assert.IsFalse(Boomerang.IsComplete(MaxD, 0f, 9999f));
            Assert.AreEqual(float.PositiveInfinity, Boomerang.TotalTime(MaxD, 0f));
        }

        // 축이 대각이어도 같은 성질이 성립한다(축 규약 (x,z) 고정 핀).
        [Test]
        public void Position_WorksOnDiagonalAxis()
        {
            float2 diag = math.normalize(new float2(1f, 1f));
            float total = Boomerang.TotalTime(MaxD, Speed);
            var far = Boomerang.Position(Origin, diag, MaxD, Speed, MaxD / Speed, out _);
            Assert.AreEqual(MaxD, math.length((far - Origin).xz), Eps);
            var home = Boomerang.Position(Origin, diag, MaxD, Speed, total, out _);
            Assert.AreEqual(0f, math.length((home - Origin).xz), Eps);
        }
    }
}
