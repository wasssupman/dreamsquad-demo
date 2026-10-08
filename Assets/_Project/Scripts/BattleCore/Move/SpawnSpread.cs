// salvaged from Assets/_Project/Scripts/Battle/Movement/SpawnSpread.cs (battle-core-rebuild unit 2)
// 이식 시 바뀐 것: 없음(순수 수학).
using Unity.Mathematics;

namespace Somnia.Battle.BattleCore.Move
{
    // 같은 문에서 나와도 겹치지 않게 옆으로 벌린다. **RNG 없는 이산 N-레인 round-robin** 이다 —
    // 같은 index 면 같은 레인이라 결정론이 구조적으로 성립한다.
    //
    // ⚠ **|오프셋| 은 반 칸을 절대 못 넘는다**(M14). 넘으면 옆 칸을 침범해 칸 환산·골 판정·
    // 칸 트림이 유닛을 다른 칸으로 본다.
    public static class SpawnSpread
    {
        /// <summary>분율 절반 폭의 상한(칸 폭 비). 0.5 미만이라 어떤 오프셋도 스폰 칸을 벗어나지 않는다.</summary>
        public const float MaxHalfFraction = 0.49f;

        /// <summary>연속 분율의 [min, max] 범위. `topScale` &lt; 1 이면 위쪽 범위만 좁힌다(키 큰 캐릭터 보정).</summary>
        public static float2 FractionRange(float spreadFraction, float topScale)
        {
            float half = math.clamp(spreadFraction, 0f, MaxHalfFraction);
            return new float2(-half, half * math.saturate(topScale));
        }

        /// <summary>폭 중앙(0) 기준 대칭 N 레인에 스폰 순번을 round-robin 배정. `laneCount` ≤ 1 이면 중앙.</summary>
        public static float LaneFraction(int index, int laneCount, float spreadFraction, float topScale)
        {
            int n = math.max(1, laneCount);
            if (n == 1) return 0f;
            int lane = ((index % n) + n) % n;             // 음수 index 안전
            float s = (lane / (float)(n - 1)) * 2f - 1f;  // −1 … +1 균등
            float half = math.clamp(spreadFraction, 0f, MaxHalfFraction);
            return s >= 0f ? s * half * math.saturate(topScale) : s * half;
        }

        /// <summary>진행방향(XZ)의 단위 수직벡터. 0 입력은 (1,0) 기준으로 폴백.</summary>
        public static float2 Perpendicular(float2 flowDir)
        {
            float2 d = math.normalizesafe(flowDir, new float2(1f, 0f));
            return new float2(-d.y, d.x);
        }

        /// <summary>부호화 분율 → 칸 중심에 더할 월드 XZ 오프셋. 범위는 안에서 clamp 한다(불변식 강제).</summary>
        public static float3 LateralOffset(float frac, float tileSize, float2 flowDir)
        {
            frac = math.clamp(frac, -MaxHalfFraction, MaxHalfFraction);
            float2 xz = Perpendicular(flowDir) * frac * tileSize;
            return new float3(xz.x, 0f, xz.y);
        }
    }
}
