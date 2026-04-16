using Unity.Mathematics;

namespace Wassup.Battle.Units
{
    // Emitted by UnitLifecycleSystem immediately before destroying a defender
    // entity. Carries the tile the defender occupied so BattleBridge can free the
    // placement slot and recompute adjacency synergy for surrounding cells.
    public struct DefenderDeathEvent
    {
        public int2 cell;
    }
}
