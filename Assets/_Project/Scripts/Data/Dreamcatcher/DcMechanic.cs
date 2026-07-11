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
    // nightmare-catcher unit 0 — PeriodicTimer(주기)·HealthThreshold(누적 임계치)
    // triggers + AreaBarrage(원격 진앙 TileAoe 폭격)·SelfBlink(자기 순간이동)
    // payloads. 보스/적 능동 스킬 편입 — 정의 계층은 진영을 모른다.
    public enum DcTriggerKind { None, AttackN, OnDamagedN, OnDeath, PeriodicTimer, HealthThreshold }
    // dreamcatcher-subconscious-unit — SelfWarmupBuff(7): reserved. 핸들러 미구현
    // (BattleBridge 분기 유실, spec-review H4) — 어떤 카드도 사용 안 함. append-only 로 잔존.
    // dreamcatcher-placement-aura — PlacementAura(8): host 부착 스폰 오라. host·기존 유닛
    // 미적용, host 생존 중 axis 매칭 **신규 배치 유닛**에 magnitude% 공속(매치영구) + duration
    // 초 warmup idle 부여. host 사망 시 회수(RegisterPlacementAura → RevokeDreamcatcherEffects).
    // nightmare-whip-aura — AllyMoveSpeedAura(9): 펄스 오라(보스 "채찍질"). PeriodicTimer
    // 펄스마다 host 기준 Chebyshev tileRange 내 **host 와 같은 진영** 유닛(host 자신 제외)에
    // MoveSpeedMul ×(1+magnitude/100), TTL=duration 모디파이어 부여. duration>periodSeconds
    // 가 authoring 계약(merge-refresh 유지) — 이탈/host 사망 시 TTL 자연 만료(revoke 없음).
    public enum DcPayloadKind
    {
        None = 0,
        ProjectileToTarget = 1,
        SelfTileAoe = 2,
        NextAttackDoubleFire = 3,
        SelfBuffLethal = 4,
        AreaBarrage = 5,
        SelfBlink = 6,
        SelfWarmupBuff = 7,
        PlacementAura = 8,
        AllyMoveSpeedAura = 9,
    }

    [Serializable]
    public struct DcTriggerSpec
    {
        public DcTriggerKind kind;
        public int period; // AttackN: fire on every N-th attack resolve
        // nightmare-catcher unit 0 — PeriodicTimer: 주기 초. <=0 이면 트리거
        // 순수함수가 발동하지 않는다(kind 디스패치가 아닌 함수 내부 가드 —
        // 값 누락(0) 카드의 매 틱 스핀-발동 방지). 기본 0 = 기존 카드 inert.
        public float periodSeconds;
        // nightmare-catcher unit 0 — HealthThreshold: 경계 간격(스폰 시점 maxHp
        // 스냅샷 비율, 예 0.10 = 90%,80%,… 누적 하향 돌파마다 발동, 래치 단조).
        // <=0 이면 발동 안 함(동일 가드). 기본 0 = inert.
        public float fraction;
    }

    [Serializable]
    public struct DcPayloadSpec
    {
        public DcPayloadKind kind;
        // ProjectileToTarget: flat damage — attacker stat modifiers (damageMul)
        // are intentionally NOT applied (card values stay predictable).
        // nightmare-catcher unit 0 — AreaBarrage: 타일당 flat 데미지(동일 원칙).
        // nightmare-whip-aura — AllyMoveSpeedAura: 이속 증가 %(20 = +20%,
        // placement-aura 의 magnitude=% 컨벤션). 음수 = 아군 슬로우(허용,
        // aggregator floor 클램프).
        public float magnitude;
        // ProjectileToTarget: trajectory/view definition. nightmare-catcher
        // unit 0 — AreaBarrage: SkyFall 낙하 비주얼. 나머지 kind 는 null 유지
        // (kind별 struct 분리는 여전히 YAGNI — 전 필드 재사용으로 신규 필드 0).
        public ProjectileData projectile;
        // dreamcatcher-content-1 — SelfTileAoe: AOE 반경(타일). 기본 0 = 기존 카드 inert.
        // nightmare-catcher unit 0 — AreaBarrage: 진앙 중심 Chebyshev AoE 반경 /
        // SelfBlink: 착지 탐색 반경(링 순회 상한).
        // nightmare-whip-aura — AllyMoveSpeedAura: host 중심 오라 반경(Chebyshev).
        public int tileRange;
        // dreamcatcher-content-1 — SelfBuffLethal: 지속/자폭 초. 기본 0.
        // nightmare-catcher unit 0 — AreaBarrage: 낙하 텔레그래프 초 → SkyFall
        // flightTime(request-carried, Meteor 의 warningSec 슬롯 대응). 0 = 즉시 착탄.
        // nightmare-whip-aura — AllyMoveSpeedAura: 펄스당 버프 TTL 초.
        // authoring 계약: duration > trigger.periodSeconds (위반 시 범위 내 점멸,
        // 베이크가 경고). <=0 은 arm 이 enqueue skip.
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
