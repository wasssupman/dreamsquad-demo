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
    }
}
