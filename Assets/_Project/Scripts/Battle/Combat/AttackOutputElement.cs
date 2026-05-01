// modifier-framework unit 5 — ECS DynamicBuffer carrier for DefenderUnitData.outputs[].
// BattleBridge attaches this buffer at defender spawn when outputs.Length > 0.
// AttackSystem reads it to resolve per-output effects instead of defender
// legacy single IncomingDamage(attack.damage) fallback.
using Unity.Entities;
using Wassup.Data;

namespace Wassup.Battle.Combat
{
    public struct AttackOutputElement : IBufferElementData
    {
        public AttackOutput value;
    }
}
