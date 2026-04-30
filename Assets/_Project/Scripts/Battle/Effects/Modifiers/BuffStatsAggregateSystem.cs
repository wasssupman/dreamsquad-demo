// Spec unit 3 (modifier-framework-and-healer): recompute BuffStats cache for every
// entity whose BuffStatsDirty flag is currently enabled, then disable the flag.
// Write authority: this system is the ONLY writer of BuffStats.
// Combine formula: final = (base + Σadd) * Πmul  OR  override_max (when any Override slot present).
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace Wassup.Battle.Effects
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(StatModifierTickSystem))]
    public partial struct BuffStatsAggregateSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // EnabledRefRW<BuffStatsDirty> in a Query acts as an enabled-component filter:
            // only entities with BuffStatsDirty currently ENABLED are iterated.
            // Use WithEntityAccess + SystemAPI.GetBuffer for readable buffer access.
            foreach (var (stats, dirty, entity) in
                     SystemAPI.Query<
                         RefRW<BuffStats>,
                         EnabledRefRW<BuffStatsDirty>>()
                              .WithAll<StatModifierSlot>()
                              .WithEntityAccess())
            {
                var slots = SystemAPI.GetBuffer<StatModifierSlot>(entity);

                // Per-stat accumulators.
                // Defaults: mul=1 (identity for product), add=0, over=0, hasOver=false.
                // base values: damageMul=1, attackSpeedMul=1, dmgTakenMul=1, regenPerSec=0.
                float dMul = 1f, dAdd = 0f, dOver = 0f; bool dHasOver = false;
                float aMul = 1f, aAdd = 0f, aOver = 0f; bool aHasOver = false;
                float tMul = 1f, tAdd = 0f, tOver = 0f; bool tHasOver = false;
                float rMul = 1f, rAdd = 0f, rOver = 0f; bool rHasOver = false;

                for (int i = 0; i < slots.Length; i++)
                {
                    var s = slots[i];

                    // Explicit 4-way stat dispatch (ref-local switch not reliably Burst-compatible).
                    if (s.stat == StatKind.DamageMul)
                    {
                        if      (s.op == CombineOp.Multiplicative) dMul *= s.magnitude;
                        else if (s.op == CombineOp.Additive)       dAdd += s.magnitude;
                        else { dOver = math.max(dOver, s.magnitude); dHasOver = true; }
                    }
                    else if (s.stat == StatKind.AttackSpeedMul)
                    {
                        if      (s.op == CombineOp.Multiplicative) aMul *= s.magnitude;
                        else if (s.op == CombineOp.Additive)       aAdd += s.magnitude;
                        else { aOver = math.max(aOver, s.magnitude); aHasOver = true; }
                    }
                    else if (s.stat == StatKind.DmgTakenMul)
                    {
                        if      (s.op == CombineOp.Multiplicative) tMul *= s.magnitude;
                        else if (s.op == CombineOp.Additive)       tAdd += s.magnitude;
                        else { tOver = math.max(tOver, s.magnitude); tHasOver = true; }
                    }
                    else // StatKind.RegenPerSec
                    {
                        if      (s.op == CombineOp.Multiplicative) rMul *= s.magnitude;
                        else if (s.op == CombineOp.Additive)       rAdd += s.magnitude;
                        else { rOver = math.max(rOver, s.magnitude); rHasOver = true; }
                    }
                }

                // Combine: override wins; otherwise (base + Σadd) * Πmul.
                stats.ValueRW.damageMul      = dHasOver ? dOver : (1f + dAdd) * dMul;
                stats.ValueRW.attackSpeedMul = aHasOver ? aOver : (1f + aAdd) * aMul;
                stats.ValueRW.dmgTakenMul    = tHasOver ? tOver : (1f + tAdd) * tMul;
                stats.ValueRW.regenPerSec    = rHasOver ? rOver : (0f + rAdd) * rMul;

                dirty.ValueRW = false;
            }
        }
    }
}
