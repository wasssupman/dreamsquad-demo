using Unity.Collections;
using Unity.Entities;

namespace Wassup.Battle.Units
{
    public struct GoalReachedEventsSingleton : IComponentData
    {
        public NativeQueue<GoalReachedEvent> queue;
    }
}
