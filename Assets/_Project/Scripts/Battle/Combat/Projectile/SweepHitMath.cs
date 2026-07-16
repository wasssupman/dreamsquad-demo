using Unity.Mathematics;

namespace Wassup.Battle.Combat.Projectile
{
    // defender-directional-volley unit 0 — path-sweep hit test for the PathHit
    // payload arm, pure and EditMode-pinned (제약 10). Point-vs-segment distance
    // on the sim plane; a zero-length segment (stall frame) degrades to a point
    // test instead of dividing by zero.
    public static class SweepHitMath
    {
        public static bool SegmentHits(float2 prevPos, float2 currPos, float2 targetPos, float hitRadius)
        {
            float2 seg = currPos - prevPos;
            float lenSq = math.lengthsq(seg);
            float t = lenSq < 1e-8f ? 0f : math.saturate(math.dot(targetPos - prevPos, seg) / lenSq);
            float2 closest = prevPos + seg * t;
            return math.distancesq(targetPos, closest) <= hitRadius * hitRadius;
        }
    }
}
