using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Wassup.Battle.Combat.Projectile
{
    // Movement axis of the projectile pipeline: advances each projectile's position
    // per its MovementKind and flags arrival (ProjectileState.impactReached) once the
    // trajectory reaches its endpoint. Payload resolution is a separate concern —
    // ProjectileHitSystem consumes the flag. Arrival lives here (not in the impact
    // system) because only the trajectory knows its own arrival condition.
    //
    // Target validity is checked through a read-only ComponentLookup<LocalTransform>.
    // EntityManager.Exists() is not Burst-compatible; HasComponent on the lookup
    // serves the same purpose within a Burst-compiled ISystem.
    [BurstCompile]
    [UpdateInGroup(typeof(BattleSimGroup))]
    public partial struct ProjectileMoveSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ProjectileTag>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.Time.DeltaTime;
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var transformLookup = SystemAPI.GetComponentLookup<LocalTransform>(isReadOnly: true);
            // DamageApplicationSystem 은 DeadTag 가 붙은 순간부터 IncomingDamage 를 뽑지
            // 않는다 — 즉 "죽었지만 아직 파괴 전"인 창에 도착한 투사체는 시체에 데미지를
            // append 하고 그대로 증발한다. 재조준은 그 창까지 덮어야 의미가 있다.
            var deadLookup = SystemAPI.GetComponentLookup<Wassup.Battle.Units.DeadTag>(isReadOnly: true);

            // 재조준(retargetTileRange > 0)용 적 후보. 그런 투사체가 하나도 없으면
            // 스냅샷을 만들지 않는다 — 기존 투사체만 나는 프레임의 비용을 0 으로 둔다.
            bool anyRetarget = false;
            foreach (var ps in SystemAPI.Query<RefRO<ProjectileState>>().WithAll<ProjectileTag>())
                // BezierHomingToEntity 는 여기 넣지 않는다: 베지어는 t=elapsed/flightTime
                // 로 진행하므로 t≈1 에서 재조준하면 새 타겟 위치로 **순간이동 후 즉시
                // 착탄**한다(HomingToEntity 는 speed*dt 로 움직여 이런 일이 없다). 곡선을
                // 새 타겟 기준으로 다시 그리려면 제어점 재산출이 필요한데 그 파라미터는
                // SO 에 있어 ISystem 이 못 읽는다 — 재조준 개통은 그 설계와 함께 온다
                // (spec README 후속 후보 "패턴 탄의 재조준/바운스 opt-in").
                if (ps.ValueRO.retargetTileRange > 0
                    && ps.ValueRO.movement == MovementKind.HomingToEntity)
                { anyRetarget = true; break; }

            var retargetEntities = default(NativeArray<Entity>);
            var retargetPositions = default(NativeArray<float3>);
            float tileSize = 1f;
            int2 gridSize = new int2(128, 128);
            float3 ffOrigin = float3.zero;
            if (anyRetarget)
            {
                if (SystemAPI.TryGetSingleton<Wassup.Battle.Effects.FlowFieldSingleton>(out var flowField))
                {
                    tileSize = flowField.tileSize;
                    gridSize = flowField.gridSize;
                    ffOrigin = flowField.origin;
                }
                // AttackUnitTag = 적 전용 태그이자 이 풀의 유일한 진영 필터다
                // (FactionTag 는 디펜더·적·해저드가 전부 갖고 있어 아무것도 안 거른다).
                // ultimate-leap unit 2 — 이탈(판 밖) 중인 적은 재조준 후보가 아니다.
                var q = SystemAPI.QueryBuilder()
                    .WithAll<LocalTransform, Wassup.Battle.Units.AttackUnitTag>()
                    .WithNone<Wassup.Battle.Units.DeadTag>()
                    // goal-tower-siege unit 1 — PastGoal 배제 제거: 골에 붙은 적으로 재조준하는
                    // 것은 낭비가 아니라 정확히 필요한 동작이다.
                    .WithNone<UltimateLeapState>()
                    .Build();
                retargetEntities = q.ToEntityArray(Allocator.Temp);
                var xf = q.ToComponentDataArray<LocalTransform>(Allocator.Temp);
                retargetPositions = new NativeArray<float3>(xf.Length, Allocator.Temp);
                for (int i = 0; i < xf.Length; i++) retargetPositions[i] = xf[i].Position;
                xf.Dispose();
            }

            foreach (var (transform, projectile, entity) in
                     SystemAPI.Query<RefRW<LocalTransform>, RefRW<ProjectileState>>()
                              .WithAll<ProjectileTag>()
                              .WithEntityAccess())
            {
                switch (projectile.ValueRO.movement)
                {
                    case MovementKind.HomingToEntity:
                    {
                        var target = projectile.ValueRO.target;
                        if (target == Entity.Null || !transformLookup.HasComponent(target)
                            || deadLookup.HasComponent(target))
                        {
                            // Target gone (destroyed by DamageApplicationSystem or a
                            // prior hit) — never apply damage to a ghost target.
                            //
                            // 단 retargetTileRange > 0 인 투사체는 파괴 대신 **현재 위치
                            // 기준 반경 안에서 다시 겨눈다**. 니들처럼 N회에 한 번 나오는
                            // 자원은 대상이 먼저 죽으면 그 주기가 통째로 버려지기 때문.
                            // 기존 투사체(화살 등)는 이 값이 0 이라 동작이 그대로다.
                            int rr = projectile.ValueRO.retargetTileRange;
                            int repick = -1;
                            if (rr > 0 && retargetPositions.IsCreated)
                            {
                                float3 here = transform.ValueRO.Position;
                                repick = BounceRetarget.FindNext(
                                    here, -1, retargetPositions, rr, tileSize, gridSize, ffOrigin);
                            }
                            if (repick < 0)
                            {
                                ecb.DestroyEntity(entity);
                                break;
                            }
                            target = retargetEntities[repick];
                            projectile.ValueRW.target = target;
                        }

                        float3 currentPos = transform.ValueRO.Position;
                        float3 targetPos = transformLookup[target].Position;
                        float3 delta = targetPos - currentPos;
                        float dist = math.length(delta);
                        float step = projectile.ValueRO.speed * dt;

                        float3 newPos = dist <= step ? targetPos : currentPos + math.normalize(delta) * step;
                        transform.ValueRW.Position = newPos;

                        // Arrival: within hitThreshold on the XZ plane. Condition moved
                        // verbatim from the legacy ProjectileHitSystem distance check,
                        // evaluated against the post-move position (Hit used to run
                        // after Move, so this is the same value it saw).
                        float dx = targetPos.x - newPos.x;
                        float dz = targetPos.z - newPos.z;
                        float thr = projectile.ValueRO.hitThreshold;
                        if (dx * dx + dz * dz <= thr * thr)
                            projectile.ValueRW.impactReached = true;
                        break;
                    }

                    case MovementKind.BezierHomingToEntity:
                    {
                        // projectile-emission-pattern unit 1 — 곡선 + 추적. 제어점은
                        // 발사 시 고정(드레인 산출), 종점만 타겟을 따라가므로 대상이
                        // 움직이면 곡선이 실시간으로 재조정된다.
                        //
                        // 대상 소실 = 파괴(HomingToEntity 의 기본 동작). **재조준은
                        // 의도적으로 미개통**이다 — 위 사전 스캔 주석 참조(t≈1 순간이동).
                        var bTarget = projectile.ValueRO.target;
                        if (bTarget == Entity.Null || !transformLookup.HasComponent(bTarget)
                            || deadLookup.HasComponent(bTarget))
                        {
                            ecb.DestroyEntity(entity);
                            break;
                        }

                        float bElapsed = projectile.ValueRO.elapsed + dt;
                        projectile.ValueRW.elapsed = bElapsed;
                        float bFlight = projectile.ValueRO.flightTime;
                        float bt = bFlight > 0f ? math.saturate(bElapsed / bFlight) : 1f;

                        float3 bTargetPos = transformLookup[bTarget].Position;
                        // sim 은 XZ 곡선만 굴린다 — 3축의 Y 는 view 공간에서 더한다
                        // (BoardSpace.ToView 가 sim-Y 를 drop, BallisticArc 선례).
                        float3 bNewPos = Bezier3.Position(
                            projectile.ValueRO.origin, projectile.ValueRO.control1,
                            projectile.ValueRO.control2, bTargetPos, bt);
                        transform.ValueRW.Position = bNewPos;

                        // 도착: 곡선 완주 또는 근접(움직이는 대상을 t<1 에 잡는 경우).
                        float bdx = bTargetPos.x - bNewPos.x;
                        float bdz = bTargetPos.z - bNewPos.z;
                        float bthr = projectile.ValueRO.hitThreshold;
                        if (bt >= 1f || bdx * bdx + bdz * bdz <= bthr * bthr)
                            projectile.ValueRW.impactReached = true;
                        break;
                    }

                    case MovementKind.BallisticArcToPoint:
                    {
                        // No target entity: fly a fixed arc to the cell-locked impact
                        // over flightTime. Target death/movement in flight is
                        // irrelevant — impact was locked at fire time.
                        float elapsed = projectile.ValueRO.elapsed + dt;
                        float flightTime = projectile.ValueRO.flightTime;
                        float t = flightTime > 0f ? math.saturate(elapsed / flightTime) : 1f;
                        transform.ValueRW.Position = BallisticArc.ArcPosition(
                            projectile.ValueRO.origin, projectile.ValueRO.impact,
                            projectile.ValueRO.arcHeight, t);
                        projectile.ValueRW.elapsed = elapsed;
                        if (elapsed >= flightTime)
                            projectile.ValueRW.impactReached = true;
                        break;
                    }

                    case MovementKind.SkyFall:
                    {
                        // Sky-fall telegraph (Meteor): sim position holds at the
                        // cell-locked impact for the whole flight — the legacy path
                        // had no sim travel either. Only elapsed advances; the
                        // falling visual is view-space only (presentation layer).
                        float elapsed = projectile.ValueRO.elapsed + dt;
                        projectile.ValueRW.elapsed = elapsed;
                        if (SkyFall.Arrived(elapsed, projectile.ValueRO.flightTime))
                            projectile.ValueRW.impactReached = true;
                        break;
                    }

                    case MovementKind.DirectionalLinear:
                    {
                        // defender-directional-volley unit 2 — straight flight along
                        // the launch direction. No target entity: the shot resolves
                        // in flight (PathHit sweeps prevPos→Position), so the only
                        // endpoint is max range. prevPos is recorded before the step
                        // so the payload gets the exact segment crossed this frame.
                        float3 currentPos = transform.ValueRO.Position;
                        projectile.ValueRW.prevPos = currentPos;

                        float2 dir = projectile.ValueRO.direction;
                        float3 newPos = currentPos + new float3(dir.x, 0f, dir.y) * (projectile.ValueRO.speed * dt);

                        float3 origin = projectile.ValueRO.origin;
                        float maxDistance = projectile.ValueRO.maxDistance;
                        float traveled = math.distance(newPos.xz, origin.xz);
                        if (traveled >= maxDistance)
                        {
                            // Land exactly on the range limit so the final sweep
                            // covers the last tile and no further — overshoot would
                            // hit past the defender's authored range.
                            float2 end = origin.xz + dir * maxDistance;
                            newPos = new float3(end.x, newPos.y, end.y);
                            // Arrival here = "flight ended", not "hit something":
                            // ProjectileHitSystem despawns after resolving this frame.
                            projectile.ValueRW.impactReached = true;
                        }
                        transform.ValueRW.Position = newPos;
                        break;
                    }

                    case MovementKind.GrenadeToCell:
                    {
                        // bomb-thrower-defender unit 1 — roll to the cell-locked
                        // impact over flightTime (request-carried, fixed), then hold
                        // through fuseSec, arriving at flightTime + fuseSec. The roll
                        // reuses BallisticArc.ArcPosition (arcHeight≈0 = ground roll);
                        // at t=1 (fuse) ArcPosition returns impact, so position stays
                        // pinned at the cell while the fuse burns.
                        float elapsed = projectile.ValueRO.elapsed + dt;
                        float flightTime = projectile.ValueRO.flightTime;
                        float t = flightTime > 0f ? math.saturate(elapsed / flightTime) : 1f;
                        transform.ValueRW.Position = BallisticArc.ArcPosition(
                            projectile.ValueRO.origin, projectile.ValueRO.impact,
                            projectile.ValueRO.arcHeight, t);
                        projectile.ValueRW.elapsed = elapsed;
                        // Arrival = travel + fuse both elapsed. HitSystem resolves the
                        // TileAoe payload only once impactReached — it never sees the
                        // fuse (Movement owns the timing, contract 3).
                        if (elapsed >= flightTime + projectile.ValueRO.fuseSec)
                            projectile.ValueRW.impactReached = true;
                        break;
                    }

                    default:
                        // Unhandled movement kind: destroy rather than leak an
                        // immortal entity (no position, no arrival, no resolve). A
                        // visible symptom (the projectile vanishes) beats a silent
                        // leak if a future arm is ever forgotten.
                        ecb.DestroyEntity(entity);
                        break;
                }
            }

            if (retargetEntities.IsCreated) retargetEntities.Dispose();
            if (retargetPositions.IsCreated) retargetPositions.Dispose();

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
