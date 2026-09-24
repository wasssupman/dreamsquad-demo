using System.Globalization;
using System.Text;

namespace Wassup.BattleCore
{
    // battle-core-rebuild unit 4 — 드림캐쳐 카드와 기믹의 **정의표 줄**.
    //
    // 이 unit 이 카드에서 갖는 것은 **자원**뿐이다: 큐 · 손패 · 각성 게이지 · 부착 등록부 ·
    // 쿨다운. 「그 카드가 무엇을 하는가」(바인딩·페이로드·발동)는 unit 7 의 것이고, 그래서
    // 여기에 효과 필드가 하나도 없다 — 있으면 다음 사람이 그것을 읽어 실행하려 든다.

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

        internal void Canonicalize(StringBuilder sb, CultureInfo inv)
        {
            MatchDefinition.Put(sb, "id", Id);
            MatchDefinition.Put(sb, "kind", (int)Kind, inv);
            MatchDefinition.Put(sb, "cost", Cost, inv);
            MatchDefinition.Put(sb, "cooldownSeconds", CooldownSeconds, inv);
            MatchDefinition.Put(sb, "retireRecall", DeclaresRetireRecall ? 1 : 0, inv);
        }
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
