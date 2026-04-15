using Unity.Entities;
using Unity.Mathematics;

namespace Wassup.Battle.Movement
{
    public struct PathWaypoint : IBufferElementData
    {
        public int2 cell;
    }
}
