using System.Collections.Generic;
using UnityEngine;
using Wassup.BattleCore;
using Wassup.BattleCore.Combat;
using Wassup.BattleCore.Combat.Projectile;
using Wassup.BattleCore.Trigger;
using Wassup.Data;
using Wassup.Skills;

namespace Wassup.BattleCoreUnity
{
    // battle-core-rebuild unit 7a — SO → **규칙(바인딩) 정의표**(← 옛 `BattleBridge.BakeUnitMechanics` ·
    // `BakeNightmareMechanics` · 실드 캐스트 bake). 여기가 `DcMechanic` 을 아는 마지막 자리다.
    //
    // **진영 중립 단일 bake** — 적 악몽(`nightmareMechanics`) · 방어유닛 능력(`UnitSkillAbility`) · 실드 캐스트
    // (`ShieldCastAbility` → 주기 × 실드)가 같은 함수를 지난다. 다른 것은 「적인가」 하나(감지자 표가 그걸로
    // 배치·퇴근을 닫는다).
    //
    // **침묵보다 거절**(구현 12): 조합 검증(`EffectComboRule` — 감지자 없음 · 부착 전용 payload 를 트리거에 매닮 · 떠난 자리 ·
    // 전원 × 결합 · 도발 × 가디언, unified-effect-layer unit 5 · 카드 빌더와 **같은 함수**) · 라우팅 없음 ·
    // payload 별 저작 검증(`BindPayload` — 카드 빌더와 공용)은 전부 loud skip 이다 — 슬롯만 구워지고 발화하고 아무 일도 안 일어나는 것이 옛 전투가
    // 반복해서 당한 형태다. 「없음 = -1」 센티널 3축은 `BindingDef.Default()` 가 명시로 시작한다(S4).
    //
    // ⚠ 저작 어휘는 **이름으로** 옮긴다(`CombatDefinitionBuilder` 매핑들과 같은 이유 — 두 어휘는 다른 어셈블리다).
    // `CoreTriggerEnumPinTests` 가 이름·값·매핑을 고정한다.
    public static class BindingDefinitionBuilder
    {
        /// <summary>
        /// 유닛·적 줄에 규칙 인덱스와 공격 수식자를 채운다. `CombatDefinitionBuilder.Fill` 이 **탄·패턴 표를 굳히기 전**에
        /// 부른다 — 규칙이 가리키는 탄·패턴이 같은 표에 등록돼야 한다.
        /// </summary>
        public static void Fill(MatchDefinition def, List<DefenderUnitData> units, AttackUnitData[] enemies,
                                List<ProjectileData> projectiles, List<ProjectilePatternData> patterns,
                                HazardSO[] hazards, MatchViewAssets view = null)
        {
            var rows = new List<BindingDef>(def.Bindings ?? System.Array.Empty<BindingDef>());
            for (int i = 0; i < def.Units.Length && i < units.Count; i++)
            {
                var d = units[i];
                if (d == null) continue;
                var mods = new List<AttackModDef>();
                var mine = new List<int>();
                var skill = d.GetAbility<UnitSkillAbility>();
                if (skill?.mechanics != null)
                    Bake(skill.mechanics, hostIsEnemy: false, d.name, d.aggroCapacity > 0, null,
                         projectiles, patterns, hazards, rows, mine, mods, def.Movement.SplitMaxChildren, view);
                BakeShieldCast(d, rows, mine);
                if (mine.Count > 0) def.Units[i].Bindings = mine.ToArray();
                if (mods.Count > 0) def.Units[i].Attack.Mods = mods.ToArray();
            }
            for (int i = 0; i < def.Enemies.Length && i < (enemies?.Length ?? 0); i++)
            {
                var e = enemies[i];
                if (e?.nightmareMechanics == null || e.nightmareMechanics.Length == 0) continue;
                var mods = new List<AttackModDef>();
                var mine = new List<int>();
                Bake(e.nightmareMechanics, hostIsEnemy: true, e.name, false, e,
                     projectiles, patterns, hazards, rows, mine, mods, def.Movement.SplitMaxChildren, view);
                if (mine.Count > 0) def.Enemies[i].Bindings = mine.ToArray();
                if (mods.Count > 0) def.Enemies[i].Attack.Mods = mods.ToArray();
            }
            def.Bindings = rows.ToArray();
        }

