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
    // dreamcatcher-shield-break unit 0 — OnShieldBreak(부여된 실드가 피격으로 완전 소진될 때).
    // 발동 지점 = DamageApplicationSystem 실드 Absorb(시간만료 경로는 없음/배제). append-only.
    public enum DcTriggerKind { None, AttackN, OnDamagedN, OnDeath, PeriodicTimer, HealthThreshold, OnKill, OnShieldBreak }
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
        // subconscious-curse-expansion unit 2 — 살찌운 제물. 적을 겨냥하는 최초의
        // 드림캐쳐(ApplyBountyMark 전용 — CommitAttach/defender 경로 유입 시 아래
        // trigger=None 가드로 무차감 거절). 표식 즉시: AwakeningReward ×magnitude
        // (각성 배율, >1 필수) 베이크 덮어쓰기 + 받는 피해 −tileRange %(0~99,
        // ApplyStackToTarget 의 tileRange 재사용 선례). 처치=배율 보상+회수,
        // 유출=무보상 회수(EnemyGone). append-only.
        BountyMark = 15,
        // dreamcatcher-shield-break unit 1 — 실드 파열(OnShieldBreak) 시 N타일 내 가장 가까운
        // M명을 L초 수면. magnitude=M·tileRange=N·duration=L 재사용(신규 DcPayloadSpec 필드 0).
        // 실행=BattleBridge.DrainShieldBreakEvents(적 쿼리+AoeTargetCap+EnemyCcEvent{Sleep}). append-only.
        AreaSleep = 16,
        // projectile-emission-pattern unit 3 — 발사 명세(ProjectilePatternData)를
        // 트리거한다. 이 payload 는 발사 내부를 모르고, emitter 는 드림캐쳐를 모른다 —
        // 접점은 "인스턴스 push" 하나다. 트리거가 사건이고 패턴이 그 한 번의 전개이므로
        // 반복 주기는 트리거 소유다(PeriodicTimer(0.5s) × 패턴(1발) = 0.5초 간격 사격).
        // append-only.
        EmitProjectilePattern = 17,
    }

    // dreamcatcher-new-abilities unit 0 — 데이터 계층 CC 선택자(공격 온-히트용). 정의
    // 계층은 Battle 타입 참조 금지라 Battle.Effects.CcKind 를 직접 못 쓴다 → 이 미러를
    // 두고 BattleBridge 가 bake 시 CcKind 로 번역(CardBuffKind→StatKind 와 동일 패턴).
    // 실제 CcEffect 로 소비되는 종류만: Stun(행동 정지=얼림), Impulse(넉백). unit 1 발견 —
    // 이 엔진의 "Slow" 는 CcEffect 가 아니라 MoveSpeedMul StatModifier(ZoneApplySystem)라
    // 제외. 슬로우 카드가 필요하면 별도 stat 페이로드로(후속). append-only.
    // dreamcatcher-content-3 unit 2 — Sleep(수면, wake-on-hit) append. AreaSleep 이 이미
    // EnemyCcEvent{Sleep} 으로 검증한 적측 수면을 온-히트 단일 대상으로 개통(lullaby_dart).
    public enum DcCcKind { Stun, Impulse, Sleep }

    // dreamcatcher-new-abilities unit 0 — 데이터 계층 스택 선택자. Battle.Effects.StackKind
    // 의 비-None 미러(번역은 BattleBridge). append-only.
    public enum DcStackKind { Fire, Ice, Bleed, Poison }

    // dreamcatcher-trigger-gates unit 1 — 사건 트리거에 얹는 동적 술어 게이트.
    // 트리거 kind 는 "언제 평가하나"(사건), 게이트는 "발화를 허용하나"(상태 술어)로
    // 직교 분해된다 — 조합마다 kind 를 늘리지 않는다(kind 폭발 방지). 게이트 술어는
    // ECS sim 이 unmanaged 로 읽을 수 있는 상태만 가능(Mono 상태 불가 — README 계약).
    // v1 어휘 = HpBelow 하나. append-only.
    public enum DcGateKind { None, HpBelow }

    // 게이트의 주어 — Self(호스트) / EventTarget(사건 상대: AttackN=피해자 등).
    // v1 배선 조합은 OnDamagedN×Self, AttackN×EventTarget 뿐 — 그 외는 bake 거절
    // (배선 표의 단일 SoT = DcTrigger.GateComboSupported). append-only.
    public enum DcGateSubject { Self, EventTarget }

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
        // dreamcatcher-trigger-gates unit 1 — 게이트 축 (기본값 = None/Self/0 →
        // 기존 카드 무손상). gateValue 는 fraction 컨벤션(0~1, 예 0.30 = HP 30%).
        // 판정은 현재값/현재 max 기준(HealthThreshold 의 스폰 스냅샷과 다름) +
        // 카운트 게이트(통과 사건만 counter 증가) — README 계약.
        public DcGateKind gate;
        public DcGateSubject gateSubject;
        public float gateValue;
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
        // boss-jjangssen unit 4 — SelfBlink: **밀집도 판정 반경**(타일, Chebyshev). 착지 앵커를
        // "이 반경 안에 방어유닛이 가장 많은 셀" 로 고른다. tileRange 는 이미 착지 탐색 링 상한이라
        // 두 반경이 다른 의미이므로 여기 싣는다(kind별 필드 재사용 컨벤션). <=0 = 자기 셀만.
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
        // projectile-emission-pattern unit 3 — EmitProjectilePattern 이 트리거할 발사
        // 명세. 정의 계층은 SO 참조를 허용한다(위 projectile·auraPrefab 선례) — 금지
        // 대상은 Entities/Battle 타입이다. null = bake 가 loud 거절. 다른 kind 는 무시.
        public ProjectilePatternData pattern;
        // boss-jjangssen unit 7 — SelfBlink 착지 슬램. 명시 필드를 **추가**한 이유: SelfBlink 는
        // 이미 magnitude(밀집 판정 반경)·tileRange(착지 탐색 링 상한)를 쓰고 있어 자유 스칼라가
        // 남지 않았고(duration/auraScale 재사용은 auraPrefab 선언 시 의미가 겹친다), 무엇보다
        // **데미지 경로**라 이름으로 grep 되어야 한다. append-only → 기존 카드는 0 = 슬램 없음.
        public float slamDamage;    // 착지 시 자기중심 피해. <=0 = 슬램 없음(이동만)
        public int slamTileRange;   // 슬램 반경(타일, Chebyshev)
        // dreamcatcher-content-3 unit 6 — ApplyStackToTarget 전용. **문안 전용 참조**다:
        // 런타임 임계 조회는 여전히 BattleBridge 의 kind→rules 레지스트리(씬의
        // stackModifierAuthoring)가 권위이고, 이 필드는 카드 문안이 "몇 중첩에 무엇이
        // 터지나"를 같은 SO 에서 읽게 해 수치가 문자열로 복제되는 것을 막는다(제약 6).
        // 정의 계층의 SO 참조는 위 projectile·auraPrefab·pattern 선례와 동일 — 금지
        // 대상은 Entities/Battle 타입이다. null = 요약 라인 생략(기존 카드 무변화).
        public StackModifierSO stackModifier;
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
