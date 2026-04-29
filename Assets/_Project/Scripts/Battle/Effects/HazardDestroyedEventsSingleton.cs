using Unity.Collections;
using Unity.Entities;

namespace Wassup.Battle.Effects
{
    public struct HazardDestroyedEventsSingleton : IComponentData
    {
        public NativeQueue<HazardDestroyedEvent> queue;
    }
}
