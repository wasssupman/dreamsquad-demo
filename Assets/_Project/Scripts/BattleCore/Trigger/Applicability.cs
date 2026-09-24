using Wassup.Battle.Units;
using Wassup.BattleCore.Combat;
using Wassup.BattleCore.Combat.Projectile;

namespace Wassup.BattleCore.Trigger
{
    // battle-core-rebuild unit 7b — 「이 카드 규칙이 **이 숙주에서** 발동할 수 있나」의 순수 판정
    // (← 옛 `Core/Dreamcatcher/DcApplicability.cs` 291줄 + `DreamcatcherAttachEval.cs` 137줄).
    //
    // **UI preflight 와 커밋 bake 가 같은 함수다**(옛 선례 계승). 부착 커밋(`HandDeck.TryAttach`)도, 드래그 중
    // 조준 링의 색(7c — `HandDeck.WouldAttach`)도 `CardBindings.Plan` 하나를 부르고, 그 안의 host 종속 판정이
    // 전부 여기다. 두 벌이면 「붙는데 무효」·「안 붙는데 초록」이 돌아온다(통통구슬 × 머신거너의 선례).
    //
    // 범위 경계: **host 종속 조건만** 본다. 「magnitude ≤ 0」·「탄 없음」처럼 어느 숙주에서나 답이 같은 것은
    // 이 층 밖이다 — 그건 카드 bake(`CardDefinitionBuilder`)가 판 밖에서 한 번 loud 하게 거절한다.
    //
    // ⚠ 숙주의 **경로는 탄 SO 선언이 아니라 그 숙주가 실제로 타는 길**이다 — 폭탄맨의 탄은 flightMode 0(유도)
    // 인데 경로는 수류탄이다(옛 spec 계약 6). 그래서 경로를 정책(`AttackPolicy.Bomb`)에서 먼저 읽는다.

    /// <summary>숙주의 실제 공격 모델. 캐스터(`HazardCast`)는 캐스터 제거로 사라졌다(계약 9).</summary>
    public enum HostArchetype : byte { Standard = 0, FacingVolley = 1, BombThrow = 2 }

    /// <summary>숙주가 실제로 타는 발사 경로.</summary>
    public enum HostRoute : byte { None = 0, Homing = 1, Ballistic = 2, Directional = 3, Grenade = 4 }

    /// <summary>host 종속 판정의 단일 입력(plain 값). 코어 상태에서 **한 번** 읽어 접는다.</summary>
    public struct HostProfile
    {
        public HostArchetype Archetype;
        public HostRoute Route;
        public bool TargetsEnemies;
        public bool HasDamageOutput;
        public bool HasLethalTimer;
        public bool HasDreamCocoon;

        /// <summary>
        /// 코어 개체 → 프로필(← 옛 `BattleBridge.BuildHostProfile/TargetsEnemies/HasPositiveDamageOutput`).
        /// 판정 순서는 옛 것 그대로다: 정책(폭탄) → 자기 발사 명세(방향 연발) → 그 외 표준.
        /// </summary>
        public static HostProfile Of(Unit u, MatchDefinition def)
        {
            var p = new HostProfile { Archetype = HostArchetype.Standard, Route = HostRoute.None };
            if (u == null) return p;
            var a = u.Attack;
            if (a != null)
            {
                if (a.Policy == AttackPolicy.Bomb) p.Archetype = HostArchetype.BombThrow;
                // 유닛 **자기** 발사 명세(방향 연발 유닛). 카드가 단 발사 명세는 규칙의 버스트라 여기 없다 —
                // 옛 전투는 카드 패턴도 같은 버퍼에 넣어 카드 한 장이 숙주 분류를 바꿨다(이식 제외).
                else if (a.PatternSlots.Count > 0) p.Archetype = HostArchetype.FacingVolley;

                if (p.Archetype == HostArchetype.BombThrow) p.Route = HostRoute.Grenade;
                else if (a.ProjectileDefIndex >= 0 && a.ProjectileDefIndex < def.Projectiles.Length)
                    p.Route = RouteOf((MovementKind)def.Projectiles[a.ProjectileDefIndex].Movement);

                p.TargetsEnemies = (a.TargetMask & Factions.AnyEnemy) != 0;
                var outs = a.Outputs;
                for (int i = 0; outs != null && i < outs.Length; i++)
                    if (outs[i].Kind == AttackOutputKind.Damage && outs[i].Magnitude > 0f) { p.HasDamageOutput = true; break; }
            }
            var pg = u.Progressive;
            p.HasLethalTimer = pg != null && pg.LethalActive;
            p.HasDreamCocoon = pg != null && pg.CocoonActive;
            return p;
        }

        private static HostRoute RouteOf(MovementKind m)
        {
            switch (m)
            {
                case MovementKind.HomingToEntity: return HostRoute.Homing;
                case MovementKind.BallisticArcToPoint:
                case MovementKind.SkyFall: return HostRoute.Ballistic;
                case MovementKind.DirectionalLinear: return HostRoute.Directional;
                case MovementKind.GrenadeToCell: return HostRoute.Grenade;
                default: return HostRoute.None;
            }
        }
    }

    public static class Applicability
    {
        /// <summary>
        /// 숙주가 **자기 공격의 대상을 규칙에 건네주는가**. 표준·방향 연발은 RESOLVE 의 대표 대상을 주고, 폭탄맨은
        /// 대상을 스스로 골라 칸에 던질 뿐 건네지 않는다(옛 `HostProvidesTarget`).
        /// </summary>
        public static bool HostProvidesTarget(HostArchetype a)
            => a == HostArchetype.Standard || a == HostArchetype.FacingVolley;

