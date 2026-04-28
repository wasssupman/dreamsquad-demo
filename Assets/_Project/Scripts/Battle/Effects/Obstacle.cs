using Unity.Entities;
using Unity.Mathematics;

namespace Wassup.Battle.Effects
{
    public struct Obstacle : IComponentData
    {
        public int2 cell;
        public float3 worldPosition;
        public float remainingLife;
    }
}
