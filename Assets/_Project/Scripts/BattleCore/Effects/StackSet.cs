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

    //
    // unit 6b2 — **열기(온천)도 여기 산다**(`Heat`). 슬롯이 아니라 카운터 하나인 이유:
    //   ① 열기에는 출처 축도 지속도 임계 파생도 없다 — 효과는 누적마다 `HeatMath` 가 내고
    //      상한에서 멈출 뿐이다(옛 `HeatAccrual.stacks` 가 그랬다).
    //   ② `StackKind` 에 값을 따지 않았다 — 그 enum 은 `Wassup.Skills.SkillStackKind` 와 **개수까지**
    //      핀으로 묶여 있고(`CoreSkillEnumPinTests`), 저쪽은 다시 동결된 옛 전투 enum 과 묶여 있다.
    //      열기 하나를 위해 동결 코드의 핀을 풀 수 없다.
    public sealed class StackSet
    {
        private readonly List<StackSlot> _slots = new List<StackSlot>(2);

        /// <summary>열기 중첩(온천). 0 = 아직 한 번도 안 쌓였다. 상한은 저작(`OnsenSpec.HeatMaxStack`).</summary>
        public int Heat { get; private set; }

        /// <summary>열기 +1(상한에서 멈춘다). 반환 = 더한 뒤의 열기. 상한 0 이하는 폴백 없이 0 에 머문다.</summary>
        public int AddHeat(int maxStack)
        {
            if (Heat < maxStack) Heat++;
            return Heat;
        }

        /// <summary>디버그 전용(`DebugSetStack`) — 열기를 그 값으로 놓는다.</summary>
        public void SetHeat(int value) => Heat = value > 0 ? value : 0;

        /// <summary>그 (출처, 종류) 슬롯의 인덱스. 없으면 -1.</summary>
        public int IndexOf(SimEntityId source, StackKind kind)
        {
            for (int i = 0; i < _slots.Count; i++)
                if (_slots[i].Source == source && _slots[i].Kind == kind) return i;
            return -1;
        }

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

        public void Reset()
        {
            _slots.Clear();
            Heat = 0;
        }
    }
}
