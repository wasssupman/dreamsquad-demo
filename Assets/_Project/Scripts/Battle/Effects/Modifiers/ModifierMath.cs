using Unity.Mathematics;

namespace Wassup.Battle.Effects
{
    // modifier-stacking-policy Unit 0 — the aggregate combine + clamp invariant,
    // shared by every multiplier stat in ModifierStatsAggregateSystem.
    // Distinct-source modifiers merge into separate slots (ModifierApplySystem keys
    // on source), so multiplicative stacking is unbounded without this floor/ceil:
    // a debuff product can decay a stat to ~0, a buff product can run away upward.
    public static class ModifierMath
    {
        // Result = clamp(override ? over : (1 + Σadd) * Πmul, floor, ceil).
        // The override branch is clamped too so an authored value can't escape policy.
        public static float CombineMul(bool hasOver, float over, float add, float mul, float floor, float ceil)
        {
            float raw = hasOver ? over : (1f + add) * mul;
            return math.clamp(raw, floor, ceil);
        }
    }
}