        // 실드 캐스트 능력 — 저작은 그대로, **주기 × 실드 규칙**으로 굽는다(옛 전용 상태·시스템 은퇴).
        // 첫 캐스트 = 배치 A초 뒤(누적 0 에서 A 초). 범위 = 유닛 사거리 재사용(계약 5) · **자기 포함**(셔틀엔 겹칠 상대가 없다).
        private static void BakeShieldCast(DefenderUnitData d, List<BindingDef> rows, List<int> mine)
        {
            var a = d.GetAbility<ShieldCastAbility>();
            if (a == null || a.cooldown <= 0f || a.amount <= 0f) return;
            var b = BindingDef.Default();
            b.Label = d.name + " 실드 캐스트";
            b.Trigger = TriggerKind.PeriodicTimer;
            b.Payload = TriggerPayload.GrantShield;
            b.Effect = SkillRouting.Resolve(b.Trigger, b.Payload);
            b.PeriodSeconds = a.cooldown;
            b.Magnitude = a.amount;
            b.TileRange = SkillMath.RangeToTiles(d.attackRange);
            b.ShieldFilter = (int)ToSkillShieldFilter(a.filter);
            b.ShieldIncludesSelf = true;
            b.ShieldTargetCount = Mathf.Max(1, a.targetCount);
            mine.Add(rows.Count);
            rows.Add(b);
        }

