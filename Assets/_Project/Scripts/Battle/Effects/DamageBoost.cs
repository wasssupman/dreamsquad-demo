using Unity.Entities;

namespace Wassup.Battle.Effects
{
    // Attached to defender units to scale their emitted damage. Combat reads
    // `multiplier` at attack-emission time; `AttackState.damage` stays untouched.
    public struct DamageBoost : IComponentData
    {
        public float remaining;
        public float multiplier;
    }
}
