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
    // dreamcatcher-content-4 unit 0 — OnRetire(이 유닛이 퇴근할 때). 발동 지점은 **브리지의
    // 퇴근 경로**(`BattleBridge.RetireDefender`)이며, 사망 경로를 한 글자도 공유하지 않는다.
    //
    // ⚠ **OnDeath 와 교차 발동하지 않는다 — 이것이 이 트리거의 존재 이유다.** 퇴근은 `DeadTag`
    // 를 달지 않고 `DefenderDied` 를 쏘지 않으므로(defender-clock-out 계약 1) OnDeath 카드가
    // 퇴근에서 안 터지고, 반대로 이 트리거는 브리지의 퇴근 경로에만 있으므로 사망에서 안 터진다.
    // 사망 채널에 플래그로 얹었다면 지금 있는 모든 OnDeath 카드가 "진짜 죽은 건가?"를 되물어야
    // 했다(그 설계는 clock-out rev 1 에서 폐기됐다).
    //
    // 적에게는 열리지 않는다 — `DcTrigger.EnemyTriggerArmed` 무변경이 곧 fail-closed 다(적은
    // 퇴근하지 않는다). append-only.
    // on-place-skill-rework unit 0 — OnPlace(9): 「이 유닛이 판에 놓여 활성화된 순간」.
    // 발화 1회, 카운터 없음. 방어유닛 자기 규칙(UnitSkillAbility) 전용이며 **카드 경로는
    // loud 거절**한다 — 카드는 배치 «후» 에 붙으므로 붙어도 영영 안 터진다.
    // append-only: 에셋이 이 enum 을 int 로 직렬화한다.
    public enum DcTriggerKind { None, AttackN, OnDamagedN, OnDeath, PeriodicTimer, HealthThreshold, OnKill, OnShieldBreak, OnRetire, OnPlace }
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
        // ultimate-leap unit 0 — 이탈→예고→강습. 보스가 판을 떠나 화면 밖으로 사라지고,
        // 착지 셀 주변 slamTileRange 타일이 예고된 뒤 duration 초 후 착지해 슬램 피해.
        // 이탈 동안 공격·이동 불가(LeapFlight) + **피격 불가**(UltimateLeapState 축).
        //
        // SelfBlink 와 분리한 이유: 그건 "즉시 텔레포트 + 뷰 아치 + 피격 가능" 이고 이건
        // "판 밖 이탈 + 예고 + 무적" 이다. 한 kind 에 플래그로 겸직시키면 DotEffect 가 겪은
        // "성격이 다른 둘이 한 슬롯을 공유" 형태가 된다.
        //
        // "생존당 1회" 는 코드에 없다 — fraction >= 0.5 면 두 번째 경계가 음수라 재발동이
        // 수학적으로 불가하다(HealthThresholdEval). 다른 보스 재사용 = 에셋 슬롯 한 줄.
        // 필드 재사용: duration=예고 초 · magnitude=밀집 탐색 반경 · tileRange=착지 링 상한 ·
        // slamDamage/slamTileRange=착지 피해와 예고 범위. 신규 슬롯 필드 0. append-only.
        UltimateLeap = 18,
        // boss-mamemo unit 2 — 실드 부여. tileRange 0 = 자신만 / >0 = 반경 내 같은 진영
        // **유닛(host 제외)**. host 를 포함하면 안 되는 이유가 이 kind 하나로 두 능력을
        // 표현하기 때문이다: ShieldMath 가 `source` 를 병합 키로 쓰므로 「경계마다 자기
        // 실드」와 「주기마다 아군 실드」가 같은 host 에서 나오면 **한 슬롯을 공유**하고,
        // 후자가 전자를 상시 재충전해 "경계에 생기는 벽" 이 "상시 실드" 로 붕괴한다.
        //
        // magnitude = 실드량. **duration 은 쓰지 않는다** — 이 엔진의 실드에는 시간 만료가
        // 없다(ShieldMath 에 TTL 축이 없고 dreamcatcher-shield-break 도 시간만료 경로를
        // 명시적으로 배제했다). 실드는 깎여야만 사라진다. bake 가 duration>0 저작을 경고한다.
        //
        // 실행은 IncomingShield 버퍼 append 다 — 가디언 전용 생산자(ShieldCastSystem,
        // caster·후보 양쪽 DefenderUnitTag 게이트)를 재사용하지 않고 그 아래층을 쓴다.
        // 병합(같은 출처 max · 교차 출처 합산)과 흡수는 DamageApplicationSystem 이 이미
        // 진영 중립으로 한다. append-only.
        GrantShield = 19,
        // elite-enemy-tier unit 5 — 분열. `OnDeath` 트리거와만 쓴다(슬라임 엘리트).
        // magnitude = 자식 수 · splitUnit = 자식 SO.
        //
        // ★**이 조합은 DcTriggerSlot 엔트리를 만들지 않는다**(버퍼 자체는 붙는다 — bake 가
        // mechanics 비어있지 않으면 무조건 AddBuffer 하고 이 조합만 엔트리를 건너뛴다).
        // 다른 payload 는 전부 슬롯을 굽지만
        // 분열은 sim 이 쓸 곳이 없다 — 자식 스폰은 SO·머티리얼·뷰가 필요한 브리지 전용 행위이고,
        // 브리지의 킬 드레인(DrainEnemyKilledEvents)이 `_enemyTypeByEntity` 로 죽은 적의
        // AttackUnitData 를 **이미 손에 들고 있다**(유출 경로가 leakedType.stabilityDamage 를
        // 읽는 것과 같은 방식). 그래서 인덱스 레지스트리·슬롯 필드·EnemyKilledEvent 필드·
        // DamageApplicationSystem 스탬프·EnemyTriggerArmed(OnDeath) 개방이 전부 불필요하다.
        // 초판 설계는 그 다섯을 다 만들려 했고 리뷰(H2)가 걷어냈다.
        // append-only.
        SplitOnDeath = 20,
        // elite-enemy-tier unit 4 — 화염 브레스. `AttackN` 과 쓴다(드래곤 3타).
        // 대상 방향 **부채꼴** 안의 후보 전원에게 즉발 피해. 투사체 캐리어를 만들지 않는다 —
        // `AttackSystem` 이 그 프레임에 이미 들고 있는 후보 배열 위에서 판정한다.
        // 필드: magnitude = 피해 · tileRange = 사거리(타일) · coneHalfAngleDeg = 반각.
        //
        // ⚠ 반각 정의역은 **(0°, 90°)** 다. 판정이 `normalize` 없는 제곱 비교라 부호 가드가
        // 필요하고(없으면 등 뒤에 대칭 콘), 그 가드가 90° 에서 정의역을 자른다. 게다가
        // cos²θ = cos²(180−θ) 라 저작 120° 는 **조용히 60° 콘으로 동작**한다 → bake 가 >= 90 을
        // loud 거절한다. 저작 초기값 50° — 45° 는 셀 대각선 경계에 정확히 걸려 부동소수 비교가
        // 동전 던지기가 된다(결정론 요건). 판정 자체는 `TileAoe.IsInCone`.
        // append-only.
        AreaBreath = 21,
        // dreamcatcher-content-4 unit 0 — 궤도 화염구. host 셀 중심을 도는 투사체 1개를
        // `duration` 초 동안 띄우고, 스친 적에게 `magnitude` 피해를 준다(PathHit 스윕).
        //
        // **신규 payload 필드 0** — 전부 기존 슬롯 재사용:
        //   magnitude = 스친 적에게 줄 피해 · duration = 지속 초 · tileRange = 궤도 반경(타일)
        //   projectile = 탄 SO. 이 SO 가 **뷰 + 선속도(speed) + 피격 반경(hitThreshold) +
        //   재타격 쿨타임(rehitCooldownSec)** 을 전부 소유한다.
        //
        // 재타격 쿨타임을 payload/슬롯/요청 struct 로 관통시키지 않는 이유: `pierceCount` 가
        // 이미 **탄 SO 에 있고 드레인(SpawnProjectile)이 직접 읽는** 선례다. "탄의 성질은
        // ProjectileData 가 소유한다"(projectile-emission-pattern 계약 3). 같은 탄 SO 로 다른
        // 쿨타임이 필요해지면 SO 복제로 갈라진다.
        //
        // 궤도 중심은 **발사 시점 고정점**이라 host 를 추적하지 않는다 — 방어유닛은 타일 고정이고,
        // 덕분에 host 가 죽거나 퇴근해도 이미 나간 화염구는 자기 수명을 산다. append-only.
        SelfOrbitProjectile = 22,
        // on-place-skill-rework unit 4 — 범위 도발(배스티온 배치 스킬). host 반경 tileRange 안
        // 적 전원을 duration 초 동안 host 에게 어그로시킨다.
        //
        // 기존 어휘로 표현되지 않아 만들었다: `ApplyCcToTarget`(10)은 **맞은 대상 1기**라 범위가
        // 아니고, `AreaSleep`(16)은 범위지만 수면 전용이며 **도발은 CC 가 아니라 타게팅 상태**다.
        //
        // host 는 가디언(AggroCapacity 보유)이어야 한다 — 어그로는 그 컴포넌트가 곧 표식이다.
        // 실행은 어그로 획득 요청 큐(AggroAcquireEvent, kind=Taunt)로 넘기고 게이트 판정은
        // 전부 AggroStateSystem(Effects)이 소유한다 — 여기서 복제하면 둘이 갈린다. append-only.
        AreaTaunt = 23,
        // dreamcatcher-content-5 unit 4 — 장판 설치(잿불). `OnKill` 과 쓴다 —
        // 처치한 **그 자리**에 해저드를 깐다(시체폭발 `OnKill × SelfTileAoe` 가 이미
        // 죽은 자리에서 터지는 선례와 같은 형태: 킬 이벤트 스탬프 → 브리지 드레인).
        //
        // **신규 payload 스칼라 0** — 카드는 「어떤 불씨를」만 말한다:
        //   hazard = 깔 장판 SO. 모양·반경·**지속**·효과·틱·뷰가 전부 그 SO 소유다.
        //
        // ⚠ **지속을 카드에 싣지 않은 것은 의도다.** 해저드 수명은 sim(Hazard.remainingLife)
        // 과 뷰(HazardVisualLifetime)가 **둘 다 SO 값을 직접 읽고** 오버라이드 파라미터가
        // 없다. 뚫으려면 Effects 맥락의 스폰 시그니처까지 바꿔야 하는데 소비자는 카드
        // 한 장뿐이라 과잉이고(제약 8), 무엇보다 한쪽만 밀면 **심과 뷰의 수명이 갈린다**.
        // 다른 지속이 필요하면 SO 를 복제해 가른다(탄 SO 복제 관례와 동일).
        //
        // ⚠ 스폰은 **브리지 전용 행위**다 — SO·머티리얼·뷰 프리팹이 필요해 sim 이 만들 수
        // 없다(SplitOnDeath 가 전용 큐·레지스트리를 전부 걷어낸 것과 같은 이유).
        // append-only.
        SpawnHazard = 24,
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
        // elite-enemy-tier unit 5 — SplitOnDeath 전용. 죽을 때 태어날 적 SO.
        // 정의 계층의 SO 참조는 위 projectile·auraPrefab·pattern·stackModifier 선례와 동일 —
        // 금지 대상은 Entities/Battle 타입이고 AttackUnitData 는 같은 Wassup.Data 다.
        // null = bake 가 loud 거절. 다른 kind 는 무시.
        //
        // ⚠ **중간 단계는 메커닉을 갖는다 — 다단계 분열이 사양이다**(슬라임: 본체 → 중간 ×2 →
        // 작은 ×4). 그래서 «자식은 메커닉이 없다» 를 재귀 방어로 쓰지 않는다(초판 규약이었고
        // 2026-08-12 에 폐기됐다). 무한 분열을 실제로 만드는 것은 **사슬이 자기에게 돌아오는
        // 것**이므로 판정은 `SplitChain.Validate`(순환·과길이·자손 총수)가 소유하고 bake 가
        // 호출한다. 저작 규칙은 하나다 — **마지막 단계의 `nightmareMechanics` 를 비워** 사슬을
        // 끝낼 것.
        public AttackUnitData splitUnit;
        // dreamcatcher-content-4 unit 8 — SelfOrbitProjectile 전용 **구슬 개수**(0/1 = 1개).
        // 명시 필드인 이유는 `slamDamage`·`coneHalfAngleDeg` 와 같다 — 이 payload 가 쓰는
        // 스칼라는 전부 임자가 있다(magnitude=피해 · duration=지속 · tileRange=궤도 반경).
        // 개수를 그중 하나에 겸직시키면 읽는 쪽이 «이게 반경인가 개수인가» 를 매번 되묻는다.
        // 배치는 위상 균등 분할(2π/n) — 같은 궤도·같은 수명, 시작 각도만 어긋난다.
        public int orbitCount;
        // dreamcatcher-content-5 unit 0 — SpawnHazard 전용. 깔 장판 SO.
        // 정의 계층의 SO 참조는 위 projectile·auraPrefab·pattern·stackModifier·splitUnit
        // 선례와 동일하다 — 금지 대상은 Entities/Battle 타입이고 HazardSO 는 같은 Wassup.Data
        // 다. null = bake 가 loud 거절. 다른 kind 는 무시.
        public HazardSO hazard;
        // elite-enemy-tier unit 4 — AreaBreath 전용 반각(도). 정의역 (0, 90) — 위 kind 주석 참조.
        // `duration` 에 겸직시키지 않는다: 그 필드는 이미 3개 kind 가 «시간» 으로 쓰고 있어
        // 겸직하면 «시간인 줄 알고» 읽는 코드가 생긴다(slamDamage 가 명시 필드가 된 선례와 같은
        // 판단). 0 = bake 경고. 다른 kind 는 무시. append-only.
        public float coneHalfAngleDeg;
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
    // dreamcatcher-content-4 unit 0 — DamageVsSleeping: 잠든 적을 때리면 그 타격의 피해 ×배율.
    // **판정은 피해자별**이다 — 잠든 적 옆의 깨어 있는 적은 그대로다. 배율은 기존
    // `DcAttackModSpec.damageMul` 재사용(2.0 = ×2) → **신규 필드 0**.
    //
    // 트리거 축(게이트 × HeavyStrike)으로 만들지 않은 이유: 강공은 그 공격의 **전 victim**
    // (근접 cleave/splash/bounce)을 배율해서, 잠든 적 옆의 깨어 있는 적까지 2배가 된다(사양 초과).
    // 적용 지점은 `DamageVsCcMul`(shatter_hymn)이 이미 피해자별로 곱해지는 2곳과 같다 —
    // 투사체 발사 시 bestTarget 스냅샷 · 근접/AoE 는 hitTarget 별. append-only.
    public enum DcAttackModKind { None, ProjectileBounce, FrontmostTarget, DamageVsSleeping }

    [Serializable]
    public struct DcAttackModSpec
    {
        public DcAttackModKind kind;
        public int count;          // ProjectileBounce: bounce count · FrontmostTarget: unused
        public int tileRange;      // ProjectileBounce: retarget radius (Chebyshev) · FrontmostTarget: unused (uses base attack range)
        public float damageMul;    // ProjectileBounce: per-bounce decay (1 = no decay) · FrontmostTarget: primary-target damage multiplier
    }
}
