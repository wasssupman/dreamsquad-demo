using System;

namespace Wassup.Data
{
    // dreamcatcher-unit-trigger Unit 0 — architecture-agnostic triggered-mechanic
    // definition. This layer is pure data + asset references: it must not reference
    // Unity.Entities or Wassup.Battle types. Interpretation (bake into unmanaged
    // slots + execution) lives entirely in BattleBridge/Combat, so an architecture
    // swap only rewrites the translator, never these definitions.
    // Kill/Damaged/NextWave triggers and SelfTileAoe/NextAttackModifier payloads
    // are follow-ups — append new enum cases at the end (existing card assets
    // serialize these as int; inserting earlier would relabel them).
    public enum DcTriggerKind { None, AttackN }
    public enum DcPayloadKind { None, ProjectileToTarget }

    [Serializable]
    public struct DcTriggerSpec
    {
        public DcTriggerKind kind;
        public int period; // AttackN: fire on every N-th attack resolve
    }

    [Serializable]
    public struct DcPayloadSpec
    {
        public DcPayloadKind kind;
        // ProjectileToTarget: flat damage — attacker stat modifiers (damageMul)
        // are intentionally NOT applied (card values stay predictable).
        public float magnitude;
        // ProjectileToTarget only — trajectory/view definition. Other payload
        // kinds leave this null; re-evaluate splitting this struct per-kind
        // when a second payload kind lands (spec README follow-up).
        public ProjectileData projectile;
    }

    [Serializable]
    public struct DcMechanic
    {
        public DcTriggerSpec trigger;
        public DcPayloadSpec payload;
    }

    // dreamcatcher-attack-mod-bounce Unit 0 — card class (c): trigger-less,
    // always-on modification of the bound unit's base attack output. Same
    // architecture-agnostic contract as the trigger definitions above: pure
    // data, no ECS references; interpretation lives in BattleBridge (bake) and
    // AttackSystem (spawn-time injection). Append new kinds at the end.
    public enum DcAttackModKind { None, ProjectileBounce }

    [Serializable]
    public struct DcAttackModSpec
    {
        public DcAttackModKind kind;
        public int count;          // ProjectileBounce: bounce count
        public int tileRange;      // retarget search radius (Chebyshev tiles)
        public float damageMul;    // per-bounce decay (1 = no decay)
    }
}
