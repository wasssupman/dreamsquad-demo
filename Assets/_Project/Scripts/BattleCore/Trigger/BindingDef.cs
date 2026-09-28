using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Wassup.Skills;

namespace Wassup.BattleCore.Trigger
{
    // battle-core-rebuild unit 7a — **규칙 하나**의 정의표 줄. 유닛 스킬·카드·기믹·배치 오라·액티브가
    // 전부 이 타입 하나다(어휘 6개념: Command · Event · **Binding** · Condition · Effect · Owner/Lifetime).
    //
    // 옛 `DcTriggerSlot` 을 옮긴 것이 아니다 — 그것은 Burst 가 읽을 수 있는 **평평한 한 줄**이라
    // `tileRange` 한 칸이 7~13가지 뜻을 겸직했다(S24). 여기서는 ⑴ 실행자를 **concrete 참조**로 들고
    // (`Skill`, 정적 라우팅 표가 bake 때 한 번 고른다) ⑵ 효과 값(종류 + 수치)은 **효과 줄**(`EffectDef` ·
    // `MatchDefinition.Effects`)에 있고 이 줄은 `EffectIndex` 로 가리킨다(skill-data-table 1b — 이 줄은 「언제 · 누구 ·
    // 조건 · 수명 → 효과 id」다). 이름 붙은 읽기는 `Wassup.Skills` 의 params 뷰가 한다(`AreaSleepParams.SleepCount` …).
    //
    // ⚠ **「없음」은 -1 이다**(S4) — 효과 줄 번호도 같다. struct 기본값 0 은 **유효 index** 라 줄은 반드시
    // `Default()` 에서 시작한다.
    public struct BindingDef
    {
        /// <summary>진단 이름(「보스_마메모 mechanic 1」). 규칙이 아니다 — 해시에도 안 싣는다.</summary>
        public string Label;

        // ── 사건(Event) ──
        public TriggerKind Trigger;
        public BindingSubject Subject;
        /// <summary>`Any` 바인딩의 주어 필터(직업 비트). 0 = 없음. 저작 노출 없음 — 7b 의 상속 바인딩이 쓴다.</summary>
        public int SubjectClassMask;
        /// <summary>
        /// unit 7b — `Any` 바인딩의 주어 필터(배치 코스트). 0 = 없음. 카드 축 `Cost1` 의 자리다(옛 `MatchesDcAxis`) —
        /// 직업 비트와 **곱(∧)** 으로 읽는다. 둘 다 0 = 전원(축 `All`).
        /// </summary>
        public int SubjectCost;
        /// <summary>
        /// unit 7d — `Any` 바인딩의 주어 필터. 위 둘과 곱(∧). `None` = 없음. 저작 「남의 배치」(unified-effect-layer unit 5)가
        /// `PlacedDefender` 를 싣는다 — 그 밖엔 기믹 코드만 쓴다.
        /// </summary>
        public BindingSubjectFilter SubjectFilter;

        // ── 트리거 상태의 저작값 ──
        /// <summary>`AttackN`·`OnDamagedN` — N 번째마다. 0 = 발동 안 함(순수 함수 가드).</summary>
        public int Period;
        /// <summary>`PeriodicTimer` 주기 초. 0 이하 = 발동도 누적도 안 함.</summary>
        public float PeriodSeconds;
        /// <summary>`HealthThreshold` 경계 간격. 0 이하 = 발동 안 함.</summary>
        public float Fraction;

        // ── 조건(Condition) — 카드 게이트 어휘 2조합만 열린다(S26) ──
        public GateKind Gate;
        public GateSubject GateSubject;
        public float GateValue;

        // ── 효과(Effect) ──
        /// <summary>실행자. **무상태** concrete 라 한 벌을 공유한다. null = 스킬 아님(bake 가 이미 거절했다).</summary>
        public ISkill Skill;

        /// <summary>
        /// unit 7d — **코어가 직접 실행하는 효과**(시즌 기믹 4종). `Wassup.Skills` 의 의도 어휘에 없는 일
        /// (열기 한 걸음 · 피로 요청 · 픽업 놓기 · 사직서 떨어뜨리기)이라 `ISkill` 로 못 싣는다 — 그 어셈블리는
        /// 무변이 계약이다(7a). 이 칸이 차 있으면 디스패처가 `Skill` 대신 이것을 부른다.
        /// ⚠ 정의표(`MatchDefinition.Bindings`)의 줄에는 **서지 않는다** — 판 규칙이 런타임에 조립하는 줄(`DefIndex = -1`)
        /// 만 든다. 그래서 해시에도 안 실린다(값은 이미 `GimmickDef` 가 싣는다).
        /// </summary>
        public ICoreEffect CoreSkill;

