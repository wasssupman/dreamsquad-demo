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
    // (`Skill`, 정적 라우팅 표가 bake 때 한 번 고른다) ⑵ 수치는 저작 이름 그대로 싣고, 이름 붙은 읽기는
    // `Wassup.Skills` 의 params 뷰가 한다(`AreaSleepParams.SleepCount` …) — 뷰는 그 어셈블리의 것이고
    // 손대지 않는다.
    //
    // ⚠ **「없음」은 -1 이다**(S4) — 탄·패턴·장판 세 축 모두. struct 기본값 0 은 **유효 index** 라
    // 미배선 줄이 0번 탄을 쏜다. 그래서 줄은 반드시 `Default()` 에서 시작한다.
    public struct BindingDef
    {
        /// <summary>진단 이름(「보스_마메모 mechanic 1」). 규칙이 아니다 — 해시에도 안 싣는다.</summary>
        public string Label;

        // ── 사건(Event) ──
        public TriggerKind Trigger;
        public EffectKind Payload;
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

        // 수치 — `SkillParams` 의 원시 칸과 같은 이름. 읽는 쪽이 payload 별 뷰로 이름을 붙인다.
        public float Magnitude;
        public float Duration;
        public int TileRange;
        /// <summary>탄·연출 index(`MatchDefinition.Projectiles`, 빔은 뷰의 스킬 VFX 표). **-1 = 없음.**</summary>
        public int DataIndex;
        /// <summary>발사 명세(`MatchDefinition.Patterns`). **-1 = 없음.**</summary>
        public int PatternDefIndex;
        /// <summary>존 장판(`MatchDefinition.Hazards`). **-1 = 없음.**</summary>
        public int HazardDefIndex;
        /// <summary>`SkillCcKind` 값. 저작 `DcCcKind`(Stun·Impulse·Sleep)와 **번호가 다르다** — 빌더가 이름으로 옮긴다.</summary>
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
        /// <summary>부채꼴 반각(도). 판정·그림은 아래 (sin, cos) — bake 가 이 저작값에서 1회 변환해 둘 다 싣는다.</summary>
        public float ConeHalfAngleDeg;
        /// <summary>반각의 (sin, cos) — `SkillMath.SectorGate` 의 인자(unified-effect-layer unit 7 · 옛 cos² 대체).</summary>
        public float ConeSinHalf;
        public float ConeCosHalf;
        public float SlamDamage;
        public int SlamTileRange;
        public int StackId;
        public int ProjectileMovement;
        public int ProjectilePayload;
        /// <summary>
        /// unified-effect-layer unit 2 — 칸 결합 탄의 **착탄 예고**(사용자 결정 U1 — 효과 파라미터 · 기본 꺼짐).
        /// 저작 칸 = `DcPayloadSpec.telegraph`(unit 5 · 기본 false — 기존 저작 무변).
        /// </summary>
        public bool Telegraph;

        // ── 소유·수명 ──
        /// <summary>발동 횟수 상한. 0 = 무제한. **수명과 다른 축**이다(정정 5).</summary>
        public int FireCap;
        public BindingLifetime Lifetime;
        /// <summary>`Timed` 수명의 초.</summary>
        public float LifetimeSeconds;
        /// <summary>떨어질 때 이 바인딩이 건 스탯을 **소급 회수**하나(H6 — 배치 오라 공속은 참, 수면은 거짓).</summary>
        public bool RevokeOnExpire;
        public BindingOrigin Origin;

        /// <summary>센티널 세 축을 명시 -1 로 시작한다(S4).</summary>
        public static BindingDef Default() => new BindingDef
        {
            Label = "",
            DataIndex = -1,
            PatternDefIndex = -1,
            HazardDefIndex = -1,
            Lifetime = BindingLifetime.Owner,
        };

        public int SkillId => Skill != null ? Skill.SkillId : SkillRouting.NotRouted;

        /// <summary>실행자가 있나 — 스킬이든 코어 효과든.</summary>
        public bool HasEffect => Skill != null || CoreSkill != null;

        /// <summary>
        /// 이번 발동의 params. 사건 스냅샷(층)은 호출부가 넘긴다 — **드레인 시점에 다시 읽지 않는다**.
        /// 사건의 자리·몸은 여기가 아니라 원점(`SkillTarget.Origin` — unified-effect-layer unit 2)으로 간다.
        /// `patternIndex` 는 정의표 줄 번호다(코어에는 host 버퍼가 없다 — 슬롯은 바인딩이 든다).
        /// </summary>
        public SkillParams ToParams(byte targetLayers)
            => new SkillParams(
                Magnitude, Duration, TileRange, Period, DataIndex,
                CcKind, Speed, HitThreshold, SlamDamage, SlamTileRange, StackId, VisualScale,
                PatternDefIndex, StatKind, StackKind, ProjectileMovement, ProjectilePayload,
                targetLayers, HazardDefIndex,
                ShieldTargetCount, ShieldIncludesSelf, ShieldFilter, ConeSinHalf, ConeCosHalf, Telegraph);

        internal void Canonicalize(StringBuilder sb, CultureInfo inv)
        {
            MatchDefinition.Put(sb, "trigger", (int)Trigger, inv);
            MatchDefinition.Put(sb, "payload", (int)Payload, inv);
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
            MatchDefinition.Put(sb, "magnitude", Magnitude, inv);
            MatchDefinition.Put(sb, "duration", Duration, inv);
            MatchDefinition.Put(sb, "tileRange", TileRange, inv);
            MatchDefinition.Put(sb, "data", DataIndex, inv);
            MatchDefinition.Put(sb, "pattern", PatternDefIndex, inv);
            MatchDefinition.Put(sb, "hazard", HazardDefIndex, inv);
            MatchDefinition.Put(sb, "cc", CcKind, inv);
            MatchDefinition.Put(sb, "stat", StatKind, inv);
            MatchDefinition.Put(sb, "stack", StackKind, inv);
            MatchDefinition.Put(sb, "shield",
                ShieldFilter.ToString(inv) + "," + ShieldTargetCount.ToString(inv) + ","
                + (ShieldIncludesSelf ? "1" : "0"));
            MatchDefinition.Put(sb, "speed", Speed, inv);
            MatchDefinition.Put(sb, "hitThreshold", HitThreshold, inv);
            MatchDefinition.Put(sb, "visualScale", VisualScale, inv);
            MatchDefinition.Put(sb, "coneDeg", ConeHalfAngleDeg, inv);
            MatchDefinition.Put(sb, "coneSinCos", ConeSinHalf.ToString("R", inv) + "," + ConeCosHalf.ToString("R", inv));
            MatchDefinition.Put(sb, "slam", SlamDamage.ToString("R", inv) + "," + SlamTileRange.ToString(inv));
            MatchDefinition.Put(sb, "stackId", StackId, inv);
            MatchDefinition.Put(sb, "projAxes", ProjectileMovement.ToString(inv) + "," + ProjectilePayload.ToString(inv));
            // unified-effect-layer unit 2 — 기본값이면 안 쓴다(unit 1 까지의 규칙 줄 해시 무변).
            if (Telegraph) MatchDefinition.Put(sb, "telegraph", 1, inv);
            MatchDefinition.Put(sb, "fireCap", FireCap, inv);
            MatchDefinition.Put(sb, "lifetime", (int)Lifetime, inv);
            MatchDefinition.Put(sb, "lifetimeSec", LifetimeSeconds, inv);
            MatchDefinition.Put(sb, "revoke", RevokeOnExpire ? 1 : 0, inv);
            MatchDefinition.Put(sb, "origin", (int)Origin, inv);
        }
    }
}