        private static void Bake(DcMechanic[] mechanics, bool hostIsEnemy, string owner, bool hostIsGuardian,
                                 AttackUnitData enemyOwner,
                                 List<ProjectileData> projectiles, List<ProjectilePatternData> patterns,
                                 HazardSO[] hazards, List<BindingDef> rows, List<int> mine, List<AttackModDef> mods,
                                 int splitCap, MatchViewAssets view = null)
        {
            for (int i = 0; i < mechanics.Length; i++)
            {
                var m = mechanics[i];
                string label = $"{owner} mechanic {i}";
                var trigger = ToCoreTrigger(m.trigger.kind);
                var payload = ToCorePayload(m.payload.kind);

                if (payload == TriggerPayload.None)
                {
                    Warn($"{label}: None 종류 — 건너뛴다.");
                    continue;
                }
                // 분열은 **의도적 무항목**(S8) — 그릇(적 줄)은 서고 항목만 건너뛴다. 실행은 7d(`OnSlain`).
                if (trigger == TriggerKind.OnDeath && payload == TriggerPayload.SplitOnDeath)
                {
                    if (m.payload.splitUnit == null) Error($"{label}: SplitOnDeath 인데 splitUnit 이 비었다 — 죽어도 안 갈라진다.");
                    else if (m.payload.magnitude < 1f) Error($"{label}: SplitOnDeath magnitude({m.payload.magnitude}) < 1 — 자식이 0기다.");
                    // 상한 = 정의표(`MovementTuningDef.SplitMaxChildren` — 저작 사고 방어선). 빌더·코어가 같은 값으로 자른다.
                    else if (m.payload.magnitude > splitCap) Error($"{label}: SplitOnDeath magnitude({m.payload.magnitude}) > {splitCap} — {splitCap}기로 잘린다.");
                    else if (enemyOwner != null && !SplitChain.Validate(enemyOwner, out string splitError)) Error($"{label}: {splitError}");
                    continue;
                }
                // unified-effect-layer unit 5 — 조합은 **검증 한 함수**(출처는 입력이 아니다). 유닛·적은 규칙을 들고 태어난다(놓인 뒤 붙지 않는다).
                if (!CheckCombo(ComboOf(in m, trigger, payload, hostIsEnemy, bindsAfterPlacement: false,
                                        hostCannotHoldAggro: !hostIsGuardian), label)) continue;
                var gate = ToCoreGate(m.trigger.gate);
                var gateSubject = ToCoreGateSubject(m.trigger.gateSubject);
                if (!TriggerValuesValid(in m, trigger, gate, gateSubject, label)) continue;
                // 강공 — **어휘 밖**(그 공격의 성질). 규칙이 아니라 공격 수식자로 접는다.
                if (payload == TriggerPayload.HeavyStrike)
                {
                    if (TryHeavyStrike(in m, trigger, gate, label, out var heavy)) mods.Add(heavy);
                    continue;
                }
                if (payload == TriggerPayload.AreaBarrage)
                {
                    // 이관(안내 유지 — `BattleBridge.cs:10290` 과 같은 뜻).
                    Warn($"{label}: AreaBarrage 는 EmitProjectilePattern 으로 이관됐다(arm 제거) — 건너뛴다. 패턴 asset 을 지정하라.");
                    continue;
                }
                // 스킬인데 라우팅이 없다.
                var effect = SkillRouting.Resolve(trigger, payload);
                if (SkillRouting.IsSkill(payload) && effect == null)
                {
                    Warn($"{label}: '{trigger} × {payload}' 조합에 라우팅이 없다 — 발화하고도 아무 일이 안 일어난다. 건너뛴다.");
                    continue;
                }
                if (effect == null)
                {
                    Warn($"{label}: '{payload}' 는 이 레이어의 규칙이 아니다(7b/7d) — 건너뛴다.");
                    continue;
                }

                var b = BindingDef.Default();
                b.Label = label;
                b.Trigger = trigger;
                b.Payload = payload;
                b.Effect = effect;
                b.Period = Mathf.Clamp(m.trigger.period, 0, ushort.MaxValue);
                b.PeriodSeconds = m.trigger.periodSeconds;
                b.Fraction = m.trigger.fraction;
                b.Gate = gate;
                b.GateSubject = gateSubject;
                b.GateValue = m.trigger.gateValue;
                b.Magnitude = m.payload.magnitude;
                b.TileRange = Mathf.Max(0, m.payload.tileRange);
                b.Duration = Mathf.Max(0f, m.payload.duration);
                // ⚠ 저작 선택자 셋은 **기본값이 진짜처럼 보이는** 함정이다(0 = 감속 · 공격력 · 없음) — 명시로 옮긴다.
                b.CcKind = (int)ToSkillCc(m.payload.ccKind);
                b.StackKind = (int)ToSkillStack(m.payload.stackKind);
                b.StatKind = (int)(TryToSkillStat(m.payload.buffStat, out var stat) ? stat : SkillStatKind.DamageMul);
                b.SlamDamage = Mathf.Max(0f, m.payload.slamDamage);
                b.SlamTileRange = Mathf.Max(0, m.payload.slamTileRange);
                b.ConeHalfAngleDeg = m.payload.coneHalfAngleDeg;
                float c = Mathf.Cos(Mathf.Deg2Rad * Mathf.Max(0f, m.payload.coneHalfAngleDeg));
                b.ConeCosSq = c * c;
                b.Origin = BindingOrigin.UnitAuthored;

                if (!BindPayload(ref b, in m, label, projectiles, patterns, hazards)) continue;

                // unit 7c — 메커닉이 선언한 연출 프리팹(옛 `BakeUnitMechanics` 의 두 갈래). 규칙이 아니라 **뷰 표**이고,
                // 규칙 줄에는 빔의 번호만 싣는다(`SkillVisual.DefIndex` — 스킬이 `HasData` 일 때만 빔을 요청한다).
                //   · 지속 피해(`AreaDot`)의 `auraPrefab` = **빔**(옛 `GetOrCreateSkillVfxIndex`). 빔은 선택이다 — 없으면 무연출.
                //   · 그 밖의 `auraPrefab` = 숙주를 따라다니는 **부착 오라**(옛 `DcAuraVisualPool.Register`, kind 무관).
                //   ⚠ 옛 bake 는 `AreaDot` 의 빔 프리팹도 오라로 **같이** 등록했다(두 갈래가 한 필드를 겸한 뒤 가드가 안 생겼다) —
                //   빔이 숙주에 기본 방향으로 박혀 떠 있게 된다. 빔 쪽만 옮긴다(7c 이식 제외).
                if (view != null && m.payload.auraPrefab != null)
                {
                    if (b.Payload == TriggerPayload.AreaDot) b.DataIndex = view.RegisterSkillVfx(m.payload.auraPrefab);
                    else view.SetBindingAura(rows.Count, m.payload.auraPrefab, m.payload.auraScale);
                }

                mine.Add(rows.Count);
                rows.Add(b);
            }
        }

        // ── 두 빌더가 같이 쓰는 검증(unified-effect-layer unit 5 — 계약 5) ─────────────
        //
        // 카드 빌더(`CardDefinitionBuilder`)와 이 빌더가 **같은 함수**를 지난다. 조합은 `EffectComboRule`(코어 · 순수),
        // 값 가드·표 참조는 아래 넷이다. 출처마다 다른 것은 **저작 인코딩**(카드 버프 % → 배율 · 카드 자리 폭발 탄 배율)뿐이고
        // 그건 각 빌더가 이 함수들 뒤에 덧씌운다 — 효과 값 정본 통일은 후속(H4 + 시트 Effects 탭).

