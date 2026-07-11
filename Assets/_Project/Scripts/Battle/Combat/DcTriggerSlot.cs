using Unity.Entities;
using Wassup.Data;

namespace Wassup.Battle.Combat
{
    // dreamcatcher-unit-trigger Unit 1 — baked, unmanaged form of one unit-bound
    // triggered card mechanic (definition layer: DcMechanic). Combat-owned:
    // counter writes happen only in AttackSystem RESOLVE; attach/remove happens
    // only through BattleBridge (the sole MonoBehaviour↔ECS gateway).
    [InternalBufferCapacity(2)]
    public struct DcTriggerSlot : IBufferElementData
    {
        // Effect-instance id — one slot per attached mechanic instance, so two
        // copies of the same card get independent counters. Separate namespace
        // from stat-modifier stackId: never compare the two.
        public int instanceId;
        public DcTriggerKind trigger;
        public ushort period;   // AttackN: fire on every N-th attack resolve
        public ushort counter;  // owned write: AttackSystem RESOLVE only
        public DcPayloadKind payload;
        public float magnitude; // flat damage — attacker damageMul intentionally not applied

        // ProjectileToTarget only — baked from ProjectileData at attach time
        // (AttackSystem cannot read managed SOs). projectileDataIndex lifetime =
        // session: _projectileDataByIndex is never reset by BeginPlacement.
        public int projectileDataIndex;
        public float speed;
        public float hitThreshold;
        public float visualScale;
        // dreamcatcher-content-1 — SelfTileAoe(OnDeath 폭발): AOE 반경(타일). 기본 0.
        // nightmare-catcher — AreaBarrage: 진앙 AoE 반경 / SelfBlink: 착지 탐색 반경.
        public int tileRange;

        // ── nightmare-catcher unit 5 — periodic/threshold trigger state +
        // barrage payload params. Baked only by the boss spawn path; defender
        // card slots leave these 0 (inert). Owned writes stay Combat:
        // elapsed/fireCount = BossPeriodicTriggerSystem, nextBoundaryIndex =
        // BossHealthThresholdSystem (counter above stays AttackSystem-only).
        public float periodSeconds;   // PeriodicTimer 주기 초 (<=0 = no-fire, 계약 9)
        public float elapsed;         // PeriodicTimer accumulator (잔여 이월)
        public int fireCount;         // AreaBarrage 진앙 round-robin (발동 시에만 증가)
        public float fraction;        // HealthThreshold 경계 간격 (<=0 = no-fire)
        public int nextBoundaryIndex; // HealthThreshold 래치 k (베이크 시 1, 단조 전진)
        public float maxHpRef;        // 스폰 시점 maxHp 스냅샷 (경계 기준 고정)
        public float duration;        // AreaBarrage 낙하 텔레그래프 초 → SkyFall flightTime
                                      // nightmare-whip-aura — AllyMoveSpeedAura: 펄스당 modifier TTL 초
    }
}
