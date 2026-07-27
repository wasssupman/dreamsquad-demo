using Wassup.Data;

namespace Wassup.Core
{
    // dreamcatcher-attack-decoupling unit 0 — "이 payload/mod 가 이 host 에서
    // 발동할 수 있는가"의 순수 판정. 지금 이 지식은 세 곳에 손으로 미러링돼
    // 있고(DreamcatcherAttachEval.WouldApply / ApplyDreamcatcherCardToUnit 의
    // 자체 preflight 체인 / BattleBridge 의 적 베이크 가드), 그 부채가 곧 이
    // spec 이 고치려는 병의 원인이다. unit 1 이 소비처를 여기로 수렴시킨다.
    //
    // 범위 경계: host **종속** 조건만 본다. magnitude<=0 · projectile==null ·
    // duration<=0 같은 **카드 데이터 검증**은 어느 host 에서든 결과가 같으므로
    // 이 계층 밖이다(기존 DreamcatcherAttachEval.cs 의 같은 판단을 잇는다).
    //
    // ECS 무참조: 입력은 브리지가 채운 plain 값(DcHostProfile)이고 여기서
    // Entity/EntityManager 를 모른다. DreamcatcherAttachEval 과 같은 자리.

    // host 의 실제 공격 모델. SO 선언이 아니라 런타임 컴포넌트로 판별한다
    // (BombLauncherState → BombThrow / HazardCastAbility → HazardCast /
    // DeployedFacing+VolleyFireState → FacingVolley / 그 외 Standard).
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
        NeedsHomingRoute,    // homing 단발 경로에만 주입되는 mod
        NeedsTargetContext,  // "그 공격의 대상"이 확정되지 않는 host
        DuplicateState,      // 이미 같은 상태 컴포넌트를 갖고 있다
    }

    public static class DcApplicability
    {
        // 이 host 에 해당 트리거의 사건 지점이 존재하는가.
        // ⚠ 잠금/해제 축: BombThrow 는 unit 3, HazardCast 는 unit 4 가 사건
        // 지점을 만들면서 이 함수의 해당 줄을 true 로 뒤집는다. 그 전까지
        // AttackN 카드는 두 아키타입에서 부착 거절이다(spec 계약 4).
        public static bool HasEventPoint(DcTriggerKind trigger, DcHostArchetype archetype)
        {
            switch (trigger)
            {
                case DcTriggerKind.AttackN:
                    // RESOLVE 에 도달하는 host 만. 폭탄맨은 early-continue,
                    // 해저드 캐스터는 attackRange 0 이라 bestTarget 이 없다.
                    return archetype == DcHostArchetype.Standard
                        || archetype == DcHostArchetype.FacingVolley;
                // 나머지 트리거는 공격 경로와 무관한 사건(피격/사망/킬/주기/
                // 임계/실드파열)이라 host 아키타입을 가리지 않는다.
                case DcTriggerKind.None:
                case DcTriggerKind.OnDamagedN:
                case DcTriggerKind.OnDeath:
                case DcTriggerKind.PeriodicTimer:
                case DcTriggerKind.HealthThreshold:
                case DcTriggerKind.OnKill:
                case DcTriggerKind.OnShieldBreak:
                    return true;
                default:
                    return false; // fail-closed
            }
        }

        public static DcRejectReason EvaluateMechanic(DcPayloadKind payload,
            DcTriggerKind trigger, in DcHostProfile host)
        {
            if (!HasEventPoint(trigger, host.archetype)) return DcRejectReason.NoEventPoint;

            switch (payload)
            {
                // 비수 — 니들은 host 의 대상으로 날아가고(host 우선), host 가
                // 대상을 못 고르면 자체 탐색한다. 어느 쪽이든 대상은 적이어야
                // 하므로 아군을 겨누는 host(힐러)에서는 성립하지 않는다.
                case DcPayloadKind.ProjectileToTarget:
                    return host.targetsEnemies
                        ? DcRejectReason.None : DcRejectReason.NeedsEnemyTargeting;

                // *그 공격의 대상*에 걸리는 페이로드 — 자체 탐색 폴백을 주지
                // 않는다(spec 계약 9). 대상이 확정되지 않는 host 에선 영구 거절.
                case DcPayloadKind.ApplyCcToTarget:
                case DcPayloadKind.ApplyStackToTarget:
                    if (!host.targetsEnemies) return DcRejectReason.NeedsEnemyTargeting;
                    return host.archetype == DcHostArchetype.Standard
                        || host.archetype == DcHostArchetype.FacingVolley
                        ? DcRejectReason.None : DcRejectReason.NeedsTargetContext;

                // 강공은 그 공격의 출력 데미지를 배율한다 — 데미지 output 필요.
                case DcPayloadKind.HeavyStrike:
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
                case DcPayloadKind.SelfWarmupBuff:
                case DcPayloadKind.PlacementAura:
                case DcPayloadKind.AllyMoveSpeedAura:
                case DcPayloadKind.SelfStatBuff:
                case DcPayloadKind.BountyMark:
                case DcPayloadKind.AreaSleep:
                    return DcRejectReason.None;

                default:
                    // 새 kind 를 추가하고 여기를 잊으면 조용히 통과하는 대신
                    // 붙지 않는다(fail-closed). total 테스트가 먼저 잡는다.
                    return DcRejectReason.NeedsTargetContext;
            }
        }

        public static DcRejectReason EvaluateAttackMod(DcAttackModKind kind, in DcHostProfile host)
        {
            switch (kind)
            {
                // 통통구슬 — 주입이 homing 단발 분기에만, 재타겟 후처리도
                // SingleSplash arm 에만 있다. 방향탄 개통은 별도 spec.
                case DcAttackModKind.ProjectileBounce:
                    return host.route == DcProjectileRoute.Homing
                        ? DcRejectReason.None : DcRejectReason.NeedsHomingRoute;

                // 끝을 보는 눈 — 데미지 output 필요. facing 유닛은 레인 타게팅이
                // 우선순위를 덮어써 보너스가 inert 지만, 그 판정은 unit 5 의
                // 후속(타게팅 규칙 의존 축) — 여기서는 기존 게이트만 미러한다.
                case DcAttackModKind.FrontmostTarget:
                    return host.hasDamageOutput
                        ? DcRejectReason.None : DcRejectReason.NeedsDamageOutput;

                case DcAttackModKind.None:
                    return DcRejectReason.None;

                default:
                    return DcRejectReason.NeedsHomingRoute; // fail-closed
            }
        }
    }
}
