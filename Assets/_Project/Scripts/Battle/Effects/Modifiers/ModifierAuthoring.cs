namespace Wassup.Battle.Effects
{
    // modifier-additive-authoring Unit 1 — classifies a stat modifier by direction.
    // Policy B: increases (multiplier >= 1) combine additively so multiple buffs sum
    // (1 + Σadd) instead of compounding; reductions (< 1) stay multiplicative for
    // diminishing returns, bounded by the modifier-stacking-policy clamp floor.
    // Single-stack value is preserved either way: (1 + (m-1)) == m, and 1 * m == m.
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

        // dreamcatcher-berserker unit 1 — 「1회분 배율 × 최대 중첩」을 누적 상한
        // (StatModifierApplyEvent.magnitudeCap)으로 환산한다. 자기 버프를 거는 arm 3곳
        // (공격 · 처치 · 경계)이 전부 이 식을 쓴다.
        //
        // `-1` 이 이 함수의 존재 이유다 — 위 FromMultiplier 가 버프를 **가산 버킷**으로 보내서
        // 슬롯에 실리는 값이 배율이 아니라 «배율 − 1» 이다. 상한만 배율 기준으로 계산하면
        // 조용히 한 스택만큼 어긋난다. 세 arm 에 복붙하면 그 어긋남이 셋으로 갈린다.
        //
        // 0 = 누적 안 함(= 기존 덮어쓰기). 최대 중첩 1 은 0 과 같다 — 1회분이 곧 상한이라
        // 두 번째 적용이 자기 값에서 멈춘다.
        public static float StackCap(float multiplier, int maxStacks)
            => maxStacks > 0 && multiplier > 1f ? (multiplier - 1f) * maxStacks : 0f;
    }
}
