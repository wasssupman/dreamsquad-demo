using NUnit.Framework;
using Wassup.Core.TimeControl;

namespace Wassup.Tests.EditMode
{
    // time-manager Unit 0 — arbitration + 멱등 lease 회귀 방지.
    // TimeManager 는 싱글턴(공유 상태)이라 각 테스트 전후로 ResetAll 로 격리한다.
    public class TimeManagerTests
    {
        [SetUp]
        public void SetUp() => TimeManager.Instance.ResetAll();

        [TearDown]
        public void TearDown() => TimeManager.Instance.ResetAll();

        [Test]
        public void NoRequest_ScaleIsOne()
        {
            Assert.AreEqual(1f, TimeManager.Instance.ScaleOf(TimeDomain.Battle), 1e-6f);
            Assert.AreEqual(1f, TimeManager.Instance.ScaleOf(TimeDomain.Interaction), 1e-6f);
        }

        [Test]
        public void PauseOverridesSlowmo_ByPriority_ThenRestores()
        {
            var drag = TimeManager.Instance.Request(TimeDomain.Battle, 0.2f);
            Assert.AreEqual(0.2f, TimeManager.Instance.ScaleOf(TimeDomain.Battle), 1e-6f);

            var pause = TimeManager.Instance.Request(TimeDomain.Battle, 0f, priority: 100);
            Assert.AreEqual(0f, TimeManager.Instance.ScaleOf(TimeDomain.Battle), 1e-6f);

            pause.Dispose();
            Assert.AreEqual(0.2f, TimeManager.Instance.ScaleOf(TimeDomain.Battle), 1e-6f);

            drag.Dispose();
            Assert.AreEqual(1f, TimeManager.Instance.ScaleOf(TimeDomain.Battle), 1e-6f);
        }

        [Test]
        public void SamePriority_LowerScaleWins()
        {
            using (TimeManager.Instance.Request(TimeDomain.Battle, 0.5f))
            using (TimeManager.Instance.Request(TimeDomain.Battle, 0.25f))
            {
                Assert.AreEqual(0.25f, TimeManager.Instance.ScaleOf(TimeDomain.Battle), 1e-6f);
            }
        }

        [Test]
        public void DomainsAreIndependent()
        {
            using (TimeManager.Instance.Request(TimeDomain.Battle, 0f, priority: 100))
            {
                Assert.AreEqual(0f, TimeManager.Instance.ScaleOf(TimeDomain.Battle), 1e-6f);
                Assert.AreEqual(1f, TimeManager.Instance.ScaleOf(TimeDomain.Interaction), 1e-6f);
            }
        }

        [Test]
        public void DoubleDispose_IsNoOp_DoesNotClobberOtherRequest()
        {
            var a = TimeManager.Instance.Request(TimeDomain.Battle, 0.5f);
            var b = TimeManager.Instance.Request(TimeDomain.Battle, 0.25f);

            a.Dispose(); // a 제거 → 남은 승자 b(0.25)
            Assert.AreEqual(0.25f, TimeManager.Instance.ScaleOf(TimeDomain.Battle), 1e-6f);

            a.Dispose(); // 이미 제거됨 → no-op, b 를 건드리면 안 됨
            Assert.AreEqual(0.25f, TimeManager.Instance.ScaleOf(TimeDomain.Battle), 1e-6f);

            b.Dispose();
            Assert.AreEqual(1f, TimeManager.Instance.ScaleOf(TimeDomain.Battle), 1e-6f);
        }

        [Test]
        public void CopiedLease_DisposeReleasesOnce()
        {
            var lease = TimeManager.Instance.Request(TimeDomain.Battle, 0.5f);
            var other = TimeManager.Instance.Request(TimeDomain.Battle, 0.75f, priority: 5); // 승자 유지용

            var copy = lease;
            copy.Dispose();  // lease 요청 해제
            lease.Dispose(); // 같은 id → no-op

            // other(pri5) 만 남아야 함
            Assert.AreEqual(0.75f, TimeManager.Instance.ScaleOf(TimeDomain.Battle), 1e-6f);
            other.Dispose();
            Assert.AreEqual(1f, TimeManager.Instance.ScaleOf(TimeDomain.Battle), 1e-6f);
        }

        [Test]
        public void ScaleChanged_FiresOnlyOnEffectiveChange()
        {
            int fires = 0;
            float last = 1f;
            System.Action<TimeDomain, float> handler = (d, s) => { if (d == TimeDomain.Battle) { fires++; last = s; } };
            TimeManager.Instance.ScaleChanged += handler;
            try
            {
                var top = TimeManager.Instance.Request(TimeDomain.Battle, 0f, priority: 100); // 1 -> 0 발화
                Assert.AreEqual(1, fires);
                Assert.AreEqual(0f, last, 1e-6f);

                // 더 낮은 우선순위 요청 추가 → 유효 스케일 여전히 0 → 발화 없음
                var low = TimeManager.Instance.Request(TimeDomain.Battle, 0.2f);
                Assert.AreEqual(1, fires);

                low.Dispose(); // 여전히 0 → 발화 없음
                Assert.AreEqual(1, fires);

                top.Dispose(); // 0 -> 1 발화
                Assert.AreEqual(2, fires);
                Assert.AreEqual(1f, last, 1e-6f);
            }
            finally
            {
                TimeManager.Instance.ScaleChanged -= handler;
            }
        }
    }
}
