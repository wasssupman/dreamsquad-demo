using Unity.Mathematics;

namespace Wassup.Battle.Movement
{
    // enemy-spawn-positioning — 스폰 측면 분산의 순수 수학.
    // 스폰마다 진행방향 수직으로 중앙 기준 ± 작은 오프셋(분율은 결정론 수열 DeterministicFraction, RNG 없음).
    // |오프셋| < 0.5·tileSize 불변식을 여기서 강제한다 → 셀 침범 방지로 WorldToCell/goal/cell-trim 등
    // 셀 단위 시스템이 유닛을 같은 셀로 본다.
    public static class SpawnSpread
    {
        // 분율 절반 폭의 상한(타일폭 비). 0.5 미만이라 어떤 오프셋도 스폰 셀을 벗어나지 않는다.
        public const float MaxHalfFraction = 0.49f;

        // 연속 랜덤 분율의 [min, max] 범위. min=−spreadFraction(하단), max=+spreadFraction·topScale(상단).
        // topScale<1 → 상단(+) 범위만 좁힘(키 큰 캐릭터가 화면 위로 솟는 것 보정). 둘 다 셀 불변식 내로 clamp.
        public static float2 FractionRange(float spreadFraction, float topScale)
        {
            float half = math.clamp(spreadFraction, 0f, MaxHalfFraction);
            return new float2(-half, half * math.saturate(topScale));
        }

        // enemy-tile-movement-integrity unit 0 — 결정론 스폰 분율.
        // 스폰 순번 index 를 golden-ratio(φ⁻¹) Weyl 저불일치 수열로 [min,max] 범위에 매핑.
        // RNG 없이 결정론적이며(같은 index→같은 값) 연속 스폰이 멀리 떨어진 분율을 받아 한 점 겹침을 줄인다.
        public static float DeterministicFraction(int index, float spreadFraction, float topScale)
        {
            float2 range = FractionRange(spreadFraction, topScale);
            float t = math.frac(index * 0.61803398875f); // φ⁻¹ Weyl 수열 → [0,1)
            return math.lerp(range.x, range.y, t);
        }

        // 진행방향(XZ)의 단위 수직벡터. 0 입력은 (1,0) 기준으로 폴백.
        public static float2 Perpendicular(float2 flowDir)
        {
            float2 d = math.normalizesafe(flowDir, new float2(1f, 0f));
            return new float2(-d.y, d.x);
        }

        // 부호화 분율 → 셀 중심에 더할 월드 XZ 오프셋(x,0,z) = 수직단위 · 분율 · tileSize.
        // frac 은 셀 불변식 보장을 위해 ±MaxHalfFraction 으로 clamp(호출측이 범위를 벗어나도 안전).
        public static float3 LateralOffset(float frac, float tileSize, float2 flowDir)
        {
            frac = math.clamp(frac, -MaxHalfFraction, MaxHalfFraction);
            float2 xz = Perpendicular(flowDir) * frac * tileSize;
            return new float3(xz.x, 0f, xz.y);
        }
    }
}
