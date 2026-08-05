namespace Wassup.Sim.Effects
{
    /// <summary>
    /// battle-sim-extraction unit 18-G/3 — 스탯 모디파이어를 **방향으로** 분류한다.
    /// 구 `ModifierAuthoring` 이식.
    ///
    /// 정책: **증가(배율 &gt;= 1)는 가산 결합**이라 여러 버프가 `1 + Σadd` 로 합쳐지고(복리로
    /// 불어나지 않는다), **감소(&lt; 1)는 곱셈 유지**라 수확체감이 걸린다(스택 정책의 바닥 클램프가
    /// 하한을 준다).
    ///
    /// 단일 스택 값은 어느 쪽이든 보존된다 — `1 + (m-1) == m` 이고 `1 * m == m`.
    /// 그래서 이 분류는 **여러 개가 겹칠 때만** 관측된다.
    /// </summary>
    public static class ModifierAuthoring
    {
        public static void FromMultiplier(float multiplier, out CombineOp op, out float magnitude)
        {
            if (multiplier >= 1f)
            {
                op = CombineOp.Additive;
                magnitude = multiplier - 1f;
            }
            else
            {
                op = CombineOp.Multiplicative;
                magnitude = multiplier;
            }
        }
    }
}
