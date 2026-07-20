using NUnit.Framework;
using UnityEngine;
using Wassup.UI;

// placement-cell-snap unit 3 — throttle(주기적 커밋) 순수 스텝 회귀 테스트.
public class PlacementSnapDebounceTests
{
    private const float Interval = 0.2f;

    private static Vector2Int C(int x, int y) => new Vector2Int(x, y);

    // tick 사이(elapsed < interval): target 이 달라도 committed 유지.
    [Test]
    public void BetweenTicks_HoldsCommitted()
    {
        var s = new PlacementSnapDebounce.State();
        Assert.AreEqual(C(3, 3), PlacementSnapDebounce.Step(ref s, C(3, 3), C(4, 3), 0.1f, Interval)); // 0.1 < 0.2
    }

    // interval 도달 시 현재 target 으로 커밋 + 경과 리셋.
    [Test]
    public void AtTick_CommitsTarget()
    {
        var s = new PlacementSnapDebounce.State();
        PlacementSnapDebounce.Step(ref s, C(3, 3), C(4, 3), 0.1f, Interval);            // 0.1
        Assert.AreEqual(C(4, 3), PlacementSnapDebounce.Step(ref s, C(3, 3), C(4, 3), 0.1f, Interval)); // 0.2 >= 0.2
    }

    // 커밋 후 경과가 리셋돼 다음 tick 까지 다시 interval 필요(주기적).
    [Test]
    public void AfterTick_ResetsAndWaitsAgain()
    {
        var s = new PlacementSnapDebounce.State();
        PlacementSnapDebounce.Step(ref s, C(3, 3), C(4, 3), 0.2f, Interval); // tick → 리셋
        Assert.AreEqual(C(4, 3), PlacementSnapDebounce.Step(ref s, C(4, 3), C(5, 3), 0.1f, Interval)); // 0.1 < 0.2 → 유지
        Assert.AreEqual(C(5, 3), PlacementSnapDebounce.Step(ref s, C(4, 3), C(5, 3), 0.1f, Interval)); // 0.2 → 커밋
    }

    // 이동 중(매 프레임 target 변경)에도 interval 마다 스텝 이동.
    [Test]
    public void MovingContinuously_StepsPerInterval()
    {
        var s = new PlacementSnapDebounce.State();
        Vector2Int committed = C(0, 0);
        int commits = 0;
        // 1.0초 동안 매 프레임(0.05s) 손가락이 계속 이동(target 증가) → interval 0.2 마다 1회 = 5회 스텝.
        for (int f = 1; f <= 20; f++)
        {
            var target = C(f, 0);
            var next = PlacementSnapDebounce.Step(ref s, committed, target, 0.05f, Interval);
            if (next != committed) commits++;
            committed = next;
        }
        Assert.AreEqual(5, commits); // 5Hz
    }

    // interval<=0 → 매 프레임 실시간(throttle off).
    [Test]
    public void ZeroInterval_RealtimeFollow()
    {
        var s = new PlacementSnapDebounce.State();
        Assert.AreEqual(C(4, 3), PlacementSnapDebounce.Step(ref s, C(3, 3), C(4, 3), 0.001f, 0f));
    }
}
