using Unity.Mathematics;

namespace Wassup.Battle.Units
{
    // Emitted by UnitLifecycleSystem immediately before destroying a defender
    // entity. Carries the tile the defender occupied so BattleBridge can free the
    // placement slot and recompute adjacency synergy for surrounding cells.
    public struct DefenderDeathEvent
    {
        public int2 cell;

        // dreamcatcher-content-1 ② (작별 선물) — OnDeath×SelfTileAoe payload baked
        // HERE (before the entity is destroyed), because the drain in BattleBridge
        // runs after UnitLifecycleSystem has already destroyed the entity — reading
        // DcTriggerSlot at drain time would throw. Defaults 0/false = no explosion.
        public bool hasOnDeathAoe;
        public float aoeDamage;
        public int aoeTileRange;
        public int aoeDataIndex;
    }
}
