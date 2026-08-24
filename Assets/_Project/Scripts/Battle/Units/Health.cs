using Unity.Entities;
using Unity.Mathematics;

namespace Wassup.Battle.Units
{
    public struct Health : IComponentData
    {
        public float value;
        public float max;

        // season-gimmick-overwork unit 1 — 최대체력 배율 적용의 순수 계산.
        // newMax 는 1 HP 바닥 (max<=0 방지), 축소 시 value 를 클램프,
        // 배율 복원 시 value 를 올리지 않는다 (무료 힐 없음). Pure + Burst-safe.
        // 소비자: MaxHealthScaleSystem (Units — Health.max 의 유일한 런타임 writer).
        // 반환: x = newValue, y = newMax.
        public static float2 ScaleMax(float value, float baseMax, float mul)
        {
            float newMax = math.max(1f, baseMax * mul);
            return new float2(math.min(value, newMax), newMax);
        }

        // Normalized HP in [0,1]. max<=0 → 0 (avoids NaN/Inf from div-by-zero).
        // Single definition shared by DamageApplicationSystem (event hpRatio payload)
        // and BattleBridge tint poll. Pure + Burst-safe.
        public static float ComputeRatio(float value, float max)
            => max > 0f ? math.clamp(value / max, 0f, 1f) : 0f;

        // first-run-tutorial low-health cue — converts a max-HP damage ratio into
        // an IncomingDamage amount. Kept pure so tutorial orchestration does not
        // duplicate health math or reach into ECS-owned Health state directly.
        public static float ComputeMaxHealthDamage(float max, float damageRatio)
            => math.max(0f, max) * math.clamp(damageRatio, 0f, 1f);
    }
}
