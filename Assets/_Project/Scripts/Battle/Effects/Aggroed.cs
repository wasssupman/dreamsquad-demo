using Unity.Entities;

namespace Wassup.Battle.Effects
{
    // aggro-targeting Unit 0 — Effects-owned. Sticky link from an aggroed enemy
    // to the guardian holding it. Written/cleared only by AggroAssignmentSystem;
    // MovementSystem and AttackSystem read it cross-context (read-only), mirroring
    // the TornadoField→MovementSystem precedent.
    public struct Aggroed : IComponentData
    {
        public Entity guardian;  // the guardian that aggroed this enemy (first-come, sticky)
    }
}