        /// <summary>
        /// skill-data-table unit 1a — 이 규칙이 가리키는 **효과 줄**(`MatchDefinition.Effects`). 읽기는 언제나
        /// `MatchDefinition.EffectOf`(규칙 인스턴스는 붙을 때 해석한 `Binding.Effect`). **-1 = 효과 값 없음** — 판 규칙이
        /// 런타임에 조립하는 코어 효과 줄(`CoreSkill` · 기믹 — 값은 `GimmickDef` 가 든다)만 이렇다.
        /// ⚠ 해시에 안 싣는다 — 해시는 해석된 효과 값만 본다(README 계약 8). 줄 번호는 정체가 아니다(계약 6).
        /// </summary>
        public int EffectIndex;

        /// <summary>
        /// **인스턴스 값**(효과 값이 아니다) — 호접몽 완주 버프의 칸 판별자. 붙는 순간 규칙 인스턴스가 `InstanceId` 로 채운다
        /// (`BindingRegistry.AttachRows`). 정의표 줄에서는 0.
        /// </summary>
        public int StackId;

        // ── 소유·수명 ──
        /// <summary>발동 횟수 상한. 0 = 무제한. **수명과 다른 축**이다(정정 5).</summary>
        public int FireCap;
        public BindingLifetime Lifetime;
        /// <summary>`Timed` 수명의 초.</summary>
        public float LifetimeSeconds;
        /// <summary>떨어질 때 이 바인딩이 건 스탯을 **소급 회수**하나(H6 — 배치 오라 공속은 참, 수면은 거짓).</summary>
        public bool RevokeOnExpire;
        public BindingOrigin Origin;

        /// <summary>효과 줄 번호를 명시 -1 로 시작한다(S4).</summary>
        public static BindingDef Default() => new BindingDef
        {
            Label = "",
            EffectIndex = -1,
            Lifetime = BindingLifetime.Owner,
        };

        public int SkillId => Skill != null ? Skill.SkillId : SkillRouting.NotRouted;

        /// <summary>실행자가 있나 — 스킬이든 코어 효과든.</summary>
        public bool HasEffect => Skill != null || CoreSkill != null;

        /// <summary>
        /// 이번 발동의 params. 사건 스냅샷(층)은 호출부가 넘긴다 — **드레인 시점에 다시 읽지 않는다**.
        /// 사건의 자리·몸은 여기가 아니라 원점(`SkillTarget.Origin` — unified-effect-layer unit 2)으로 간다.
        /// `patternIndex` 는 정의표 줄 번호다(코어에는 host 버퍼가 없다 — 슬롯은 바인딩이 든다).
        /// 효과 값은 `e`(해석된 효과 줄 — `MatchDefinition.EffectOf`) · 트리거 값(`Period`)과 인스턴스 값(`StackId`)은 이 줄.
        /// </summary>
        public SkillParams ToParams(in EffectDef e, byte targetLayers)
            => new SkillParams(
                e.Magnitude, e.Duration, e.TileRange, Period, e.DataIndex,
                e.CcKind, e.Speed, e.HitThreshold, e.Damage, e.SlamTileRange, StackId, e.VisualScale,
                e.PatternDefIndex, e.StatKind, e.StackKind, e.ProjectileMovement, e.ProjectilePayload,
                targetLayers, e.HazardDefIndex,
                e.ShieldTargetCount, e.ShieldIncludesSelf, e.ShieldFilter, e.ConeSinHalf, e.ConeCosHalf, e.Telegraph);

