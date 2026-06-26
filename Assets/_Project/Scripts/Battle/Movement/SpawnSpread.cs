using Unity.Mathematics;

namespace Wassup.Battle.Movement
{
    // enemy-spawn-positioning 1 — 스폰 측면 분산의 순수 수학.
    // 슬롯(상/중/하…) → 진행방향 수직 sub-cell 오프셋. |오프셋| < 0.5·tileSize 불변식을 여기서 강제한다
    // (셀 침범 방지 → WorldToCell/goal/cell-trim 등 셀 단위 시스템이 유닛을 같은 셀로 본다).
    public static class SpawnSpread
    {
        // 슬롯 절반 폭의 상한(타일폭 비). 0.5 미만이라 어떤 슬롯도 스폰 셀을 벗어나지 않는다.
        public const float MaxHalfFraction = 0.49f;

        // 슬롯 인덱스 → [-half, +half] 부호화 분율. slotCount<=1 → 0(중앙).
        public static float SlotFraction(int slotIndex, int slotCount, float spreadFraction)
        {
            if (slotCount <= 1) return 0f;
            float half = math.clamp(spreadFraction, 0f, MaxHalfFraction);
            float t = (float)slotIndex / (slotCount - 1); // 0..1
            return math.lerp(-half, half, t);
        }

        // 진행방향(XZ)의 단위 수직벡터. 0 입력은 (1,0) 기준으로 폴백.
        public static float2 Perpendicular(float2 flowDir)
        {
            float2 d = math.normalizesafe(flowDir, new float2(1f, 0f));
            return new float2(-d.y, d.x);
        }

        // 셀 중심에 더할 월드 XZ 오프셋(x,0,z) = 수직단위 · 분율 · tileSize.
        public static float3 LateralOffset(int slotIndex, int slotCount, float spreadFraction, float tileSize, float2 flowDir)
        {
            float frac = SlotFraction(slotIndex, slotCount, spreadFraction);
            float2 xz = Perpendicular(flowDir) * frac * tileSize;
            return new float3(xz.x, 0f, xz.y);
        }
    }

    // 슬롯 배정 정책. Sequential=lane별 순차 round-robin, Random=map seed 결정론 랜덤.
    public enum SpawnSpreadMode { Sequential, Random }
}
