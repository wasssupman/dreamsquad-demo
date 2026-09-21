using NUnit.Framework;
using Wassup.Battle.Combat;

// defender-deploy-phase unit 3 — 행동 시작 가능 여부의 단일 표. 여기가 빨개지면 Attack·Movement 의 START 게이트가 같이 바뀐 것.
public class UnitActionPhaseTests
{
    [Test]
    public void Resolve_RanksLockedOverSwinging()
    {
        Assert.AreEqual(ActionPhase.Free, UnitActionPhase.Resolve(false, false));
        Assert.AreEqual(ActionPhase.Swinging, UnitActionPhase.Resolve(false, true));
        Assert.AreEqual(ActionPhase.Locked, UnitActionPhase.Resolve(true, false));
        Assert.AreEqual(ActionPhase.Locked, UnitActionPhase.Resolve(true, true), "잠금이 스윙보다 위");
    }

    [Test]
    public void OnlyFree_CanStart_AndEveryPhase_ResolvesSwing()
    {
        Assert.IsTrue(UnitActionPhase.CanStartAction(ActionPhase.Free));
        Assert.IsFalse(UnitActionPhase.CanStartAction(ActionPhase.Swinging));
        Assert.IsFalse(UnitActionPhase.CanStartAction(ActionPhase.Locked));
        // combat-action-lock 규약: 시작된 스윙은 CC 중에도 완료
        Assert.IsTrue(UnitActionPhase.CanResolveSwing(ActionPhase.Locked));
        Assert.IsTrue(UnitActionPhase.CanResolveSwing(ActionPhase.Swinging));
    }
}
