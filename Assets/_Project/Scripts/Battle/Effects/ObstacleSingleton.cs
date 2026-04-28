using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Wassup.Battle.Effects
{
    public struct ObstacleSingleton : IComponentData
    {
        public NativeHashSet<int2> blockedCells;
    }
}
