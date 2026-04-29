using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Wassup.Battle.Effects
{
    public struct HazardSingleton : IComponentData
    {
        public NativeParallelMultiHashMap<int2, HazardEffect> cellToEffects;
    }
}