        /// <summary>저작 메커닉 하나의 조합 입력. 탄 결합은 직접 탄(대상 탄) 또는 발사 명세 탄에서 읽는다.</summary>
        internal static EffectCombo ComboOf(in DcMechanic m, TriggerKind trigger, TriggerPayload payload, bool hostIsEnemy,
                                            bool bindsAfterPlacement, bool hostCannotHoldAggro)
        {
            var c = new EffectCombo
            {
                Trigger = trigger,
                Subject = BindingSubject.Self,
                Payload = payload,
                HostIsEnemy = hostIsEnemy,
                BindsAfterPlacement = bindsAfterPlacement,
                HostCannotHoldAggro = hostCannotHoldAggro,
            };
            ProjectileData shot = null;
            if (payload == TriggerPayload.ProjectileToTarget) shot = m.payload.projectile;
            else if (payload == TriggerPayload.EmitProjectilePattern && m.payload.pattern != null)
            {
                shot = m.payload.pattern.barrel;
                c.FanOut = shot != null && m.payload.pattern.fanOutToAllCandidates;
            }
            if (shot != null)
            {
                c.HasProjectile = true;
                c.Binding = MovementBinding.Of(CombatDefinitionBuilder.Translate(shot.flightMode).Item1);
            }
            return c;
        }

        /// <summary>조합 검증 — 거절이면 사유 셋 중 하나로 짖는다.</summary>
        internal static bool CheckCombo(in EffectCombo c, string label)
        {
            var v = EffectComboRule.Check(in c);
            if (v == ComboVerdict.Allowed) return true;
            Warn($"{label}: '{c.Trigger} × {c.Payload}'({c.Subject}) — {EffectComboRule.Describe(v)}. 건너뛴다.");
            return false;
        }

        /// <summary>트리거 저작값 가드 — 0 이면 영영 안 도는 주기·경계·N번째 · 열리지 않은 게이트.</summary>
        internal static bool TriggerValuesValid(in DcMechanic m, TriggerKind trigger, GateKind gate, GateSubject gateSubject, string label)
        {
            var t = m.trigger;
            if ((trigger == TriggerKind.AttackN || trigger == TriggerKind.OnDamagedN) && t.period <= 0) { Warn($"{label}: {trigger} period <= 0 — 건너뛴다."); return false; }
            if (trigger == TriggerKind.PeriodicTimer && t.periodSeconds <= 0f) { Warn($"{label}: PeriodicTimer periodSeconds <= 0 — 건너뛴다."); return false; }
            if (trigger == TriggerKind.HealthThreshold && t.fraction <= 0f) { Warn($"{label}: HealthThreshold fraction <= 0 — 건너뛴다."); return false; }
            if (!SkillRouting.GateComboSupported(trigger, gate, gateSubject)) { Warn($"{label}: 게이트 조합 {trigger}×{gate}×{gateSubject} 는 열리지 않았다 — 건너뛴다(S26)."); return false; }
            if (gate != GateKind.None && (t.gateValue <= 0f || t.gateValue >= 1f)) { Warn($"{label}: gateValue 가 (0,1) 밖 — 건너뛴다."); return false; }
            return true;
        }

        /// <summary>강공 — 규칙이 아니라 공격 수식자. AttackN 전용 · 배율 > 1(1 이하는 강공이 아니다 — 조용히 1 로 두지 않는다).</summary>
        internal static bool TryHeavyStrike(in DcMechanic m, TriggerKind trigger, GateKind gate, string label, out AttackModDef mod)
        {
            mod = default;
            if (trigger != TriggerKind.AttackN) { Warn($"{label}: HeavyStrike 는 AttackN 전용 — 건너뛴다."); return false; }
            if (m.payload.magnitude <= 1f) { Warn($"{label}: HeavyStrike magnitude <= 1(강공이 아님) — 건너뛴다."); return false; }
            mod = new AttackModDef
            {
                Kind = AttackModKind.HeavyStrike,
                Period = m.trigger.period,
                DamageMul = m.payload.magnitude,
                Gate = gate,
                GateValue = m.trigger.gateValue,
            };
            return true;
        }

