using Wassup.Data;

namespace Wassup.Core
{
    // dreamcatcher-attack-decoupling — "이 mechanic/mod 가 이 host 에서 발동할 수
    // 있는가"의 순수 판정. UI preflight(DreamcatcherAttachEval)와 커밋 bake
    // (ApplyDreamcatcherCardToUnit)가 **같은 함수**를 호출한다 — 예전엔 두 미러를
    // 손으로 맞췄고 그 부채가 "붙는데 무효" 조합의 원인이었다.
    //
    // 범위 경계: host **종속** 조건만 본다. magnitude<=0 · projectile==null ·
    // duration<=0 같은 **카드 데이터 검증**은 어느 host 에서든 결과가 같으므로
    // 이 계층 밖이다(기존 DreamcatcherAttachEval.cs 의 같은 판단을 잇는다).
    //
    // ECS 무참조: 입력은 브리지가 채운 plain 값(DcHostProfile)이고 여기서
    // Entity/EntityManager 를 모른다. DreamcatcherAttachEval 과 같은 자리.

    // host 의 실제 공격 모델. SO 선언이 아니라 런타임 컴포넌트로 판별한다
    // (BombLauncherState → BombThrow / HazardCastAbility → HazardCast /
    // defender PatternSlot → FacingVolley / 그 외 Standard).
    public enum DcHostArchetype { Standard, FacingVolley, BombThrow, HazardCast }

    // host 가 실제로 타는 발사 경로. ProjectileData.flightMode 를 그대로 쓰면
    // 안 된다 — Projectile_Bomb 은 flightMode 0(Homing) 이지만 폭탄맨은
    // GrenadeToCell 하드코딩 경로로 쏜다(spec 계약 6).
    public enum DcProjectileRoute { None, Homing, Ballistic, Directional, Grenade }

    public struct DcHostProfile
    {
        public DcHostArchetype archetype;
        public DcProjectileRoute route;
        public bool targetsEnemies;   // AttackState.targetMask 의 Enemy 비트
        public bool hasDamageOutput;  // 양수 Damage output 보유
        public bool hasLethalTimer;   // 이중 상태 거부용
        public bool hasDreamCocoon;   // 〃
    }

    // 거절 사유 — loud 경고 문구와 UI 표시가 같은 어휘를 쓰도록. None = 지원.
    public enum DcRejectReason
    {
        None = 0,
        NoEventPoint,        // 이 host 에는 그 트리거의 사건 지점이 없다
        NeedsEnemyTargeting, // 적을 겨누지 않는 host (힐러)
        NeedsDamageOutput,   // 데미지 output 이 없는 host
        // 재조준 가능한 경로(homing/directional)를 요구하는 mod. 이름은 개통 전 어휘가
        // 남은 것 — 지금 의미는 "Ballistic/Grenade 처럼 착탄이 고정된 경로는 불가"다.
        NeedsHomingRoute,
        NeedsTargetContext,  // "그 공격의 대상"이 확정되지 않는 host
        DuplicateState,      // 이미 같은 상태 컴포넌트를 갖고 있다
        // unit 3 — host 가 대상을 못 주는데 폴백 반경(payload.tileRange)마저 0.
        // 붙어도 니들이 영영 안 나가므로 계약 4("부착 가능 ⇒ 발동")상 거절이다.
        NeedsFallbackRange,
        // 배선 안 된 kind 가 판정에 도달했다 = 통합 버그. 정상 거절 사유와 섞으면
        // 다음 사람이 틀린 단서를 쫓는다. total 테스트가 이 값으로 미분류를 잡는다.
        Unclassified,
    }

    public static class DcApplicability
    {
        // 이 host 가 **자기 공격의 대상**을 확정해 주는가. Standard/FacingVolley 는
        // RESOLVE 의 bestTarget 을 주고, BombThrow(blind bombardment)·HazardCast
        // (attackRange 0)는 주지 못한다 — 후자에서 대상이 필요한 payload 는 스스로
        // 골라야 하고(unit 2 폴백), 폴백 반경이 없으면 발동할 수 없다.
        public static bool HostProvidesTarget(DcHostArchetype archetype)
            => archetype == DcHostArchetype.Standard || archetype == DcHostArchetype.FacingVolley;

