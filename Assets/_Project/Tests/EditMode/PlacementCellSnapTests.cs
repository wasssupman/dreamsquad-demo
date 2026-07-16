using NUnit.Framework;
using UnityEngine;
using Wassup.UI;

// placement-cell-snap unit 0 — 히스테리시스 셀 선택 순수 함수 회귀 테스트.
public class PlacementCellSnapTests
{
    private static readonly Vector2Int Grid = new Vector2Int(10, 10);
    private const float Margin = 0.18f;

    private static Vector2Int Resolve(Vector2Int? cur, float fx, float fy, float margin = Margin)
        => PlacementCellSnap.Resolve(cur, new Vector2(fx, fy), margin, Grid);

    // current == null → 순수 round (floor(f+0.5), half-up).
    [Test]
    public void NoCurrent_RoundsHalfUp()
    {
        Assert.AreEqual(new Vector2Int(0, 0), Resolve(null, 0.4f, 0.4f));
        Assert.AreEqual(new Vector2Int(1, 1), Resolve(null, 0.6f, 0.6f));
        Assert.AreEqual(new Vector2Int(3, 3), Resolve(null, 2.5f, 2.5f)); // 2.5 → 3
    }

    // 경계 지터 흡수: current=3 에서 frac.x 가 3.5±margin 밴드 안이면 3 유지(naive round 면 4 로 튐).
    [Test]
    public void BoundaryJitter_KeepsCurrent()
    {
        var cur = new Vector2Int(3, 5);
        Assert.AreEqual(3, Resolve(cur, 3.5f, 5f).x);
        Assert.AreEqual(3, Resolve(cur, 3.6f, 5f).x);
        Assert.AreEqual(3, Resolve(cur, 3.45f, 5f).x);
    }

    // 확실한 이동: 밴드(3+0.68=3.68) 초과 → 4 로 전환.
    [Test]
    public void PastBand_TransitionsUp()
    {
        Assert.AreEqual(4, Resolve(new Vector2Int(3, 5), 3.8f, 5f).x);
    }

    // 반대 방향 대칭: 밴드 하한(3-0.68=2.32) 미만 → 2 로 전환.
    [Test]
    public void PastBand_TransitionsDown()
    {
        Assert.AreEqual(2, Resolve(new Vector2Int(3, 5), 2.2f, 5f).x);
    }

    // x/y 독립: 한 축만 밴드를 벗어나면 그 축만 전환.
    [Test]
    public void Axes_ResolveIndependently()
    {
        var r = Resolve(new Vector2Int(3, 3), 3.8f, 3.5f);
        Assert.AreEqual(new Vector2Int(4, 3), r); // x 전환, y 는 3.5 가 밴드 안이라 유지
    }

    // margin=0 → 순수 round 와 동일(상한 exclusive 라 3.5 는 4).
    [Test]
    public void ZeroMargin_EqualsPureRound()
    {
        Assert.AreEqual(4, Resolve(new Vector2Int(3, 5), 3.5f, 5f, 0f).x);
        Assert.AreEqual(3, Resolve(new Vector2Int(3, 5), 3.4f, 5f, 0f).x);
    }

    // margin 0.5+ 는 상한(0.49) 으로 clamp 되어도 이웃 진입이 여전히 가능.
    [Test]
    public void OverMargin_ClampedStillTransitions()
    {
        Assert.AreEqual(4, Resolve(new Vector2Int(3, 5), 4.0f, 5f, 0.9f).x);
    }

    // 결과는 grid 범위로 clamp.
    [Test]
    public void Result_ClampedToGrid()
    {
        Assert.AreEqual(new Vector2Int(9, 0), Resolve(null, 20f, -3f));
    }
}
