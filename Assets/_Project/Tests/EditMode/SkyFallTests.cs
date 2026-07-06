using NUnit.Framework;
using Wassup.Battle.Combat.Projectile;

namespace Wassup.Tests.EditMode
{
    // Progress/arrival math for the SkyFall trajectory (unit 7 of
    // projectile-trajectory-payload). This drives the Meteor telegraph timing
    // (warningSec → flightTime), so the boundaries are pinned exactly —
    // especially the legacy warningSec=0 "resolve immediately" behavior.
    public class SkyFallTests
    {
        [Test]
        public void Progress_ZeroElapsed_IsZero()
        {
            Assert.AreEqual(0f, SkyFall.Progress(0f, 2f));
        }

        [Test]
        public void Progress_Midpoint_IsHalf()
        {
            Assert.AreEqual(0.5f, SkyFall.Progress(1f, 2f), 1e-5f);
        }

        [Test]
        public void Progress_AtFlightTime_IsOne()
        {
            Assert.AreEqual(1f, SkyFall.Progress(2f, 2f));
        }

        [Test]
        public void Progress_PastFlightTime_ClampsToOne()
        {
            Assert.AreEqual(1f, SkyFall.Progress(5f, 2f));
        }

        [Test]
        public void Progress_ZeroFlightTime_IsOne()
        {
            // Legacy warningSec=0 semantics: resolve on the first tick.
            Assert.AreEqual(1f, SkyFall.Progress(0f, 0f));
        }

        [Test]
        public void Progress_NegativeFlightTime_IsOne()
        {
            Assert.AreEqual(1f, SkyFall.Progress(0f, -1f));
        }

        [Test]
        public void Arrived_BeforeFlightTime_False()
        {
            Assert.IsFalse(SkyFall.Arrived(1.9f, 2f));
        }

        [Test]
        public void Arrived_AtFlightTime_True()
        {
            Assert.IsTrue(SkyFall.Arrived(2f, 2f));
        }

        [Test]
        public void Arrived_PastFlightTime_True()
        {
            Assert.IsTrue(SkyFall.Arrived(2.1f, 2f));
        }

        [Test]
        public void Arrived_ZeroFlightTime_TrueAtZeroElapsed()
        {
            // First MoveSystem tick (elapsed = dt >= 0) must arrive immediately.
            Assert.IsTrue(SkyFall.Arrived(0f, 0f));
        }

        // ── FallProgress (unit 9 — 뷰 낙하 압축 재매핑) ─────────────────────

        [Test]
        public void FallProgress_PortionOne_IsIdentity()
        {
            Assert.AreEqual(0.4f, SkyFall.FallProgress(0.4f, 1f), 1e-5f);
        }

        [Test]
        public void FallProgress_WaitWindow_IsZero()
        {
            // fp=0.35 → 대기 구간은 p < 0.65. 그 안에선 낙하 진행 0(뷰 숨김 구간).
            Assert.AreEqual(0f, SkyFall.FallProgress(0.5f, 0.35f));
            Assert.AreEqual(0f, SkyFall.FallProgress(0.65f, 0.35f), 1e-5f);
        }

        [Test]
        public void FallProgress_FallWindow_Ramps()
        {
            // p=0.825 는 낙하 구간의 중간: (0.825-0.65)/0.35 = 0.5.
            Assert.AreEqual(0.5f, SkyFall.FallProgress(0.825f, 0.35f), 1e-4f);
        }

        [Test]
        public void FallProgress_AtImpact_IsOne()
        {
            Assert.AreEqual(1f, SkyFall.FallProgress(1f, 0.35f), 1e-5f);
        }

        [Test]
        public void FallProgress_ZeroPortion_GuardsDivide()
        {
            // authoring 은 [Range(0.05,1)] 로 막지만 순수함수 계약으로 가드를 핀:
            // fp=0 → NaN/Inf 없이 유한값(대기 전 구간 → impact 직전 점프).
            float v = SkyFall.FallProgress(1f, 0f);
            Assert.IsFalse(float.IsNaN(v) || float.IsInfinity(v));
            Assert.AreEqual(1f, v, 1e-3f);
        }
    }
}
