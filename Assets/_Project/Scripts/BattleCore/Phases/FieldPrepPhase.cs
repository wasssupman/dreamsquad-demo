using Unity.Mathematics;
using Wassup.Battle.Units;
using Wassup.BattleCore.Map;
using Wassup.BattleCore.Move;

namespace Wassup.BattleCore
{
    // battle-core-rebuild unit 2 — **장(場)을 먼저 세운다.** 이동이 읽을 것을 이 단계가 굽는다.
    //
    // 순서가 계약이다(옛 전투의 `[UpdateBefore]` 사슬을 호출 순서로 명시한 것):
    //   ① 장애물 재수집 → 시그니처 → 바뀐 틱에만 흐름장 부분 재빌드 · 벽 캐시 무효화
    //   ② 어그로 상태(만료 · 가디언 사망 해제 · 수용량 재계산)
    //   ③ 공용 사냥판(무제한 감지용)
    //   ④ 순찰 스텝
    //
    // ⚠ **장애물이 바뀌면 어그로가 풀린다**(M10 의 「리무버 둘」 중 둘째). 그 경로가 없으면
    // 길이 막힌 뒤에도 적이 옛 추격판을 하강해 **못 가는 곳으로 영원히 밀린다**.
    // 단 **도발된 적은 필드만 떼고 어그로 표시는 남긴다** — 도발은 1회성이라 재획득 경로가
    // 없어 통째로 풀면 도발이 그 자리에서 사라진다.
    public sealed class FieldPrepPhase : ITickPhase
    {
        public string Name => "FieldPrep";

        private readonly MapRuntime _map;
        private readonly ChaseFieldPool _chasePool;
        private readonly PatrolScratch _patrol;

        private int2[] _defenderCells = new int2[16];
        private int2[] _enemyCells = new int2[32];
        private float3[] _enemyPositions = new float3[32];
        private float[] _enemyRadii = new float[32];
        private readonly byte[] _fullMask;

        public FieldPrepPhase(MapRuntime map, ChaseFieldPool chasePool)
        {
            _map = map;
            _chasePool = chasePool;
            _patrol = new PatrolScratch(map.Snapshot.CellCount);
            _fullMask = new byte[math.max(1, map.Snapshot.CellCount)];
        }

        public void Run(TickContext ctx)
        {
            if (_map == null || _map.Snapshot.CellCount == 0) return;

            RebuildObstacles(ctx);
            StepAggro(ctx);
            RebuildHuntField(ctx);
            StepPatrol(ctx);
        }

        // ── ① 장애물 ──────────────────────────────────────────────────────────
        private void RebuildObstacles(TickContext ctx)
        {
            var obstacles = _map.Obstacles;
            obstacles.BeginRebuild();

            var units = ctx.World.Units;
            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i];
                if (u.Dead) continue;
                if (u.Kind == UnitKind.Defender && u.Footprint != null)
                {
                    obstacles.BlockRect(u.Footprint.Anchor, u.Footprint.Width, u.Footprint.Height);
                    continue;
                }
                // 길막 장판 — 「막으면 돌아간다」의 다른 소스. 거점은 통행을 안 막는다(점유만).
                if (u.Kind == UnitKind.BlockingHazard)
                    obstacles.Block(_map.CellOf(u.Position));
            }

            if (!obstacles.EndRebuild()) return;   // 안 바뀌었으면 다시 굽지 않는다

            _map.Flow.Rebuild(obstacles);
            _map.Nav.Invalidate(obstacles.Signature);

