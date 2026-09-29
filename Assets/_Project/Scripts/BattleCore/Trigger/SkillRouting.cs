using Wassup.Skills;
using Wassup.Skills.Concrete;

namespace Wassup.BattleCore.Trigger
{
    // battle-core-rebuild unit 7a — (트리거 × 페이로드) → concrete **정적 라우팅 표**
    // (← 옛 `Core/Dreamcatcher/DcSkillRouting.cs` 111줄 + `Data/Dreamcatcher/SkillPayloadPolicy.cs`).
    //
    // ⚠ **표는 은퇴하지 않는다**(정정 8 · C3). 은퇴한 것은 「Burst 용 int skillId 를 unmanaged 슬롯에
    // 굽는 인코딩」뿐이고, `BindingDef` 는 concrete **참조**를 직접 든다. 표가 남아야 하는 이유는
    // **부착 범위 프리뷰가 드래그 중에 형을 묻기 때문**이다 — 그 시점엔 사건도 자리도 없다.
    // bake(`MatchDefinitionBuilder`)와 형 카탈로그(`RangeCatalog`)가 **이 함수 하나**를 부른다 —
    // 두 벌이면 특수 케이스(자리의 주인이 다른 폭발 셋 등)가 한쪽에만 들어가 같은 저작이
    // 소비처에 따라 다른 스킬로 간다(옛 `DcApplicability` 가 세운 「preflight 와 bake 는 한 함수」).
    //
    // 모양은 옛 것 그대로다: 트리거별 분기(`OnKill`·`OnDeath`·`OnDamagedN`·`OnShieldBreak`·`OnRetire`·
    // `None`·`HealthThreshold`) → 폴백 `ForPayload`.
    public static class SkillRouting
    {
        /// <summary>「스킬 아님」 — `SkillRegistry.NotRouted` 와 같은 값.</summary>
        public const int NotRouted = SkillRegistry.NotRouted;

        // concrete 33 — **무상태**라 한 벌을 판·스레드 사이에 공유한다(계약 5 of skill-layer).
        // 옛 레지스트리는 34 였고 `CastHazardSkill`(28)이 캐스터 제거로 빠졌다(계약 9).
        private static readonly SkillRegistry s_registry = BuildRegistry();

        public static SkillRegistry Registry => s_registry;

        private static SkillRegistry BuildRegistry()
        {
            var r = new SkillRegistry();
            r.Register(new AreaSleepSkill());
            r.Register(new AllySpeedAuraSkill());
            r.Register(new GrantShieldSkill());
            r.Register(new SelfAreaBlastSkill());
            r.Register(new BlinkToClusterSkill());
            r.Register(new UltimateLeapSkill());
            r.Register(new EmitPatternSkill());
            r.Register(new AreaTauntSkill());
            r.Register(new AllyStatAuraSkill());
            r.Register(new OpponentStatAuraSkill());
            r.Register(new GainCostSkill());
            r.Register(new ReduceSkillCooldownSkill());
            r.Register(new AreaStackSkill());
            r.Register(new AreaCcSkill());
            r.Register(new AreaDotSkill());
            r.Register(new TargetCcSkill());
            r.Register(new TargetStackSkill());
            r.Register(new SelfStatBuffSkill());
            r.Register(new TargetProjectileSkill());
            r.Register(new DeathSiteBlastSkill());
            r.Register(new DeathSiteHazardSkill());
            r.Register(new ThresholdSelfBuffSkill());
            r.Register(new GrantSelfChargeSkill());
            r.Register(new OrbitProjectileSkill());
            r.Register(new SelfBuffLethalSkill());
            r.Register(new DreamCocoonSkill());
            r.Register(new BountyMarkSkill());
            r.Register(new TileStatBurstSkill());
            r.Register(new AllyBuffFieldSkill());
            r.Register(new PullFieldSkill());
            r.Register(new PortalSkill());
            r.Register(new TileMeteorSkill());
            r.Register(new ConeBreathSkill());
            return r;
        }

        /// <summary>그 조합의 concrete. 없으면 null — 호출부가 loud 하게 거절한다.</summary>
        public static ISkill Resolve(TriggerKind trigger, EffectKind payload)
        {
            int id = SkillIdFor(trigger, payload);
            return id != NotRouted && s_registry.TryGet(id, out var skill) ? skill : null;
        }

