using Unity.Entities;

namespace Wassup.Battle.Combat
{
    // nightmare-catcher unit 4 — marker for the boss among enemies (AttackUnitTag).
    //
    // elite-enemy-tier unit 0 — ★attached only when `AttackUnitData.tier == EnemyTier.Boss`.
    // It used to be "nightmareMechanics is non-empty", which made every enemy carrying a
    // special mechanic a boss — and this tag is no longer inert: CC immunity
    // (CcApplySystem / EffectSpawner), aggro immunity (AggroStateSystem), a MovementSystem
    // branch and an AttackSystem cleave exception all gate on it. Elites carry mechanics
    // WITHOUT this tag; that separation is the whole point of the tier axis.
    //
    // Trigger arms stay faction-neutral by gating on buffer presence
    // (DcTriggerSlot / ThreatEntry), never on this tag or DefenderUnitTag.
    public struct BossTag : IComponentData { }
}
