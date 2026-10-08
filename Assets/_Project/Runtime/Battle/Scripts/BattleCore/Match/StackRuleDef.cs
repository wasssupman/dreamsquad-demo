using System.Globalization;
using System.Text;

namespace Somnia.Battle.BattleCore
{
    // battle-core-rebuild unit 6a — 스택의 저작. 옛 `StackModifierSO` 의 **수치 부분**이다.
    //
    // ⚠ **줄의 주인은 스택 종류가 아니라 저작 자산이다**(F31). 옛 전투는 `StackKind` 당
    // 전역 한 벌이라 드래곤과 킨들러가 불 스택 규칙을 물리적으로 공유했고, 드래곤을 4→10
    // 올렸더니 킨들러가 같이 올라갔다. 여기서는 **자산당 한 줄**이고 부여자가 줄 번호를 싣는다.
    // 이 unit 이 여는 것은 **축**이지 밸런스가 아니다 — 값은 오늘과 같게 저작한다.
    //
    // ⚠ 필드를 더하면 `Canonicalize` 도 같이 고친다.

    public enum StackThresholdMode : byte
    {
        /// <summary>임계에 도달한 그 순간 1회. 스택은 그대로 남는다(**올라가는 길에만** 발화).</summary>
        Edge = 0,
        /// <summary>발화 뒤 `AtStack` 만큼 깎는다. 기준은 **차감된 최종 중첩**에 맞춘다.</summary>
        Consume = 1,
    }

    public enum StackDerivedKind : byte
    {
        /// <summary>지속 피해. `Magnitude` = 틱당 피해(주기 0 이면 DPS), `Duration` = 지속.</summary>
        ApplyDot = 0,
        /// <summary>기절. `Magnitude` 가 곧 지속(초)이고 `Duration` 은 안 읽는다.</summary>
        ApplyStun = 1,
        /// <summary>스탯 모디파이어. `Stat`·`Op`·`Magnitude`·`Duration` 을 쓴다.</summary>
        ApplyStat = 2,
    }

    public struct StackThresholdDef
    {
        public int AtStack;
        public StackThresholdMode Mode;
        public StackDerivedKind Derived;
        public float Magnitude;
        public float Duration;
        /// <summary>`ApplyStat` 전용 — `Effects.StatKind` 의 int 값.</summary>
        public int Stat;
        /// <summary>`ApplyStat` 전용 — `Effects.CombineOp` 의 int 값.</summary>
        public int Op;
        /// <summary>
        /// `ApplyDot` 전용 — 이산 틱 간격(초). 0 이면 연속(`Magnitude` = DPS)이라 피해
        /// 숫자가 초당 수십 번 튄다. &gt;0 이면 **`Magnitude` 는 틱당 피해**로 뜻이 바뀐다.
        /// </summary>
        public float TickInterval;

        internal void Canonicalize(StringBuilder sb, CultureInfo inv, string prefix)
            => MatchDefinition.Put(sb, prefix,
                AtStack.ToString(inv) + ","
                + ((int)Mode).ToString(inv) + ","
                + ((int)Derived).ToString(inv) + ","
                + Magnitude.ToString("R", inv) + ","
                + Duration.ToString("R", inv) + ","
                + Stat.ToString(inv) + ","
                + Op.ToString(inv) + ","
                + TickInterval.ToString("R", inv));
    }

    public struct StackRuleDef
    {
        /// <summary>저작 자산 이름. 어느 줄이 어느 에셋인지를 로그·해시가 말하게 한다.</summary>
        public string Id;

        /// <summary>`Effects.StackKind` 의 int 값.</summary>
        public int Kind;

        /// <summary>최대 중첩. 0 이하는 폴백(`StackRules.DefaultMaxStack`)으로 접힌다.</summary>
        public int MaxStack;

        /// <summary>1회 적용이 주는 지속(초). 매 적용이 이 값으로 **갱신**한다.</summary>
        public float PerAppDuration;

        /// <summary>임계 규칙. **`AtStack` 비내림차순 저작**이고 빌더가 fail-closed 로 검증한다(F13).</summary>
        public StackThresholdDef[] Thresholds;

        public int ThresholdCount => Thresholds != null ? Thresholds.Length : 0;

        internal void Canonicalize(StringBuilder sb, CultureInfo inv)
        {
            MatchDefinition.Put(sb, "id", Id);
            MatchDefinition.Put(sb, "kind", Kind, inv);
            MatchDefinition.Put(sb, "maxStack", MaxStack, inv);
            MatchDefinition.Put(sb, "perAppDuration", PerAppDuration, inv);
            for (int i = 0; i < ThresholdCount; i++)
                Thresholds[i].Canonicalize(sb, inv, "th" + i.ToString(inv));
        }
    }
}
