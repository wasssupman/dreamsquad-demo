using Unity.Entities;

namespace Wassup.Battle.Units.HealthBar
{
    public struct HealthBarState : IComponentData
    {
        public Entity owner;
        public float yOffset;
        public float baseScale;
    }
}
