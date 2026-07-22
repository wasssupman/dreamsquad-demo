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

        // Hold at the cell-locked impact for flightTime, then arrive (Meteor
        // telegraph semantics: warningSec → flightTime). The sim position never
        // travels — the falling visual is view-space only, added by the
        // presentation layer. flightTime is request-carried, not speed-derived
        // (zero travel distance).
        SkyFall = 2,

        // Fly a straight line along a fire-time direction for maxDistance, then
        // despawn. No target entity and no point arrival — hits happen in flight
        // via the PathHit payload sweep (defender-directional-volley unit 1;
        // move arm lands in unit 2).
        DirectionalLinear = 3,

        // bomb-thrower-defender unit 1 — roll to a cell-locked impact over a
        // request-carried travelSec (flightTime, fixed — not speed-derived), hold
        // at the cell through fuseSec, then arrive (impactReached) at
        // flightTime + fuseSec. Reuses BallisticArc.ArcPosition for the roll
        // (arcHeight≈0 = ground roll); resolves as TileAoe at arrival.
        GrenadeToCell = 4,
    }
}
