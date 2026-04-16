using Unity.Entities;

namespace Wassup.Battle.Units.HealthBar
{
    // Identifies a health-bar visual entity. Lives in the Units context because
    // it is tightly coupled to Health (Units-owned).
    public struct HealthBarTag : IComponentData { }
}
