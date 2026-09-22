using Unity.Mathematics;
using Wassup.BattleCore.Map;

namespace Wassup.BattleCore
{
    // battle-core-rebuild unit 1·4 — phase 0. 커맨드의 자리.
    //
    // **`Execute` 는 `BattleMatch.Apply` 가 곧바로 부른다**(틱을 기다리지 않는다) —
    // 그것이 「동기 + receipt」의 뜻이다. 이 클래스가 파이프라인의 0번에도 서 있는 것은
    // 그 자리가 **Immediate seam**(커맨드가 만든 사건의 same-frame 하류)이기 때문이고,
    // 그 드레인은 트리거 레이어가 생기는 unit 7 에서 `Run` 안으로 들어온다.
    //
    // unit 4 — **여기에 판정이 없다.** 배치·퇴근·당김·카드는 전부 담당자에게 넘긴다.
    // 이 클래스가 하는 일은 「어느 담당자에게 가나」뿐이고, 그것이 계약 12 의 이행이다 —
    // 커맨드마다 판정을 조금씩 여기 두면 이 파일이 새 브리지가 된다.
    // 남아 있는 판정은 **디버그 커맨드**뿐이고 그쪽은 정의상 판정을 갖지 않는다(시나리오가 곧 의도다).
    public sealed class CommandPhase : ITickPhase
    {
        public string Name => "Command";

        private readonly BattleWorld _world;
        private readonly MatchClock _clock;
        private readonly MatchDefinition _def;
        private readonly MapRuntime _map;
        private readonly PlacementService _placement;
        private readonly CostLedger _cost;
        private readonly WaveScheduler _waves;
        private readonly HandDeck _hand;

        public CommandPhase(BattleWorld world, MatchClock clock, MatchDefinition def, MapRuntime map,
                            PlacementService placement, CostLedger cost, WaveScheduler waves, HandDeck hand)
        {
            _world = world;
            _clock = clock;
            _def = def;
            _map = map;
            _placement = placement;
            _cost = cost;
            _waves = waves;
            _hand = hand;
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
                case CommandKind.PlaceDefender:
                    return _placement.TryPlace(cmd.DefIndex, cmd.Cell, cmd.Facing, tick);
                case CommandKind.LandDefender:
                    return _placement.Land(cmd.Target);
                case CommandKind.Retire:
                    return _placement.Retire(cmd.Target, tick);
                case CommandKind.FinishPlacement:
                    return _clock.FinishPlacement()
                        ? Receipt.Ok
                        : Receipt.Reject(RejectReason.NotRunningOrPlacementClosed);
                case CommandKind.PullWave:
                    return _waves.TryPull(tick);
                case CommandKind.PullBonus:
                    return _waves.TryPullBonus(tick);
                case CommandKind.AttachCard:
                    return _hand.TryAttach(cmd.CardIndex, cmd.Target, tick);
                case CommandKind.CastActive:
                    return _hand.TryCast(cmd.CardIndex, tick);
                case CommandKind.Submit:
                    return Submit();

                // ── 디버그 ── 판정을 갖지 않는다(시나리오가 곧 의도다).
                case CommandKind.DebugSpawnEnemy: return DebugSpawn(cmd, tick);
                case CommandKind.DebugDestroy: return DebugDestroy(cmd, tick);
                case CommandKind.DebugSetObstacle: return DebugObstacle(cmd);
                case CommandKind.DebugSpawnDefender: return DebugSpawnDefender(cmd, tick);
                case CommandKind.DebugForceWave:
                    return _waves.ForceNext() ? Receipt.Ok : Receipt.Reject(RejectReason.NoMoreWaves);

                default: return Receipt.Reject(RejectReason.UnknownCommand);
            }
        }

        // 디버그 스폰 — **판정을 갖지 않는다**. 배치 마스크·점유·코스트를 전부 건너뛰므로
        // 골든이 「방어유닛이 선 판」을 배치 판정 없이 세울 수 있다.
        // 스폰 배선 자체는 `PlacementService` 와 **같은 함수**를 지난다 — 두 벌이면
        // 「어떤 경로로 태어났나」가 공격·점유 규칙을 바꾼다.
        private Receipt DebugSpawnDefender(in Command cmd, int tick)
        {
            if (cmd.DefIndex < 0 || cmd.DefIndex >= _def.Units.Length)
                return Receipt.Reject(RejectReason.InvalidUnit);

            ref var d = ref _def.Units[cmd.DefIndex];
            int w = math.max(1, d.FootprintWidth);
            int h = math.max(1, d.FootprintHeight);
            _placement.SpawnDefender(cmd.DefIndex, cmd.Cell, w, h, cmd.Facing, tick, deploying: false);
            return Receipt.Ok;
        }

        private Receipt Submit()
        {
            if (!_clock.SubmitUnlocked) return Receipt.Reject(RejectReason.SubmitLocked);
            _clock.EndMatch(MatchEndReason.Submitted);
            return Receipt.Ok;
        }

        // 디버그 스폰. 레인을 주면 그 입구 칸에서 나오고 **경로·측면 분산도 그 레인에서** 나온다 —
        // 웨이브 생성기도 같은 배선(`EnemySpawn`)을 쓴다.
        private Receipt DebugSpawn(in Command cmd, int tick)
        {
            if (cmd.DefIndex < 0 || cmd.DefIndex >= _def.Enemies.Length)
                return Receipt.Reject(RejectReason.InvalidUnit);
            if (cmd.Lane >= 0 && _map.Snapshot.Spawns.Length == 0)
                return Receipt.Reject(RejectReason.MissingMap);

            var u = EnemySpawn.At(_ctx, _map, cmd.DefIndex, cmd.Lane, cmd.Cell, -1, tick);
            return u != null ? Receipt.Ok : Receipt.Reject(RejectReason.MissingMap);
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

        // 틱 문맥. 커맨드는 **틱 밖**(동기)에서 들어오는데 스폰 조립이 문맥(정의표·월드·풀)을
        // 요구한다. 판당 한 벌을 `BattleMatch` 가 넘겨 주므로 언제나 최신이다.
        private TickContext _ctx;

        public void Bind(TickContext ctx) => _ctx = ctx;
    }
}
