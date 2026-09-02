using Wassup.Data;
using Wassup.Skills;
using Wassup.Skills.Concrete;

namespace Wassup.Core
{
    // dreamcatcher-attach-range-preview unit 1 — 트리거×페이로드 → 스킬 concrete 라우팅의 **정본**.
    //
    // 원래 `BattleBridge.SkillIdForMechanic` 이었다(skill-layer-migration). 부착 프리뷰가 「이 카드는
    // 어떤 도형으로 작용하나」를 묻기 위해 같은 표가 필요해졌고, 표를 두 벌 두면 특수 케이스(자리의
    // 주인이 다른 폭발 셋 등)가 한쪽에만 추가되어 같은 저작이 소비처에 따라 다른 스킬로 간다.
    // 그래서 bake(브리지)와 카탈로그(`DcRangeCatalog`)가 **이 함수 하나**를 부른다 —
    // `DcApplicability` 가 세운 「UI preflight 와 bake 가 한 함수」 선례.
    //
    // 자리가 `Wassup.Runtime` 의 Core 인 이유: `Wassup.Skills` 는 별개 어셈블리이고 noEngineReferences 라
    // `Wassup.Data` enum 을 볼 수 없다. Runtime 은 Skills 를 참조하므로 concrete `Id` 상수를 읽을 수 있다.
    // ECS 무참조 — 입력은 enum 둘, 출력은 int 다.
    public static class DcSkillRouting
    {
        public static int SkillIdFor(DcTriggerKind trigger, DcPayloadKind kind)
        {
            if (trigger == DcTriggerKind.OnKill)
            {
                if (kind == DcPayloadKind.SelfTileAoe) return DeathSiteBlastSkill.Id;
                if (kind == DcPayloadKind.SpawnHazard) return DeathSiteHazardSkill.Id;
            }
            // unit 3d″ — **작별 선물.** `OnKill × SelfTileAoe`(시체폭발)와 같은 concrete 를
            // 쓴다. 「실려 온 자리에서 터진다」가 같은 규칙이고, **누구의 자리인가**는
            // 스킬이 아니라 감지자가 정하기 때문이다(죽인 자리 ↔ 죽은 자리).
            // ⚠ `ForPayload(SelfTileAoe)` 로 가면 **안 된다** — 그건 살아 있는
            // 시전자 발밑을 묻는 `SelfAreaBlastSkill` 이고, 드레인 시점엔 시전자가 없다.
            if (trigger == DcTriggerKind.OnDeath && kind == DcPayloadKind.SelfTileAoe)
                return DeathSiteBlastSkill.Id;
            // unit 3d‴ — 피격 N회. **자기 자리 폭발**은 살아 있는 시전자 발밑이라
            // `SelfAreaBlastSkill` 이 맞다(작별 선물과 반대 축이다).
            if (trigger == DcTriggerKind.OnDamagedN)
            {
                if (kind == DcPayloadKind.SelfTileAoe) return SelfAreaBlastSkill.Id;
                // ⚠ `NextAttackDoubleFire` 는 여기가 아니라 **트리거 무관 스위치**에 있다.
                // 여기 두면 `OnPlace × 충전` 이 라우팅을 못 찾아 0(=스킬 아님)이 되는데,
                // 그 payload 의 arm 은 이미 철거돼서 **아무 일도 안 하고 아무 말도 안 한다.**
                // 그 침묵을 PlayMode 가 잡았다(unit 8).
            }
            // unit 3e — 실드 파열. 피격 N회와 **같은 실행기**를 쓰므로 모양이 같다.
            // ⚠ `AreaSleep` 은 concrete 가 「재우자마자 내가 깨울 자리」를 뺀다 — 레거시
            // 파열엔 없던 규칙이다. 재우는 **수**는 그대로고(뺄 만큼 더 뽑는다) 달라지는
            // 것은 «누가» 자느냐다. 자장가의 계약이 그쪽이 옳다고 보므로 concrete 를
            // 둘로 가르지 않고 이 차이를 여기 적어 둔다.
            if (trigger == DcTriggerKind.OnShieldBreak)
            {
                if (kind == DcPayloadKind.SelfTileAoe) return SelfAreaBlastSkill.Id;
                if (kind == DcPayloadKind.AreaSleep) return AreaSleepSkill.Id;
            }
            // unit 3e — 퇴근 운석. 죽은 자리 폭발과 **같은 규칙**이다(실려 온 자리에서
            // 터진다). 다른 것은 값뿐 — 자리의 주인이 「비워진 칸」이고 예고 시간이 있다.
            if (trigger == DcTriggerKind.OnRetire && kind == DcPayloadKind.SelfTileAoe)
                return DeathSiteBlastSkill.Id;
            // unit 4a — **부착되는 순간** 발동하는 것들(트리거 없음). 이 조합은 감지자가
            // 아니라 **부착 지점**이 발화시킨다.
            if (trigger == DcTriggerKind.None)
            {
                if (kind == DcPayloadKind.SelfBuffLethal) return SelfBuffLethalSkill.Id;
                if (kind == DcPayloadKind.DreamCocoon) return DreamCocoonSkill.Id;
                if (kind == DcPayloadKind.BountyMark) return BountyMarkSkill.Id;
            }
            // 경계에서 켜진 자기 버프는 **출처가 다르다**(「빈사에서 켜졌다」).
            if (trigger == DcTriggerKind.HealthThreshold && kind == DcPayloadKind.SelfStatBuff)
                return ThresholdSelfBuffSkill.Id;
            return ForPayload(kind);
        }

