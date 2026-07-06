using Unity.Mathematics;

namespace Wassup.Battle.Combat.Projectile
{
    // Pure trajectory math for MovementKind.BallisticArcToPoint. XZ (and the linear
    // Y) interpolate from origin to the cell-locked impact; a sine bump is added on
    // top of Y so the shell rises and falls. Burst-compatible, EditMode-testable.
    public static class BallisticArc
    {
        // t in [0,1]. At t=0 → origin, t=1 → impact (the arc term is 0 at both ends);
        // t=0.5 → apex (linear midpoint + arcHeight).
        // Visual arc height above the board plane at progress t (sine bump: 0 at both
        // ends, apex = arcHeight at t=0.5). The presentation layer adds this in VIEW
        // space because BoardSpace.ToView drops sim Y on the flat tilemap board — a
        // bump baked into sim Y is invisible.
        public static float ArcHeight(float arcHeight, float t)
            => math.sin(t * math.PI) * arcHeight;

        public static float3 ArcPosition(float3 origin, float3 impact, float arcHeight, float t)
        {
            float3 p = math.lerp(origin, impact, t);
            p.y += ArcHeight(arcHeight, t);
            return p;
        }

        // Flight time derived from horizontal (XZ) distance and speed, floored at
        // minTime so a point-blank shot still arcs visibly instead of resolving on
        // the first frame. speed <= 0 falls back to minTime.
        public static float FlightTime(float3 origin, float3 impact, float speed, float minTime)
        {
            float dx = impact.x - origin.x;
            float dz = impact.z - origin.z;
            float dist = math.sqrt(dx * dx + dz * dz);
            float t = speed > 0f ? dist / speed : minTime;
            return math.max(t, minTime);
        }
    }
}
