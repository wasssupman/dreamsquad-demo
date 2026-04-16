using Unity.Entities;

namespace Wassup.Battle.Effects
{
    // Attached to defender units to shorten the cooldown applied after each
    // attack. Combat multiplies the reset cooldown by `multiplier` (e.g. 0.5
    // halves time between shots). `AttackState.cooldownDuration` stays untouched.
    public struct CooldownReduction : IComponentData
    {
        public float remaining;
        public float multiplier;
    }
}
