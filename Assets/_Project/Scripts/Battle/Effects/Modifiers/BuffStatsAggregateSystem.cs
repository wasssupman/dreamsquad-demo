// Spec unit 3 (modifier-framework-and-healer): recompute BuffStats cache for every
// entity with a BuffStats component.
// Write authority: this system is the ONLY writer of BuffStats.
// Combine formula: final = (base + Σadd) * Πmul  OR  override_max (when any Override slot present).
//
// Unit 7 adapter note: during the legacy-migration period (units 7–8) this system
// unconditionally recomputes ALL BuffStats entities every frame (dirty flag ignored).
// It also reads legacy DamageBoost / CooldownReduction / SynergyBuff components via
// ComponentLookup and folds them into the aggregate result so AttackSystem can drop
// its own legacy lookups without any buff regression.
// Unit 9 will restore dirty-only iteration and remove this legacy read path.
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
        // Legacy component lookups — adapter period only (units 7–8).
        // Removed in unit 9 together with the legacy component definitions.
        private ComponentLookup<DamageBoost>       _damageBoostLookup;
        private ComponentLookup<CooldownReduction> _cooldownReductionLookup;
        private ComponentLookup<SynergyBuff>       _synergyBuffLookup;

        public void OnCreate(ref SystemState state)
        {
            _damageBoostLookup       = state.GetComponentLookup<DamageBoost>(isReadOnly: true);
            _cooldownReductionLookup = state.GetComponentLookup<CooldownReduction>(isReadOnly: true);
            _synergyBuffLookup       = state.GetComponentLookup<SynergyBuff>(isReadOnly: true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _damageBoostLookup.Update(ref state);
            _cooldownReductionLookup.Update(ref state);
            _synergyBuffLookup.Update(ref state);

            // Unit 7 adapter: iterate ALL BuffStats entities unconditionally (no dirty filter).
            // StatModifierSlot buffer may be absent on entities that only carry legacy components,
            // so we use WithEntityAccess + TryGetBuffer to handle both cases.
            foreach (var (stats, entity) in
                     SystemAPI.Query<RefRW<BuffStats>>()
                              .WithEntityAccess())
            {
                // Per-stat accumulators.
                // Defaults: mul=1 (identity for product), add=0, over=0, hasOver=false.
                // base values: damageMul=1, attackSpeedMul=1, dmgTakenMul=1, regenPerSec=0.
                float dMul = 1f, dAdd = 0f, dOver = 0f; bool dHasOver = false;
                float aMul = 1f, aAdd = 0f, aOver = 0f; bool aHasOver = false;
                float tMul = 1f, tAdd = 0f, tOver = 0f; bool tHasOver = false;
                float rMul = 1f, rAdd = 0f, rOver = 0f; bool rHasOver = false;

                // ── New-framework modifier slots (may be absent) ──────────────────
                if (SystemAPI.HasBuffer<StatModifierSlot>(entity))
                {
                    var slots = SystemAPI.GetBuffer<StatModifierSlot>(entity);
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
                }

                // ── Legacy adapter (unit 7–8): fold legacy components into accumulators ──
                // DamageBoost.multiplier → damageMul multiplicative layer.
                if (_damageBoostLookup.HasComponent(entity))
                    dMul *= _damageBoostLookup[entity].multiplier;

                // CooldownReduction.multiplier is a cooldown scale (0.5 = half cooldown).
                // BuffStats.attackSpeedMul is the *inverse* (cooldown = base / attackSpeedMul),
                // so: attackSpeedMul *= 1 / cooldownReduction.multiplier.
                if (_cooldownReductionLookup.HasComponent(entity))
                {
                    float crMul = _cooldownReductionLookup[entity].multiplier;
                    if (crMul > 0f)
                        aMul *= 1f / crMul;
                }

                // SynergyBuff.damageMul → damageMul multiplicative layer.
                if (_synergyBuffLookup.HasComponent(entity))
                    dMul *= _synergyBuffLookup[entity].damageMul;

                // Combine: override wins; otherwise (base + Σadd) * Πmul.
                stats.ValueRW.damageMul      = dHasOver ? dOver : (1f + dAdd) * dMul;
                stats.ValueRW.attackSpeedMul = aHasOver ? aOver : (1f + aAdd) * aMul;
                stats.ValueRW.dmgTakenMul    = tHasOver ? tOver : (1f + tAdd) * tMul;
                stats.ValueRW.regenPerSec    = rHasOver ? rOver : (0f + rAdd) * rMul;
            }
        }
    }
}
