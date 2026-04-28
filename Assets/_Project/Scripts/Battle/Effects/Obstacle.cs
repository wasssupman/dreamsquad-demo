using Unity.Entities;
using Unity.Mathematics;

namespace Wassup.Battle.Effects
{
    public struct Obstacle : IComponentData
    {
        public int2 cell;
        // Presentation-only: visual presenter reads this for placement.
        // ECS trim does not consume worldPosition (uses `cell`).
        public float3 worldPosition;
        public float remainingLife;
    }
}
