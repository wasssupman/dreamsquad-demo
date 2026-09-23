namespace Wassup.BattleCore.Effects
{
    // battle-core-rebuild unit 6a — 스택 저작을 읽는 **순수 함수들**.
    //
    // 임계 발화 자체(사건 발행·파생 효과 적용)는 틱 단계가 한다 — 여기는 「어느 줄인가」와
    // 「이 임계가 이번에 발화하나」만 답한다. 그래야 EditMode 가 규칙을 통째로 고정한다.
    public static class StackRules
    {
        /// <summary>
        /// 미등록 스택의 최대치(F14). 여러 생산자가 같은 기본값을 복사해 쓰던 것을 한 곳으로
        /// 수렴시킨 값이고, 폴백이 없으면 미등록 스택이 무한히 증가한다.
        /// </summary>
        public const int DefaultMaxStack = 5;

        /// <summary>
        /// 부여자가 실은 줄 번호를 실제 줄로 푼다. 반환 = 줄 인덱스(없으면 -1).
        ///
        /// 규칙: 번호가 범위 안이고 **그 줄의 종류가 맞으면** 그 줄. 아니면 그 종류의 **첫 줄**.
        /// ⚠ 미지정(-1)과 기본값 0 이 같은 답을 내는 것은 우연이 아니다 — 0 번 줄의 종류가
        /// 맞으면 그것이 곧 「그 종류의 첫 줄」이고, 아니면 폴백으로 떨어진다. 그래서
        /// `default(AttackOutputDef)` 가 조용히 남의 줄을 밟지 않는다.
        /// </summary>
        public static int Resolve(StackRuleDef[] rules, StackKind kind, int index)
        {
            if (rules == null || rules.Length == 0) return -1;
            if (index >= 0 && index < rules.Length && rules[index].Kind == (int)kind) return index;
            for (int i = 0; i < rules.Length; i++)
                if (rules[i].Kind == (int)kind) return i;
            return -1;
        }

        /// <summary>그 줄(없으면 폴백)의 최대 중첩.</summary>
        public static int MaxStackOf(StackRuleDef[] rules, int ruleIndex)
        {
            if (rules == null || ruleIndex < 0 || ruleIndex >= rules.Length) return DefaultMaxStack;
            int max = rules[ruleIndex].MaxStack;
            return max > 0 ? max : DefaultMaxStack;
        }

        /// <summary>그 줄(없으면 0)의 1회 지속.</summary>
        public static float PerAppDurationOf(StackRuleDef[] rules, int ruleIndex)
            => rules != null && ruleIndex >= 0 && ruleIndex < rules.Length
                ? rules[ruleIndex].PerAppDuration : 0f;

        /// <summary>
        /// 이 임계가 이번 변화에서 발화하나. **올라가는 길에만** 발화한다 —
        /// `prevStack &lt; AtStack ≤ stackCount`.
        /// </summary>
        public static bool Fires(in StackThresholdDef rule, int prevStack, int stackCount)
            => rule.AtStack > prevStack && rule.AtStack <= stackCount;

        /// <summary>소비형이 발화한 뒤의 중첩. 0 아래로는 안 내려간다.</summary>
        public static int AfterConsume(in StackThresholdDef rule, int stackCount)
            => rule.Mode == StackThresholdMode.Consume
                ? (stackCount - rule.AtStack > 0 ? stackCount - rule.AtStack : 0)
                : stackCount;

        /// <summary>
        /// 임계 배열이 **비내림차순**인가(F13 — 옛 fail-silent 가정을 fail-closed 로 승격).
        ///
        /// ⚠ **같은 임계를 여러 줄 쓰는 것은 정상이다.** 라이브 피로도 저작이 5·5·5 로
        /// 세 스탯을 한꺼번에 건다 — 그래서 「비내림차순」이지 「엄격 증가」가 아니다.
        /// </summary>
        public static bool IsAscending(StackThresholdDef[] thresholds)
        {
            if (thresholds == null) return true;
            for (int i = 1; i < thresholds.Length; i++)
                if (thresholds[i].AtStack < thresholds[i - 1].AtStack) return false;
            return true;
        }
    }
}
