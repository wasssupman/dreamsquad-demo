using NUnit.Framework;
using Wassup.Core;

namespace Wassup.Tests.EditMode
{
    // heart-stress-axis unit 0 — 마음 체력 → 차오르는 스트레스.
    //
    // 고정하는 계약 셋:
    //   (1) 방향이 **반전**이다 — 만피가 0 이고 체력 0 이 100 이다. 뒤집히면 판이
    //       시작하자마자 끝나거나 영원히 안 끝난다.
    //   (2) `max <= 0` 은 0 을 준다(100 이 아니다). 마음이 미저작·미스폰인 판을
    //       «이미 무너졌다» 로 읽으면 즉시 종료된다.
    //   (3) 0~100 밖으로 새지 않는다 — 과피해로 체력이 음수가 되어도 100 이 상한이다.
    public class StressMathTests
    {
        private const float Tol = 1e-4f;

        [Test]
        public void FullHealth_IsZeroStress()
            => Assert.AreEqual(0f, StressMath.FromHealth(1000f, 1000f), Tol);

        [Test]
        public void ZeroHealth_IsFullStress()
            => Assert.AreEqual(StressMath.Max, StressMath.FromHealth(0f, 1000f), Tol);

        [Test]
        public void HalfHealth_IsHalfStress()
            => Assert.AreEqual(50f, StressMath.FromHealth(500f, 1000f), Tol);

        [Test]
        public void ScaleIndependent_SameRatioSameStress()
        {
            // 정본 HP 스케일(덱 goalStabilityMax)이 바뀌어도 표시는 같아야 한다 —
            // 「100 은 표시 정규화이지 HP 최대치가 아니다」의 실측.
            Assert.AreEqual(StressMath.FromHealth(750f, 1000f),
                            StressMath.FromHealth(1125f, 1500f), Tol);
        }

        [Test]
        public void NegativeHealth_ClampsToFull()
        {
            // 과피해(한 방에 -50)로 체력이 음수가 될 수 있다. 스트레스가 100 을 넘으면
            // 게이지가 바깥으로 그려지고 종료 판정이 «100 초과» 를 따로 다뤄야 한다.
            Assert.AreEqual(StressMath.Max, StressMath.FromHealth(-50f, 1000f), Tol);
        }

        [Test]
        public void OverHeal_ClampsToZero()
            => Assert.AreEqual(0f, StressMath.FromHealth(1200f, 1000f), Tol);

        [Test]
        public void NoHeartAuthored_IsZeroStress_NotFull()
        {
            // ⚠ 이 단언이 뒤집히면 마음이 없는 판(미저작·미스폰·teardown 직후)이
            // 시작하자마자 종료된다. 폴백 방향이 이 spec 에서 가장 위험한 한 줄이다.
            Assert.AreEqual(0f, StressMath.FromHealth(0f, 0f), Tol);
            Assert.AreEqual(0f, StressMath.FromHealth(0f, -1f), Tol);
        }

        [Test]
        public void IsFull_MatchesEndCondition()
        {
            Assert.IsTrue(StressMath.IsFull(StressMath.FromHealth(0f, 1000f)));
            Assert.IsFalse(StressMath.IsFull(StressMath.FromHealth(1f, 1000f)),
                "체력이 1 이라도 남았으면 판은 안 끝난다");
            Assert.IsFalse(StressMath.IsFull(StressMath.FromHealth(0f, 0f)),
                "마음이 없는 판은 «스트레스 만점» 이 아니다");
        }
    }
}