        public static int SkillIdFor(TriggerKind trigger, EffectKind kind)
        {
            if (trigger == TriggerKind.OnKill)
            {
                // 시체 폭발 — **죽인 적의 자리**에서 터진다. 누구의 자리인가는 감지자가 정한다.
                if (kind == EffectKind.SelfTileAoe) return DeathSiteBlastSkill.Id;
                if (kind == EffectKind.SpawnHazard) return DeathSiteHazardSkill.Id;
            }
            // 작별 선물 — 같은 concrete, **죽은 나의 자리**. `ForPayload(SelfTileAoe)` 로 가면 안 된다 —
            // 그건 살아 있는 시전자 발밑을 묻는 `SelfAreaBlastSkill` 이고 드레인 시점엔 시전자가 없다.
            if (trigger == TriggerKind.OnDeath && kind == EffectKind.SelfTileAoe)
                return DeathSiteBlastSkill.Id;
            // 피격 N회 — 자기 자리 폭발은 **살아 있는 시전자 발밑**이다.
            // ⚠ `NextAttackDoubleFire` 는 여기가 아니라 폴백 표다 — 여기 두면 `OnPlace × 충전` 이
            // 라우팅 0 을 받아 조용히 죽는다(옛 unit 8 의 PlayMode 가 잡은 침묵).
            if (trigger == TriggerKind.OnDamagedN && kind == EffectKind.SelfTileAoe)
                return SelfAreaBlastSkill.Id;
            // 실드 파열 — 피격 N회와 같은 실행기. `AreaSleep` 은 「재우자마자 내가 깨울 자리」를
            // 뺀다(S9) — 그 규칙은 concrete 가 갖고, 재우는 **수**는 그대로다.
            if (trigger == TriggerKind.OnShieldBreak)
            {
                if (kind == EffectKind.SelfTileAoe) return SelfAreaBlastSkill.Id;
                if (kind == EffectKind.AreaSleep) return AreaSleepSkill.Id;
            }
            // 퇴근 운석 — 죽은 자리 폭발과 같은 규칙, 자리의 주인은 **비워진 칸**(몸 0).
            if (trigger == TriggerKind.OnRetire && kind == EffectKind.SelfTileAoe)
                return DeathSiteBlastSkill.Id;
            // 부착되는 순간(트리거 없음) — 감지자가 아니라 부착 지점이 발화시킨다(7b).
            if (trigger == TriggerKind.None)
            {
                if (kind == EffectKind.SelfBuffLethal) return SelfBuffLethalSkill.Id;
                if (kind == EffectKind.DreamCocoon) return DreamCocoonSkill.Id;
                if (kind == EffectKind.BountyMark) return BountyMarkSkill.Id;
            }
            // 경계에서 켜진 자기 버프는 **출처가 다르다**(「빈사에서 켜졌다」).
            if (trigger == TriggerKind.HealthThreshold && kind == EffectKind.SelfStatBuff)
                return ThresholdSelfBuffSkill.Id;
            return ForPayload(kind);
        }

        /// <summary>트리거 무관 표. concrete 가 없는 payload 는 `NotRouted`.</summary>
        public static int ForPayload(EffectKind kind)
        {
            switch (kind)
            {
                case EffectKind.AreaSleep: return AreaSleepSkill.Id;
                case EffectKind.AllyMoveSpeedAura: return AllySpeedAuraSkill.Id;
                case EffectKind.GrantShield: return GrantShieldSkill.Id;
                case EffectKind.SelfTileAoe: return SelfAreaBlastSkill.Id;
                case EffectKind.SelfBlink: return BlinkToClusterSkill.Id;
                case EffectKind.UltimateLeap: return UltimateLeapSkill.Id;
                case EffectKind.EmitProjectilePattern: return EmitPatternSkill.Id;
                case EffectKind.AreaTaunt: return AreaTauntSkill.Id;
                case EffectKind.AreaBreath: return ConeBreathSkill.Id;
                // 충전 부여는 **트리거를 모른다** — 무엇이 불렀든 「다음 공격」은 같은 일이다.
                case EffectKind.NextAttackDoubleFire: return GrantSelfChargeSkill.Id;
                // 장판도 실려 온 자리에 깔린다 — OnKill 블록에만 두면 나머지 조합이 조용히 죽는다.
                case EffectKind.SpawnHazard: return DeathSiteHazardSkill.Id;
                case EffectKind.AllyStatAura: return AllyStatAuraSkill.Id;
                case EffectKind.OpponentStatAura: return OpponentStatAuraSkill.Id;
                case EffectKind.GainCost: return GainCostSkill.Id;
                case EffectKind.ReduceSkillCooldown: return ReduceSkillCooldownSkill.Id;
                case EffectKind.AreaApplyStack: return AreaStackSkill.Id;
                case EffectKind.AreaCc: return AreaCcSkill.Id;
                case EffectKind.AreaDot: return AreaDotSkill.Id;
                case EffectKind.ApplyCcToTarget: return TargetCcSkill.Id;
                case EffectKind.ApplyStackToTarget: return TargetStackSkill.Id;
                case EffectKind.SelfStatBuff: return SelfStatBuffSkill.Id;
                case EffectKind.ProjectileToTarget: return TargetProjectileSkill.Id;
                case EffectKind.SelfOrbitProjectile: return OrbitProjectileSkill.Id;
                default: return NotRouted;
            }
        }

