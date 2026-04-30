// modifier-framework unit 5 — ECS DynamicBuffer carrier for DefenderUnitData.outputs[].
// BattleBridge attaches this buffer at defender spawn when outputs.Length > 0.
// AttackSystem reads it at hit time to resolve per-output effects instead of the
// legacy single IncomingDamage(attack.damage) path.
using Unity.Entities;
using Wassup.Data;

namespace Wassup.Battle.Combat
{
    public struct AttackOutputElement : IBufferElementData
    {
        public AttackOutput value;
    }
}