        /// <summary>`e` = 해석된 효과 줄(`MatchDefinition.EffectOf`). 키·순서는 1a 이전과 같다 — 해시 무변(README 계약 8).</summary>
        internal void Canonicalize(StringBuilder sb, CultureInfo inv, in EffectDef e)
        {
            MatchDefinition.Put(sb, "trigger", (int)Trigger, inv);
            MatchDefinition.Put(sb, "payload", (int)e.Kind, inv);
            MatchDefinition.Put(sb, "skill", SkillId, inv);
            MatchDefinition.Put(sb, "subject", (int)Subject, inv);
            MatchDefinition.Put(sb, "subjectClass", SubjectClassMask, inv);
            // unit 7b — 기본값이면 안 쓴다(7a 까지의 규칙 줄 해시 무변).
            if (SubjectCost != 0) MatchDefinition.Put(sb, "subjectCost", SubjectCost, inv);
            // unit 7d — 기본값이면 안 쓴다(7c 까지의 규칙 줄 해시 무변).
            if (SubjectFilter != BindingSubjectFilter.None) MatchDefinition.Put(sb, "subjectFilter", (int)SubjectFilter, inv);
            MatchDefinition.Put(sb, "period", Period, inv);
            MatchDefinition.Put(sb, "periodSec", PeriodSeconds, inv);
            MatchDefinition.Put(sb, "fraction", Fraction, inv);
            MatchDefinition.Put(sb, "gate", (int)Gate, inv);
            MatchDefinition.Put(sb, "gateSubject", (int)GateSubject, inv);
            MatchDefinition.Put(sb, "gateValue", GateValue, inv);
            MatchDefinition.Put(sb, "magnitude", e.Magnitude, inv);
            MatchDefinition.Put(sb, "duration", e.Duration, inv);
            MatchDefinition.Put(sb, "tileRange", e.TileRange, inv);
            MatchDefinition.Put(sb, "data", e.DataIndex, inv);
            MatchDefinition.Put(sb, "pattern", e.PatternDefIndex, inv);
            MatchDefinition.Put(sb, "hazard", e.HazardDefIndex, inv);
            MatchDefinition.Put(sb, "cc", e.CcKind, inv);
            MatchDefinition.Put(sb, "stat", e.StatKind, inv);
            MatchDefinition.Put(sb, "stack", e.StackKind, inv);
            MatchDefinition.Put(sb, "shield",
                e.ShieldFilter.ToString(inv) + "," + e.ShieldTargetCount.ToString(inv) + ","
                + (e.ShieldIncludesSelf ? "1" : "0"));
            MatchDefinition.Put(sb, "speed", e.Speed, inv);
            MatchDefinition.Put(sb, "hitThreshold", e.HitThreshold, inv);
            MatchDefinition.Put(sb, "visualScale", e.VisualScale, inv);
            MatchDefinition.Put(sb, "coneDeg", e.ConeHalfAngleDeg, inv);
            MatchDefinition.Put(sb, "coneSinCos", e.ConeSinHalf.ToString("R", inv) + "," + e.ConeCosHalf.ToString("R", inv));
            // skill-data-table 1b(U10) — 슬램 피해는 `damage` 로 갔다. 첫 자리는 은퇴(언제나 0)지만 키 모양을 지킨다 —
            // 바꾸면 슬램이 없는 규칙 줄 전부(대다수)의 정본 텍스트가 흔들린다.
            MatchDefinition.Put(sb, "slam", "0," + e.SlamTileRange.ToString(inv));
            // 새 칸 — 0 이면 안 쓴다(계약 8).
            if (e.Damage != 0f) MatchDefinition.Put(sb, "damage", e.Damage, inv);
            MatchDefinition.Put(sb, "stackId", StackId, inv);
            MatchDefinition.Put(sb, "projAxes", e.ProjectileMovement.ToString(inv) + "," + e.ProjectilePayload.ToString(inv));
            // unified-effect-layer unit 2 — 기본값이면 안 쓴다(unit 1 까지의 규칙 줄 해시 무변).
            if (e.Telegraph) MatchDefinition.Put(sb, "telegraph", 1, inv);
            // skill-data-table unit 3 — 기본값(`Flat`)이면 안 쓴다(계약 8 — 라이브 전량 `Flat` → 해시 무변).
            if (e.MagnitudeMode != MagnitudeMode.Flat)
            {
                MatchDefinition.Put(sb, "magnitudeMode", (int)e.MagnitudeMode, inv);
                MatchDefinition.Put(sb, "basisStat", (int)e.BasisStat, inv);
                MatchDefinition.Put(sb, "ratio", e.Ratio, inv);
            }
            MatchDefinition.Put(sb, "fireCap", FireCap, inv);
            MatchDefinition.Put(sb, "lifetime", (int)Lifetime, inv);
            MatchDefinition.Put(sb, "lifetimeSec", LifetimeSeconds, inv);
            MatchDefinition.Put(sb, "revoke", RevokeOnExpire ? 1 : 0, inv);
            MatchDefinition.Put(sb, "origin", (int)Origin, inv);
        }
    }

    /// <summary>
    /// skill-data-table unit 1a — **효과 줄**(스킬의 정체 · 사용자 결정 U6). 종류 + 수치 + 안정 `Id`. 규칙 줄(`BindingDef`)이
    /// `EffectIndex` 로 가리킨다 — 탄 · 패턴 · 장판 표와 같은 참조 표(README 계약 1).
    ///
    /// 담지 않는 것: 실행자(`BindingDef.Skill` — 라우팅이 트리거 × 종류로 고른다) · 트리거 값(`Period` …) · **인스턴스 값**
    /// (호접몽 `StackId = InstanceId` · 부착 캐스트 FireCap/Lifetime — 규칙 인스턴스가 든다).
    /// ⚠ 「없음」은 -1 이다(S4) — 탄·패턴·장판 세 축. 그래서 줄은 `Default()` 에서 시작한다.
    /// </summary>
    public struct EffectDef
    {
        /// <summary>안정 id(`{소유자}.{자리}` — 1a 는 저작 경로에서 파생). 서버 어휘(계약 6) · **해시 밖**(계약 8).</summary>
        public string Id;
        /// <summary>효과 종류. 해시 키는 `"payload"` 그대로(골든 무관).</summary>
        public EffectKind Kind;

