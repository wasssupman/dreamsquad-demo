using Unity.Entities;
using Unity.Mathematics;

namespace Wassup.Battle.Effects
{
    // Multi-cell occupancy. ObstacleLifetimeSystem uses this to populate blockedCells.
    public struct BlockingHazardCellsBuffer : IBufferElementData
    {
        public int2 cell;
    }
}
