using System.Collections.Generic;
using Wassup.Sim.Effects;
using Wassup.Sim.Movement;
using Wassup.Sim.Units;

namespace Wassup.Sim.Combat
{
    /// <summary>
    /// battle-sim-extraction unit 18-H/2 — 캡처 #26(P6). 구 `ProjectileMoveSystem` 이식.
    ///
    /// 궤적 축 전담: `MovementKind` 마다 위치를 전진시키고 끝점에 닿으면
    /// <see cref="ProjectileState.impactReached"/> 를 세운다. **착탄 해결은 여기 없다** —
    /// 도착 조건은 궤적만 알기 때문에 플래그를 세우는 쪽이 이동이고, 그 플래그를 읽는 쪽이
    /// `ProjectileHitSystem`(#27)이다.
    ///
    /// ⚠ **투사체 파괴는 #41 을 거치지 않는다.** 사망 릴레이는 `DeadTag` 를 가진 **유닛**의
    /// 계약이고, 투사체는 수명 만료 계열처럼 자기 자리에서 즉시 파괴된다(해저드 수명 선례).
    ///
    /// ⚠ **P6 라서 이동(P4)보다 뒤다** — 호밍이 읽는 대상 위치가 **이번 틱에 걸은 뒤**의
    /// 최신 위치라는 뜻이고, 그게 구 sim 의 순서다.
    /// </summary>
    public sealed class ProjectileMoveSystem
    {
        private readonly SimCommandBuffer _ecb = new SimCommandBuffer();
        private readonly List<SimEntityId> _retargetEntities = new List<SimEntityId>();
        private readonly List<SimVec3> _retargetPositions = new List<SimVec3>();