        // 이 트리거가 실행 경로에 배선돼 있는가. **archetype 축은 없다** — unit 3·4 로
        // 전 아키타입이 자기 "공격 성립" 지점 1곳을 갖췄기 때문이다(계약 2 표):
        // Standard/FacingVolley = RESOLVE · BombThrow = 폭탄 발사 · HazardCast = 캐스트.
        // 남은 역할은 하나뿐 — 새 DcTriggerKind 를 append 하고 arm 배선을 잊었을 때
        // 조용히 통과시키지 않는 fail-closed 게이트.
        public static bool IsTriggerWired(DcTriggerKind trigger)
        {
            switch (trigger)
            {
                case DcTriggerKind.None:
                case DcTriggerKind.AttackN:
                case DcTriggerKind.OnDamagedN:
                case DcTriggerKind.OnDeath:
                case DcTriggerKind.PeriodicTimer:
                case DcTriggerKind.HealthThreshold:
                case DcTriggerKind.OnKill:
                case DcTriggerKind.OnShieldBreak:
                // dreamcatcher-content-4 unit 0 — 퇴근. 사건 지점은 AttackSystem 계열이 아니라
                // **브리지의 퇴근 경로**(RetireDefender)다. 이 함수가 묻는 것은 "배선돼 있나"
                // 뿐이고 배선 위치는 묻지 않는다 — OnDeath 가 UnitLifecycleSystem 에,
                // OnShieldBreak 가 DamageApplicationSystem 에 있는 것과 같다.
                case DcTriggerKind.OnRetire:
                // on-place-skill-rework unit 0 — 배치. 사건 지점은 브리지의 배치 확정 경로가
                // 붙이는 `JustDeployed` 태그이고 소비는 BossPeriodicTriggerSystem 이다.
                // ⚠ 이 함수는 "배선돼 있나"만 묻는다. **카드가 이 트리거를 쓸 수 있는지는 별개**이며,
                // 카드 bake 가 loud 거절한다(카드는 배치 후에 붙어 영영 안 터진다) —
                // `EmitProjectilePattern` 이 카드 경로에서 거절되는 것과 같은 자리·같은 이유.
                case DcTriggerKind.OnPlace:
                    return true;
                default:
                    return false;
            }
        }

