using Unity.Entities;
using Unity.Mathematics;

namespace Wassup.Battle.Units
{
    // shield-guardian-defender unit 0 — 출처별 실드 슬롯 (Units 소유).
    // 같은 출처 재부여 = max(잔량, B) 중첩 불가, 서로 다른 출처는 슬롯 추가(합산).
    // source 는 중첩 키일 뿐 수명 링크 아님 — 부여자 사망에도 잔여 실드 유지
    // (Entity 는 version 포함이라 재활용 id 와 키 충돌 없음).
    // 쓰기는 DamageApplicationSystem 단독 (spec 계약 1).
    public struct ShieldSlot : IBufferElementData
    {
        public Entity source;
        public float value;
    }

    // 병합·흡수 순수 계산 — sim-critical, EditMode 검증 대상 (제약 10).
    public static class ShieldMath
    {
        // 같은 출처 슬롯이 있으면 max 갱신, 없으면 새 슬롯. 삽입 순서 = 부여 순서.
        public static void Merge(ref DynamicBuffer<ShieldSlot> slots, Entity source, float amount)
        {
            if (amount <= 0f) return;
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i].source != source) continue;
                var slot = slots[i];
                slot.value = math.max(slot.value, amount);
                slots[i] = slot;
                return;
            }
            slots.Add(new ShieldSlot { source = source, value = amount });
        }

        // 오래된 슬롯(앞)부터 차감, 소진 슬롯은 제거(RemoveAt — 삽입 순서 유지, 결정론).
        // 반환 = 실드를 뚫고 나온 관통 데미지.
        public static float Absorb(ref DynamicBuffer<ShieldSlot> slots, float damage)
        {
            while (damage > 0f && slots.Length > 0)
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

        // 유효 실드 합 — 표기(unit 2)·타겟팅 유효HP(unit 1)가 공유하는 read-only 합산.
        public static float Sum(in DynamicBuffer<ShieldSlot> slots)
        {
            float total = 0f;
            for (int i = 0; i < slots.Length; i++) total += slots[i].value;
            return total;
        }

        // 특정 출처가 이 대상에 이미 부여한 실드량(없으면 0). 부여 성사 판정용(unit 4):
        // 기존값 >= amount 면 Merge 가 max 로 no-op → 재부여/VFX 를 스킵해야 헛발동이 없다.
        public static float ValueFromSource(in DynamicBuffer<ShieldSlot> slots, Entity source)
        {
            for (int i = 0; i < slots.Length; i++)
                if (slots[i].source == source) return slots[i].value;
            return 0f;
        }
    }
}
