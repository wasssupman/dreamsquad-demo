// salvaged from Assets/_Project/Scripts/Battle/Effects/HeatMath.cs (battle-core-rebuild unit 6b2)
// 이식 시 바뀐 것: 네임스페이스만(`Somnia.Battle.Battle.Effects` → `Somnia.Battle.BattleCore.Effects`). 산식은 **그대로**다.
using Unity.Mathematics;

namespace Somnia.Battle.BattleCore.Effects
{
    // 온천 "열기" 회복 ↔ 손실 반전 산식(F10).
    // 아키텍처 무관 순수 함수: plain 값 in → 부호 있는 체력 델타 out. 호출 측은 **부호만 보고**
    // 회복/피해 인박스로 라우팅한다(제약 10 — `ModifierMath` 모범과 같은 형).
    public static class HeatMath
    {
        // stacks ≤ flipThreshold → 회복(≥0, 오버힐 잘라 실제 증가량), 초과 → 손실(≤0, 체력 1 바닥).
        // 반환: >0 회복 · <0 피해 · 0 무동작.
        public static float Delta(int stacks, int flipThreshold, float maxHp, float currentHp, float healPercent, float lossPercent)
        {
            if (stacks <= flipThreshold)
            {
                // 회복: 최대 체력 초과분 제거(오버힐 없음 → 만피 유닛 VFX 스팸 방지).
                float headroom = math.max(0f, maxHp - currentHp);
                return math.min(maxHp * healPercent, headroom);
            }

            // 과열(반전): 체력 1 밑으로는 안 내림 — 열기는 사망 원인이 될 수 없다.
            float floorRoom = math.max(0f, currentHp - 1f);
            return -math.min(maxHp * lossPercent, floorRoom);
        }
    }
}