        // 판정은 mechanic 전체를 본다 — trigger(게이트 축)와 payload(반경 축)가 둘 다
        // host 와 얽히기 때문이다. host 속성은 profile 하나로 접혀 있고, magnitude/
        // duration 같은 순수 데이터 검증은 여기 없다(bake 가 최종 판정).
        public static DcRejectReason EvaluateMechanic(in DcMechanic mechanic, in DcHostProfile host)
        {
            var trigger = mechanic.trigger;
            var payload = mechanic.payload;
            if (!IsTriggerWired(trigger.kind)) return DcRejectReason.NoEventPoint;

            // 게이트 주어가 EventTarget 인데 host 가 대상을 확정해 주지 않으면 게이트를
            // **평가할 수단이 없다**. 폭탄맨/캐스터의 사건 지점은 RESOLVE 처럼 bestTarget
            // 을 들고 있지 않아서, 그대로 두면 게이트가 실패가 아니라 **없는 것처럼**
            // 무시되고 조건 없이 발동한다(사양 초과 — 조용한 무효보다 나쁘다).
            if (trigger.gate != DcGateKind.None
                && trigger.gateSubject == DcGateSubject.EventTarget
                && !HostProvidesTarget(host.archetype))
                return DcRejectReason.NeedsTargetContext;

            switch (payload.kind)
            {
                // 비수 — 니들은 host 의 대상으로 날아가고(host 우선), host 가
                // 대상을 못 고르면 자체 탐색한다. 어느 쪽이든 대상은 적이어야
                // 하므로 아군을 겨누는 host(힐러)에서는 성립하지 않는다.
                case DcPayloadKind.ProjectileToTarget:
                    if (!host.targetsEnemies) return DcRejectReason.NeedsEnemyTargeting;
                    // host 가 대상을 안 주는 부류면 폴백 반경이 유일한 수단이다.
                    return HostProvidesTarget(host.archetype) || payload.tileRange > 0
                        ? DcRejectReason.None : DcRejectReason.NeedsFallbackRange;

                // *그 공격의 대상*에 걸리는 페이로드 — 자체 탐색 폴백을 주지
                // 않는다(spec 계약 9). 대상이 확정되지 않는 host 에선 영구 거절.
                case DcPayloadKind.ApplyCcToTarget:
                case DcPayloadKind.ApplyStackToTarget:
                    if (!host.targetsEnemies) return DcRejectReason.NeedsEnemyTargeting;
                    return HostProvidesTarget(host.archetype)
                        ? DcRejectReason.None : DcRejectReason.NeedsTargetContext;

                // 강공은 **그 공격의 출력 데미지**를 배율한다 → RESOLVE 에 도달하는 host
                // 전용이다. 폭탄맨/캐스터에서 지금 거절되는 건 그 에셋들이 outputs 를
                // 안 가진 우연일 뿐이라, 아키타입 축을 명시해 구조적으로 막는다.
                case DcPayloadKind.HeavyStrike:
                    if (!HostProvidesTarget(host.archetype)) return DcRejectReason.NeedsTargetContext;
                    return host.hasDamageOutput
                        ? DcRejectReason.None : DcRejectReason.NeedsDamageOutput;

                // 이중 상태 거부(기존 apply preflight 미러).
                case DcPayloadKind.SelfBuffLethal:
                    return host.hasLethalTimer
                        ? DcRejectReason.DuplicateState : DcRejectReason.None;
                case DcPayloadKind.DreamCocoon:
                    return host.hasDreamCocoon
                        ? DcRejectReason.DuplicateState : DcRejectReason.None;

                // self / 오라 / 지역 계열 — host 의 공격 모델과 무관.
                case DcPayloadKind.None:
                case DcPayloadKind.SelfTileAoe:
                case DcPayloadKind.NextAttackDoubleFire:
                case DcPayloadKind.AreaBarrage:
                case DcPayloadKind.SelfBlink:
                // ultimate-leap unit 0 — self 계열이라 host 의 공격 모델과 무관하다.
                // 실제로는 보스 bake 전용(BakeNightmareMechanics)이지만 그건 **authoring 사실**
                // 이지 적용성 판정이 아니다 — 바로 위 SelfBlink 도 같은 처지로 여기 있다.
                // "카드가 아니다" 를 이 레이어에서 표현하려 들면 Unclassified(=통합 버그)와
                // 정상 거절이 섞인다.
                case DcPayloadKind.UltimateLeap:
                case DcPayloadKind.SelfWarmupBuff:
                case DcPayloadKind.PlacementAura:
                case DcPayloadKind.AllyMoveSpeedAura:
                case DcPayloadKind.SelfStatBuff:
                case DcPayloadKind.BountyMark:
                case DcPayloadKind.AreaSleep:
                // boss-mamemo unit 2 — 실드 부여. self(tileRange 0) / 반경 아군(>0) 어느 쪽이든
                // host 의 공격 모델과 무관하다. **어느 arm 이 잡느냐**(경계=self / 주기=반경)는
                // authoring 사실이라 bake 가 판정하고, 여기선 적용성만 본다 — UltimateLeap 이
                // 보스 전용인데도 여기 있는 것과 같은 이유다(위 주석).
                case DcPayloadKind.GrantShield:
                // 발사 명세(projectile-emission-pattern) — host 의 공격 모델과 직교다.
                // 대상은 패턴의 selection 이 스스로 뽑고(host 가 대상을 줄 필요 없음),
                // 진영은 host 진영의 반대로 자동 도출되며(spec README 계약 7), 데미지도
                // 패턴이 자기 값을 갖는다 → targetsEnemies / HostProvidesTarget /
                // hasDamageOutput 어느 축도 게이트가 아니다. 명세 자체의 유효성
                // (pattern/barrel null)은 bake 가 최종 판정한다(위 주석의 분업).
                case DcPayloadKind.EmitProjectilePattern:
                // elite-enemy-tier unit 5 — 분열. host 의 공격 모델과 완전히 무관하다(사망
                // 사건만 쓴다). UltimateLeap·SelfBlink 와 같은 처지로 여기 둔다: 실제로는
                // 적 SO 전용이고 **슬롯조차 만들지 않는다**(브리지 킬 드레인이 SO 를 직독) —
                // 그건 authoring 사실이지 적용성 판정이 아니다. 자식 SO 유효성은 bake 가
                // 최종 판정한다(위 주석의 분업).
                case DcPayloadKind.SplitOnDeath:
                // elite-enemy-tier unit 4 — 화염 브레스. host 의 **공격 모델**과 무관하다(대상은
                // AttackSystem 의 후보 배열에서 자기가 고르고, 진영은 host 의 targetMask 로 도출).
                // 실제로는 적 SO 전용이지만 그건 authoring 사실이지 적용성 판정이 아니다 —
                // SelfBlink·UltimateLeap 과 같은 처지(위 주석). 반각 정의역은 bake 가 판정한다.
                case DcPayloadKind.AreaBreath:
                // dreamcatcher-content-4 unit 0 — 궤도 화염구. host 의 공격 모델과 완전히
                // 무관하다: 대상을 host 가 줄 필요가 없고(구슬이 스치는 적을 스스로 만난다),
                // 데미지도 payload 가 자기 값을 가지며, 진영 축은 아예 없다(PathHit 후보 풀이
                // AttackUnitTag 하드코딩이라 아군을 때리는 경로가 존재하지 않는다).
                // 탄 SO null·값 유효성은 bake 가 최종 판정한다(위 주석의 분업).
                case DcPayloadKind.SelfOrbitProjectile:
                    return DcRejectReason.None;

                default:
                    // 새 kind 를 추가하고 여기를 잊으면 조용히 통과하는 대신 붙지
                    // 않는다(fail-closed). 전용 사유를 쓰는 이유: 예전엔 여기가
                    // NeedsTargetContext 를 반환해 경고 문구와 UI 에 **틀린 단서**가
                    // 실렸다. 배선 누락과 정상 거절은 구분돼야 한다.
                    return DcRejectReason.Unclassified;
            }
        }

