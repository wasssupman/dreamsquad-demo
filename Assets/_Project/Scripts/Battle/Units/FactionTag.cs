using Unity.Entities;

namespace Wassup.Battle.Units
{
    // Identifies an entity's faction for attack-targeting filtering.
    // Owned by Units context (entity identity). Read by Combat (AttackSystem).
    public struct FactionTag : IComponentData
    {
        public Faction value;
    }
}
