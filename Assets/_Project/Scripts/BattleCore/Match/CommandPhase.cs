using Unity.Mathematics;
using Wassup.Battle.Units;
using Wassup.BattleCore.Map;
using Wassup.BattleCore.Move;

namespace Wassup.BattleCore
{
    // battle-core-rebuild unit 1 — phase 0. 커맨드의 자리.
    //
    // **`Execute` 는 `BattleMatch.Apply` 가 곧바로 부른다**(틱을 기다리지 않는다) —
    // 그것이 「동기 + receipt」의 뜻이다. 이 클래스가 파이프라인의 0번에도 서 있는 것은
    // 그 자리가 **Immediate seam**(커맨드가 만든 사건의 same-frame 하류)이기 때문이고,
    // 그 드레인은 트리거 레이어가 생기는 unit 7 에서 `Run` 안으로 들어온다.
    //
    // unit 2 — 점유표가 `MapRuntime.Occupancy` 로 옮겨갔고 **다칸 footprint** 를 본다(M29).
    // 진짜 배치 판정(코스트 · 쿨다운 · 보드 상한 · 손패)은 `PlacementService` 가 생기는
    // unit 4 의 몫이다. 여기 있는 것은 **공간 판정**(층 ∩ 층 · 점유)까지다.
    public sealed class CommandPhase : ITickPhase
    {
        public string Name => "Command";

        private readonly BattleWorld _world;
        private readonly MatchClock _clock;
        private readonly MatchDefinition _def;
        private readonly MapRuntime _map;

        public CommandPhase(BattleWorld world, MatchClock clock, MatchDefinition def, MapRuntime map)
        {
            _world = world;
            _clock = clock;
            _def = def;
            _map = map;
        }

        public void Run(TickContext ctx)
        {
            // unit 7 — Immediate seam 드레인이 여기 들어온다. 지금은 커맨드가 동기라
            // 이 자리에서 할 일이 없다(빈 단계를 지우지 않는 이유는 위 주석).
        }

        public Receipt Execute(in Command cmd, int tick)
        {
            // 종료 뒤에는 전부 거절한다 — 판이 끝난 뒤의 입력이 상태를 움직이면
            // 결과 화면이 판 뒤에 바뀐다(계약 5).
            if (_clock.Ended) return Receipt.Reject(RejectReason.MatchEnded);

            switch (cmd.Kind)
            {
                case CommandKind.PlaceDefender: return Place(cmd, tick);
                case CommandKind.Retire: return RetireAt(cmd, tick);
                case CommandKind.Submit: return Submit();
                case CommandKind.DebugSpawnEnemy: return DebugSpawn(cmd, tick);
                case CommandKind.DebugDestroy: return DebugDestroy(cmd, tick);
                case CommandKind.DebugSetObstacle: return DebugObstacle(cmd);
                case CommandKind.DebugSpawnDefender: return DebugSpawnDefender(cmd, tick);
                default: return Receipt.Reject(RejectReason.UnknownCommand);
            }
        }

        private Receipt Place(in Command cmd, int tick)
        {
            if (cmd.DefIndex < 0 || cmd.DefIndex >= _def.Units.Length)
                return Receipt.Reject(RejectReason.InvalidUnit);

            ref var d = ref _def.Units[cmd.DefIndex];
            int w = math.max(1, d.FootprintWidth);
            int h = math.max(1, d.FootprintHeight);

            // **앵커는 min 코너**다. 손끝 칸에서 유닛이 위로 자란다 — 대표 칸은 없다.
            int2 anchor = cmd.Cell;

            var map = _map.Snapshot;
            if (map.CellCount > 0)
            {
                // 다칸은 **전 칸**이 판정을 통과해야 한다. 한 칸만 보면 건물이 벽을 파고든다.
                for (int dy = 0; dy < h; dy++)
                for (int dx = 0; dx < w; dx++)
                {
                    var c = new int2(anchor.x + dx, anchor.y + dy);
                    if (!map.InBounds(c)) return Receipt.Reject(RejectReason.OutOfBounds);
                    if (!map.PlaceableAt(c, (byte)d.PlacementLayers))
                        return Receipt.Reject(RejectReason.NotBuildable);
                }
            }
            if (!_map.Occupancy.IsFree(anchor, w, h)) return Receipt.Reject(RejectReason.Occupied);

            // `deploying: false` — 배치 페이즈(비행 → 배치 모션 → 활성화)는 그 길이를 아는
            // 담당자(`PlacementService`)가 생기는 unit 4 의 몫이다. 지금 true 로 두면
            // **빠져나올 길이 없는** 상태가 되고, 다음 사람은 그것을 버그로 읽는다.
            var u = _world.Spawn(UnitKind.Defender, Faction.DefenderUnit, cmd.DefIndex,
                                 FootCenter(anchor, w), d.BodyRadiusTiles, d.Health,
                                 deploying: false, tick: tick);

            AttachDefenderParts(u, in d, anchor, w, h);
            // unit 3 — 공격은 **정의표에서** 온다. 스폰 경로가 여럿이어도(배치·디버그·소환·웨이브)
            // 전부 같은 함수를 지나야 「어떤 경로로 태어났나」가 공격 규칙을 바꾸지 않는다.

            _map.Occupancy.Occupy(u.Id, anchor, w, h);
            return Receipt.Ok;
        }

