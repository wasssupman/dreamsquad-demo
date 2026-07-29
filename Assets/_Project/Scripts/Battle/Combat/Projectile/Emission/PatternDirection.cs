using Unity.Mathematics;

namespace Wassup.Battle.Combat.Projectile.Emission
{
    // projectile-shot-sequence unit 0 — 패턴의 정규화 방향값을 실제 평면 방향으로
    // 바꾸는 architecture-neutral 순수 함수. Direction binding 은 unit 1에서 소비한다.
    public static class PatternDirection
    {
        public static float2 Resolve(float2 baseDirection, float minAngleDeg,
                                     float maxAngleDeg, float directionT)
        {
            float angleDeg = math.lerp(minAngleDeg, maxAngleDeg, math.saturate(directionT));
            math.sincos(math.radians(angleDeg), out float sin, out float cos);
            return new float2(
                baseDirection.x * cos - baseDirection.y * sin,
                baseDirection.x * sin + baseDirection.y * cos);
        }
    }
}
