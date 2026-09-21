using NUnit.Framework;
using Wassup.UnitAi;

// defender-autobattle-ai unit 0 — 방어유닛 AI 진리표 = 규칙서. 여기가 빨개지면 판정이 바뀐 것이다(계약 3: 동작 무변 기본).
public class DefenderAiTests
{
    private static DefenderAiInput In(bool deploying = false, bool locked = false, bool swinging = false,
        DefenderAttackPolicy policy = DefenderAttackPolicy.Target, bool summonAlive = false)
        => new DefenderAiInput { deploying = deploying, actionLocked = locked, swinging = swinging, policy = policy, summonAlive = summonAlive };

    [Test]
    public void Priority_DeployingOverEverything()
    {
        Assert.AreEqual(DefenderAiState.Deploying, DefenderAi.Resolve(In(deploying: true, locked: true, swinging: true, policy: DefenderAttackPolicy.Summon, summonAlive: true)));
    }

    [Test]
    public void Priority_LockedOverSwinging_OverSustaining()
    {
        Assert.AreEqual(DefenderAiState.Locked, DefenderAi.Resolve(In(locked: true, swinging: true, policy: DefenderAttackPolicy.Summon, summonAlive: true)));
        Assert.AreEqual(DefenderAiState.Engaging, DefenderAi.Resolve(In(swinging: true, policy: DefenderAttackPolicy.Summon, summonAlive: true)));
        Assert.AreEqual(DefenderAiState.Sustaining, DefenderAi.Resolve(In(policy: DefenderAttackPolicy.Summon, summonAlive: true)));
        Assert.AreEqual(DefenderAiState.Ready, DefenderAi.Resolve(In()));
    }

    [Test]
    public void Sustaining_OnlyForSummonPolicy()
    {
        Assert.AreEqual(DefenderAiState.Ready, DefenderAi.Resolve(In(policy: DefenderAttackPolicy.Target, summonAlive: true)), "Target 정책은 summonAlive 를 무시");
        Assert.AreEqual(DefenderAiState.Ready, DefenderAi.Resolve(In(policy: DefenderAttackPolicy.Bomb, summonAlive: true)));
        Assert.AreEqual(DefenderAiState.Ready, DefenderAi.Resolve(In(policy: DefenderAttackPolicy.Summon, summonAlive: false)));
    }

    [Test]
    public void CanStartAttack_ReadyOrSustaining_WithCooldown()
    {
        Assert.IsTrue(DefenderAi.CanStartAttack(DefenderAiState.Ready, true));
        Assert.IsTrue(DefenderAi.CanStartAttack(DefenderAiState.Sustaining, true), "소환사는 유지 중에도 시도한다(쿨 리셋 규칙 유지)");
        Assert.IsFalse(DefenderAi.CanStartAttack(DefenderAiState.Ready, false));
        Assert.IsFalse(DefenderAi.CanStartAttack(DefenderAiState.Engaging, true));
        Assert.IsFalse(DefenderAi.CanStartAttack(DefenderAiState.Locked, true));
        Assert.IsFalse(DefenderAi.CanStartAttack(DefenderAiState.Deploying, true));
    }
}