        // 수치 — `SkillParams` 의 원시 칸과 같은 이름. 읽는 쪽이 종류별 뷰로 이름을 붙인다.
        public float Magnitude;
        public float Duration;
        public int TileRange;
        /// <summary>탄·연출 index(`MatchDefinition.Projectiles`, 빔은 뷰의 스킬 VFX 표). **-1 = 없음.**</summary>
        public int DataIndex;
        /// <summary>발사 명세(`MatchDefinition.Patterns`). **-1 = 없음.**</summary>
        public int PatternDefIndex;
        /// <summary>존 장판(`MatchDefinition.Hazards`). **-1 = 없음.**</summary>
        public int HazardDefIndex;
        /// <summary>`SkillCcKind` 값(저작 `DcCcKind` 와 번호가 다르다 — 빌더가 이름으로 옮긴다).</summary>
        public int CcKind;
        /// <summary>`SkillStatKind` 값.</summary>
        public int StatKind;
        /// <summary>`SkillStackKind` 값.</summary>
        public int StackKind;
        /// <summary>실드 대상 선정(`SkillShieldFilter`) · 대상 수(0 = 전원) · 자기 포함.</summary>
        public int ShieldFilter;
        public int ShieldTargetCount;
        public bool ShieldIncludesSelf;
        public float Speed;
        public float HitThreshold;
        public float VisualScale;
        /// <summary>부채꼴 반각(도). 판정·그림은 아래 (sin, cos) — bake 가 1회 변환해 둘 다 싣는다.</summary>
        public float ConeHalfAngleDeg;
        public float ConeSinHalf;
        public float ConeCosHalf;
        /// <summary>
        /// skill-data-table 1b(U10) — **이 효과의 피해**(한 효과 = 피해 원천 ≤ 1). 종류가 정한다: 발사 명세 = 탄 한 발 ·
        /// 길막을 세우는 명세 = 길막 폭발 · 장판 = DoT(틱당 · 주기 0 이면 DPS) · 도약 2종 = 착지 슬램. 패턴 · 장판 · 길막
        /// 줄에는 피해 칸이 없다(모양만). ⚠ `Magnitude` 로 피해를 싣는 종류(탄 · 자폭 · 브레스 …)는 unit 4 까지 그대로다.
        /// </summary>
        public float Damage;
        public int SlamTileRange;
        public int ProjectileMovement;
        public int ProjectilePayload;
        /// <summary>칸 결합 탄의 착탄 예고(사용자 결정 U1 · 기본 꺼짐).</summary>
        public bool Telegraph;

        /// <summary>
        /// skill-data-table unit 3(U7) — 수치 방식. `Flat`(기본) = 위 칸 그대로 · `OwnerStatRatio` = 그 종류의 비율 칸
        /// (`EffectMagnitude` — 피해 · 실드량)을 **시전 순간** 소유자 `BasisStat` 최종값 × `Ratio` 로 채운다(계약 9 ·
        /// 드레인이 해석한 사본만 실행에 간다 — 이 줄은 안 바뀐다). 기본값이면 해시에 안 쓴다(계약 8).
        /// </summary>
        public MagnitudeMode MagnitudeMode;
        public BasisStat BasisStat;
        /// <summary>비율(> 0). `Flat` 에서는 읽지 않는다.</summary>
        public float Ratio;

        public static EffectDef Default() => new EffectDef
        {
            Id = "",
            DataIndex = -1,
            PatternDefIndex = -1,
            HazardDefIndex = -1,
        };

        /// <summary>
        /// 효과 줄을 표에 넣고 번호를 돌려준다(빌더 · 고정구 공용). **같은 id · 같은 값**이 이미 있으면 그 줄을 다시 가리킨다
        /// (같은 카드 두 장). 값이 같아도 id 가 다르면 다른 줄이다(U13 — 소유자마다 따로 조정).
        /// </summary>
        public static int Intern(List<EffectDef> table, in EffectDef e)
        {
            for (int i = 0; i < table.Count; i++)
                if (table[i].Equals(e)) return i;
            table.Add(e);
            return table.Count - 1;
        }
    }
}
