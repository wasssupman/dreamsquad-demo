using Unity.Collections;
using Unity.Entities;

namespace Wassup.Battle.Units
{
    // Singleton component that publishes a persistent-allocated NativeQueue of
    // defender-death events. BattleBridge owns the queue lifetime (StartBattle
    // creates, TeardownCurrentBattle + OnDestroy dispose), mirroring the
    // GoalReachedEventsSingleton pattern from Phase 0.
    public struct DefenderDeathEventsSingleton : IComponentData
    {
        public NativeQueue<DefenderDeathEvent> queue;
    }
}
