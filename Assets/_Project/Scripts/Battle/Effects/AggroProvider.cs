using Unity.Entities;

namespace Wassup.Battle.Effects
{
    // aggro-targeting Unit 0 — Effects-owned. Attached to guardians (defenders
    // with aggroCapacity > 0). Marks an aggro anchor with a simultaneous-hold
    // cap and an acquisition range. The live count is NOT stored here —
    // AggroAssignmentSystem derives it each tick by counting Aggroed enemies
    // (avoids increment/decrement drift).
    public struct AggroProvider : IComponentData
    {
        public int capacity;  // max enemies held at once
        public float range;   // acquisition range (world units)
    }
}
