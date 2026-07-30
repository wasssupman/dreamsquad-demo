using NUnit.Framework;
using UnityEngine;
using Wassup.UI;

// placement-cell-snap unit 0 — 히스테리시스 셀 선택 순수 함수 회귀 테스트.
public class PlacementCellSnapTests
{
    private static readonly Vector2Int Grid = new Vector2Int(10, 10);
    private const float Margin = 0.18f;

    // drag-cancel-affordance unit 3 — 히스테리시스 케이스는 격자 밖 관용과 직교하므로, 이 헬퍼는
    // 관용을 넓게 열어(WideTolerance) **종전의 무조건 clamp 거동**을 재현한다. 관용 자체의 계약은
    // 아래 OutsideTolerance_* 테스트가 따로 잰다 — 두 관심사를 한 헬퍼에 섞지 않는다.
    private const int WideTolerance = 999;

    private static Vector2Int Resolve(Vector2Int? cur, float fx, float fy, float margin = Margin)
        => PlacementCellSnap.Resolve(cur, new Vector2(fx, fy), margin, Grid, WideTolerance).Value;

    private static Vector2Int? ResolveTol(Vector2Int? cur, float fx, float fy, int tol, float margin = Margin)
        => PlacementCellSnap.Resolve(cur, new Vector2(fx, fy), margin, Grid, tol);

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

    // 큰 margin(0.9): 밴드 [3-1.4, 3+1.4) — 손가락이 이웃 칸 중심(4.0)에 있어도 끈적하게 3 유지.
    [Test]
    public void LargeMargin_HoldsDeepIntoNeighbor()
    {
        Assert.AreEqual(3, Resolve(new Vector2Int(3, 5), 4.0f, 5f, 0.9f).x);
    }

    // 상한 clamp: margin 2.0 은 MaxMargin(0.95) 과 동일 동작(밴드 [1.55, 4.45) → 4.3 은 안쪽).
    [Test]
    public void OverMargin_ClampsToMax()
    {
        Assert.AreEqual(Resolve(new Vector2Int(3, 5), 4.3f, 5f, 0.95f),
                        Resolve(new Vector2Int(3, 5), 4.3f, 5f, 2.0f));
    }

    // 아무리 끈적해도 밴드를 확실히 벗어나면 손가락이 있는 칸으로 전환(갇히지 않음). round(4.7)=5.
    [Test]
    public void LargeMargin_ExitsToFingerCell()
    {
        Assert.AreEqual(5, Resolve(new Vector2Int(3, 5), 4.7f, 5f, 0.95f).x);
    }

    // 결과는 grid 범위로 clamp (관용 안에서는 종전 그대로).
    [Test]
    public void Result_ClampedToGrid()
    {
        Assert.AreEqual(new Vector2Int(9, 0), Resolve(null, 20f, -3f));
    }

    // --- drag-cancel-affordance unit 3: 격자 밖 관용 → "칸 없음" ---
    // 이 계약이 "보드 밖에서 놓으면 취소" 를 성립시킨다. 예전엔 무조건 clamp 라 화면 어디를
    // 눌러도 칸이 나왔고, EndDrag 의 "칸 없음 → 취소" 분기가 도달 불가였다.

    [Test]
    public void OutsideTolerance_WithinTolerance_SnapsToBorderCell()
    {
        // cx = -1 (frac -1.0 → round -1) : tol 1 이면 테두리 칸 0 으로 붙는다.
        Assert.AreEqual(new Vector2Int(0, 0), ResolveTol(null, -1.0f, -1.0f, 1));
        // 반대편도 대칭: cx = 10 → 9.
        Assert.AreEqual(new Vector2Int(9, 9), ResolveTol(null, 10.0f, 10.0f, 1));
    }

    [Test]
    public void OutsideTolerance_BeyondTolerance_ReturnsNull()
    {
        Assert.IsNull(ResolveTol(null, -2.0f, 5f, 1), "x 가 관용 밖이면 칸 없음");
        Assert.IsNull(ResolveTol(null, 5f, -2.0f, 1), "y 가 관용 밖이면 칸 없음");
        Assert.IsNull(ResolveTol(null, 11.0f, 5f, 1), "상한 쪽도 대칭");
    }

    [Test]
    public void OutsideTolerance_Zero_RejectsAnyOffGridRound()
    {
        Assert.IsNull(ResolveTol(null, -1.0f, 5f, 0), "tol 0 = 격자 밖 반올림은 즉시 칸 없음");
        Assert.AreEqual(new Vector2Int(0, 5), ResolveTol(null, -0.4f, 5f, 0), "격자 안은 그대로");
    }

