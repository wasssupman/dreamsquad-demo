using Unity.Entities;

namespace Wassup.Battle.Units
{
    public struct Health : IComponentData
    {
        public float value;
        public float max;
    }
}
