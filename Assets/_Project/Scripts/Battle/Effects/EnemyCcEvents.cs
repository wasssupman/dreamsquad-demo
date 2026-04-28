using Unity.Collections;
using Unity.Entities;

namespace Wassup.Battle.Effects
{
    public struct EnemyCcEvent
    {
        public Entity target;
        public CcEffect effect;
    }

    public struct EnemyCcEventsSingleton : IComponentData
    {
        public NativeQueue<EnemyCcEvent> queue;
    }
}
