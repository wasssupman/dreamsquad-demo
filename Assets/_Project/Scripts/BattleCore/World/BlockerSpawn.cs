using Unity.Mathematics;
using Wassup.Battle.Units;
using Wassup.BattleCore.Map;

namespace Wassup.BattleCore
{
    // battle-core-rebuild unit 6b — **길막 설치물을 세우는 단 하나의 문.**
    //
    // 생산자가 둘이다(탄 착탄 `PayloadKind.SpawnBlocker` · 디버그 커맨드). 각자 세우면 자리
    // 검증이 한쪽에만 붙는다 — 옛 전투도 `EffectSpawner.CanSpawnBlockingHazard` 한 곳이었다.
    //
    // 거절 넷(옛 `ValidateCellsForBlockingHazard` 그대로):
    //   · 판 밖 · **골 칸**(길막이 골을 덮으면 흐름장이 골을 잃는다) · 이미 막힌 칸 ·
    //     방어유닛 점유 칸(다칸 유닛은 **점유 rect 전체**를 본다 — 옛 unit 10 의 수정).
    //
    // ⚠ 개체는 **유닛**이다(`UnitKind.BlockingHazard`) — 체력이 있고 맞고 부서진다. 그래서
    // 소멸은 `BattleWorld.Destroy`(= `UnitDestroyed`) 한 문이고 여기 제거 경로가 없다.
    // 통행을 막는 것은 개체가 아니라 `FieldPrepPhase` 의 장애물 재수집이다(unit 2 경로).
    public static class BlockerSpawn
    {
        public enum Reject : byte { None = 0, NoDefinition, OutOfBounds, GoalCell, Blocked, Occupied }

        /// <summary>
        /// `defIndex` 줄의 길막을 `cell` 중심에 세운다. 거절이면 null 이고 이유를 `reason` 에 싣는다.
        /// </summary>
        public static Unit TrySpawn(BattleWorld world, MapRuntime map, MatchDefinition def,
                                    int defIndex, int2 cell, int tick, out Reject reason)
        {
            if (def == null || defIndex < 0 || defIndex >= def.BlockingHazards.Length)
            {
                reason = Reject.NoDefinition;
                return null;
            }
            ref var bd = ref def.BlockingHazards[defIndex];
            if (bd.MaxHealth <= 0f) { reason = Reject.NoDefinition; return null; }

            reason = Validate(map, cell, bd.SpanRadius);
            if (reason != Reject.None) return null;

            float3 pos = map != null ? map.CenterOf(cell) : new float3(cell.x, 0f, cell.y);
            return world.Spawn(UnitKind.BlockingHazard, Faction.BlockingHazard, defIndex,
                               pos, bd.BodyRadius, bd.MaxHealth, deploying: false, tick: tick);
        }

        private static Reject Validate(MapRuntime map, int2 center, int span)
        {
            if (map == null || map.Snapshot.CellCount == 0) return Reject.None;   // 맵 없는 픽스처
            var snap = map.Snapshot;
            var blocked = map.Obstacles.Blocked;
            for (int dy = -span; dy <= span; dy++)
            for (int dx = -span; dx <= span; dx++)
            {
                var c = new int2(center.x + dx, center.y + dy);
                if (!snap.InBounds(c)) return Reject.OutOfBounds;
                if (snap.IsGoalCell(c)) return Reject.GoalCell;
                if (blocked[GridMath.CellIndex(c, map.GridSize)]) return Reject.Blocked;
                if (map.Occupancy.IsOccupied(c)) return Reject.Occupied;
            }
            return Reject.None;
        }
    }
}