        public void Run(SimWorld world)
        {
            float dt = world.DeltaTime;

            // ── 재조준 후보 스냅샷 (필요할 때만) ────────────────────────────
            // ⚠ `BezierHomingToEntity` 는 **의도적으로 제외**한다. 베지어는 t = elapsed/flightTime
            //   으로 진행하므로 t≈1 에서 재조준하면 새 대상 위치로 **순간이동 후 즉시 착탄**한다
            //   (호밍은 speed·dt 로 걸어서 그런 일이 없다). 곡선을 다시 그리려면 저작 파라미터가
            //   필요한데 sim 은 그걸 모른다 — 재조준 개통은 그 설계와 함께 온다.
            bool anyRetarget = false;
            foreach (var e in world.With<ProjectileTag>())
            {
                if (!world.TryGet<ProjectileState>(e, out var ps)) continue;
                if (ps.retargetTileRange > 0 && ps.movement == MovementKind.HomingToEntity)
                {
                    anyRetarget = true;
                    break;
                }
            }

            _retargetEntities.Clear();
            _retargetPositions.Clear();
            float tileSize = 1f;
            var gridSize = new SimInt2(128, 128);
            SimVec3 ffOrigin = default;
            if (anyRetarget)
            {
                if (SimSingleton.TryGet<FlowFieldSingleton>(world, out var flowField))
                {
                    tileSize = flowField.tileSize;
                    gridSize = flowField.gridSize;
                    ffOrigin = flowField.origin;
                }
                // ⚠ `AttackUnitTag` 이 이 풀의 **유일한 진영 필터**다 — `FactionTag` 는 디펜더·적·
                //   해저드가 전부 갖고 있어 아무것도 거르지 못한다.
                foreach (var e in world.With<AttackUnitTag>())
                {
                    if (world.Has<DeadTag>(e)) continue;
                    if (world.Has<PastGoalTag>(e)) continue;
                    if (world.Has<UltimateLeapState>(e)) continue; // 판 밖은 후보가 아니다
                    if (!world.TryGet<SimTransform>(e, out var xf)) continue;
                    _retargetEntities.Add(e);
                    _retargetPositions.Add(xf.Position);
                }
            }

            foreach (var entity in world.With<ProjectileTag>())
            {
                if (!world.TryGet<ProjectileState>(entity, out var projectile)) continue;
                if (!world.TryGet<SimTransform>(entity, out var transform)) continue;

                switch (projectile.movement)
                {
                    case MovementKind.HomingToEntity:
                    {
                        var target = projectile.target;
                        if (!IsLiveTarget(world, target))
                        {
                            // ⚠ **유령 대상에 피해를 주지 않는다.** 죽은 대상은
                            //   `DamageApplicationSystem` 이 버퍼를 뽑지 않으므로 "죽었지만 아직
                            //   파괴 전" 창에 도착한 투사체는 시체에 피해를 얹고 증발한다 —
                            //   그래서 유효성 판정이 `DeadTag` 까지 덮는다.
                            //
                            // 단 `retargetTileRange > 0` 이면 파괴 대신 **현재 위치 기준**으로
                            // 다시 겨눈다. N회에 한 번 나오는 자원은 대상이 먼저 죽으면 그 주기가
                            // 통째로 버려지기 때문. 기존 투사체는 이 값이 0 이라 동작이 그대로다.
                            int repick = -1;
                            if (projectile.retargetTileRange > 0 && _retargetPositions.Count > 0)
                                repick = BounceRetarget.FindNext(
                                    transform.Position, -1, _retargetPositions,
                                    projectile.retargetTileRange, tileSize, gridSize, ffOrigin);

                            if (repick < 0)
                            {
                                _ecb.Destroy(entity);
                                break;
                            }
                            target = _retargetEntities[repick];
                            projectile.target = target;
                        }

                        SimVec3 currentPos = transform.Position;
                        SimVec3 targetPos = world.Get<SimTransform>(target).Position;
                        SimVec3 delta = targetPos - currentPos;
                        float dist = SimMath.Length(delta);
                        float step = projectile.speed * dt;

                        SimVec3 newPos = dist <= step ? targetPos : currentPos + SimMath.Normalize(delta) * step;
                        transform.Position = newPos;

                        // 도착 판정은 **XZ 평면**이고, 이동 **후** 위치로 평가한다(구 sim 에서
                        // 착탄이 이동 뒤에 돌며 보던 바로 그 값).
                        float dx = targetPos.x - newPos.x;
                        float dz = targetPos.z - newPos.z;
                        float thr = projectile.hitThreshold;
                        if (dx * dx + dz * dz <= thr * thr) projectile.impactReached = true;
                        break;
                    }

                    case MovementKind.BezierHomingToEntity:
                    {
                        // 제어점은 발사 시 고정이고 **종점만** 대상을 따라간다 — 대상이 움직이면
                        // 곡선이 실시간으로 재조정된다. 대상 소실 = 파괴(재조준 미개통, 위 참조).
                        if (!IsLiveTarget(world, projectile.target))
                        {
                            _ecb.Destroy(entity);
                            break;
                        }

                        float bElapsed = projectile.elapsed + dt;
                        projectile.elapsed = bElapsed;
                        float bFlight = projectile.flightTime;
                        float bt = bFlight > 0f ? SimMath.Saturate(bElapsed / bFlight) : 1f;

                        SimVec3 bTargetPos = world.Get<SimTransform>(projectile.target).Position;
                        SimVec3 bNewPos = Bezier3.Position(
                            projectile.origin, projectile.control1, projectile.control2, bTargetPos, bt);
                        transform.Position = bNewPos;

                        // 도착 = 곡선 완주 **또는** 근접(움직이는 대상을 t<1 에 잡는 경우).
                        float bdx = bTargetPos.x - bNewPos.x;
                        float bdz = bTargetPos.z - bNewPos.z;
                        float bthr = projectile.hitThreshold;
                        if (bt >= 1f || bdx * bdx + bdz * bdz <= bthr * bthr) projectile.impactReached = true;
                        break;
                    }

                    case MovementKind.BallisticArcToPoint:
                    {
                        // 대상 엔티티가 없다 — 착탄점이 발사 시 고정이라 비행 중 대상의
                        // 사망/이동이 아무 영향을 주지 않는다.
                        float elapsed = projectile.elapsed + dt;
                        float flightTime = projectile.flightTime;
                        float t = flightTime > 0f ? SimMath.Saturate(elapsed / flightTime) : 1f;
                        transform.Position = BallisticArc.ArcPosition(
                            projectile.origin, projectile.impact, projectile.arcHeight, t);
                        projectile.elapsed = elapsed;
                        if (elapsed >= flightTime) projectile.impactReached = true;
                        break;
                    }

                    case MovementKind.SkyFall:
                    {
                        // ⚠ sim 위치는 **비행 내내 착탄점에 고정**된다 — 레거시 경로에도 sim 이동이
                        //   없었다. `elapsed` 만 흐르고 떨어지는 그림은 뷰 공간 전용이다.
                        float elapsed = projectile.elapsed + dt;
                        projectile.elapsed = elapsed;
                        if (SkyFall.Arrived(elapsed, projectile.flightTime)) projectile.impactReached = true;
                        break;
                    }

                    case MovementKind.DirectionalLinear:
                    {
                        // 대상이 없고 비행 중에 해결된다(PathHit 가 prevPos→Position 을 스윕).
                        // ⚠ `prevPos` 를 **전진 전에** 기록해야 payload 가 이번 프레임에 지난
                        //   정확한 선분을 받는다.
                        SimVec3 currentPos = transform.Position;
                        projectile.prevPos = currentPos;

                        SimVec2 dir = projectile.direction;
                        SimVec3 newPos = currentPos + new SimVec3(dir.x, 0f, dir.y) * (projectile.speed * dt);

                        SimVec3 origin = projectile.origin;
                        float maxDistance = projectile.maxDistance;
                        float traveled = SimMath.Distance(new SimVec2(newPos.x, newPos.z), new SimVec2(origin.x, origin.z));
                        if (traveled >= maxDistance)
                        {
                            // ⚠ **사거리 위에 정확히 착지시킨다** — 넘어가면 마지막 스윕이 저작
                            //   사거리 밖 타일까지 때린다.
                            SimVec2 end = new SimVec2(origin.x, origin.z) + dir * maxDistance;
                            newPos = new SimVec3(end.x, newPos.y, end.y);
                            // 여기서의 도착은 "명중" 이 아니라 **"비행 종료"** 다 — 착탄 시스템이
                            // 이번 프레임 해결 후 소멸시킨다.
                            projectile.impactReached = true;
                        }
                        transform.Position = newPos;
                        break;
                    }

                    case MovementKind.GrenadeToCell:
                    {
                        // 셀까지 굴러간 뒤(`flightTime`) 신관(`fuseSec`)이 탈 동안 그 셀에 머문다.
                        // 구르기는 아치 함수 재사용이고(높이 ≈ 0), `t=1` 에서 그 함수가 착탄점을
                        // 돌려주므로 신관 동안 위치가 자연히 고정된다.
                        float elapsed = projectile.elapsed + dt;
                        float flightTime = projectile.flightTime;
                        float t = flightTime > 0f ? SimMath.Saturate(elapsed / flightTime) : 1f;
                        transform.Position = BallisticArc.ArcPosition(
                            projectile.origin, projectile.impact, projectile.arcHeight, t);
                        projectile.elapsed = elapsed;
                        // ⚠ 착탄 시스템은 신관을 **보지 않는다** — 타이밍은 이동 소유다.
                        if (elapsed >= flightTime + projectile.fuseSec) projectile.impactReached = true;
                        break;
                    }

                    default:
                        // ⚠ 모르는 궤적은 **파괴**한다. 위치도 도착도 해결도 없는 불멸 엔티티를
                        //   남기느니, 미래에 arm 을 빠뜨렸을 때 "투사체가 사라진다" 는 보이는 증상이
                        //   조용한 누수보다 낫다.
                        _ecb.Destroy(entity);
                        break;
                }

                world.Set(entity, projectile);
                world.Set(entity, transform);
            }

            _ecb.Playback(world);
        }

        /// 대상이 존재하고, 위치가 있고, 아직 죽지 않았는가. 셋 다 필요하다(위 ⚠ 참조).
        private static bool IsLiveTarget(SimWorld world, SimEntityId target)
            => !target.IsNull && world.Has<SimTransform>(target) && !world.Has<DeadTag>(target);
    }
}
