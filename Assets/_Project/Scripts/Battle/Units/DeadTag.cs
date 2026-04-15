using Unity.Entities;

namespace Wassup.Battle.Units
{
    // Tag indicating the unit's health has reached zero. Produced by DamageApplicationSystem,
    // consumed by UnitLifecycleSystem which destroys the entity.
    public struct DeadTag : IComponentData { }
}