        // 디버그 스폰 — **판정을 갖지 않는다**(시나리오가 곧 의도다). 배치 마스크·점유·코스트를
        // 전부 건너뛰므로 골든이 「방어유닛이 선 판」을 unit 4 없이 세울 수 있다.
        private Receipt DebugSpawnDefender(in Command cmd, int tick)
        {
            if (cmd.DefIndex < 0 || cmd.DefIndex >= _def.Units.Length)
                return Receipt.Reject(RejectReason.InvalidUnit);

            ref var d = ref _def.Units[cmd.DefIndex];
            int w = math.max(1, d.FootprintWidth);
            int h = math.max(1, d.FootprintHeight);
            int2 anchor = cmd.Cell;

            var u = _world.Spawn(UnitKind.Defender, Faction.DefenderUnit, cmd.DefIndex,
                                 FootCenter(anchor, w), d.BodyRadiusTiles, d.Health,
                                 deploying: false, tick: tick);
            AttachDefenderParts(u, in d, anchor, w, h);
            _map.Occupancy.Occupy(u.Id, anchor, w, h);
            return Receipt.Ok;
        }

        private Receipt RetireAt(in Command cmd, int tick)
        {
            var u = _world.Find(cmd.Target);
            if (u == null) return Receipt.Reject(RejectReason.NoSuchEntity);
            if (u.Kind != UnitKind.Defender) return Receipt.Reject(RejectReason.InvalidUnit);

            _map.Occupancy.Release(u.Id);
            _world.Destroy(u.Id, tick);
            return Receipt.Ok;
        }

        private Receipt Submit()
        {
            if (!_clock.SubmitUnlocked) return Receipt.Reject(RejectReason.SubmitLocked);
            _clock.EndMatch(MatchEndReason.Submitted);
            return Receipt.Ok;
        }

