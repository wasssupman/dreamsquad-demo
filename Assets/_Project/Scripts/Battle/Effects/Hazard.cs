using Unity.Entities;
using Unity.Mathematics;

namespace Wassup.Battle.Effects
{
    public struct Hazard : IComponentData
    {
        public float remainingLife;
    }

    [InternalBufferCapacity(2)]
    public struct HazardEffectsBuffer : IBufferElementData
    {
        public HazardEffect effect;
    }

    [InternalBufferCapacity(9)]
    public struct HazardCellsBuffer : IBufferElementData
    {
        public int2 cell;
    }
}
