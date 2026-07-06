namespace Wassup.Battle.Combat.Projectile
{
    // Trajectory axis of a projectile (orthogonal to PayloadKind). Selects how the
    // projectile's position evolves each frame and when it counts as "arrived".
    // Default (0) is HomingToEntity so existing spawns keep the legacy homing
    // behavior with no code change. Adding a new trajectory (e.g. BezierToPoint)
    // is a new enum case + a position pure-function + one MoveSystem switch arm —
    // no new system/drain/tag.
    public enum MovementKind : byte
    {
        // Track a target entity's live position; arrive when within hitThreshold.
        // Destroys if the target is gone (legacy projectile behavior).
        HomingToEntity = 0,

        // Lerp XZ from origin to a cell-locked impact point with a sine arc in Y;
        // arrive when elapsed >= flightTime. No target entity — impact is fixed at
        // fire time, so target death/movement in flight is irrelevant.
        BallisticArcToPoint = 1,
    }
}
