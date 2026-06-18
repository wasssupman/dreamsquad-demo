using Unity.Entities;

namespace Wassup.Battle.Units
{
    // aggro-targeting Unit 4 — defender class exposed to ECS so enemies can filter
    // and prioritize targets by class (e.g. Shooter → Ranger). Baked from
    // DefenderUnitData.role at spawn.
    public struct DefenderClassTag : IComponentData
    {
        public Wassup.Data.DefenderClass value;
    }
}
