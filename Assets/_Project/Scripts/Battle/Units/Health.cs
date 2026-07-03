using Unity.Entities;
using Unity.Mathematics;

namespace Wassup.Battle.Units
{
    public struct Health : IComponentData
    {
        public float value;
        public float max;

        // Normalized HP in [0,1]. max<=0 → 0 (avoids NaN/Inf from div-by-zero).
        // Single definition shared by DamageApplicationSystem (event hpRatio payload)
        // and BattleBridge tint poll. Pure + Burst-safe.
        public static float ComputeRatio(float value, float max)
            => max > 0f ? math.clamp(value / max, 0f, 1f) : 0f;
    }
}
