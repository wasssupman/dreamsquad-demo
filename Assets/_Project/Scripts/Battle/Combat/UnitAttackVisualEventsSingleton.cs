using Unity.Collections;
using Unity.Entities;

namespace Wassup.Battle.Combat
{
    // Phase 8 §13 follow-up — singleton entity holding the queue AttackSystem
    // enqueues each time a defender fires. BattleBridge drains and notifies the
    // SpineDefenderPool so both projectile-based and melee defenders get their
    // attack animation triggered consistently.
    public struct DefenderAttackEventsSingleton : IComponentData
    {
        public NativeQueue<DefenderAttackEvent> queue;
    }
}