        /// <summary>
        /// 규칙 한 줄 × 숙주 → 거절 사유(`None` = 발동한다). 트리거(게이트 축)와 payload(대상 축)가 둘 다 숙주와
        /// 얽혀 줄 전체를 본다(옛 `EvaluateMechanic`).
        /// </summary>
        public static RejectReason Evaluate(in BindingDef d, in HostProfile host)
        {
            // 게이트 주어가 사건 대상인데 숙주가 대상을 안 주면 게이트를 **평가할 수단이 없다** — 두면 게이트가
            // 없는 것처럼 무시되고 조건 없이 발동한다(사양 초과).
            if (d.Gate != GateKind.None && d.GateSubject == GateSubject.EventTarget && !HostProvidesTarget(host.Archetype))
                return RejectReason.NeedsTargetContext;

            switch (d.Payload)
            {
                // 비수 — 숙주의 대상으로 날아가고, 숙주가 대상을 못 주면 폴백 반경으로 스스로 찾는다.
                case TriggerPayload.ProjectileToTarget:
                    if (!host.TargetsEnemies) return RejectReason.NeedsEnemyTargeting;
                    return HostProvidesTarget(host.Archetype) || d.TileRange > 0
                        ? RejectReason.None : RejectReason.NeedsFallbackRange;
                // 「그 공격의 대상」에 걸리는 것 — 폴백이 없다.
                case TriggerPayload.ApplyCcToTarget:
                case TriggerPayload.ApplyStackToTarget:
                    if (!host.TargetsEnemies) return RejectReason.NeedsEnemyTargeting;
                    return HostProvidesTarget(host.Archetype) ? RejectReason.None : RejectReason.NeedsTargetContext;
                // 이중 상태 — 덮어쓰면 원래 타이머가 리셋되고 멀티 메커닉 카드가 부분 적용된다(카드 전체 거절).
                case TriggerPayload.SelfBuffLethal:
                    return host.HasLethalTimer ? RejectReason.DuplicateState : RejectReason.None;
                case TriggerPayload.DreamCocoon:
                    return host.HasDreamCocoon ? RejectReason.DuplicateState : RejectReason.None;
                // 표식 — 적 전용(적 판별·이중 표식은 `CardBindings` 가 숙주 종류로 본다).
                case TriggerPayload.BountyMark:
                    return RejectReason.None;
                // self · 오라 · 지역 · 판 밖 — 숙주의 공격 모델과 무관(옛 목록 그대로).
                case TriggerPayload.SelfTileAoe:
                case TriggerPayload.NextAttackDoubleFire:
                case TriggerPayload.SelfBlink:
                case TriggerPayload.UltimateLeap:
                case TriggerPayload.PlacementAura:
                case TriggerPayload.AllyMoveSpeedAura:
                case TriggerPayload.SelfStatBuff:
                case TriggerPayload.AreaSleep:
                case TriggerPayload.GrantShield:
                case TriggerPayload.EmitProjectilePattern:
                case TriggerPayload.AreaBreath:
                case TriggerPayload.AreaTaunt:
                case TriggerPayload.SelfOrbitProjectile:
                case TriggerPayload.SpawnHazard:
                case TriggerPayload.AllyStatAura:
                case TriggerPayload.OpponentStatAura:
                case TriggerPayload.GainCost:
                case TriggerPayload.ReduceSkillCooldown:
                case TriggerPayload.AreaApplyStack:
                case TriggerPayload.AreaCc:
                case TriggerPayload.AreaDot:
                    return RejectReason.None;
                default:
                    // 배선 누락은 정상 거절과 섞지 않는다 — 전용 사유(fail-closed).
                    return RejectReason.Unclassified;
            }
        }

        /// <summary>공격 수식자 × 숙주(옛 `EvaluateAttackMod` + 강공의 payload 판정).</summary>
        public static RejectReason EvaluateAttackMod(in AttackModDef m, in HostProfile host)
        {
            if (m.Gate != GateKind.None && !HostProvidesTarget(host.Archetype)) return RejectReason.NeedsTargetContext;
            switch (m.Kind)
            {
                // 통통구슬 — 재조준 가능한 경로(유도 · 직선)만. 포물선·수류탄은 착탄 칸이 발사 때 고정된다.
                case AttackModKind.ProjectileBounce:
                    return host.Route == HostRoute.Homing || host.Route == HostRoute.Directional
                        ? RejectReason.None : RejectReason.NeedsHomingRoute;
                case AttackModKind.FrontmostTarget:
                case AttackModKind.DamageVsSleeping:
                    return host.HasDamageOutput ? RejectReason.None : RejectReason.NeedsDamageOutput;
                // 강공 — **그 공격의 출력 피해**를 곱한다. RESOLVE 에 도달하는 숙주 전용.
                case AttackModKind.HeavyStrike:
                    if (!HostProvidesTarget(host.Archetype)) return RejectReason.NeedsTargetContext;
                    return host.HasDamageOutput ? RejectReason.None : RejectReason.NeedsDamageOutput;
                default:
                    return RejectReason.Unclassified;
            }
        }

        /// <summary>
        /// 부착 제한(정적 술어). 옛 `DreamcatcherAttachEval.MeetsAttachRequirement` — 무효 저작은 어디에도 안 붙는다.
        /// </summary>
        public static bool MeetsRequirement(in AttachRequirementDef r, in UnitDef host)
        {
            switch (r.Kind)
            {
                case AttachRequirementKind.None: return true;
                case AttachRequirementKind.Class: return !r.Invalid && host.Role == r.Role;
                case AttachRequirementKind.UnitId:
                    return !r.Invalid && !string.IsNullOrEmpty(r.UnitId)
                           && string.Equals(host.Id, r.UnitId, System.StringComparison.Ordinal);
                default: return false;
            }
        }
    }
}
