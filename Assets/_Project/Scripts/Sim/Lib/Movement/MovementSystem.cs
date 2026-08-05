using System.Collections.Generic;
using Wassup.Sim.Combat;
using Wassup.Sim.Effects;
using Wassup.Sim.Units;

namespace Wassup.Sim.Movement
{
    /// <summary>
    /// battle-sim-extraction unit 18-F/4 — 캡처 **#17** · <see cref="SimPhase.Movement"/>(P4).
    /// 구 `MovementSystem` 이식. **위치 갱신의 단일 권한**이고 P4 를 혼자 쓴다.
    ///
    /// 방향의 출처가 넷이다 — patrol step(#16) · defender field(#7, 보스 사냥) · goal flow(맵) ·
    /// chase field(#8, 어그로). `AiState`(#14)가 어느 것을 쓸지 고르고, 이 시스템은 **고른 방향을
    /// 걷는 일**만 한다.
    ///
    /// ⚠ **`locked` 는 자기주도 이동만 막는다** — 외력(임펄스·토네이도·포털)은 그대로 먹는다.
    /// CC(남이 건 것)와 `LeapFlight`(본체 자신의 상태)를 한 변수에 접는 이유는 소비 지점이
    /// 전부 같고 출처만 다르기 때문이다.
    ///
    /// ⚠ **`locked` 를 AiState 직후에 계산한다** — Chasing/goal/tornado 분기가 flow-step 앞에서
    /// continue 하므로, 뒤로 미루면 그 경로들이 잠금을 무시한다.
    /// </summary>
    public sealed class MovementSystem
    {
        private readonly List<SimEntityId> _pastGoal = new List<SimEntityId>();
        private readonly List<PortalLink> _portals = new List<PortalLink>();
        private readonly List<TornadoField> _tornados = new List<TornadoField>();
        private int[] _chaseScratch = new int[64];

