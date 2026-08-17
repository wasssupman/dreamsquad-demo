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
        // **위치 계산에 대상 엔티티를 참조하지 않는다** — 중심은 발사 시점 고정점이다
        // (방어유닛은 타일 고정이라 그것으로 충분하다).
        //
        // ⚠ **단 주인이 사라지면 구슬도 사라진다**(content-5, 2026-08-17 사용자 결정).
        // content-4 는 반대로 «host 가 죽거나 퇴근해도 이미 나간 화염구는 자기 수명을 산다»
        // 를 계약으로 적었는데, 화면에서는 **주인 없는 빈 자리에서 혼자 도는 구슬**이었다.
        // 궤도는 «누구 주위를 돈다» 가 정의라 주인이 없으면 의미가 없다 — 다른 궤적
        // (직선·왕복·호밍)은 던지면 제 갈 길을 가는 것이 사양이므로 이 규칙을 공유하지 않는다.
        // 판정은 `owner` 생존이며 ProjectileMoveSystem 의 이 arm 이 소유한다.
        // 필드 재사용: origin = 궤도 중심 · maxDistance = 반경(월드) · speed = **각속도(rad/s)**
        // · flightTime/elapsed = 지속/누적 · prevPos = 직전 위치(PathHit 스윕) ·
        // direction = 접선(front-most 정렬용).
        // 도착 = `elapsed >= flightTime` → impactReached. ⚠ PathHit 에게 그 플래그는 "착탄"이
        // 아니라 **"비행 종료"**(최종 스윕 후 소멸)다 — DirectionalLinear 과 공유하는 규약.
        // sim 은 XZ 평면만 돌고 높이는 뷰가 더한다(BoardSpace 가 sim-Y 를 drop).
        OrbitAroundPoint = 6,

        // dreamcatcher-content-5 unit 1 — 발사 축을 따라 나갔다 **돌아오는** 직선 왕복.
        // OrbitAroundPoint 과 마찬가지로 **대상 엔티티를 참조하지 않는다**(발사점이 고정점).
        // 필드 재사용: origin = 발사점(= 귀환점) · direction = **발사 축(불변)** ·
        // maxDistance = 편도 거리(월드) · speed = 선속도 · elapsed = 누적 ·
        // prevPos = 직전 위치(PathHit 스윕).
        //
        // ⚠ **`direction` 을 되먹이지 말 것.** 이 궤적에서 그 필드는 위치 계산의 **입력**이라,
        // 「지금 돌아오는 중이니 뒤집자」로 매 프레임 갱신하면 다음 프레임이
        // `origin − axis*(…)` 를 내고 **발사점 뒤로 날아간다**(초판 설계의 실제 결함).
        // 궤도가 같은 함정을 피한 것은 거기서 direction 이 접선 = **파생값**이었기 때문이다.
        // 「지금 어느 다리인가」는 어디에도 저장하지 않는다 — 진행 방향이 필요한 곳(넉백)은
        // 그 프레임 스윕 `pos − prevPos` 를, 화면 facing 은 뷰의 직전 위치 차이를 쓴다.
        //
        // 도착 = `speed*elapsed >= 2*maxDistance`(왕복 완료) → impactReached.
        // PathHit 에게 그 플래그는 "착탄"이 아니라 **"비행 종료"**(최종 스윕 후 소멸)다 —
        // DirectionalLinear·OrbitAroundPoint 과 공유하는 규약.
        BoomerangReturn = 7,

        // on-place-skill-rework unit 10 — 하늘에서 떨어지지만 **적을 겨누는** 낙하탄.
        //
        // `SkyFall`(위)과 같은 그림, **다른 조준**이다. 저쪽은 셀 바인딩이라 발사 시점의 칸에
        // 위치를 고정하고 다시 조준하지 않는다(예고가 움직이면 안 되므로 의도된 설계). 이쪽은
        // **엔티티 바인딩**이라 위치가 임자의 live 위치를 따른다 — `HomingToEntity` 가 그러는
        // 것과 같은 이유이며, 예외가 아니라 **바인딩의 정의**다.
        //
        // ⚠ **이 축이 없어서 났던 사고를 다시 만들지 말 것.** unit 1·8 은 「반경 안 적 전원에게
        // 1발씩」을 셀 바인딩 궤적으로 표현하려 했다. unit 1 은 칸당 1발로 접어 발수가 적 수와
        // 어긋났고, unit 8 은 요청에 `target` 을 실어 `TileAoe` 팔이 임자만 고르게 했다 — 그러자
        // **한 탄에 조준이 둘**(궤적=칸/발사시점, 페이로드=적/착탄시점)이 되어 예고 시간만큼
        // 어긋났다. 실측: 예고 0.40s × 적 속도 2.00 = **0.80타일** 이동인데 칸 소속 유지 폭은
        // 중심 ±0.50타일 → 최소 예고에도 벗어나 **피해 0**. 뒤 슬롯(0.72s=1.44타일)은 전원 헛방.
        // unit 8 이전엔 `target` 이 비어 그 칸에 **누가 있든** 때려서 행군하는 뒤 적이 빈 칸을
        // 채웠고, 조준이 낡았다는 사실이 그렇게 가려져 있었다.
        //
        // 도착 = `SkyFall.Arrived(elapsed, flightTime)` — 시간 도착이 곧 **예고**의 정의다.
        // 필드 재사용은 `SkyFall` 과 같다: arcHeight = 낙하 시작 높이(view 전용) ·
        // flightTime/elapsed = 예고/누적. 페이로드는 `SingleSplash`(대상 하나) 짝이다 —
        // tile 판정도 임자 게이트도 타지 않는다.
        SkyFallOnEntity = 8,
    }
}
