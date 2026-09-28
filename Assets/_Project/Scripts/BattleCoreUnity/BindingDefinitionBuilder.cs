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
    // `BakeNightmareMechanics` · 실드 캐스트 bake).
    //
    // skill-data-table unit 4 — 방어유닛 · 적은 **소유 줄(`bindings` → 효과 에셋)** 만 굽는다(`BindingSpecBuilder` — 카드와 같은 한 경로).
    // 옛 저장처(적 `nightmareMechanics` · 방어유닛 `UnitSkillAbility` · 코드가 굽던 `ShieldCastAbility`)는 이전됐다. 이 파일에 남은 것은
    // 두 빌더가 같이 쓰는 잎 함수(조합 · 트리거 가드 · 강공 · 종류별 값 가드 · 연출 · 줄 싣기)와 번호가 다른 어휘의 번역이다.
    // 잎 함수는 아직 옛 메커닉 모양(`DcMechanic` — `BindingSpecView` 가 소유 줄에서 비춘 것)을 받는다.
    //
    // **침묵보다 거절**(구현 12): 조합 검증(`EffectComboRule` — 감지자 없음 · 부착 전용 payload 를 트리거에 매닮 · 떠난 자리 ·
    // 전원 × 결합 · 도발 × 가디언, unified-effect-layer unit 5 · 카드 빌더와 **같은 함수**) · 라우팅 없음 ·
    // payload 별 저작 검증(`BindPayload` — 카드 빌더와 공용)은 전부 loud skip 이다 — 슬롯만 구워지고 발화하고 아무 일도 안 일어나는 것이 옛 전투가
    // 반복해서 당한 형태다. 「없음 = -1」 센티널 3축은 `BindingDef.Default()` 가 명시로 시작한다(S4).
    //
    // 트리거 · 효과 · 게이트 · 주체는 저작이 **코어 enum 을 직접** 든다(skill-data-table unit 4 — 거울 enum · 번역 함수 은퇴).
    // 번호가 다른 어휘(CC · 스택 · 실드 필터 · 공격 수식자)만 아래에서 **이름으로** 옮긴다.
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
            var effects = new List<EffectDef>(def.Effects ?? System.Array.Empty<EffectDef>());
            for (int i = 0; i < def.Units.Length && i < units.Count; i++)
            {
                var d = units[i];
                if (d == null) continue;
                var mods = new List<AttackModDef>();
                var mine = new List<int>();
                // skill-data-table unit 4 — 소유 줄(`bindings`)만 읽는다(규칙 레일 능력 둘 · 옛 칸은 이전됐다).
                if (d.bindings != null && d.bindings.Length > 0)
                {
                    bool unusedRecall = false;
                    BindingSpecBuilder.Bake(d.bindings, new RuleOwner { Origin = BindingOrigin.UnitAuthored, Label = d.name,
                                                                        HostIsGuardian = d.aggroCapacity > 0 },
                                            projectiles, patterns, hazards, rows, effects, mine, mods, ref unusedRecall, view);
                }
                if (mine.Count > 0) def.Units[i].Bindings = mine.ToArray();
                if (mods.Count > 0) def.Units[i].Attack.Mods = mods.ToArray();
            }
            for (int i = 0; i < def.Enemies.Length && i < (enemies?.Length ?? 0); i++)
            {
                var e = enemies[i];
                if (e == null) continue;
                if (e.splitUnit != null) ValidateSplit(e, def.Movement.SplitMaxChildren);
                if (e.bindings == null || e.bindings.Length == 0) continue;
                var mods = new List<AttackModDef>();
                var mine = new List<int>();
                bool unusedRecall = false;
                BindingSpecBuilder.Bake(e.bindings, new RuleOwner { Origin = BindingOrigin.UnitAuthored, Label = e.name, IsEnemy = true },
                                        projectiles, patterns, hazards, rows, effects, mine, mods, ref unusedRecall, view);
                if (mine.Count > 0) def.Enemies[i].Bindings = mine.ToArray();
                if (mods.Count > 0) def.Enemies[i].Attack.Mods = mods.ToArray();
            }
            def.Bindings = rows.ToArray();
            def.Effects = effects.ToArray();
        }

        /// <summary>
        /// skill-data-table unit 1a·1b — 규칙 줄 하나를 싣는다(두 빌더 공용): 효과 줄을 **효과 표**에 넣고(`EffectDef.Intern` —
        /// 같은 id · 같은 값이면 그 줄) 규칙 줄이 그 번호를 가리키게 한 뒤 소유자 목록에 단다. id = 효과 에셋 id(unit 4 — 서버 어휘) ·
        /// 빌더가 펴는 둘째 줄(배치 오라 수면 · 스쿼드 · 드림스톤)만 파생 id.
        /// </summary>
        internal static void AddRow(List<BindingDef> rows, List<EffectDef> effects, List<int> mine,
                                    BindingDef b, EffectDef fx, string id)
        {
            fx.Id = id ?? "";
            b.EffectIndex = EffectDef.Intern(effects, in fx);
            mine?.Add(rows.Count);
            rows.Add(b);
        }

        // skill-data-table unit 4 — 분열 = 적 고유 값(`splitUnit` · `splitCount`)의 가드. 옛 메커닉 갈래(`Bake` 의 분열 분기)와 같은 셋.
        private static void ValidateSplit(AttackUnitData e, int splitCap)
        {
            string label = $"{e.name} split";
            if (e.splitCount < 1) Error($"{label}: splitCount({e.splitCount}) < 1 — 자식이 0기다.");
            else if (e.splitCount > splitCap) Error($"{label}: splitCount({e.splitCount}) > {splitCap} — {splitCap}기로 잘린다.");
            else if (!SplitChain.Validate(e, out string splitError)) Error($"{label}: {splitError}");
        }

        /// <summary>
        /// 효과 저작이 선언한 연출 프리팹(`payload.auraPrefab`)을 그 줄의 뷰 표에 싣는다 — 빔(`AreaDot`) 또는 부착 오라.
        /// `row` = 곧 붙을 줄 번호(`AddRow` 직전의 `rows.Count`). skill-data-table unit 2(U15 — 연출은 효과 기준): 카드 빌더도
        /// 같은 함수를 지난다 — 예전엔 유닛·적 소유 줄만 이 연출을 실어서, 같은 효과를 카드가 들면 오라·빔이 조용히 빠졌다.
        /// </summary>
        internal static void BakeAuthoredVisual(ref EffectDef fx, in DcMechanic m, int row, MatchViewAssets view)
        {
            if (view == null || m.payload.auraPrefab == null) return;
            if (fx.Kind == EffectKind.AreaDot) fx.DataIndex = view.RegisterSkillVfx(m.payload.auraPrefab);
            else view.SetBindingAura(row, m.payload.auraPrefab, m.payload.auraScale);
        }

        // ── 두 빌더가 같이 쓰는 검증(unified-effect-layer unit 5 — 계약 5) ─────────────
        //
        // 카드 빌더(`CardDefinitionBuilder`)와 이 빌더가 **같은 함수**를 지난다. 조합은 `EffectComboRule`(코어 · 순수),
        // 값 가드·표 참조는 아래 넷이다. 출처마다 다른 것은 **저작 인코딩**(카드 버프 % → 배율 · 카드 자리 폭발 탄 배율)뿐이고
        // 그건 각 빌더가 이 함수들 뒤에 덧씌운다 — 효과 값 정본 통일은 후속(H4 + 시트 Effects 탭).

        /// <summary>저작 메커닉 하나의 조합 입력. 탄 결합은 직접 탄(대상 탄) 또는 발사 명세 탄에서 읽는다.</summary>
        internal static EffectCombo ComboOf(in DcMechanic m, TriggerKind trigger, EffectKind payload, bool hostIsEnemy,
                                            bool bindsAfterPlacement, bool hostCannotHoldAggro)
        {
            var c = new EffectCombo
            {
                Trigger = trigger,
                Subject = m.trigger.subject,
                Payload = payload,
                HostIsEnemy = hostIsEnemy,
                BindsAfterPlacement = bindsAfterPlacement,
                HostCannotHoldAggro = hostCannotHoldAggro,
            };
            ProjectileData shot = null;
            if (payload == EffectKind.ProjectileToTarget) shot = m.payload.projectile;
            else if (payload == EffectKind.EmitProjectilePattern && m.payload.pattern != null)
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

        /// <summary>
        /// 저작 트리거 · 효과 종류가 **정의된 값**인가(에셋 정수가 enum 밖이면 짖고 건너뛴다 — 옛 번역 함수의 「모르는 값 → 없음」 몫).
        /// </summary>
        internal static bool KnownKinds(in DcMechanic m, string label)
        {
            if (!System.Enum.IsDefined(typeof(TriggerKind), m.trigger.kind)) { Error($"{label}: 모르는 트리거({(int)m.trigger.kind}) — 건너뛴다."); return false; }
            if (!System.Enum.IsDefined(typeof(EffectKind), m.payload.kind)) { Error($"{label}: 모르는 효과 종류({(int)m.payload.kind}) — 건너뛴다."); return false; }
            return true;
        }

        /// <summary>
        /// 두 빌더 공용 — 저작 축 둘(주체 · 예고)을 규칙 줄에 싣는다. 「남의 배치」 = `Any` + **판에 배치된 방어유닛**만
        /// (순찰 소환물·거점 제외) · 수명 = 숙주(`Owner` — 숙주가 떠나면 같이 떨어진다).
        /// </summary>
        internal static void ApplyAuthoredAxes(ref BindingDef b, ref EffectDef fx, in DcMechanic m)
        {
            b.Subject = m.trigger.subject;
            if (b.Subject == BindingSubject.Any)
            {
                b.SubjectFilter = BindingSubjectFilter.PlacedDefender;
                b.Lifetime = BindingLifetime.Owner;
            }
            fx.Telegraph = m.payload.telegraph;
        }

        /// <summary>조합 검증 — 거절이면 사유 셋 중 하나로 짖는다.</summary>
        // unified-effect-layer unit 7 — 부채꼴 반각(도) → (sin, cos) **bake 1회**(`AttackShapeBake` 선례 — sim 은 삼각함수를
        // 부르지 않는다). 두 빌더(유닛 · 카드)가 같은 변환을 이 한 곳에서 부른다 — 사본이 갈리면 같은 저작 각도가
        // 유닛과 카드에서 다른 콘이 된다. 정의역 거절(반각 ≥ 90)은 `BindPayload` 의 몫이다.
        internal static void BakeCone(ref EffectDef fx, float halfAngleDeg)
        {
            float rad = Mathf.Deg2Rad * Mathf.Max(0f, halfAngleDeg);
            fx.ConeHalfAngleDeg = halfAngleDeg;
            fx.ConeSinHalf = Mathf.Sin(rad);
            fx.ConeCosHalf = Mathf.Cos(rad);
        }

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
        internal static bool BindPayload(ref BindingDef b, ref EffectDef fx, in DcMechanic m, string label,
                                         List<ProjectileData> projectiles, List<ProjectilePatternData> patterns,
                                         HazardSO[] hazards)
        {
            var p = m.payload;
            switch (fx.Kind)
            {
                case EffectKind.ProjectileToTarget:
                {
                    if (p.projectile == null || p.magnitude <= 0f) { Warn($"{label}: ProjectileToTarget 탄 없음 / magnitude <= 0 — 건너뛴다."); return false; }
                    var (mv, pl) = CombatDefinitionBuilder.Translate(p.projectile.flightMode);
                    // 칸 결합(타격 운석)은 효과 좌표 = 맞은 적의 자리 · 착탄 반경 = tileRange · 낙하 = duration(unit 1 — 칸 결합 갈래가 의도에서 읽는다).
                    if (MovementBinding.Of(mv) == BindingClass.Direction
                        && (p.projectile.hitThreshold <= 0f || p.projectile.speed <= 0f || p.tileRange <= 0))
                    { Warn($"{label}: 경로 스윕 탄의 굵기/속도/거리(tileRange) 중 0 이 있다 — 건너뛴다."); return false; }
                    fx.DataIndex = CombatDefinitionBuilder.IndexOf(projectiles, p.projectile);
                    fx.Speed = p.projectile.speed;
                    fx.HitThreshold = p.projectile.hitThreshold;
                    fx.VisualScale = p.projectile.visualScale;
                    fx.ProjectileMovement = (int)mv;
                    fx.ProjectilePayload = (int)pl;
                    return true;
                }
                case EffectKind.SelfOrbitProjectile:
                    if (p.projectile == null || p.magnitude <= 0f || p.duration <= 0f || p.tileRange <= 0
                        || p.projectile.speed <= 0f || p.projectile.hitThreshold <= 0f)
                    { Warn($"{label}: SelfOrbitProjectile 탄·피해·지속·반경·속도·굵기 중 빈 것이 있다 — 건너뛴다."); return false; }
                    fx.DataIndex = CombatDefinitionBuilder.IndexOf(projectiles, p.projectile);
                    fx.VisualScale = p.projectile.visualScale;
                    fx.Speed = p.projectile.speed;
                    fx.HitThreshold = p.projectile.hitThreshold;
                    b.Period = Mathf.Clamp(p.orbitCount <= 0 ? 1 : p.orbitCount, 1, 16);   // 구슬 개수(옛 슬롯 period 재사용)
                    if (b.Trigger == TriggerKind.PeriodicTimer && m.trigger.periodSeconds < p.duration) Warn($"{label}: 주기 < 지속 — 화염구가 겹쳐 쌓인다.");
                    return true;
                case EffectKind.AreaBreath:
                    // 판정 게이트(`SkillMath.SectorGate`)는 볼록 쐐기(반각 < 90°)만 잰다 — 그 이상은 반평면·reflex 라
                    // 조용히 다른 도형이 된다. 거절(`AttackShapeBake` 가 reflex 를 거절하는 것과 같은 규율).
                    if (p.coneHalfAngleDeg >= 90f) { Error($"{label}: AreaBreath 반각({p.coneHalfAngleDeg}°) >= 90 — 부채꼴 게이트의 정의역(볼록 쐐기) 밖이다. 건너뛴다."); return false; }
                    if (p.coneHalfAngleDeg <= 0f) Warn($"{label}: AreaBreath 반각이 0 이하 — 정면 한 줄만 맞는다.");
                    if (p.tileRange <= 0) Warn($"{label}: AreaBreath 사거리가 0 — 같은 셀만 맞는다.");
                    if (p.magnitude <= 0f) Warn($"{label}: AreaBreath 피해가 0 이하 — 발동해도 아무 일이 없다.");
                    return true;
                case EffectKind.EmitProjectilePattern:
                    return BindPattern(ref fx, p.pattern, p.tileRange, label, projectiles, patterns);
                case EffectKind.SelfTileAoe:
                case EffectKind.UltimateLeap:
                    // 폭발·착지 슬램이 탄 요청 하나로 표현된다 — 탄이 없으면 **피해까지** 사라진다.
                    if (p.projectile == null)
                    {
                        Warn($"{label}: {fx.Kind} 에 ProjectileData 가 없어 요청이 드롭된다 — 건너뛴다. payload.projectile 을 지정하라.");
                        return false;
                    }
                    if (fx.Kind == EffectKind.SelfTileAoe && p.magnitude <= 0f) { Warn($"{label}: SelfTileAoe magnitude <= 0 — 건너뛴다."); return false; }
                    fx.DataIndex = CombatDefinitionBuilder.IndexOf(projectiles, p.projectile);
                    fx.VisualScale = 0f;   // 유닛 bake 는 탄 배율을 안 실었다(0 = 뷰가 1 로 읽는다) — 카드는 빌더가 덧씌운다
                    // unit 7d — 「생존당 1회」는 **`fireCap 1`** 이다(정정 5 의 짝). 옛 전투는 `fraction ≥ 0.5` 라 둘째 경계가
                    // 음수가 되어 **우연히** 1회였다 — 값 한 칸이 0.4 가 되면 조용히 2회가 된다. ⚠ **궁극기에만** 준다 —
                    // 같은 경계 트리거를 빈사폭주·진동갑주·가호가 쓰고 그쪽은 다회 발동이 사양이다.
                    if (fx.Kind == EffectKind.UltimateLeap) b.FireCap = 1;
                    return true;
                case EffectKind.SelfBlink:
                case EffectKind.AllyMoveSpeedAura:
                case EffectKind.AreaSleep:
                    // 연출용 탄(퍼프·펄스) — **선택**이다. 없으면 연출만 없다.
                    if (p.projectile != null) fx.DataIndex = CombatDefinitionBuilder.IndexOf(projectiles, p.projectile);
                    if (fx.Kind == EffectKind.AreaSleep)
                    {
                        if (p.magnitude < 1f || p.duration <= 0f) { Warn($"{label}: AreaSleep 에 인원(>=1)·수면 초(>0)가 없다 — 매 주기 no-op. 건너뛴다."); return false; }
                        if (p.tileRange <= 0) { Warn($"{label}: AreaSleep 의 tileRange 가 0 이라 host 셀만 본다 — 건너뛴다."); return false; }
                        if (b.Trigger == TriggerKind.PeriodicTimer && p.duration >= m.trigger.periodSeconds)
                            Warn($"{label}: AreaSleep duration({p.duration}) >= periodSeconds({m.trigger.periodSeconds}) — 수면이 끊기지 않아 대상이 생존 내내 고착한다.");
                    }
                    if (fx.Kind == EffectKind.AllyMoveSpeedAura && p.duration <= m.trigger.periodSeconds)
                        Warn($"{label}: AllyMoveSpeedAura duration({p.duration}) <= periodSeconds({m.trigger.periodSeconds}) — 펄스 사이에 만료(점멸)한다.");
                    return true;
                case EffectKind.ApplyCcToTarget:
                    if (p.duration <= 0f) { Warn($"{label}: ApplyCcToTarget duration <= 0 — 건너뛴다."); return false; }
                    return true;
                case EffectKind.ApplyStackToTarget:
                    if (p.magnitude < 1f) { Warn($"{label}: ApplyStackToTarget magnitude < 1(스택 없음) — 건너뛴다."); return false; }
                    return true;
                case EffectKind.AreaDot:
                    // 틱 간격(0 이면 magnitude 가 DPS). 빔 프리팹은 뷰의 것(7c) — 여기선 index 를 안 싣는다(무연출).
                    fx.Speed = Mathf.Max(0f, p.tickIntervalSec);
                    return true;
                case EffectKind.AllyStatAura:
                case EffectKind.OpponentStatAura:
                    if (p.buffStat == CardBuffKind.EffectiveHealth)
                    {
                        // 번역 산식이 역수(1/(1+p/100))라 오라 concrete 의 (1+p/100) 과 갈린다 — 조용히 틀린 배율보다 거절.
                        Warn($"{label}: 스탯 오라에 EffectiveHealth 는 배선되지 않았다(산식이 역수) — 건너뛴다.");
                        return false;
                    }
                    if (!TryToSkillStat(p.buffStat, out _)) { Warn($"{label}: 오라 스탯 {p.buffStat} 을 옮길 수 없다 — 건너뛴다."); return false; }
                    return true;
                case EffectKind.GrantShield:
                    // 트리거 × 반경 블랙리스트는 은퇴(unit 5) — concrete 가 자기(반경 0)·주변(반경 > 0)을 둘 다 받는다.
                    if (p.magnitude <= 0f) { Warn($"{label}: GrantShield 에 실드량(>0)이 없다 — 매 발동 no-op. 건너뛴다."); return false; }
                    if (p.duration > 0f) Warn($"{label}: GrantShield 의 duration({p.duration}) 은 무시된다 — 실드는 시간이 아니라 피해로만 사라진다.");
                    return true;
                case EffectKind.AreaTaunt:
                    // 가디언 여부는 조합 검증(`EffectComboRule` ⑦)이 본다.
                    if (p.duration <= 0f || p.tileRange <= 0) { Warn($"{label}: AreaTaunt 에 도발 초·반경이 없다 — 매 발동 no-op. 건너뛴다."); return false; }
                    return true;
                case EffectKind.SpawnHazard:
                    int h = hazards != null && p.hazard != null ? System.Array.IndexOf(hazards, p.hazard) : -1;
                    if (h < 0) { Warn($"{label}: SpawnHazard 의 장판이 이 판의 장판 표에 없다 — 건너뛴다(카드면 `WithCardHazards` 를 거쳤나)."); return false; }
                    // U10 — 장판 DoT 피해 = 효과 줄의 피해(unit 4 전까지 장판 SO 의 DoT `param1` 을 빌더가 옮긴다). DoT ≤ 1.
                    if (!BoardEffectDefinitionBuilder.TryDotDamage(p.hazard, out float dot)) { Error($"{label}: 장판 '{p.hazard.name}' 에 DoT 하위 효과가 둘 이상 — 효과 줄 피해 칸 하나에 못 담는다. 건너뛴다."); return false; }
                    fx.HazardDefIndex = h;
                    fx.Damage = dot;
                    return true;
                default:
                    return true;
            }
        }

        // 발사 명세 — 옛 `TryBuildPatternSlot` 의 거절을 옮겼다(규칙 경로 전용 — 평타 다연발은 따로 굽는다).
        internal static bool BindPattern(ref EffectDef fx, ProjectilePatternData pattern, int tileRange, string label,
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
            fx.PatternDefIndex = idx;
            // skill-data-table 1b(U10) — 탄 피해 = 효과 줄의 피해(unit 4 전까지 명세 SO 의 `damage` 를 빌더가 옮긴다 — 명세 줄은
            // 모양만). 탄이 **길막을 세우면** 그 착탄은 피해를 안 주고(설치) 피해 칸은 **길막 폭발**이다(길막 SO `explodeDamage`).
            var blocker = pattern.barrel.spawnBlocker;
            if (blocker != null)
            {
                if (pattern.damage > 0f) Warn($"{label}: 길막을 세우는 명세의 damage({pattern.damage}) 는 쓰이지 않는다 — 피해 칸 = 길막 폭발 {blocker.explodeDamage}.");
                fx.Damage = Mathf.Max(0f, blocker.explodeDamage);
            }
            else fx.Damage = pattern.damage;
            CombatDefinitionBuilder.IndexOf(projectiles, pattern.barrel);   // 탄 표에 등록
            return true;
        }

        // ── 저작 어휘 → 코어 어휘(번호가 다른 것만 · 이름으로) ─────────────────────

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
