using System.Globalization;
using System.Text;

namespace Wassup.BattleCore
{
    // battle-core-rebuild unit 4 — 드림캐쳐 카드와 기믹의 **정의표 줄**.
    //
    // unit 4 가 연 칸은 **자원**(값·쿨다운·인수인계 선언)이고, unit 7b 가 **그 카드가 무엇을 싣는가**를
    // 더했다 — 규칙 줄 번호(`Bindings`)·공격 수식자·액티브 규칙·부착 제한·적 표식 여부. 여기 있는 것은
    // 전부 **값**이다: 실행은 규칙 레이어(`BindingRegistry` → `TriggerDispatcher` → concrete)가 하고,
    // 손패 담당자(`HandDeck`)는 이 줄을 읽어 규칙 등록부에 넘기기만 한다.

    public enum CardKind : byte
    {
        /// <summary>방어유닛에 붙는다. 쓰면 **풀에서 이탈**하고, 숙주가 판을 떠날 때만 돌아온다(D17).</summary>
        Attach = 0,
        /// <summary>즉시 시전한다. 성공하면 값을 치르고 **덱 뒤로 재활용**된다(D17).</summary>
        Active = 1,
    }

    public struct CardDef
    {
        public string Id;
        public CardKind Kind;

        /// <summary>각성에서 깎는 값. 카드 **종류가 아니라 이 카드**가 정한다(D15).</summary>
        public int Cost;

        /// <summary>액티브 재사용 대기(초). 0 = 대기 없음(K1 「기록 자체가 없으면 준비된 것」).</summary>
        public float CooldownSeconds;

        /// <summary>
        /// 「인수인계」 선언. 이 카드가 붙은 유닛을 **플레이어가 퇴근**시키면 그 유닛의 나머지
        /// 카드가 부착 순서 그대로 큐 **맨 앞**으로 온다(D9).
        ///
        /// ⚠ 옛 전투는 이 판정을 두 곳(`DeclaresRetireRecall` ↔ 브리지 부착 화이트리스트)에
        /// 두었고, 한쪽만 넓히면 「붙는데 무효」 또는 「검증 없이 발동」이 됐다(D10 · 중복 2).
        /// 여기 **한 칸**이 그 두 판정을 접은 자리다 — 저작이 곧 판정이다.
        /// </summary>
        public bool DeclaresRetireRecall;

        // ── unit 7b — 카드가 싣는 규칙 ────────────────────────────────────────

        /// <summary>
        /// 붙을 때 숙주에 매다는 규칙 줄(`MatchDefinition.Bindings` 인덱스). null/빈 = 없음.
        /// 한 줄 = 저작 메커닉 하나(배치 오라는 **둘** — 공속 + 수면, 회수가 비대칭이라 한 줄로 못 접는다).
        /// 줄마다 **host 종속 적용성**이 따로 판정된다(`Applicability`) — 한 줄이 이 숙주에서 안 돌아도
        /// 나머지는 붙는다(옛 「메커닉 단위 skip, 전량 무효일 때만 카드 거절」).
        /// </summary>
        public int[] Bindings;

        /// <summary>
        /// Squad 카드의 스탯 줄. 숙주에 매다는 것은 위와 같지만 **붙는 순간 판 위의 해당 유닛 전원에게도
        /// 한 번씩** 발동한다(이후 배치분은 그 줄이 `OnPlace(Any)` 로 상속시킨다). 회수는 그 줄의 소급 중화다.
        /// </summary>
        public int[] SquadBindings;

        /// <summary>항상 켜진 공격 수식자(튕김·최전방·수면 배율 + 저작상 payload 인 강공). null/빈 = 없음.</summary>
        public Combat.AttackModDef[] AttackMods;

        /// <summary>액티브의 규칙 줄(`trigger None`, 시전자 없음). -1 = 없음(부착 카드 · bake 거절).</summary>
        public int ActiveBinding;

        /// <summary>액티브가 **두 칸**을 받는가(포탈). 입구 == 출구는 거절이다.</summary>
        public bool NeedsTwoCells;

        /// <summary>
        /// 적을 겨누는 카드(살찌운 제물). 방어유닛에 안 붙고, **부착 상한 밖**이다(D14).
        /// 조준 라우팅(7c)이 코어에 이 한 칸을 묻는다 — 뷰가 메커닉을 뒤져 추측하지 않게.
        /// </summary>
        public bool TargetsEnemies;

