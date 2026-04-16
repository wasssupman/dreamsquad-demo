using Unity.Entities;
using Unity.Mathematics;

namespace Wassup.Battle.Units
{
    // Carries the placement cell coordinate on each defender entity so
    // UnitLifecycleSystem can report the tile back to BattleBridge for synergy
    // recalculation on death, without needing a managed lookup.
    public struct DefenderTile : IComponentData
    {
        public int2 cell;
    }
}
