using System.Collections.Generic;

namespace Wassup.BattleCore.Effects
{
    // battle-core-rebuild unit 6a — 한 개체에 쌓인 **스택 슬롯들**.
    //
    // ⚠ 병합 키는 **2축**(출처, 종류)이고 **출처 태그를 안 싣는다**(F2). 스탯 슬롯(4축 + 꼬리표)
    // 과의 **의도된 비대칭**이다 — 꼬리표를 실으면 오라가 스택을 집어 간다.
    //
    // ⚠ 줄 번호(`RuleIndex`)는 키가 **아니다.** 같은 출처가 같은 종류를 다른 줄로 다시 걸면
    // 마지막 줄이 이긴다 — 「이 적의 불 스택 규칙은 하나」가 유지된다.

    public struct StackSlot
    {
        public SimEntityId Source;
        public StackKind Kind;

        /// <summary>이 슬롯이 따르는 `MatchDefinition.StackRules` 줄. -1 = 규칙 없음.</summary>
        public int RuleIndex;

        public int Count;
        public int MaxStack;

        /// <summary>마지막으로 임계를 발화시킨 중첩. **올라가는 길에만** 발화하게 하는 캐시.</summary>
        public int LastTriggered;

        /// <summary>남은 시간(초). 매 적용이 1회 지속으로 **갱신**한다(옛 `RefreshAll`).</summary>
        public float Remaining;
    }

    public sealed class StackSet
    {
        private readonly List<StackSlot> _slots = new List<StackSlot>(2);

        public IReadOnlyList<StackSlot> Slots => _slots;

        public int Count => _slots.Count;

        public bool Any => _slots.Count > 0;

        /// <summary>
        /// 스택을 더한다. 반환 = 더한 뒤의 중첩(0 이면 아무 일도 없었다).
        /// 지속은 **덮어쓰기**다(긴 쪽이 아니다 — 옛 `RefreshAll` 규약 그대로).
        /// </summary>
        public int Add(SimEntityId source, StackKind kind, int ruleIndex, int delta,
                       int maxStack, float perAppDuration)
        {
            if (kind == StackKind.None || delta <= 0) return 0;
            int cap = maxStack > 0 ? maxStack : StackRules.DefaultMaxStack;

            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i].Source != source || _slots[i].Kind != kind) continue;

                var slot = _slots[i];
                slot.RuleIndex = ruleIndex;
                slot.MaxStack = cap;
                slot.Count = slot.Count + delta > cap ? cap : slot.Count + delta;
                slot.Remaining = perAppDuration;
                _slots[i] = slot;
                return slot.Count;
            }

            var fresh = new StackSlot
            {
                Source = source,
                Kind = kind,
                RuleIndex = ruleIndex,
                Count = delta > cap ? cap : delta,
                MaxStack = cap,
                LastTriggered = 0,
                Remaining = perAppDuration,
            };
            _slots.Add(fresh);
            return fresh.Count;
        }

        /// <summary>임계 발화 뒤의 상태를 되쓴다. 틱 단계가 소비형 차감까지 계산해 넘긴다.</summary>
        public void Commit(int index, int count, int lastTriggered)
        {
            if (index < 0 || index >= _slots.Count) return;
            var slot = _slots[index];
            slot.Count = count;
            slot.LastTriggered = lastTriggered;
            _slots[index] = slot;
        }

        internal void Tick(int index, float dt)
        {
            var slot = _slots[index];
            slot.Remaining -= dt;
            _slots[index] = slot;
        }

        /// <summary>만료된 슬롯을 삽입 순서를 유지한 채 지운다. 반환 = 지운 수.</summary>
        public int RemoveExpired(List<StackSlot> removed = null)
        {
            int n = 0;
            for (int i = 0; i < _slots.Count;)
            {
                if (_slots[i].Remaining <= 0f)
                {
                    removed?.Add(_slots[i]);
                    _slots.RemoveAt(i);
                    n++;
                }
                else i++;
            }
            return n;
        }

        /// <summary>그 종류의 총 중첩. 오버헤드 아이콘이 읽는 값이다(뷰는 6c).</summary>
        public int CountOf(StackKind kind)
        {
            int total = 0;
            for (int i = 0; i < _slots.Count; i++)
                if (_slots[i].Kind == kind) total += _slots[i].Count;
            return total;
        }

        public void Reset() => _slots.Clear();
    }
}
