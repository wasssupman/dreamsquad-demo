using Unity.Collections;
using Unity.Entities;

namespace Wassup.Battle.Movement
{
    public struct MovementPauseRequest
    {
        public Entity target;
        public float duration;
    }

    public struct MovementPauseRequestEventsSingleton : IComponentData
    {
        public NativeQueue<MovementPauseRequest> queue;
    }
}
