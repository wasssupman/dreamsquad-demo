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

        // projectile-emission-pattern unit 1 — 곡선으로 날면서 타겟을 추적한다.
        // HomingToEntity(직진·추적)와 BallisticArcToPoint(곡선·셀고정)가 배타적이라
        // 표현 불가였던 조합. 제어점 2개는 발사 시 결정론 생성(드레인이 SO 파라미터로
        // 산출), 종점 P3 는 타겟의 live 위치라 매 프레임 곡선이 갱신된다. 대상 소실
        // 정책은 HomingToEntity 상속(retargetTileRange 있으면 재조준, 없으면 파괴).
        // sim 은 XZ 곡선만 굴리고 3축의 Y 는 view 공간에서 더한다(BoardSpace 가
        // sim-Y 를 drop 하므로 — BallisticArc 선례).
        BezierHomingToEntity = 5,
    }
}
