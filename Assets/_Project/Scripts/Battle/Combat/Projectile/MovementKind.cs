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
        // 산출), 종점 P3 는 타겟의 live 위치라 매 프레임 곡선이 갱신된다. **대상 소실 =
        // 파괴이며 재조준은 미지원**이다 — t=elapsed/flightTime 로 진행하므로 t≈1 에서
        // 재조준하면 새 타겟으로 순간이동 후 즉시 착탄한다(곡선을 다시 그리려면 SO
        // 파라미터가 필요한데 ISystem 이 못 읽는다).
        // sim 은 XZ 곡선만 굴리고 3축의 Y 는 view 공간에서 더한다(BoardSpace 가
        // sim-Y 를 drop 하므로 — BallisticArc 선례).
        BezierHomingToEntity = 5,

        // dreamcatcher-content-4 unit 1 — 한 점을 도는 원운동(궤도 화염구).
        // **대상 엔티티를 참조하지 않는다** — BallisticArcToPoint 계열이다. 방어유닛은 타일
        // 고정이라 궤도 중심이 발사 시점 고정점이면 충분하고, 덕분에 host 가 죽거나 퇴근해도
        // 이미 나간 화염구는 자기 수명을 산다.
        // 필드 재사용: origin = 궤도 중심 · maxDistance = 반경(월드) · speed = **각속도(rad/s)**
        // · flightTime/elapsed = 지속/누적 · prevPos = 직전 위치(PathHit 스윕) ·
        // direction = 접선(front-most 정렬용).
        // 도착 = `elapsed >= flightTime` → impactReached. ⚠ PathHit 에게 그 플래그는 "착탄"이
        // 아니라 **"비행 종료"**(최종 스윕 후 소멸)다 — DirectionalLinear 과 공유하는 규약.
        // sim 은 XZ 평면만 돌고 높이는 뷰가 더한다(BoardSpace 가 sim-Y 를 drop).
        OrbitAroundPoint = 6,
    }
}