            // 낡은 추격판 무효화 = 어그로 해제(M10). 도발은 표시만 남긴다.
            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i];
                var aggro = u.Aggro;
                if (aggro == null || aggro.Target.IsNone) continue;
                if (aggro.Chase != null)
                {
                    _chasePool.Return(aggro.Chase);
                    aggro.Chase = null;
                }
                if (!aggro.Taunted) aggro.Target = SimEntityId.None;
            }
        }

        // ── ② 어그로 상태 ─────────────────────────────────────────────────────
        private void StepAggro(TickContext ctx)
        {
            var units = ctx.World.Units;

            // 만료·가디언 사망 해제. 시한(>0)만 감소한다 — 0 은 무기한 센티널이다.
            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i];
                var aggro = u.Aggro;
                if (aggro == null || aggro.Target.IsNone) continue;

                if (aggro.Remaining > 0f)
                {
                    aggro.Remaining -= ctx.Dt;
                    if (aggro.Remaining <= 0f) { Release(aggro); continue; }
                }

                var guardian = ctx.World.Find(aggro.Target);
                if (guardian == null || guardian.Dead || guardian.Health <= 0f) Release(aggro);
            }

            // 수용량 재계산은 **full recompute** 다 — 증감으로 유지하면 drift 가 쌓인다.
            for (int i = 0; i < units.Count; i++)
                if (units[i].Aggro != null) units[i].Aggro.Held = 0;

            for (int i = 0; i < units.Count; i++)
            {
                var aggro = units[i].Aggro;
                if (aggro == null || aggro.Target.IsNone || units[i].Dead) continue;
                var guardian = ctx.World.Find(aggro.Target);
                if (guardian?.Aggro != null) guardian.Aggro.Held++;
            }
        }

        private void Release(Aggro aggro)
        {
            aggro.Target = SimEntityId.None;
            aggro.Remaining = 0f;
            aggro.Taunted = false;
            if (aggro.Chase != null) { _chasePool.Return(aggro.Chase); aggro.Chase = null; }
        }

        // ── ③ 공용 사냥판 ─────────────────────────────────────────────────────
        //
        // 헌터(무제한 감지)가 하나도 없으면 굽지 않는다 — 소비자가 없는 필드다.
        // 반경 = 동시에 살아 있는 헌터 **사거리의 min fold**(M7).
        private void RebuildHuntField(TickContext ctx)
        {
            var units = ctx.World.Units;
            int range = int.MaxValue;
            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i];
                if (u.Dead || u.Detection == null || !u.Detection.Unlimited) continue;
                float atk = u.DefIndex >= 0 && u.DefIndex < ctx.Def.Enemies.Length
                    ? ctx.Def.Enemies[u.DefIndex].AttackRange : 1f;
                range = math.min(range, GridMath.RangeToTiles(atk));
            }
            if (range == int.MaxValue) { _map.Hunt.Clear(); return; }
            range = math.max(1, range);

            int count = 0;
            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i];
                if (u.Dead || u.Deploying) continue;
                if (((int)u.Faction & (int)Faction.DefenderUnit) == 0) continue;
                if (count >= _defenderCells.Length) Grow(ref _defenderCells);
                _defenderCells[count++] = _map.CellOf(u.Position);
            }

            _map.Hunt.Rebuild(_map.Snapshot.CellLayers, _map.HuntLayers, _map.Obstacles,
                              _defenderCells, count, range);
        }

        // ── ④ 순찰 ────────────────────────────────────────────────────────────
        //
        // 순찰 스텝은 이동 **앞**에서 굽는다(옛 `PatrolFieldSystem` 이 Movement 앞이었던 것과 같다).
        // 매 틱 굽는 이유: 목적지가 움직이는 적이라 필드가 매 틱 무효가 된다 — 그래서 유닛당
        // 격자 버퍼를 들지 않고 **방향 하나로 접는다.**
        private void StepPatrol(TickContext ctx)
        {
            var units = ctx.World.Units;

            int patrolCount = 0;
            for (int i = 0; i < units.Count; i++)
                if (units[i].Patrol != null && !units[i].Dead) patrolCount++;
            if (patrolCount == 0) return;

            int enemyCount = 0;
            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i];
                if (u.Dead || !u.IsTargetable()) continue;
                if (((int)u.Faction & (int)Faction.EnemyUnit) == 0) continue;
                if (enemyCount >= _enemyCells.Length)
                {
                    Grow(ref _enemyCells);
                    Grow(ref _enemyPositions);
                    Grow(ref _enemyRadii);
                }
                _enemyCells[enemyCount] = _map.CellOf(u.Position);
                _enemyPositions[enemyCount] = u.Position;
                // ⚠ **대상 몸을 함께 넘긴다.** 안 넘기면 순찰 이동만 다른 답을 받는다 —
                // 보스가 사거리 안인데 이동은 밖으로 읽어 **이미 쏠 수 있는데 계속 다가간다.**
                _enemyRadii[enemyCount] = u.HitRadius;
                enemyCount++;
            }

            var gridSize = _map.GridSize;
            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i];
                if (u.Patrol == null || u.Move == null || u.Dead) continue;

                // 소환사가 죽으면 소환물도 사라진다 — 이동을 멈추는 것이 아니라 소멸이다.
                //
                // ⚠ **여기서 지우지 않는다.** 표시만 하고 소멸은 `CombatPhase` 의 사망 단계가
                // `BattleWorld.Destroy` 로 한다(unit 3 사망 2단계). 두 번째 제거 경로를 만들면
                // 계약 7(「모든 소멸은 소멸 이벤트를 낸다」)이 경로마다 따로 지켜져야 하고,
                // 그러면 언젠가 한쪽이 조용히 빠진다 — 실제로 초판이 `Dead` 만 세우고
                // `UnitDestroyed` 를 안 내서 뷰가 그 순찰병을 영원히 들고 있었다.
                // `DeathTick` 을 함께 찍는 것도 계약이다: 안 찍으면 표시 틱과 소멸 틱이 같아져
                // 시체가 자기 자리를 읽을 창(시체 폭발·사직서 드랍)이 이 경로에만 없어진다.
                if (!u.Patrol.SummonedBy.IsNone && ctx.World.Find(u.Patrol.SummonedBy) == null)
                {
                    u.Dead = true;
                    u.DeathTick = ctx.Tick;
                    u.Move.PatrolStep = float2.zero;
                    continue;
                }

                byte layers = u.Move.TraversalLayers;
                MovementCellTrim.FillWalkMask(_map.Snapshot.CellLayers, gridSize,
                                              layers == 0 ? TraversalSlots.DefaultMask : layers,
                                              _map.Obstacles.HasObstacles, _map.Obstacles.Blocked, _fullMask);
                PatrolAreaMath.FillAreaMask(_fullMask, gridSize, u.Patrol.Anchor, u.Patrol.Radius,
                                            _patrol.AreaMask);

                float range = u.DefIndex >= 0 && u.DefIndex < ctx.Def.Units.Length
                    ? ctx.Def.Units[u.DefIndex].AttackRange : 1f;

                u.Move.PatrolStep = PatrolAreaMath.StepDir(
                    _patrol.AreaMask, _fullMask, gridSize,
                    u.Patrol.Anchor, u.Patrol.Home, u.Patrol.Radius,
                    _map.CellOf(u.Position), u.Position, u.HitRadius,
                    GridMath.RangeToTiles(range), _map.TileSize,
                    _enemyCells, _enemyPositions, _enemyRadii, enemyCount, _patrol);
            }
        }

        private static void Grow<T>(ref T[] buffer)
        {
            var next = new T[buffer.Length * 2];
            System.Array.Copy(buffer, next, buffer.Length);
            buffer = next;
        }
    }
}
