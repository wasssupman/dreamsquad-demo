using Unity.Mathematics;

namespace Wassup.Battle.Effects
{
    // season-gimmick-onsen unit 1 [중립] — 온천 "열기" 회복↔손실 반전 산식.
    // 아키텍처 무관 순수 함수: plain 값 in → 부호 있는 HP 델타 out. ECS/Mono 어느 쪽도
    // 모른다(제약 10 seam, ModifierMath 모범 동형). 호출 측은 부호만 보고 힐/피해 채널로 라우팅.
    public static class HeatMath
    {
        // stacks ≤ flipThreshold → 회복(≥0, 오버힐 잘라 실제 증가량), 초과 → 손실(≤0, HP 1 바닥).
        // 반환: >0 회복 · <0 피해 · 0 no-op.
        public static float Delta(int stacks, int flipThreshold, float maxHp, float currentHp, float healPercent, float lossPercent)
        {
            if (stacks <= flipThreshold)
            {
                // 회복: 최대체력 초과분 제거(오버힐 없음 → 만피 유닛 VFX 스팸 방지).
                float headroom = math.max(0f, maxHp - currentHp);
                return math.min(maxHp * healPercent, headroom);
            }

            // 과열(반전): HP 1 밑으로는 안 내림 — 열기는 사망 원인이 될 수 없다.
            float floorRoom = math.max(0f, currentHp - 1f);
            return -math.min(maxHp * lossPercent, floorRoom);
        }
    }
}
