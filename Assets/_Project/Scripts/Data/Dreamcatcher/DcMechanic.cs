using System;
using UnityEngine;

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
    // dreamcatcher-kill-and-threshold unit 0 — OnKill(이 유닛이 적을 처치할 때마다).
    // 발동 지점은 DamageApplicationSystem 킬 처리(주기/카운터 없음) — 다른 트리거처럼
    // AttackSystem RESOLVE 를 타지 않는다. append-only.
    public enum DcTriggerKind { None, AttackN, OnDamagedN, OnDeath, PeriodicTimer, HealthThreshold, OnKill }
    // dreamcatcher-subconscious-unit — SelfWarmupBuff(7): reserved. 핸들러 미구현
    // (BattleBridge 분기 유실, spec-review H4) — 어떤 카드도 사용 안 함. append-only 로 잔존.
    // dreamcatcher-placement-aura — PlacementAura(8): host 부착 스폰 오라. host·기존 유닛
    // 미적용, host 생존 중 axis 매칭 **신규 배치 유닛**에 magnitude% 공속(매치영구) + duration
    // 초 warmup idle 부여. host 사망 시 회수(RegisterPlacementAura → RevokeDreamcatcherEffects).
    // nightmare-whip-aura — AllyMoveSpeedAura(9): 펄스 오라(보스 "채찍질"). PeriodicTimer
    // 펄스마다 host 기준 Chebyshev tileRange 내 **host 와 같은 진영** 유닛(host 자신 제외)에
    // MoveSpeedMul ×(1+magnitude/100), TTL=duration 모디파이어 부여. duration>periodSeconds
    // 가 authoring 계약(merge-refresh 유지) — 이탈/host 사망 시 TTL 자연 만료(revoke 없음).
    // dreamcatcher-new-abilities unit 0 — ApplyCcToTarget(N번째 공격이 맞은 적에게 CC),
    // ApplyStackToTarget(맞은 적에게 원소 스택/DoT) payloads.
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
        ApplyCcToTarget = 10,
        ApplyStackToTarget = 11,
        // dreamcatcher-kill-and-threshold unit 0 — 발동 시 시전 유닛 자신에게 StatModifier
        // 부여(buffStat 선택자). last_stand(HealthThreshold×공격력) / devouring(OnKill×공속).
        SelfStatBuff = 12,
        // dreamcatcher-heavy-strike unit 0 — 응축된 일격. AttackN(period=N) 으로 발동하는
        // 강공: 추가 캐리어를 발사하는 다른 payload 와 달리 그 발동 공격 자신의 출력
        // 데미지를 magnitude 배(2.0=×2)로 만든다. 전 victim(근접 cleave/splash/bounce)
        // 에 적용 — primary 한정인 끝을 보는 눈과 다르다. 발동은 unit 1(AttackSystem),
        // 적용은 unit 2(melee + ProjectileHitSystem, hit-site 배율).
        HeavyStrike = 13,
        // subconscious-curse-expansion unit 0 — 호접몽. 즉발(trigger=None, no slot):
        // 부착 즉시 Sleep(duration 초, 기존 wake-on-hit 이 곧 리스크) + 완주 감시
        // (DreamCocoon 컴포넌트). 무피격 완주 시 self 영구 스탯버프, 피격 wake 시
        // 파탄(버프 없음). magnitude=버프 %(35=+35%), duration=잠 초(> 0.05 필수),
        // buffStat 재사용(SelfStatBuff 선례). append-only.
        DreamCocoon = 14,
    }

    // dreamcatcher-new-abilities unit 0 — 데이터 계층 CC 선택자(공격 온-히트용). 정의
    // 계층은 Battle 타입 참조 금지라 Battle.Effects.CcKind 를 직접 못 쓴다 → 이 미러를
    // 두고 BattleBridge 가 bake 시 CcKind 로 번역(CardBuffKind→StatKind 와 동일 패턴).
    // 실제 CcEffect 로 소비되는 종류만: Stun(행동 정지=얼림), Impulse(넉백). unit 1 발견 —
    // 이 엔진의 "Slow" 는 CcEffect 가 아니라 MoveSpeedMul StatModifier(ZoneApplySystem)라
    // 제외. 슬로우 카드가 필요하면 별도 stat 페이로드로(후속). append-only.
    public enum DcCcKind { Stun, Impulse }

    // dreamcatcher-new-abilities unit 0 — 데이터 계층 스택 선택자. Battle.Effects.StackKind
    // 의 비-None 미러(번역은 BattleBridge). append-only.
    public enum DcStackKind { Fire, Ice, Bleed, Poison }

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
        // dreamcatcher-heavy-strike — HeavyStrike: 강공 데미지 배율(2.0 = ×2).
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
        // nightmare-whip-aura unit 3 rev 2 — 메커닉이 host 에 상시 부착하는 루핑
        // 오라 연출. 메커닉 데이터가 자기 연출을 선언하고 드림캐쳐 파이프라인
        // (bake → DcAuraVisualPool)이 구동한다 — 범용 인프라(StatusFx 등)에 payload
        // kind 분기를 넣지 않는다. null = 무연출(기존 카드 전부 기본값 유지).
        // 어떤 payload kind 든 선언만 하면 동일 경로를 탄다(kind-blind).
        public GameObject auraPrefab;
        public float auraScale; // <=0 = 1 처리 (베이크/풀 공통 해석)
        // dreamcatcher-new-abilities unit 0 — payload 다형성이 필드 다중화를 강제한다
        // (위 "kind별 struct 분리 YAGNI" 는 스칼라 재사용 전제였고, CC/스택은 kind
        // 판별자가 필요). ApplyCcToTarget: ccKind + duration(초). Stun 은 duration 만,
        // Impulse 는 magnitude(넉백 속도)도 사용. ApplyStackToTarget: stackKind +
        // magnitude(스택 수, floor 1~255) + duration(스택당 지속) + tileRange(maxStack 상한,
        // 0=기본 5). 다른 kind 는 기본값 무시.
        public DcCcKind ccKind;
        public DcStackKind stackKind;
        // dreamcatcher-kill-and-threshold unit 0 — SelfStatBuff 선택자. 어떤 스탯을 자신에게
        // 부여할지(CardBuffKind — 정의 계층이 Battle.StatKind 를 모르게 유지, bake 가
        // MapDcBuff 로 번역). magnitude=%(30=+30%), duration=TTL초(<=0 = 영구, arm 이 해석).
        // last_stand=AttackDamage / devouring=AttackSpeed. 다른 kind 는 무시.
        // subconscious-curse-expansion unit 0 — DreamCocoon 도 재사용(완주 버프 스탯).
        public CardBuffKind buffStat;
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
    public enum DcAttackModKind { None, ProjectileBounce, FrontmostTarget }

    [Serializable]
    public struct DcAttackModSpec
    {
        public DcAttackModKind kind;
        public int count;          // ProjectileBounce: bounce count · FrontmostTarget: unused
        public int tileRange;      // ProjectileBounce: retarget radius (Chebyshev) · FrontmostTarget: unused (uses base attack range)
        public float damageMul;    // ProjectileBounce: per-bounce decay (1 = no decay) · FrontmostTarget: primary-target damage multiplier
    }
}