        /// <summary>
        /// 부착 제한(정적 술어 — 「누구에게 붙을 수 있나」). 발동 시점의 게이트와 층이 다르다.
        /// 무효 저작(값 없음·모르는 직업)은 **어디에도 안 붙는다**(fail-closed).
        /// </summary>
        public AttachRequirementDef Requirement;

        public static CardDef Default() => new CardDef { Id = "", ActiveBinding = -1 };

        internal void Canonicalize(StringBuilder sb, CultureInfo inv)
        {
            MatchDefinition.Put(sb, "id", Id);
            MatchDefinition.Put(sb, "kind", (int)Kind, inv);
            MatchDefinition.Put(sb, "cost", Cost, inv);
            MatchDefinition.Put(sb, "cooldownSeconds", CooldownSeconds, inv);
            MatchDefinition.Put(sb, "retireRecall", DeclaresRetireRecall ? 1 : 0, inv);
            // unit 7b — **기본값이면 한 줄도 안 쓴다**(unit 4 까지의 카드 줄 해시 무변).
            UnitDef.PutBindings(sb, inv, Bindings);
            if (SquadBindings != null && SquadBindings.Length > 0)
            {
                var parts = new string[SquadBindings.Length];
                for (int i = 0; i < parts.Length; i++) parts[i] = SquadBindings[i].ToString(inv);
                MatchDefinition.Put(sb, "squadBindings", string.Join(",", parts));
            }
            if (AttackMods != null)
                for (int i = 0; i < AttackMods.Length; i++)
                    AttackMods[i].Canonicalize(sb, inv, "attackMod" + i.ToString(inv));
            if (ActiveBinding >= 0) MatchDefinition.Put(sb, "active", ActiveBinding, inv);
            if (NeedsTwoCells) MatchDefinition.Put(sb, "twoCells", 1, inv);
            if (TargetsEnemies) MatchDefinition.Put(sb, "targetsEnemies", 1, inv);
            if (Requirement.Kind != AttachRequirementKind.None) Requirement.Canonicalize(sb, inv);
        }
    }

    /// <summary>부착 제한의 종류 — 저작 `DcAttachType` 미러(번호 같음).</summary>
    public enum AttachRequirementKind : byte { None = 0, Class = 1, UnitId = 2 }

    /// <summary>
    /// 부착 제한 한 칸. 옛 저작은 `attachType + attachValue`(문자열 한 칸)였고, 정의표에서는 **해석을 끝낸 값**으로
    /// 싣는다 — 직업은 `UnitDef.Role` 과 같은 int, 유닛은 `UnitDef.Id`(저장용 안정 키, ordinal 비교).
    /// `Invalid` = 저작이 무효(빈 값·모르는 직업) → 어디에도 안 붙는다(옛 fail-closed).
    /// </summary>
    public struct AttachRequirementDef
    {
        public AttachRequirementKind Kind;
        public int Role;
        public string UnitId;
        public bool Invalid;

        internal void Canonicalize(StringBuilder sb, CultureInfo inv)
            => MatchDefinition.Put(sb, "attachReq",
                ((int)Kind).ToString(inv) + "," + Role.ToString(inv) + "," + (UnitId ?? "~") + ","
                + (Invalid ? "1" : "0"));
    }

    // ── 시즌 기믹 ────────────────────────────────────────────────────────────
    //
    // unit 4 는 **고르기만** 했다(`GimmickHost`). unit 6b2 가 그 줄에 **셈판의 수치**를 싣는다.
    //
    // ⚠ 옛 전투는 기믹마다 config 싱글턴 하나를 두고 「존재 = 이 기믹 활성」으로 썼다
    // (`RedBullGimmickConfig` 외 3 + `RequireForUpdate`). 그것은 Burst 가 SO 를 못 만지는 데서 온
    // **기계**라 안 옮긴다 — 값만 여기로 오고, 게이트는 「그 기믹이 뽑혔나」(`GimmickHost`) 하나다.
    //
    // 모양은 `AttackState` 가 `BombSpec`/`SummonSpec` 을 드는 것과 같다: 종류 + 종류별 중첩 구조체,
    // **읽는 쪽이 종류로 고른다.** 종류가 닫힌 집합(시즌 기믹 4)이라 제약 8 에 맞는다.
    //
    // ⚠ 전부 **append-only**. 필드를 더하면 `Canonicalize` 도 같이 고친다(안 고치면 저작을
    // 바꿨는데 `configHash` 가 그대로다).

