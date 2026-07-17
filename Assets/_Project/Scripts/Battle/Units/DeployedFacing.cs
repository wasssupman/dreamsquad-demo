using Unity.Entities;
using Unity.Mathematics;

namespace Wassup.Battle.Units
{
    // defender-directional-volley unit 1 — permanent attack facing chosen in the
    // deployment aim phase (Arknights-style). Units context owns the single
    // write at activation (via BattleBridge, the only Mono↔ECS gateway); Combat
    // reads it to gate lane targeting and orient volleys. Immutable afterwards.
    public struct DeployedFacing : IComponentData
    {
        public int2 value; // cardinal unit vector on the tile grid
    }
}
