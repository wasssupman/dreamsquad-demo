using System.Collections.Generic;

namespace Wassup.Sim.Units
{
    /// <summary>
    /// battle-sim-extraction unit 18-G/3 — 출처별 실드 슬롯. 구 `ShieldSlot` 이식.
    ///
    /// ⚠ `source` 는 **중첩 키일 뿐 수명 링크가 아니다** — 부여자가 죽어도 남은 실드는 유지된다.
    /// 신 sim 의 `SimEntityId` 는 매치 내 비재사용이라 구 `Entity`(version 포함)가 주던
    /// "재활용 id 와 키 충돌 없음" 성질이 그대로 성립한다.
    ///
    /// 쓰기는 <see cref="DamageApplicationSystem"/> 단독이다.
    /// </summary>
    public struct ShieldSlot
    {
        public SimEntityId source;
        public float value;
    }

    /// <summary>
    /// battle-sim-extraction unit 18-G/3 — 실드 병합·흡수 순수 계산. 구 `ShieldMath` 이식.
    ///
    /// 구 시그니처의 `ref DynamicBuffer&lt;T&gt;` 가 `List&lt;T&gt;` 로 바뀌며 `ref` 가 사라졌다 —
    /// 구 `DynamicBuffer` 는 값 타입 핸들이라 `ref` 가 필요했고 `List` 는 참조 타입이라 아니다.
    /// **동작은 같다**(둘 다 같은 저장소를 제자리 수정한다).
    /// </summary>
    public static class ShieldMath
    {
        /// <summary>
        /// 같은 출처면 `max(잔량, amount)` 로 갱신(중첩 불가), 없으면 새 슬롯을 **뒤에** 붙인다.
        /// ⇒ 리스트 순서 = 부여 순서이고, 그게 <see cref="Absorb"/> 의 소진 순서를 정한다.
        /// </summary>
        public static void Merge(List<ShieldSlot> slots, SimEntityId source, float amount)
        {
            if (amount <= 0f) return;
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i].source != source) continue;
                var slot = slots[i];
                slot.value = SimMath.Max(slot.value, amount);
                slots[i] = slot;
                return;
            }
            slots.Add(new ShieldSlot { source = source, value = amount });
        }

        /// <summary>
        /// 오래된 슬롯(앞)부터 차감하고 소진된 슬롯은 제거한다. 반환값은 **실드를 뚫고 나온
        /// 관통 데미지**다.
        ///
        /// ⚠ 앞에서 지우는 것이 결정론의 근거다 — 삽입 순서가 유지되므로 같은 입력이 같은
        /// 순서로 소진된다. "빈 슬롯을 뒤로 스왑" 같은 최적화는 순서를 흔들어 골든을 가른다.
        /// </summary>
        public static float Absorb(List<ShieldSlot> slots, float damage)
        {
            while (damage > 0f && slots.Count > 0)
            {
                var slot = slots[0];
                if (slot.value > damage)
                {
                    slot.value -= damage;
                    slots[0] = slot;
                    return 0f;
                }
                damage -= slot.value;
                slots.RemoveAt(0);
            }
            return damage;
        }

        /// 유효 실드 합. 표기와 타겟팅 유효HP 가 공유하는 읽기 전용 합산.
        public static float Sum(List<ShieldSlot> slots)
        {
            float total = 0f;
            for (int i = 0; i < slots.Count; i++) total += slots[i].value;
            return total;
        }

        /// <summary>
        /// 특정 출처가 이미 부여한 양(없으면 0). 부여 **성사** 판정용 —
        /// 기존값 &gt;= amount 면 <see cref="Merge"/> 가 max 로 no-op 이라 재부여·VFX 를 스킵해야
        /// 헛발동이 없다.
        /// </summary>
        public static float ValueFromSource(List<ShieldSlot> slots, SimEntityId source)
        {
            for (int i = 0; i < slots.Count; i++)
                if (slots[i].source == source) return slots[i].value;
            return 0f;
        }
    }

    /// <summary>
    /// battle-sim-extraction unit 18-G/3 — 실드 부여 요청 1건. 구 `IncomingShield` 이식.
    /// <see cref="IncomingHeal"/> 동형: 생산자가 붙이고 <see cref="DamageApplicationSystem"/> 이
    /// 매 프레임 병합 후 비운다.
    /// </summary>
    public struct IncomingShield
    {
        public SimEntityId source;
        public float amount;
    }
}
