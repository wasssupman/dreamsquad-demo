using Unity.Entities;
using Unity.Mathematics;

namespace Wassup.Battle.Effects
{
    public struct HazardDestroyedEvent
    {
        public Entity hazardEntity;
        public int hazardSoIndex;
        public float3 worldPosition;
        public int2 centerCell;
    }
}