        // 트리거 무관 표. 여전히 concrete 가 없는 payload 는 `SkillRegistry.NotRouted`(0) —
        // 그건 「스킬이 아니다」이고, 스킬인데 0 인 조합은 bake 게이트가 거절한다.
        public static int ForPayload(DcPayloadKind kind)
        {
            switch (kind)
            {
                case DcPayloadKind.AreaSleep: return AreaSleepSkill.Id;
                case DcPayloadKind.AllyMoveSpeedAura: return AllySpeedAuraSkill.Id;
                case DcPayloadKind.GrantShield: return GrantShieldSkill.Id;
                case DcPayloadKind.SelfTileAoe: return SelfAreaBlastSkill.Id;
                case DcPayloadKind.SelfBlink: return BlinkToClusterSkill.Id;
                case DcPayloadKind.UltimateLeap: return UltimateLeapSkill.Id;
                case DcPayloadKind.EmitProjectilePattern: return EmitPatternSkill.Id;
                case DcPayloadKind.AreaTaunt: return AreaTauntSkill.Id;
                case DcPayloadKind.AreaBreath: return ConeBreathSkill.Id;
                // 충전 부여는 **트리거를 모른다** — 「다음 공격이 세진다」는 무엇이 그것을
                // 불렀든 같은 일이다. 트리거별 블록에 두면 그 트리거 밖 조합이 조용히 죽는다.
                case DcPayloadKind.NextAttackDoubleFire: return GrantSelfChargeSkill.Id;
                // 장판도 **실려 온 자리**에 깔린다 — 「누구의 자리인가」는 감지자가 정한다
                // (`DeathSiteBlastSkill` 과 같은 논리). `SelfTileAoe` 가 concrete 둘로 갈린
                // 것은 「죽은 자리 ↔ 내 발밑」이 **다른 규칙**이어서인데, 장판은 그 갈림이
                // 없다. OnKill 블록에만 두면 나머지 조합이 조용히 죽는다.
                case DcPayloadKind.SpawnHazard: return DeathSiteHazardSkill.Id;
                case DcPayloadKind.AllyStatAura: return AllyStatAuraSkill.Id;
                case DcPayloadKind.OpponentStatAura: return OpponentStatAuraSkill.Id;
                case DcPayloadKind.GainCost: return GainCostSkill.Id;
                case DcPayloadKind.ReduceSkillCooldown: return ReduceSkillCooldownSkill.Id;
                case DcPayloadKind.AreaApplyStack: return AreaStackSkill.Id;
                case DcPayloadKind.AreaCc: return AreaCcSkill.Id;
                case DcPayloadKind.AreaDot: return AreaDotSkill.Id;
                case DcPayloadKind.ApplyCcToTarget: return TargetCcSkill.Id;
                case DcPayloadKind.ApplyStackToTarget: return TargetStackSkill.Id;
                case DcPayloadKind.SelfStatBuff: return SelfStatBuffSkill.Id;
                case DcPayloadKind.ProjectileToTarget: return TargetProjectileSkill.Id;
                case DcPayloadKind.SelfOrbitProjectile: return OrbitProjectileSkill.Id;
                default: return SkillRegistry.NotRouted;
            }
        }
    }
}
