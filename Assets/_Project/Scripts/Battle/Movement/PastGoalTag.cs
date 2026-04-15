using Unity.Entities;

namespace Wassup.Battle.Movement
{
    // Signals entity reached end of its path; consumed by UnitLifecycleSystem
    public struct PastGoalTag : IComponentData { }
}