        /// <summary>
        /// payload 별 값 가드 + 표 참조 해석(두 빌더 공용). false = loud skip. 조합(트리거 × 효과)은 여기서 보지 않는다 — `CheckCombo` 몫.
        /// </summary>
        internal static bool BindPayload(ref BindingDef b, in DcMechanic m, string label,
                                         List<ProjectileData> projectiles, List<ProjectilePatternData> patterns,
                                         HazardSO[] hazards)
        {
            var p = m.payload;
            switch (b.Payload)
            {
                case TriggerPayload.ProjectileToTarget:
                {
                    if (p.projectile == null || p.magnitude <= 0f) { Warn($"{label}: ProjectileToTarget 탄 없음 / magnitude <= 0 — 건너뛴다."); return false; }
                    var (mv, pl) = CombatDefinitionBuilder.Translate(p.projectile.flightMode);
                    // 칸 결합(타격 운석)은 효과 좌표 = 맞은 적의 자리 · 착탄 반경 = tileRange · 낙하 = duration(unit 1 — 칸 결합 갈래가 의도에서 읽는다).
                    if (MovementBinding.Of(mv) == BindingClass.Direction
                        && (p.projectile.hitThreshold <= 0f || p.projectile.speed <= 0f || p.tileRange <= 0))
                    { Warn($"{label}: 경로 스윕 탄의 굵기/속도/거리(tileRange) 중 0 이 있다 — 건너뛴다."); return false; }
                    b.DataIndex = CombatDefinitionBuilder.IndexOf(projectiles, p.projectile);
                    b.Speed = p.projectile.speed;
                    b.HitThreshold = p.projectile.hitThreshold;
                    b.VisualScale = p.projectile.visualScale;
                    b.ProjectileMovement = (int)mv;
                    b.ProjectilePayload = (int)pl;
                    return true;
                }
                case TriggerPayload.SelfOrbitProjectile:
                    if (p.projectile == null || p.magnitude <= 0f || p.duration <= 0f || p.tileRange <= 0
                        || p.projectile.speed <= 0f || p.projectile.hitThreshold <= 0f)
                    { Warn($"{label}: SelfOrbitProjectile 탄·피해·지속·반경·속도·굵기 중 빈 것이 있다 — 건너뛴다."); return false; }
                    b.DataIndex = CombatDefinitionBuilder.IndexOf(projectiles, p.projectile);
                    b.VisualScale = p.projectile.visualScale;
                    b.Speed = p.projectile.speed;
                    b.HitThreshold = p.projectile.hitThreshold;
                    b.Period = Mathf.Clamp(p.orbitCount <= 0 ? 1 : p.orbitCount, 1, 16);   // 구슬 개수(옛 슬롯 period 재사용)
                    if (b.Trigger == TriggerKind.PeriodicTimer && m.trigger.periodSeconds < p.duration) Warn($"{label}: 주기 < 지속 — 화염구가 겹쳐 쌓인다.");
                    return true;
                case TriggerPayload.AreaBreath:
                    // 판정이 부호 가드 있는 제곱 비교라 90° 에서 정의역이 잘리고 120° 는 조용히 60° 콘이 된다 — 거절.
                    if (p.coneHalfAngleDeg >= 90f) { Error($"{label}: AreaBreath 반각({p.coneHalfAngleDeg}°) >= 90 — 조용히 (180−각) 콘이 된다. 건너뛴다."); return false; }
                    if (p.coneHalfAngleDeg <= 0f) Warn($"{label}: AreaBreath 반각이 0 이하 — 정면 한 줄만 맞는다.");
                    if (p.tileRange <= 0) Warn($"{label}: AreaBreath 사거리가 0 — 같은 셀만 맞는다.");
                    if (p.magnitude <= 0f) Warn($"{label}: AreaBreath 피해가 0 이하 — 발동해도 아무 일이 없다.");
                    return true;
                case TriggerPayload.EmitProjectilePattern:
                    return BindPattern(ref b, p.pattern, p.tileRange, label, projectiles, patterns);
                case TriggerPayload.SelfTileAoe:
                case TriggerPayload.UltimateLeap:
                    // 폭발·착지 슬램이 탄 요청 하나로 표현된다 — 탄이 없으면 **피해까지** 사라진다.
                    if (p.projectile == null)
                    {
                        Warn($"{label}: {b.Payload} 에 ProjectileData 가 없어 요청이 드롭된다 — 건너뛴다. payload.projectile 을 지정하라.");
                        return false;
                    }
                    if (b.Payload == TriggerPayload.SelfTileAoe && p.magnitude <= 0f) { Warn($"{label}: SelfTileAoe magnitude <= 0 — 건너뛴다."); return false; }
                    b.DataIndex = CombatDefinitionBuilder.IndexOf(projectiles, p.projectile);
                    b.VisualScale = 0f;   // 유닛 bake 는 탄 배율을 안 실었다(0 = 뷰가 1 로 읽는다) — 카드는 빌더가 덧씌운다
                    // unit 7d — 「생존당 1회」는 **`fireCap 1`** 이다(정정 5 의 짝). 옛 전투는 `fraction ≥ 0.5` 라 둘째 경계가
                    // 음수가 되어 **우연히** 1회였다 — 값 한 칸이 0.4 가 되면 조용히 2회가 된다. ⚠ **궁극기에만** 준다 —
                    // 같은 경계 트리거를 빈사폭주·진동갑주·가호가 쓰고 그쪽은 다회 발동이 사양이다.
                    if (b.Payload == TriggerPayload.UltimateLeap) b.FireCap = 1;
                    return true;
                case TriggerPayload.SelfBlink:
                case TriggerPayload.AllyMoveSpeedAura:
                case TriggerPayload.AreaSleep:
                    // 연출용 탄(퍼프·펄스) — **선택**이다. 없으면 연출만 없다.
                    if (p.projectile != null) b.DataIndex = CombatDefinitionBuilder.IndexOf(projectiles, p.projectile);
                    if (b.Payload == TriggerPayload.AreaSleep)
                    {
                        if (p.magnitude < 1f || p.duration <= 0f) { Warn($"{label}: AreaSleep 에 인원(>=1)·수면 초(>0)가 없다 — 매 주기 no-op. 건너뛴다."); return false; }
                        if (p.tileRange <= 0) { Warn($"{label}: AreaSleep 의 tileRange 가 0 이라 host 셀만 본다 — 건너뛴다."); return false; }
                        if (b.Trigger == TriggerKind.PeriodicTimer && p.duration >= m.trigger.periodSeconds)
                            Warn($"{label}: AreaSleep duration({p.duration}) >= periodSeconds({m.trigger.periodSeconds}) — 수면이 끊기지 않아 대상이 생존 내내 고착한다.");
                    }
                    if (b.Payload == TriggerPayload.AllyMoveSpeedAura && p.duration <= m.trigger.periodSeconds)
                        Warn($"{label}: AllyMoveSpeedAura duration({p.duration}) <= periodSeconds({m.trigger.periodSeconds}) — 펄스 사이에 만료(점멸)한다.");
                    return true;
                case TriggerPayload.ApplyCcToTarget:
                    if (p.duration <= 0f) { Warn($"{label}: ApplyCcToTarget duration <= 0 — 건너뛴다."); return false; }
                    return true;
                case TriggerPayload.ApplyStackToTarget:
                    if (p.magnitude < 1f) { Warn($"{label}: ApplyStackToTarget magnitude < 1(스택 없음) — 건너뛴다."); return false; }
                    return true;
                case TriggerPayload.AreaDot:
                    // 틱 간격(0 이면 magnitude 가 DPS). 빔 프리팹은 뷰의 것(7c) — 여기선 index 를 안 싣는다(무연출).
                    b.Speed = Mathf.Max(0f, p.tickIntervalSec);
                    return true;
                case TriggerPayload.AllyStatAura:
                case TriggerPayload.OpponentStatAura:
                    if (p.buffStat == CardBuffKind.EffectiveHealth)
                    {
                        // 번역 산식이 역수(1/(1+p/100))라 오라 concrete 의 (1+p/100) 과 갈린다 — 조용히 틀린 배율보다 거절.
                        Warn($"{label}: 스탯 오라에 EffectiveHealth 는 배선되지 않았다(산식이 역수) — 건너뛴다.");
                        return false;
                    }
                    if (!TryToSkillStat(p.buffStat, out _)) { Warn($"{label}: 오라 스탯 {p.buffStat} 을 옮길 수 없다 — 건너뛴다."); return false; }
                    return true;
                case TriggerPayload.GrantShield:
                    // 트리거 × 반경 블랙리스트는 은퇴(unit 5) — concrete 가 자기(반경 0)·주변(반경 > 0)을 둘 다 받는다.
                    if (p.magnitude <= 0f) { Warn($"{label}: GrantShield 에 실드량(>0)이 없다 — 매 발동 no-op. 건너뛴다."); return false; }
                    if (p.duration > 0f) Warn($"{label}: GrantShield 의 duration({p.duration}) 은 무시된다 — 실드는 시간이 아니라 피해로만 사라진다.");
                    return true;
                case TriggerPayload.AreaTaunt:
                    // 가디언 여부는 조합 검증(`EffectComboRule` ⑦)이 본다.
                    if (p.duration <= 0f || p.tileRange <= 0) { Warn($"{label}: AreaTaunt 에 도발 초·반경이 없다 — 매 발동 no-op. 건너뛴다."); return false; }
                    return true;
                case TriggerPayload.SpawnHazard:
                    int h = hazards != null && p.hazard != null ? System.Array.IndexOf(hazards, p.hazard) : -1;
                    if (h < 0) { Warn($"{label}: SpawnHazard 의 장판이 이 판의 장판 표에 없다 — 건너뛴다(카드면 `WithCardHazards` 를 거쳤나)."); return false; }
                    b.HazardDefIndex = h;
                    return true;
                default:
                    return true;
            }
        }