        // 디버그 스폰. 레인을 주면 그 입구 칸에서 나오고 **경로·측면 분산도 그 레인에서** 나온다 —
        // 웨이브 생성기가 생기는 unit 4 가 같은 배선을 쓴다.
        private Receipt DebugSpawn(in Command cmd, int tick)
        {
            if (cmd.DefIndex < 0 || cmd.DefIndex >= _def.Enemies.Length)
                return Receipt.Reject(RejectReason.InvalidUnit);

            ref var d = ref _def.Enemies[cmd.DefIndex];
            var map = _map.Snapshot;

            int lane = cmd.Lane;
            int2 cell = cmd.Cell;
            if (lane >= 0)
            {
                if (map.Spawns.Length == 0) return Receipt.Reject(RejectReason.MissingMap);
                lane %= map.Spawns.Length;
                cell = map.Spawns[lane];
            }

            float3 pos = map.CellCount > 0 ? map.CellCenter(cell) : new float3(cell.x, 0f, cell.y);

            // 측면 분산 — 같은 문에서 나와도 겹치지 않게. RNG 없는 이산 N-레인 round-robin 이라
            // 같은 순번이면 같은 자리다. |오프셋| 은 반 칸을 못 넘는다(M14).
            if (lane >= 0)
            {
                float2 heading = HeadingAt(cell, (byte)d.TraversalLayers);
                float frac = SpawnSpread.LaneFraction(_spawnOrdinal++, 5, 0.4f, 1f);
                pos += SpawnSpread.LateralOffset(frac, map.TileSize, heading);
            }

            var u = _world.Spawn(UnitKind.Enemy, Faction.EnemyUnit, cmd.DefIndex,
                                 pos, d.BodyRadius, d.Health, deploying: false, tick: tick);

            var move = _world.Parts.RentMove();
            move.Speed = d.MoveSpeed;
            move.Radius = AgentRadiusTiles;
            move.TraversalLayers = (byte)d.TraversalLayers;
            move.Engage = (EngageMovement)math.clamp(d.EngageMovement, 0, 2);
            // 경로 선택 — **좁은 쪽이 이긴다**: 적 정의 > 웨이브 컨셉 > 레인 기본.
            // 컨셉은 웨이브 생성기(unit 4)가 채우므로 여기서는 -1 이다.
            move.PathIndex = WaypointRouting.ResolvePathIndex(
                d.WaypointPathIndex, -1, lane >= 0 ? map.RouteForSpawn(lane) : -1);
            u.Move = move;

            // **감지 0 = 오늘과 같은 경로.** 부착 자체가 게이트다 — 분기가 아니라 부재로 표현한다.
            if (d.DetectionRange != 0f)
            {
                var det = _world.Parts.RentDetection();
                det.Range = d.DetectionRange;
                u.Detection = det;
            }

            // unit 3 — 적도 방어유닛과 **같은 함수**로 공격을 얻는다(통합 루프가 둘을 구분하지 않는다).
            u.Attack = CombatPhase.BuildAttackState(in d, _def, _world.Parts);

            return Receipt.Ok;
        }

        private Receipt DebugDestroy(in Command cmd, int tick)
        {
            if (!_world.IsAlive(cmd.Target)) return Receipt.Reject(RejectReason.NoSuchEntity);
            _map.Occupancy.Release(cmd.Target);
            _world.Destroy(cmd.Target, tick);
            return Receipt.Ok;
        }

        private Receipt DebugObstacle(in Command cmd)
        {
            if (!_map.Snapshot.InBounds(cmd.Cell)) return Receipt.Reject(RejectReason.OutOfBounds);
            _map.Obstacles.SetManual(cmd.Cell, cmd.Flag);
            return Receipt.Ok;
        }

        // 방어유닛의 부분 부착 — **배치와 디버그 스폰이 같은 함수를 지난다.** 두 벌로 두면
        // 「어떤 경로로 태어났나」가 규칙을 바꾸고, 그 차이는 골든이 아니라 플레이에서만 보인다.
        private void AttachDefenderParts(Unit u, in UnitDef d, int2 anchor, int w, int h)
        {
            var fp = _world.Parts.RentFootprint();
            fp.Anchor = anchor;
            fp.Width = w;
            fp.Height = h;
            u.Footprint = fp;

            if (d.AggroCapacity > 0)
            {
                var aggro = _world.Parts.RentAggro();
                aggro.Capacity = d.AggroCapacity;
                u.Aggro = aggro;
            }

            u.Attack = CombatPhase.BuildAttackState(in d, _def, _world.Parts);
        }

        /// <summary>몸 반지름(칸). **군집 통과로 검산한 값**이다 — 단독 통과는 검산이 아니다.</summary>
        private const float AgentRadiusTiles = 0.25f;

        // 스폰 순번. 측면 분산 레인 배정의 결정론 키다(RNG 없음).
        private int _spawnOrdinal;

        // 발밑 = 하단 행 가로 중앙. 사거리 원점·몸 원이 전부 이 점이다.
        private float3 FootCenter(int2 anchor, int width)
        {
            float ts = _map.TileSize;
            return new float3((anchor.x + (width - 1) * 0.5f) * ts, 0f, anchor.y * ts);
        }

        // 그 칸에서 골로 향하는 방향. 측면 분산이 **진행방향 수직**으로 벌리기 위한 값이다.
        private float2 HeadingAt(int2 cell, byte layers)
        {
            if (_map.Snapshot.CellCount == 0) return new float2(1f, 0f);
            var slot = _map.Flow.GoalSlot(layers == 0 ? TraversalSlots.DefaultMask : layers);
            var dir = slot.DirAt(cell);
            return math.lengthsq(dir) > 1e-6f ? dir : new float2(1f, 0f);
        }
    }
}
