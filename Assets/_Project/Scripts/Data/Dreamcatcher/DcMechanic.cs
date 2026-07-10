using System;

namespace Wassup.Data
{
    // dreamcatcher-unit-trigger Unit 0 — architecture-agnostic triggered-mechanic
    // definition. This layer is pure data + asset references: it must not reference
    // Unity.Entities or Wassup.Battle types. Interpretation (bake into unmanaged
    // slots + execution) lives entirely in BattleBridge/Combat, so an architecture
    // swap only rewrites the translator, never these definitions.
    // Append new enum cases at the end (existing card assets serialize these as
    // int; inserting earlier would relabel them).
    // dreamcatcher-content-1 — OnDamagedN(5회 피격), OnDeath(사망) triggers +
    // SelfTileAoe(사망 폭발), NextAttackDoubleFire(다음 공격 2연발),
    // SelfBuffLethal(즉발 공속버프+자폭) payloads.
    public enum DcTriggerKind { None, AttackN, OnDamagedN, OnDeath }
    // dreamcatcher-subconscious-unit — SelfWarmupBuff(느린 각성): 부착 즉시 공속
    // +magnitude% (만료 타이머 없음, 유닛 생존 동안) + duration 초 warmup idle. 자폭
    // 없음. 엔티티에 붙는 StatModifier 라 유닛 사망 시 함께 소멸. append 유지.
    public enum DcPayloadKind { None, ProjectileToTarget, SelfTileAoe, NextAttackDoubleFire, SelfBuffLethal, SelfWarmupBuff }

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
        // dreamcatcher-content-1 — SelfTileAoe: AOE 반경(타일). 기본 0 = 기존 카드 inert.
        public int tileRange;
        // dreamcatcher-content-1 — SelfBuffLethal: 지속/자폭 초. 기본 0.
        public float duration;
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
