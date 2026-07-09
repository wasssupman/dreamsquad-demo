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
            // fp=0 은 authoring 이 [Range(0.05,1)] 로 막는 도달 불가 입력 — 순수함수
            // 계약만 핀다. divide 가드(math.max(fp, 1e-4))가 NaN/Inf 를 막고, 분자도
            // 0(=p-(1-0)=0)이라 saturate(0)=0 으로 수렴한다: fp→0 이면 낙하 진행이
            // 전 구간 0(뷰가 상공에 붙박임)이라는 의미. 실 최솟값에서의 정상 도달은
            // FallProgress_MinAuthoredPortion_ReachesOneAtImpact 가 별도로 핀다.
            float v = SkyFall.FallProgress(1f, 0f);
            Assert.IsFalse(float.IsNaN(v) || float.IsInfinity(v));
            Assert.AreEqual(0f, v, 1e-3f);
        }

        [Test]
        public void FallProgress_MinAuthoredPortion_ReachesOneAtImpact()
        {
            // authoring 최솟값 fp=0.05 에선 착탄(p=1)에 낙하 완료(1)에 도달해야 한다
            // (뷰가 지면=heightOffset 0 에 안착). fp=0 퇴화 케이스와 대비되는 실계약.
            Assert.AreEqual(1f, SkyFall.FallProgress(1f, 0.05f), 1e-4f);
        }
    }
}
