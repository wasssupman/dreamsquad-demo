using Unity.Entities;

namespace Wassup.Battle.Effects
{
    // Attached to defender units that currently benefit from adjacency synergy.
    // Phase 4 policy: the component is added when there is at least one same-type
    // neighbor and removed when the count drops to zero — AttackSystem uses a
    // simple HasComponent check to multiply the emitted damage.
    public struct SynergyBuff : IComponentData
    {
        public float damageMul;
    }
}