    // 히스테리시스가 "테두리 칸에 올라선 뒤의 관용" 을 담당한다 — tol 0 이어도 밴드 안에서는
    // 테두리 칸이 유지되므로 가장자리 배치가 tol 에 의해 갑자기 빡빡해지지 않는다(load-bearing).
    [Test]
    public void OutsideTolerance_HysteresisStillHoldsBorderCell()
    {
        var held = ResolveTol(new Vector2Int(0, 5), -0.5f, 5f, 0);
        Assert.AreEqual(new Vector2Int(0, 5), held, "밴드 안(|f - 0| < 0.5+margin)이면 테두리 칸 유지");
        Assert.IsNull(ResolveTol(new Vector2Int(0, 5), -1.0f, 5f, 0), "밴드를 벗어나면 칸 없음");
    }

    // --- unit 7: EvaluateStretch (끈적함 블롭 신호) ---

    private static void Stretch(Vector2Int committed, float fx, float fy, float margin,
                                out Vector2 dir, out float t)
        => PlacementCellSnap.EvaluateStretch(committed, new Vector2(fx, fy), margin, out dir, out t);

    // 셀 중심 = 긴장 0.
    [Test]
    public void Stretch_AtCenter_IsZero()
    {
        Stretch(new Vector2Int(3, 3), 3f, 3f, Margin, out _, out var t);
        Assert.AreEqual(0f, t, 1e-4f);
    }

    // ★ 정합 계약: Resolve 가 실제로 넘어가는 지점에서 t 는 1 이어야 한다(블롭이 거짓말하면 안 됨).
    [Test]
    public void Stretch_AtResolveTransitionPoint_IsOne()
    {
        float half = 0.5f + Margin;                 // 0.68
        float justInside = 3f + half - 1e-3f;       // Resolve: 아직 3 유지
        float justOutside = 3f + half + 1e-3f;      // Resolve: 4 로 전환

        Assert.AreEqual(3, Resolve(new Vector2Int(3, 3), justInside, 3f).x, "밴드 안이면 유지");
        Stretch(new Vector2Int(3, 3), justInside, 3f, Margin, out _, out var tIn);
        Assert.AreEqual(1f, tIn, 5e-3f, "전환 직전 t≈1");

        Assert.AreEqual(4, Resolve(new Vector2Int(3, 3), justOutside, 3f).x, "밴드 밖이면 전환");
        Stretch(new Vector2Int(3, 3), justOutside, 3f, Margin, out _, out var tOut);
        Assert.AreEqual(1f, tOut, 1e-4f, "전환 시점 t 는 1 로 clamp");
    }

    // 대각선: 체비쇼프라 한 축만 밴드에 닿아도 t=1 (Resolve 도 그 축에서 넘어감).
    // 유클리드였다면 t≈0.71 로 "아직 안 끊김"이라 거짓말했을 케이스.
    [Test]
    public void Stretch_Diagonal_UsesChebyshevMatchingResolve()
    {
        float half = 0.5f + Margin;
        Stretch(new Vector2Int(3, 3), 3f + half, 3f + half, Margin, out _, out var t);
        Assert.AreEqual(1f, t, 1e-4f);
        // 같은 지점에서 Resolve 는 두 축 모두 전환 → 시각(t=1)과 판정이 일치.
        Assert.AreEqual(new Vector2Int(4, 4), Resolve(new Vector2Int(3, 3), 3f + half, 3f + half));
    }

    // dir 은 당기는 방향 단위벡터. 중심에선 zero.
    [Test]
    public void Stretch_Dir_IsNormalizedPullDirection()
    {
        Stretch(new Vector2Int(3, 3), 3.5f, 3f, Margin, out var dir, out _);
        Assert.AreEqual(1f, dir.magnitude, 1e-3f);
        Assert.AreEqual(1f, dir.x, 1e-3f);

        Stretch(new Vector2Int(3, 3), 3f, 3f, Margin, out var zero, out _);
        Assert.AreEqual(Vector2.zero, zero);
    }

    // margin clamp 를 Resolve 와 공유 — 과한 margin 도 같은 상한으로 정규화.
    [Test]
    public void Stretch_SharesMarginClampWithResolve()
    {
        Stretch(new Vector2Int(3, 3), 4.3f, 3f, 0.95f, out _, out var atMax);
        Stretch(new Vector2Int(3, 3), 4.3f, 3f, 2.0f, out _, out var over);
        Assert.AreEqual(atMax, over, 1e-5f);
    }
}
