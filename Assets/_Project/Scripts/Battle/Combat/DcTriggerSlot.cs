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
        // barrage payload params. periodSeconds/elapsed/fireCount 는 보스 스폰
        // 경로만 bake(디펜더 카드는 0=inert). fraction/maxHpRef/nextBoundaryIndex
        // 는 dreamcatcher-kill-and-threshold unit 1 에서 디펜더 last_stand
        // (HealthThreshold×SelfStatBuff)도 bake 한다. Owned writes stay Combat:
        // elapsed/fireCount = BossPeriodicTriggerSystem, nextBoundaryIndex =
        // HealthThresholdSystem (counter above stays AttackSystem-only).
        public float periodSeconds;   // PeriodicTimer 주기 초 (<=0 = no-fire, 계약 9)
        public float elapsed;         // PeriodicTimer accumulator (잔여 이월)
        public int fireCount;         // AreaBarrage 진앙 round-robin (발동 시에만 증가)
        public float fraction;        // HealthThreshold 경계 간격 (<=0 = no-fire)
        public int nextBoundaryIndex; // HealthThreshold 래치 k (베이크 시 1, 단조 전진)
        public float maxHpRef;        // 스폰 시점 maxHp 스냅샷 (경계 기준 고정)
        public float duration;        // AreaBarrage 낙하 텔레그래프 초 → SkyFall flightTime
                                      // nightmare-whip-aura — AllyMoveSpeedAura: 펄스당 modifier TTL 초

        // dreamcatcher-new-abilities unit 0 — 온-히트 payload 선택자. bake 시 데이터
        // 계층 DcCcKind/DcStackKind 를 Battle enum 으로 번역 저장(hot path 무번역).
        // ApplyCcToTarget=ccKind, ApplyStackToTarget=stackKind. 소비는 unit 1.
        public Wassup.Battle.Effects.CcKind ccKind;
        public Wassup.Battle.Effects.StackKind stackKind;

        // dreamcatcher-kill-and-threshold unit 0 — SelfStatBuff 대상 스탯. bake 시
        // CardBuffKind→StatKind 번역 저장(ccKind/stackKind 선례). arm 은 magnitude(배율)·
        // duration(TTL, <=0=영구)과 함께 self 에 StatModifierApplyEvent enqueue. 기본값
        // DamageMul(0) 은 SelfStatBuff 가 아닌 슬롯에선 inert.
        public Wassup.Battle.Effects.StatKind buffStat;
        // dreamcatcher-kill-and-threshold unit 1 — SelfStatBuff 재부여 merge 키(stackId).
        // 위 instanceId(트리거 인스턴스 네임스페이스)와 달리 이건 **StatModifier stackId
        // 네임스페이스**의 값 — bake 가 BattleBridge._dcStackCounter(squad 이펙트와 동일
        // 단일 할당자)에서 뽑는다. 같은 슬롯이 매 킬/틱 같은 stackId 로 enqueue → 비스택
        // refresh(지속만 갱신). instanceId 를 잘라 쓰지 않으므로 두 네임스페이스는 여전히
        // 분리(위 instanceId 주석의 불변식 유지).
        public ushort statBuffStackId;
    }
}