        public static DcRejectReason EvaluateAttackMod(DcAttackModKind kind, in DcHostProfile host)
        {
            switch (kind)
            {
                // 통통구슬 — homing 단발과 방향탄(Directional) 둘 다 지원한다.
                // 방향탄은 pierce 예산이 0 이 되는 순간 호밍으로 전환해 튕긴다
                // (`ProjectileHitSystem` PathHit arm). Ballistic/Grenade 는 대상 개념이
                // 달라 여전히 비적용 — 착탄 셀이 발사 시점에 고정되므로 재조준할 대상이
                // 없다(attack-mod-bounce 계약 4 의 원래 제외 사유).
                case DcAttackModKind.ProjectileBounce:
                    return host.route == DcProjectileRoute.Homing
                        || host.route == DcProjectileRoute.Directional
                        ? DcRejectReason.None : DcRejectReason.NeedsHomingRoute;

                // 끝을 보는 눈 — 데미지 output 필요. facing 유닛은 레인 타게팅이
                // 우선순위를 덮어써 보너스가 inert 지만, 그 판정은 unit 5 의
                // 후속(타게팅 규칙 의존 축) — 여기서는 기존 게이트만 미러한다.
                case DcAttackModKind.FrontmostTarget:
                    return host.hasDamageOutput
                        ? DcRejectReason.None : DcRejectReason.NeedsDamageOutput;

                // dreamcatcher-content-4 unit 0 — 수면 특효. 배율을 곱할 **자기 피해**가
                // 있어야 성립한다(힐러처럼 Damage output 이 없으면 곱할 것이 없다).
                // 대상 확정(HostProvidesTarget)은 요구하지 않는다 — 판정이 피해자별이라
                // 근접 cleave/AoE 처럼 host 가 "그 공격의 대상"을 못 주는 경우에도
                // 맞은 적 각각을 보고 판정할 수 있다(HeavyStrike 와 갈리는 지점).
                case DcAttackModKind.DamageVsSleeping:
                    return host.hasDamageOutput
                        ? DcRejectReason.None : DcRejectReason.NeedsDamageOutput;

                case DcAttackModKind.None:
                    return DcRejectReason.None;

                default:
                    return DcRejectReason.Unclassified; // 배선 누락 (fail-closed)
            }
        }
    }
}