    public enum GimmickKind : byte
    {
        /// <summary>종류 미상(빌더가 모르는 SO). 셈판이 하나도 안 돈다 — 고르기·알리기만 된다.</summary>
        None = 0,
        /// <summary>「괜찮아. 먹고 달리자!」 — 레드불 픽업 → 라스트런.</summary>
        RedBull = 1,
        /// <summary>「뜨끈하니 좋네요오오.. 뜨겁네?」 — 열기(회복 ↔ 손실 반전).</summary>
        Onsen = 2,
        /// <summary>「불금은 없습니다!」 — 피로 스택 → 번아웃.</summary>
        Burnout = 3,
        /// <summary>「집에 가도 되나요?」 — 사직서 누적 → 임계.</summary>
        ClockOut = 4,
    }

    /// <summary>레드불 → 라스트런. 옛 `RedBullGimmickData` 의 수치.</summary>
    public struct RedBullSpec
    {
        /// <summary>스폰 주기(초). **주기를 도는 자는 unit 7**(Match 호스트 바인딩)이다.</summary>
        public float SpawnInterval;
        /// <summary>안 먹힌 픽업의 수명(초).</summary>
        public float Lifetime;
        /// <summary>판 위 동시 존재 상한. 0 이하 = 스폰 없음(fail-closed).</summary>
        public int MaxActive;
        /// <summary>라스트런 공격속도 배율(곱).</summary>
        public float LastRunAttackSpeedMul;
        /// <summary>라스트런 길이(초). 버프도 이 길이, crash 도 이 뒤다.</summary>
        public float LastRunDuration;
        /// <summary>crash 피해 = 최대 체력 × 이 비율.</summary>
        public float LastRunDamageFraction;

        internal void Canonicalize(StringBuilder sb, CultureInfo inv)
        {
            MatchDefinition.Put(sb, "redbull.spawnInterval", SpawnInterval, inv);
            MatchDefinition.Put(sb, "redbull.lifetime", Lifetime, inv);
            MatchDefinition.Put(sb, "redbull.maxActive", MaxActive, inv);
            MatchDefinition.Put(sb, "redbull.lastRunAttackSpeedMul", LastRunAttackSpeedMul, inv);
            MatchDefinition.Put(sb, "redbull.lastRunDuration", LastRunDuration, inv);
            MatchDefinition.Put(sb, "redbull.lastRunDamageFraction", LastRunDamageFraction, inv);
        }
    }

    /// <summary>온천 열기. 옛 `OnsenGimmickData` 의 수치.</summary>
    public struct OnsenSpec
    {
        /// <summary>열기 누적 주기(초). **주기와 대상 필터는 unit 7**(유닛 호스트 per-unit 타이머).</summary>
        public float HeatInterval;
        /// <summary>이 열기 **이하**는 회복, 초과는 손실(반전).</summary>
        public int FlipThreshold;
        /// <summary>회복량 = 최대 체력 × 이 비율.</summary>
        public float HealPercent;
        /// <summary>손실량 = 최대 체력 × 이 비율(체력 1 바닥 — F10).</summary>
        public float LossPercent;
        /// <summary>열기 카운터 상한.</summary>
        public int HeatMaxStack;

        internal void Canonicalize(StringBuilder sb, CultureInfo inv)
        {
            MatchDefinition.Put(sb, "onsen.heatInterval", HeatInterval, inv);
            MatchDefinition.Put(sb, "onsen.flipThreshold", FlipThreshold, inv);
            MatchDefinition.Put(sb, "onsen.healPercent", HealPercent, inv);
            MatchDefinition.Put(sb, "onsen.lossPercent", LossPercent, inv);
            MatchDefinition.Put(sb, "onsen.heatMaxStack", HeatMaxStack, inv);
        }
    }

