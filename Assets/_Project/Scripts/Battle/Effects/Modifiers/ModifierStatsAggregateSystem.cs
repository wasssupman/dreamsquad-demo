// Spec unit 3 (modifier-framework-and-healer): recompute ModifierStats cache for every
// entity with a dirty ModifierStats component.
// Write authority: this system is the ONLY writer of ModifierStats.
// Combine formula: final = (base + Σadd) * Πmul  OR  override_max (when any Override slot present).
//
// Unit 9: legacy adapter (DamageBoost / CooldownReduction / SynergyBuff fold-in) removed.
// dirty-only iteration restored: only entities with ModifierStatsDirty enabled are processed.
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace Wassup.Battle.Effects
{
    [BurstCompile]
    [UpdateInGroup(typeof(BattleSimGroup))]
    [UpdateAfter(typeof(StatModifierTickSystem))]
    public partial struct ModifierStatsAggregateSystem : ISystem
    {
        // modifier-stacking-policy — framework clamp bounds (not per-unit stats).
        // Ranges are wide enough that normal single modifiers never hit them; only
        // pathological cross-source stacking is bounded. The move floor is preemptive:
        // no authored slow currently reaches it (Ice slow is x0.4), and there is no
        // full-stop movement CC consumer yet — a root, if ever needed, must be a
        // dedicated movement flag, not moveSpeedMul->0 (which this floor would cap).
        private const float MulStatFloor = 0.2f;   // damage/attackSpeed/dmgTaken: max -80%
        private const float MulStatCeil = 5f;      //   … max +400%
        private const float MoveMulFloor = 0.15f;
        private const float MoveMulCeil = 3f;
        // season-gimmick-overwork unit 1 — maxHealth 전용 floor: 라스트런 ×0.1(-90%) 이
        // 일반 floor(0.2) 에 걸리면 안 된다. 1 HP 바닥은 Units 의 ScaleMax 가 보장.
        private const float MaxHealthMulFloor = 0.05f;

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (stats, dirty, entity) in
                     SystemAPI.Query<RefRW<ModifierStats>, EnabledRefRW<ModifierStatsDirty>>()
                              .WithEntityAccess())
            {
                // Per-stat accumulators.
                // Defaults: mul=1 (identity for product), add=0, over=0, hasOver=false.
                // base values: damageMul=1, attackSpeedMul=1, dmgTakenMul=1, regenPerSec=0, moveSpeedMul=1.
                float dMul = 1f, dAdd = 0f, dOver = 0f; bool dHasOver = false;
                float aMul = 1f, aAdd = 0f, aOver = 0f; bool aHasOver = false;
                float tMul = 1f, tAdd = 0f, tOver = 0f; bool tHasOver = false;
                float rMul = 1f, rAdd = 0f, rOver = 0f; bool rHasOver = false;
                float mMul = 1f, mAdd = 0f, mOver = 0f; bool mHasOver = false;
                // dreamcatcher-new-abilities unit 2 — DamageVsCcMul (base 1, 부재→1).
                float vMul = 1f, vAdd = 0f, vOver = 0f; bool vHasOver = false;
                // season-gimmick-overwork unit 1 — MaxHealthMul (base 1, 부재→1).
                float hMul = 1f, hAdd = 0f, hOver = 0f; bool hHasOver = false;

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
                        else if (s.stat == StatKind.RegenPerSec)
                        {
                            if      (s.op == CombineOp.Multiplicative) rMul *= s.magnitude;
                            else if (s.op == CombineOp.Additive)       rAdd += s.magnitude;
                            else { rOver = math.max(rOver, s.magnitude); rHasOver = true; }
                        }
                        else if (s.stat == StatKind.MoveSpeedMul)
                        {
                            if      (s.op == CombineOp.Multiplicative) mMul *= s.magnitude;
                            else if (s.op == CombineOp.Additive)       mAdd += s.magnitude;
                            else { mOver = math.max(mOver, s.magnitude); mHasOver = true; }
                        }
                        else if (s.stat == StatKind.DamageVsCcMul)
                        {
                            if      (s.op == CombineOp.Multiplicative) vMul *= s.magnitude;
                            else if (s.op == CombineOp.Additive)       vAdd += s.magnitude;
                            else { vOver = math.max(vOver, s.magnitude); vHasOver = true; }
                        }
                        else if (s.stat == StatKind.MaxHealthMul)
                        {
                            if      (s.op == CombineOp.Multiplicative) hMul *= s.magnitude;
                            else if (s.op == CombineOp.Additive)       hAdd += s.magnitude;
                            else { hOver = math.max(hOver, s.magnitude); hHasOver = true; }
                        }
                    }
                }

                // Combine: override wins; otherwise (base + Σadd) * Πmul, then clamp.
                // modifier-stacking-policy — distinct-source multiplicative modifiers
                // stack unbounded (ModifierApplySystem keys slots on source); the clamp
                // stops a debuff product decaying a stat to ~0 or a buff running away.
                // regenPerSec is a resource value (base 0), not a multiplier — no clamp
                // beyond preventing a negative rate.
                stats.ValueRW.damageMul      = ModifierMath.CombineMul(dHasOver, dOver, dAdd, dMul, MulStatFloor, MulStatCeil);
                stats.ValueRW.attackSpeedMul = ModifierMath.CombineMul(aHasOver, aOver, aAdd, aMul, MulStatFloor, MulStatCeil);
                stats.ValueRW.dmgTakenMul    = ModifierMath.CombineMul(tHasOver, tOver, tAdd, tMul, MulStatFloor, MulStatCeil);
                stats.ValueRW.regenPerSec    = math.max(0f, rHasOver ? rOver : (0f + rAdd) * rMul);
                stats.ValueRW.moveSpeedMul   = ModifierMath.CombineMul(mHasOver, mOver, mAdd, mMul, MoveMulFloor, MoveMulCeil);
                // dreamcatcher-new-abilities unit 2 — base 1, 부재→1 (슬롯 없으면 vMul=1).
                stats.ValueRW.damageVsCcMul  = ModifierMath.CombineMul(vHasOver, vOver, vAdd, vMul, MulStatFloor, MulStatCeil);
                // season-gimmick-overwork unit 1 — 전용 floor (라스트런 ×0.1 통과).
                stats.ValueRW.maxHealthMul   = ModifierMath.CombineMul(hHasOver, hOver, hAdd, hMul, MaxHealthMulFloor, MulStatCeil);

                dirty.ValueRW = false;
            }
        }
    }
}
