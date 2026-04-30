using Unity.Collections;
using Unity.Entities;

namespace Wassup.Battle.Combat
{
    // Singleton holding the queue AttackSystem enqueues each time any attacker
    // fires. BattleBridge drains and notifies SpineUnitPool so defenders and
    // enemies alike get attack animation + facing-flip from a single channel.
    public struct UnitAttackVisualEventsSingleton : IComponentData
    {
        public NativeQueue<UnitAttackVisualEvent> queue;
    }
}
