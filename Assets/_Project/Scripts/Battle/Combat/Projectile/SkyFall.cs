using Unity.Mathematics;

namespace Wassup.Battle.Combat.Projectile
{
    // Pure progress math for MovementKind.SkyFall. The sim position holds at the
    // cell-locked impact; only elapsed advances. Progress feeds the view-space
    // falling visual (unit 9) — like BallisticArc, height is presentation-side
    // because BoardSpace.ToView drops sim Y. Burst-compatible, EditMode-testable.
    public static class SkyFall
    {
        // t in [0,1]. Non-positive flightTime → 1 (arrive immediately, matching
        // the legacy warningSec=0 "resolve on first tick" behavior).
        public static float Progress(float elapsed, float flightTime)
            => flightTime > 0f ? math.saturate(elapsed / flightTime) : 1f;

        // Arrival condition owned by the trajectory (spec contract 3).
        public static bool Arrived(float elapsed, float flightTime)
            => elapsed >= flightTime;
    }
}