        // 발사 명세 — 옛 `TryBuildPatternSlot` 의 거절을 옮겼다(규칙 경로 전용 — 평타 다연발은 따로 굽는다).
        internal static bool BindPattern(ref BindingDef b, ProjectilePatternData pattern, int tileRange, string label,
                                        List<ProjectileData> projectiles, List<ProjectilePatternData> patterns)
        {
            if (pattern == null || pattern.barrel == null) { Warn($"{label}: EmitProjectilePattern 에 탄(barrel) 있는 패턴이 필요하다 — 건너뛴다."); return false; }
            var (movement, _) = CombatDefinitionBuilder.Translate(pattern.barrel.flightMode);
            var binding = MovementBinding.Of(movement);
            // 방향 패턴의 사거리는 payload 저작값 — 0 이면 사거리 0 탄이 나가 **완전 무동작**.
            if (binding == BindingClass.Direction && tileRange <= 0) { Warn($"{label}: 방향 패턴인데 payload tileRange 가 0 — 건너뛴다."); return false; }
            if (pattern.barrel.flightMode == ProjectileFlightMode.SkyFall && pattern.telegraphSec <= 0f)
                Warn($"{label}: SkyFall 패턴의 telegraphSec 가 0 — 예고 없이 즉착탄한다.");
            // 「한 발이 반경 안 전원에게」는 범위가 없으면 **맵 전체**가 된다 · 적 조준 궤적 전용이다(칸 조준은 조준이 둘).
            if (pattern.fanOutToAllCandidates && pattern.scopeTileRange <= 0) { Warn($"{label}: fanOutToAllCandidates 인데 scopeTileRange 가 0 — 맵 전체 동시 발사가 된다. 건너뛴다."); return false; }
            // 「전원 × 대상 결합이 아닌 탄」은 조합 검증(`EffectComboRule` ⑥)이 본다 — 부르는 쪽이 `CheckCombo` 를 먼저 지난다.
            if (binding == BindingClass.Direction)
            {
                if (pattern.damage <= 0f) Warn($"{label}: 방향 패턴인데 damage 가 0 이하 — 규칙 경로는 패턴 SO 의 damage 를 그대로 쓴다.");
                if (pattern.randomizeShotsPerTrigger) Warn($"{label}: randomizeShotsPerTrigger 는 규칙 경로에서 아무 일도 안 한다 — 발마다 다르게 하려면 shots 를 벌려라.");
            }
            if (!pattern.TryToSpec(0, out _)) { Warn($"{label}: 발사 명세 계약 위반(발 수·각도·선정/궤적 짝) — 건너뛴다."); return false; }
            int idx = patterns.IndexOf(pattern);
            if (idx < 0) { patterns.Add(pattern); idx = patterns.Count - 1; }
            b.PatternDefIndex = idx;
            CombatDefinitionBuilder.IndexOf(projectiles, pattern.barrel);   // 탄 표에 등록
            return true;
        }

