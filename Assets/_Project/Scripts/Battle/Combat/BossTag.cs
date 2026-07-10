using Unity.Entities;

namespace Wassup.Battle.Combat
{
    // nightmare-catcher unit 4 — marker for the boss among enemies
    // (AttackUnitTag). Attached only by the spawn bake when the enemy's
    // AttackUnitData.nightmareMechanics is non-empty (unit 5). Gates nothing by
    // itself — the new trigger arms stay faction-neutral by gating on buffer
    // presence (DcTriggerSlot / ThreatEntry), never on this tag or
    // DefenderUnitTag. The tag exists for identification: bake targeting,
    // debug queries, and future boss-only rules (aggro resist follow-up).
    public struct BossTag : IComponentData { }
}
