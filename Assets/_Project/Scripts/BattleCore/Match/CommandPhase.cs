using System.Collections.Generic;
using Unity.Mathematics;
using Wassup.Battle.Units;

namespace Wassup.BattleCore
{
    // battle-core-rebuild unit 1 — phase 0. 커맨드의 자리.
    //
    // **`Execute` 는 `BattleMatch.Apply` 가 곧바로 부른다**(틱을 기다리지 않는다) —
    // 그것이 「동기 + receipt」의 뜻이다. 이 클래스가 파이프라인의 0번에도 서 있는 것은
    // 그 자리가 **Immediate seam**(커맨드가 만든 사건의 same-frame 하류)이기 때문이고,
    // 그 드레인은 트리거 레이어가 생기는 unit 7 에서 `Run` 안으로 들어온다.
    //
    // 이 unit 의 판정은 **스텁**이다. 진짜 배치 판정(층 마스크 · footprint · 코스트 ·
    // 쿨다운 · 보드 상한)은 `PlacementService` 가 생기는 unit 4 의 몫이고, 여기 있는
    // 점유표는 그때 통째로 그쪽으로 간다.
    public sealed class CommandPhase : ITickPhase
    {
        public string Name => "Command";

        private readonly BattleWorld _world;
        private readonly MatchClock _clock;
        private readonly MatchDefinition _def;

        // 셀 점유 스텁. 키는 셀을 long 하나로 접은 것(사전 키가 struct 비교를 타지 않게).
        // 역방향(`개체 → 칸`)을 같이 드는 이유: 해제가 O(1) 이어야 하고, 무엇보다
        // **두 방향이 한 함수에서만 같이 바뀌어야** 쌍이 깨지지 않기 때문이다.
        private readonly Dictionary<long, int> _occupied = new Dictionary<long, int>(64);
        private readonly Dictionary<int, long> _cellOf = new Dictionary<int, long>(64);

        public CommandPhase(BattleWorld world, MatchClock clock, MatchDefinition def)
        {
            _world = world;
            _clock = clock;
            _def = def;
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
                default: return Receipt.Reject(RejectReason.UnknownCommand);
            }
        }

        private Receipt Place(in Command cmd, int tick)
        {
            if (cmd.DefIndex < 0 || cmd.DefIndex >= _def.Units.Length)
                return Receipt.Reject(RejectReason.InvalidUnit);

            long key = Key(cmd.Cell);
            if (_occupied.ContainsKey(key)) return Receipt.Reject(RejectReason.Occupied);

            ref var d = ref _def.Units[cmd.DefIndex];
            // `deploying: false` — 배치 페이즈(비행 → 배치 모션 → 활성화)는 그 길이를
            // 아는 담당자(`PlacementService`)가 생기는 unit 4 의 몫이다. 지금 true 로
            // 두면 **빠져나올 길이 없는** 상태가 되고, 다음 사람은 그것을 버그로 읽는다.
            var u = _world.Spawn(UnitKind.Defender, Faction.DefenderUnit, cmd.DefIndex,
                                 CellCenter(cmd.Cell), d.BodyRadiusTiles, d.Health,
                                 deploying: false, tick: tick);
            _occupied[key] = u.Id.Value;
            _cellOf[u.Id.Value] = key;
            return Receipt.Ok;
        }

        private Receipt RetireAt(in Command cmd, int tick)
        {
            var u = _world.Find(cmd.Target);
            if (u == null) return Receipt.Reject(RejectReason.NoSuchEntity);
            if (u.Kind != UnitKind.Defender) return Receipt.Reject(RejectReason.InvalidUnit);

            Release(u.Id);
            _world.Destroy(u.Id, tick);
            return Receipt.Ok;
        }

        private Receipt Submit()
        {
            if (!_clock.SubmitUnlocked) return Receipt.Reject(RejectReason.SubmitLocked);
            _clock.EndMatch(MatchEndReason.Submitted);
            return Receipt.Ok;
        }

        private Receipt DebugSpawn(in Command cmd, int tick)
        {
            if (cmd.DefIndex < 0 || cmd.DefIndex >= _def.Enemies.Length)
                return Receipt.Reject(RejectReason.InvalidUnit);

            ref var d = ref _def.Enemies[cmd.DefIndex];
            _world.Spawn(UnitKind.Enemy, Faction.EnemyUnit, cmd.DefIndex,
                         CellCenter(cmd.Cell), d.BodyRadius, d.Health,
                         deploying: false, tick: tick);
            return Receipt.Ok;
        }

        private Receipt DebugDestroy(in Command cmd, int tick)
        {
            if (!_world.IsAlive(cmd.Target)) return Receipt.Reject(RejectReason.NoSuchEntity);
            Release(cmd.Target);
            _world.Destroy(cmd.Target, tick);
            return Receipt.Ok;
        }

        // 점유 해제는 소멸과 **항상 짝**이다(UML §3 의 `Occupy`/`Release`). 쌍이 깨지면
        // 죽은 유닛이 칸을 영영 물고 있게 되고, 그 증상은 「가끔 못 놓는 칸」으로 나온다.
        private void Release(SimEntityId id)
        {
            if (!_cellOf.TryGetValue(id.Value, out long key)) return;
            _cellOf.Remove(id.Value);
            _occupied.Remove(key);
        }

        // 셀 → 판 좌표. 진짜 매핑(타일 크기·원점·보드 평면)은 `MapSnapshot` 이 채워지는
        // unit 2 의 몫이다. 이 unit 의 판은 비어 있어서 셀 번호를 그대로 쓴다.
        private static float3 CellCenter(int2 cell) => new float3(cell.x, 0f, cell.y);

        private static long Key(int2 cell) => ((long)cell.x << 32) ^ (uint)cell.y;
    }
}