        public void Run(SimWorld world)
        {
            if (!SimSingleton.TryGet(world, out FlowFieldSingleton field)) return;   // 분류 C
            if (!field.IsCreated) return;

            float dt = world.DeltaTime;
            bool hasHuntField = SimSingleton.TryGet(world, out DefenderFieldSingleton huntField)
                                && huntField.IsCreated;
            bool hasObstacles = SimSingleton.TryGet(world, out ObstacleSingleton obstacles)
                                && obstacles.IsCreated;

            SnapshotCarriers(world);
            _pastGoal.Clear();

            foreach (SimEntityId e in world.With<PathFollowState>())
            {
                if (world.Has<PastGoalTag>(e)) continue;
                if (!world.TryGet(e, out SimTransform transform)) continue;

                SimVec3 current = transform.Position;
                PathFollowState follow = world.Get<PathFollowState>(e);

                AiState ai = world.TryGet(e, out EnemyAiState aiState) ? aiState.value : AiState.Marching;
                bool patrolling = world.Has<PatrolStep>(e);

                // 자기주도 이동 잠금. CC = 남이 건 것 / LeapFlight = 본체 자신의 상태.
                List<CcEffect> cc = world.GetBuffer<CcEffect>(e);
                bool locked = CcActionLock.IsLocked(cc) || world.Has<LeapFlight>(e);

                if (ai == AiState.Standoff) continue;   // 완전 정지(공격은 #33 이 한다)

                if (ai == AiState.Chasing)
                {
                    if (!locked) StepChase(world, e, ref transform, current, follow, in field,
                                            hasObstacles, in obstacles, dt);
                    // chasing 은 flow/portal/tornado/goal 을 전부 건너뛴다.
                    continue;
                }

                // ── 1. 포털 진입 — 반경 안이면 exit 으로 텔레포트 ──────────────
                for (int p = 0; p < _portals.Count; p++)
                {
                    PortalLink portal = _portals[p];
                    float pdx = current.x - portal.entryWorld.x;
                    float pdz = current.z - portal.entryWorld.z;
                    if (pdx * pdx + pdz * pdz > portal.entryRadius * portal.entryRadius) continue;
                    current = new SimVec3(portal.exitWorld.x, current.y, portal.exitWorld.z);
                    transform.Position = current;
                    world.Set(e, transform);
                    break;
                }

                // ── 2. 현재 셀 + 골 판정 ───────────────────────────────────────
                SimInt2 cell = GridMath.WorldToCell(current, field.tileSize, field.gridSize,
                                                    origin: field.origin);
                int idx = GridMath.CellIndex(cell, field.gridSize);

                // 보스 사냥: hunt-dist 가 유한 = 도달 가능한 방어유닛 존재 → goal 대신 defender field.
                bool hunting = hasHuntField && world.Has<BossTag>(e)
                               && huntField.dist[idx] != int.MaxValue;

                // ⚠ 사냥 중엔 골을 지나쳐도 **누수하지 않는다**(방어유닛 전멸 후에만 도달 처리).
                // ⚠ 순찰병도 제외 — 거점 박스 안에 골이 들어올 수 있고, 태그가 붙으면
                //    이 루프가 영구 제외 + #41 의 파괴 루프는 `AttackUnitTag` 를 요구해 파괴도
                //    안 되고 → 소환사가 남은 판 내내 재소환하지 못한다.
                if (!hunting && !patrolling && field.IsGoalCell(cell))
                {
                    _pastGoal.Add(e);
                    continue;
                }

                // ── 3. 토네이도 견인 — 이동을 대체하지 않는 **후처리 가산 변위** ──
                SimVec3 pull = SimVec3.Zero;
                for (int t = 0; t < _tornados.Count; t++)
                {
                    TornadoField f = _tornados[t];
                    SimInt2 centerCell = GridMath.WorldToCell(f.centerWorld, field.tileSize,
                                                              field.gridSize, origin: field.origin);
                    if (GridMath.ChebyshevDistance(cell, centerCell) > f.tileRange) continue;

                    var toCenter = new SimVec3(f.centerWorld.x - current.x, 0f, f.centerWorld.z - current.z);
                    float centerDist = SimMath.Length(toCenter);
                    float pullStep = f.pullSpeed * dt;
                    pull = (centerDist <= pullStep || centerDist < 1e-4f)
                        ? toCenter                                   // 중심까지 남은 변위 전부
                        : SimMath.Normalize(toCenter) * pullStep;
                    break;                                            // 첫 매칭 필드만
                }
                bool hasPull = SimMath.LengthSq(pull) > 1e-8f;

                // ── Engaging 이동 정책 ────────────────────────────────────────
                if (ai == AiState.Engaging)
                {
                    EngageMovement engage = world.TryGet(e, out EnemyBehavior behavior)
                        ? behavior.engageMovement
                        : EngageMovement.Halt;
                    bool advance;
                    if (engage == EngageMovement.Advance) advance = true;
                    else if (engage == EngageMovement.Pulse)
                        advance = !(world.TryGet(e, out AttackState atk) && atk.hitDelayRemaining > 0f);
                    else advance = false;   // Halt

                    if (locked || !advance)
                    {
                        // 정지 중에도 **외력은 먹는다**(기존 "Halt 도 당겨짐" 거동 보존).
                        if (hasPull) ApplyPullOnly(world, e, ref transform, current, pull,
                                                    cell, in field, hasObstacles, in obstacles);
                        continue;
                    }
                }

                // ── 4. flow step — patrol / hunting / goal 세 소스 ─────────────
                SimVec2 dir;
                bool zeroFlowRecovery = false;
                if (patrolling)
                {
                    dir = world.Get<PatrolStep>(e).dir;
                    if (SimMath.LengthSq(dir) < 1e-6f)
                    {
                        // 거점 도착·사격 위치 도달·고립 = 정지. **goal 기반 복구로 떨어뜨리지 않는다**
                        // — 그 dist 는 순찰병의 목적지와 무관하다.
                        if (hasPull) ApplyPullOnly(world, e, ref transform, current, pull,
                                                    cell, in field, hasObstacles, in obstacles);
                        continue;
                    }
                }
                else
                {
                    dir = hunting ? huntField.flow[idx] : field.flow[idx];
                    if (SimMath.LengthSq(dir) < 1e-6f)
                    {
                        zeroFlowRecovery = true;
                        // zero-flow 셀 — 임펄스로 도달 불가 셀에 밀렸을 수 있다. 4-이웃 중
                        // dist 가 작은 쪽으로. **사냥 중이면 복구도 defender dist 기준**이다.
                        SimVec2 recov = FlowRecovery.RecoveryDir(
                            cell, hunting ? huntField.dist : field.dist, field.gridSize);
                        if (SimMath.LengthSq(recov) < 1e-6f)
                        {
                            if (hasPull) ApplyPullOnly(world, e, ref transform, current, pull,
                                                        cell, in field, hasObstacles, in obstacles);
                            continue;   // 진짜 고립 — 자기주도 이동 없음, 외력만
                        }
                        dir = recov;
                    }
                }

                float speedMul = world.TryGet(e, out ModifierStats stats) ? stats.moveSpeedMul : 1f;

                SimVec3 impulse = SimVec3.Zero;
                if (cc != null)
                    for (int i = 0; i < cc.Count; i++)
                        if (cc[i].kind == CcKind.Impulse) impulse += cc[i].vector * dt;

                SimVec2 stepDir = SimMath.NormalizeSafe(dir);
                SimVec3 flowStep = locked
                    ? SimVec3.Zero
                    : new SimVec3(stepDir.x, 0f, stepDir.y) * (follow.speed * speedMul * dt);
                SimVec3 desired = current + flowStep + impulse;

                // 코너 엣지-허깅 측면 복원. 복구 분기는 스킵(이미 교정 이동 중)하고,
                // 임펄스 측면 성분은 이 프레임 보존한다(넉백은 이후 프레임에 점진 복귀).
                if (!zeroFlowRecovery && !locked)
                    desired += LateralRecenter.Compute(current, cell, stepDir,
                                                       follow.speed * speedMul, dt,
                                                       field.tileSize, field.origin);

                desired += pull;
                desired = MovementCellTrim.ClampDisplacement(current, desired, field.tileSize);
                desired = MovementCellTrim.Apply(desired, cell, in field, hasObstacles, in obstacles);

                transform.Position = desired;
                world.Set(e, transform);
            }

            for (int i = 0; i < _pastGoal.Count; i++) world.Set(_pastGoal[i], default(PastGoalTag));
        }