        /// <summary>
        /// 「이 payload 는 스킬인가」의 단일 정본(← `SkillPayloadPolicy.IsSkill`). **스킬인데 라우팅이
        /// 없으면 bake 가 거절하고 짖는다** — `skillId 0` 의 뜻이 이전 도중 「arm 이 처리」에서
        /// 「아무도 처리 안 함」으로 뒤집혀 `OnPlace × 충전` 이 조용히 죽어 있었다.
        /// 스킬이 아닌 것은 각자 이유가 다르다 — 뭉뚱그리지 말 것.
        /// </summary>
        public static bool IsSkill(EffectKind kind)
        {
            switch (kind)
            {
                case EffectKind.None: return false;                    // 센티넬
                case EffectKind.PlacementAura: return false;           // 발동 규칙(시제) — 7b 가 `Any` 바인딩 둘로 편다
                case EffectKind.HeavyStrike: return false;             // 그 공격의 성질(자기참조) — `AttackMod`
                case EffectKind.SplitOnDeath: return false;            // 배선이 다른 길(7d)
                case EffectKind.RecallAttachedToFront: return false;   // 손패 동작
                case EffectKind.AreaBarrage: return false;             // 발사 명세로 이관
                case EffectKind.SelfWarmupBuff: return false;          // 죽은 값
                default: return !IsActiveCast(kind) && !IsAlwaysOn(kind);  // 액티브 시전 — 카드 빌더가 실행자를 id 로 고른다 · 상시 효과 — 빌더가 펴는 코어 모양
            }
        }

        /// <summary>skill-data-table unit 4 — 액티브 시전 효과(`EffectKind.ActiveMeteor` ~ `ActivePortal`). 시전(`TriggerKind.Cast`)과만 짝이다.</summary>
        public static bool IsActiveCast(EffectKind kind)
            => kind >= EffectKind.ActiveMeteor && kind <= EffectKind.ActivePortal;

        /// <summary>
        /// skill-data-table unit 8 — **상시 효과**(`FactionStatBuff` ~ `DamageVsSleeping`). 트리거 없음(보유 시작 순간부터 계속)과만 짝이다.
        /// 라우팅 표 밖 — 빌더가 진영 버프 줄(「남의 배치 × 자기 스탯 버프」) · 공격 수식자로 편다(계약 11).
        /// </summary>
        public static bool IsAlwaysOn(EffectKind kind)
            => kind >= EffectKind.FactionStatBuff && kind <= EffectKind.DamageVsSleeping;

        /// <summary>
        /// 부착 즉시(`trigger == None`) **에서만** 유효한 payload. ⚠ 면제가 아니라 **거절 사유**다 —
        /// 트리거에 매달면 라우팅이 없어 조용히 죽는다(옛 unit 8 리뷰 H-2).
        /// </summary>
        public static bool OnlyValidWithNoTrigger(EffectKind kind)
            => kind == EffectKind.SelfBuffLethal
            || kind == EffectKind.DreamCocoon
            || kind == EffectKind.BountyMark;

        /// <summary>
        /// 이 조합을 **잡는 감지자가 있나**(← `DcTrigger.HasDetector`). `OnPlace`·`OnRetire` 는 적에게
        /// 사건 자체가 없다(적은 배치·퇴근되지 않는다). fail-closed — 감지자 없는 바인딩을 붙이면
        /// 발화 없는 침묵이 된다.
        /// </summary>
        public static bool HasDetector(TriggerKind kind, bool hostIsEnemy)
        {
            switch (kind)
            {
                case TriggerKind.PeriodicTimer:
                case TriggerKind.HealthThreshold:
                case TriggerKind.AttackN:
                case TriggerKind.OnDamagedN:
                case TriggerKind.OnKill:
                case TriggerKind.OnShieldBreak:
                case TriggerKind.OnDeath:
                    return true;
                case TriggerKind.OnPlace:
                case TriggerKind.OnRetire:
                    return !hostIsEnemy;
                default:
                    return false;   // None 과 미래의 새 종류 — 배선 전엔 닫아 둔다
            }
        }

        /// <summary>
        /// 게이트 배선 표(← `DcTrigger.GateComboSupported`). v1 = 궁지폭발(`OnDamagedN × Self`) ·
        /// 처형타(`AttackN × EventTarget`) 둘뿐이고 나머지는 loud 거절(S26 — 보류).
        /// </summary>
        public static bool GateComboSupported(TriggerKind trigger, GateKind gate, GateSubject subject)
        {
            if (gate == GateKind.None) return true;
            if (gate != GateKind.HpBelow) return false;
            if (trigger == TriggerKind.OnDamagedN && subject == GateSubject.Self) return true;
            if (trigger == TriggerKind.AttackN && subject == GateSubject.EventTarget) return true;
            return false;
        }

        /// <summary>
        /// 게이트 판정(← `DcTrigger.GatePass`). `HpBelow` 는 `&lt;=`(정확히 경계면 통과).
        /// 판정은 **현재 체력/현재 최대 체력**이다(경계 트리거의 스폰 스냅샷과 다르다).
        /// </summary>
        public static bool GatePass(GateKind gate, float gateValue, float hp, float maxHp)
        {
            switch (gate)
            {
                case GateKind.None: return true;
                case GateKind.HpBelow:
                    if (gateValue <= 0f || maxHp <= 0f) return false;   // 무값 카드·미베이크 가드
                    return hp <= maxHp * gateValue;
                default: return false;
            }
        }
    }
}
