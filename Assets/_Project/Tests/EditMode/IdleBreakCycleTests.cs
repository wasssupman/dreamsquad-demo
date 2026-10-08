using NUnit.Framework;
using Somnia.Battle.Presentation;

// idle-break-shared unit 0 — 대기 컷 전이 규칙. 두 뷰(Spine·스프라이트)가 이 구조체를 공유하므로
// 여기가 빨개지면 양쪽 대기 동작이 같이 바뀐 것이다.
public class IdleBreakCycleTests
{
    [Test]
    public void Inactive_NeverTransitions()
    {
        var c = IdleBreakCycle.Idle;
        Assert.IsFalse(c.Active);
        Assert.IsFalse(c.Tick(10f));
        c.BeginLoop(1f);
        c.Stop();
        Assert.IsFalse(c.Tick(10f), "Stop 뒤에는 시간이 흘러도 전이하지 않는다");
    }

    [Test]
    public void Loop_ThenBreak_ThenLoop()
    {
        var c = IdleBreakCycle.Idle;
        c.BeginLoop(1f);
        Assert.IsTrue(c.Looping);
        Assert.IsFalse(c.Tick(0.5f));
        Assert.IsTrue(c.Tick(0.5f), "대기 시간이 다 되면 전이");
        c.BeginBreak(0.7f);
        Assert.IsFalse(c.Looping);
        Assert.IsFalse(c.Tick(0.69f));
        Assert.IsTrue(c.Tick(0.02f), "컷 한 바퀴가 끝나면 전이");
        c.BeginLoop(2f);
        Assert.IsTrue(c.Looping);
    }

    [Test]
    public void ZeroInterval_TransitionsOnNextTick()
    {
        var c = IdleBreakCycle.Idle;
        c.BeginLoop(0f);
        Assert.IsTrue(c.Tick(0.001f), "간격 0 = 다음 틱에 바로 컷(연속 재생)");
        c.BeginLoop(-3f);
        Assert.AreEqual(0f, c.Timer, "음수 간격은 0 으로");
    }

    [Test]
    public void PickBreak_AvoidsRepeat_WhenTwoOrMore()
    {
        var c = IdleBreakCycle.Idle;
        int first = c.PickBreak(2, 0.3f);
        for (int i = 0; i < 20; i++)
        {
            int next = c.PickBreak(2, (i % 10) / 10f);
            Assert.AreNotEqual(first, next, "컷이 2개면 반드시 번갈아 나온다");
            first = next;
        }
        Assert.AreEqual(0, c.PickBreak(1, 0.9f), "컷이 하나면 회피 불가 — 그대로");
        Assert.AreEqual(-1, c.PickBreak(0, 0.5f), "컷이 없으면 -1");
    }

    [Test]
    public void LastIndex_SurvivesStop()
    {
        var c = IdleBreakCycle.Idle;
        c.BeginLoop(1f);
        int picked = c.PickBreak(3, 0.5f);
        c.Stop();
        Assert.AreEqual(picked, c.LastIndex, "원샷이 끼어들어 멈춰도 직전 컷 기억은 남는다 — 복귀 뒤 같은 컷 연속 방지");
    }
}
