using NUnit.Framework;
using Wassup.UnitAi;

// defender-deploy-phase — 배치 페이즈 시계 규칙(로직 레이어). 적용 레이어 테스트(DeploymentActivationSystemTests)와 짝.
public class DeployPhaseClockTests
{
    [Test]
    public void InFlight_DoesNotTick()
    {
        float r = 0.5f;
        Assert.IsFalse(DeployPhaseClock.Advance(false, ref r, 10f));
        Assert.AreEqual(0.5f, r, "비행 중엔 시계가 흐르지 않는다");
    }

    [Test]
    public void Deploying_ActivatesWhenRemainingReachesZero()
    {
        float r = 0.25f;
        Assert.IsFalse(DeployPhaseClock.Advance(true, ref r, 0.1f));
        Assert.IsFalse(DeployPhaseClock.Advance(true, ref r, 0.1f));
        Assert.IsTrue(DeployPhaseClock.Advance(true, ref r, 0.1f), "0.3 ≥ 0.25 — 활성화");
        Assert.LessOrEqual(r, 0f);
    }

    [Test]
    public void ZeroRemaining_ActivatesOnFirstTick()
    {
        float r = 0f;
        Assert.IsTrue(DeployPhaseClock.Advance(true, ref r, 0.016f), "모션 없는 유닛은 첫 틱에 활성화");
    }
}