    /// <summary>
    /// 번아웃 피로. 옛 `BurnoutGimmickData` 의 수치.
    ///
    /// ⚠ 상한·1회 지속·임계 규칙은 **여기 없다** — 그것은 피로 스택 **저작 자산**의 몫이고
    /// (`StackRuleDef`, F31 「줄의 주인은 저작 자산」), 이 줄은 그 줄 번호만 가리킨다.
    /// 옛 브리지가 SO 부재 때 박던 폴백(`5` · `25f`)은 옮기지 않았다 — 줄이 없으면 6a 의 스택
    /// 폴백(`StackRules.DefaultMaxStack`) 하나로 떨어진다.
    /// </summary>
    public struct BurnoutSpec
    {
        /// <summary>피로 누적 주기(초). **주기와 대상 필터는 unit 7**.</summary>
        public float FatigueInterval;
        /// <summary>주기당 피로 증가량.</summary>
        public int FatigueAmount;
        /// <summary>피로 스택 저작의 `MatchDefinition.StackRules` 줄. -1 = 없음(종류 첫 줄로 폴백).</summary>
        public int FatigueStackRule;

        internal void Canonicalize(StringBuilder sb, CultureInfo inv)
        {
            MatchDefinition.Put(sb, "burnout.fatigueInterval", FatigueInterval, inv);
            MatchDefinition.Put(sb, "burnout.fatigueAmount", FatigueAmount, inv);
            MatchDefinition.Put(sb, "burnout.fatigueStackRule", FatigueStackRule, inv);
        }
    }

    /// <summary>
    /// 사직서 → 운석. 옛 `ClockOutGimmickData` 의 수치.
    ///
    /// ⚠ 운석 **실행**(barrage)은 unit 7 이다 — 이 unit 은 임계 도달을 사건으로만 낸다.
    /// 수치는 지금 싣는다(제약 6 — 판 안의 기믹 수치는 전부 정의표에서 온다). 운석 투사체 저작
    /// (`meteorProjectile`)의 탄 표 편입은 실행이 오는 unit 7 의 것이다.
    /// </summary>
    public struct ClockOutSpec
    {
        /// <summary>임계. 판 위 사직서가 이 수에 닿을 때마다 그만큼 소모된다. 0 이하 = 임계 없음.</summary>
        public int ResignationThreshold;
        public int MeteorCount;
        public float MeteorDamage;
        public int MeteorTileRange;
        public float MeteorWarningSec;
        public float MeteorStaggerSec;

        internal void Canonicalize(StringBuilder sb, CultureInfo inv)
        {
            MatchDefinition.Put(sb, "clockout.resignationThreshold", ResignationThreshold, inv);
            MatchDefinition.Put(sb, "clockout.meteorCount", MeteorCount, inv);
            MatchDefinition.Put(sb, "clockout.meteorDamage", MeteorDamage, inv);
            MatchDefinition.Put(sb, "clockout.meteorTileRange", MeteorTileRange, inv);
            MatchDefinition.Put(sb, "clockout.meteorWarningSec", MeteorWarningSec, inv);
            MatchDefinition.Put(sb, "clockout.meteorStaggerSec", MeteorStaggerSec, inv);
        }
    }

    // 시즌 기믹 한 줄. 고르는 것은 `GimmickHost`, 셈판은 unit 6b2, 무엇이 언제 놓는가는 unit 7.
    public struct GimmickDef
    {
        public string Id;
        public GimmickKind Kind;

        // 종류별 수치. **`Kind` 가 고른 하나만 읽힌다**(나머지는 기본값 그대로 흘러간다).
        public RedBullSpec RedBull;
        public OnsenSpec Onsen;
        public BurnoutSpec Burnout;
        public ClockOutSpec ClockOut;

        internal void Canonicalize(StringBuilder sb, CultureInfo inv)
        {
            MatchDefinition.Put(sb, "id", Id);
            MatchDefinition.Put(sb, "kind", (int)Kind, inv);
            // 읽히는 것만 굽는다 — 안 읽히는 칸이 해시를 움직이면 「아무 규칙도 안 바꿨는데
            // 골든이 빨갛다」가 된다.
            switch (Kind)
            {
                case GimmickKind.RedBull: RedBull.Canonicalize(sb, inv); break;
                case GimmickKind.Onsen: Onsen.Canonicalize(sb, inv); break;
                case GimmickKind.Burnout: Burnout.Canonicalize(sb, inv); break;
                case GimmickKind.ClockOut: ClockOut.Canonicalize(sb, inv); break;
            }
        }
    }
}