        /// <summary>
        /// 어그로 추격의 self-walk. chase field(#8 이 구움)를 하강한다.
        /// dir zero = 목적지(사거리 내 walk 셀) 도착 또는 고립 → 정지. 도착 셀은 정의상 발사
        /// 조건을 만족하므로 다음 틱에 #14 가 `Standoff` 로 전이시킨다.
        /// </summary>
        private void StepChase(SimWorld world, SimEntityId e, ref SimTransform transform,
                               SimVec3 current, PathFollowState follow,
                               in FlowFieldSingleton field, bool hasObstacles,
                               in ObstacleSingleton obstacles, float dt)
        {
            List<AggroChaseCell> chase = world.GetBuffer<AggroChaseCell>(e);
            if (chase == null) return;                                   // 필드 없으면 정지
            int n = field.gridSize.x * field.gridSize.y;
            if (chase.Count != n) return;                                // 그리드 불일치 — 정지

            if (_chaseScratch.Length < n) _chaseScratch = new int[n];
            for (int i = 0; i < n; i++) _chaseScratch[i] = chase[i].dist;

            SimInt2 chaseCell = GridMath.WorldToCell(current, field.tileSize, field.gridSize,
                                                     origin: field.origin);
            SimVec2 chaseDir = FlowRecovery.RecoveryDir(chaseCell, _chaseScratch, field.gridSize);
            if (SimMath.LengthSq(chaseDir) <= 1e-6f) return;

            float speedMul = world.TryGet(e, out ModifierStats stats) ? stats.moveSpeedMul : 1f;
            SimVec3 desired = current + new SimVec3(chaseDir.x, 0f, chaseDir.y)
                                        * (follow.speed * speedMul * dt);
            desired = MovementCellTrim.ClampDisplacement(current, desired, field.tileSize);
            transform.Position = MovementCellTrim.Apply(desired, chaseCell, in field,
                                                        hasObstacles, in obstacles);
            world.Set(e, transform);
        }

        private static void ApplyPullOnly(SimWorld world, SimEntityId e, ref SimTransform transform,
                                          SimVec3 current, SimVec3 pull, SimInt2 cell,
                                          in FlowFieldSingleton field, bool hasObstacles,
                                          in ObstacleSingleton obstacles)
        {
            SimVec3 desired = MovementCellTrim.ClampDisplacement(current, current + pull, field.tileSize);
            transform.Position = MovementCellTrim.Apply(desired, cell, in field, hasObstacles, in obstacles);
            world.Set(e, transform);
        }

        private void SnapshotCarriers(SimWorld world)
        {
            _portals.Clear();
            foreach (SimEntityId p in world.With<PortalLink>()) _portals.Add(world.Get<PortalLink>(p));
            _tornados.Clear();
            foreach (SimEntityId t in world.With<TornadoField>()) _tornados.Add(world.Get<TornadoField>(t));
        }
    }

    /// <summary>
    /// battle-sim-extraction unit 18-F/4 — 캡처 **#44** · <see cref="SimPhase.Destruction"/>(P12).
    /// 구 `BlinkApplySystem` 이식. 블링크 seam 의 **Movement 쪽 소비자**다.
    ///
    /// 위치가 Movement 소유라 Combat 이 직접 못 쓴다 — 요청을 받아 대입만 한다.
    /// **y 는 현재값을 유지**하고 방향은 다음 프레임 flow field 가 공급한다(포털 텔레포트 선례).
    /// 생산자(#42/#43)보다 뒤라 **같은 틱 요청이 같은 틱에 착지**한다.
    /// </summary>
    public sealed class BlinkApplySystem
    {
        private readonly SimChannel<BlinkRequestEvent> _channel;
        public BlinkApplySystem(SimChannel<BlinkRequestEvent> channel) => _channel = channel;

        public void Run(SimWorld world)
        {
            List<BlinkRequestEvent> reqs = _channel.Drain();
            for (int i = 0; i < reqs.Count; i++)
            {
                BlinkRequestEvent req = reqs[i];
                // 요청과 적용 사이(같은 틱)에 대상이 파괴됐으면 조용히 드롭.
                if (!world.TryGet(req.entity, out SimTransform t)) continue;
                t.Position = new SimVec3(req.destWorld.x, t.Position.y, req.destWorld.z);
                world.Set(req.entity, t);
            }
        }
    }
}