        // ── 저작 어휘 → 코어 어휘(이름으로) ─────────────────────────────────

        public static TriggerKind ToCoreTrigger(DcTriggerKind authored)
        {
            switch (authored)
            {
                case DcTriggerKind.None: return TriggerKind.None;
                case DcTriggerKind.AttackN: return TriggerKind.AttackN;
                case DcTriggerKind.OnDamagedN: return TriggerKind.OnDamagedN;
                case DcTriggerKind.OnDeath: return TriggerKind.OnDeath;
                case DcTriggerKind.PeriodicTimer: return TriggerKind.PeriodicTimer;
                case DcTriggerKind.HealthThreshold: return TriggerKind.HealthThreshold;
                case DcTriggerKind.OnKill: return TriggerKind.OnKill;
                case DcTriggerKind.OnShieldBreak: return TriggerKind.OnShieldBreak;
                case DcTriggerKind.OnRetire: return TriggerKind.OnRetire;
                case DcTriggerKind.OnPlace: return TriggerKind.OnPlace;
                default:
                    Error($"모르는 트리거({authored}) — 없음으로 접는다(bake 가 건너뛴다).");
                    return TriggerKind.None;
            }
        }

        public static TriggerPayload ToCorePayload(DcPayloadKind authored)
        {
            // 33 값 — 이름으로 옮긴다(`System.Enum.TryParse` 는 이름 일치다. 번호 캐스트가 아니다).
            if (System.Enum.TryParse(authored.ToString(), out TriggerPayload core)
                && System.Enum.IsDefined(typeof(TriggerPayload), core)) return core;
            Error($"모르는 페이로드({authored}) — 없음으로 접는다(bake 가 건너뛴다).");
            return TriggerPayload.None;
        }

