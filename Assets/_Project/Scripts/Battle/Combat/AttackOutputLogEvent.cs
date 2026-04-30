// Attack-output log event channel — AttackSystem enqueues one per output fired;
// BattleBridge drains each frame and forwards to BattleLogger.RecordAttackOutput.
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Wassup.Data;
using Wassup.Battle.Effects;

namespace Wassup.Battle.Combat
{
    public struct AttackOutputLogEvent
    {
        public Entity attacker;
        public AttackOutputKind kind;
        public float magnitude;
        public StatKind stat;       // meaningful only when kind == ApplyStat
        public StackKind stackKind; // meaningful only when kind == ApplyStack
        public float duration;
        public float3 sourcePos;
        public float3 targetPos;
    }

    public struct AttackOutputLogEventsSingleton : IComponentData
    {
        public NativeQueue<AttackOutputLogEvent> queue;
    }
}