        public static GateKind ToCoreGate(DcGateKind authored)
            => authored == DcGateKind.HpBelow ? GateKind.HpBelow : GateKind.None;

        public static GateSubject ToCoreGateSubject(DcGateSubject authored)
            => authored == DcGateSubject.EventTarget ? GateSubject.EventTarget : GateSubject.Self;

        /// <summary>저작 CC(Stun·Impulse·Sleep) → 스킬 어휘. **번호가 다르다**(스킬 쪽은 Slow·Impulse·DoT·Stun·Sleep).</summary>
        public static SkillCcKind ToSkillCc(DcCcKind authored)
        {
            switch (authored)
            {
                case DcCcKind.Impulse: return SkillCcKind.Impulse;
                case DcCcKind.Sleep: return SkillCcKind.Sleep;
                default: return SkillCcKind.Stun;   // 옛 `MapDcCc` 의 기본 = 기절
            }
        }

        /// <summary>저작 스택(Fire·Ice·Bleed·Poison) → 스킬 어휘(None 이 0 이라 번호가 하나 밀린다).</summary>
        public static SkillStackKind ToSkillStack(DcStackKind authored)
        {
            switch (authored)
            {
                case DcStackKind.Fire: return SkillStackKind.Fire;
                case DcStackKind.Ice: return SkillStackKind.Ice;
                case DcStackKind.Poison: return SkillStackKind.Poison;
                default: return SkillStackKind.Bleed;   // 옛 `MapDcStack` 의 기본 = 출혈
            }
        }

        /// <summary>저작 버프 축 → 스탯(옛 `MapDcBuff` 의 스탯 부분). `CostRate` 는 스탯이 아니다(7b 메타 의도).</summary>
        public static bool TryToSkillStat(CardBuffKind authored, out SkillStatKind stat)
        {
            switch (authored)
            {
                case CardBuffKind.AttackDamage: stat = SkillStatKind.DamageMul; return true;
                case CardBuffKind.AttackSpeed: stat = SkillStatKind.AttackSpeedMul; return true;
                case CardBuffKind.EffectiveHealth: stat = SkillStatKind.DmgTakenMul; return true;
                case CardBuffKind.MoveSpeed: stat = SkillStatKind.MoveSpeedMul; return true;
                case CardBuffKind.DamageVsCc: stat = SkillStatKind.DamageVsCcMul; return true;
                default: stat = SkillStatKind.DamageMul; return false;
            }
        }

        public static SkillShieldFilter ToSkillShieldFilter(ShieldTargetFilter authored)
        {
            switch (authored)
            {
                case ShieldTargetFilter.All: return SkillShieldFilter.Nearest;
                case ShieldTargetFilter.MinHealth: return SkillShieldFilter.MostHurt;
                default: return SkillShieldFilter.Self;
            }
        }

        /// <summary>저작 공격 수식자(카드 — 7b) → 코어 축. 앞 넷은 이름·번호가 같다(핀 테스트).</summary>
        public static AttackModKind ToCoreAttackMod(DcAttackModKind authored)
        {
            switch (authored)
            {
                case DcAttackModKind.ProjectileBounce: return AttackModKind.ProjectileBounce;
                case DcAttackModKind.FrontmostTarget: return AttackModKind.FrontmostTarget;
                case DcAttackModKind.DamageVsSleeping: return AttackModKind.DamageVsSleeping;
                default: return AttackModKind.None;
            }
        }

        private static void Warn(string msg) => Debug.LogWarning("[BindingDefinitionBuilder] " + msg);
        private static void Error(string msg) => Debug.LogError("[BindingDefinitionBuilder] " + msg);
    }
}
