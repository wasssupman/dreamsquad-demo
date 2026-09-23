using Unity.Mathematics;
using Wassup.Battle.Units;
using Wassup.BattleCore.Map;
using Wassup.BattleCore.Move;

namespace Wassup.BattleCore
{
    // battle-core-rebuild unit 4 — 적 하나를 세우는 **단일 배선**.
    //
    // 스폰 경로가 셋이다(디버그 커맨드 · 웨이브 · 보너스 웨이브). 셋이 각자 조립하면
    // 「어떤 경로로 태어났나」가 이동·감지·공격 규칙을 바꾼다 — 옛 전투에서 분열 자식만
    // 레인·경로를 안 물려받던 것이 그런 갈림의 사례다. 그래서 함수 하나를 지난다.
    //
    // 매니저가 아니다: 상태가 없고 판정도 없다. 「값 → 개체」의 조립뿐이다.
    public static class EnemySpawn
    {
        /// <summary>
        /// 적 하나. `lane` 이 0 이상이면 그 입구 칸에서 나오고 `cell` 은 무시된다.
        /// `conceptPathIndex` 는 웨이브 컨셉이 지정한 경로(-1 = 무지정).
        ///
        /// 측면 분산은 **RNG 없는 이산 N-레인 round-robin** 이다 — 같은 순번이면 같은 자리라
        /// 결정론이 구조적으로 성립한다. |오프셋| 은 반 칸을 못 넘는다(M14).
        /// </summary>
        public static Unit At(TickContext ctx, MapRuntime map, int defIndex, int lane,
                              int2 cell, int conceptPathIndex, int tick)
        {
            ref var d = ref ctx.Def.Enemies[defIndex];
            var snapshot = map.Snapshot;

            if (lane >= 0)
            {
                if (snapshot.Spawns.Length == 0) return null;
                lane %= snapshot.Spawns.Length;
                cell = snapshot.Spawns[lane];
            }

            float3 pos = snapshot.CellCount > 0
                ? snapshot.CellCenter(cell)
                : new float3(cell.x, 0f, cell.y);

            if (lane >= 0)
            {
                // ⚠ 분산 값은 **정의표에서** 온다(계약 6). 여기 리터럴을 되돌리지 말 것 —
                // 그러면 적이 퍼지는 폭이 저작 밖에서 정해지고 `configHash` 가 그것을 못 본다.
                ref var mt = ref ctx.Def.Movement;
                float2 heading = HeadingAt(map, cell, (byte)d.TraversalLayers);
                float frac = SpawnSpread.LaneFraction(ctx.World.SpawnOrdinal++,
                                                      mt.SpawnSubLaneCount,
                                                      mt.SpawnSpreadFraction,
                                                      mt.SpawnSpreadTopScale);
                pos += SpawnSpread.LateralOffset(frac, snapshot.TileSize, heading);
            }

            var u = ctx.World.Spawn(UnitKind.Enemy, Faction.EnemyUnit, defIndex,
                                    pos, d.BodyRadius, d.Health, deploying: false, tick: tick);

            u.Move = ctx.World.Parts.RentMove();
            u.Move.Speed = d.MoveSpeed;
            u.Move.Radius = ctx.Def.Movement.AgentRadiusTiles;
            u.Move.TraversalLayers = (byte)d.TraversalLayers;
            u.Move.Engage = (EngageMovement)math.clamp(d.EngageMovement, 0, 2);
            // 경로 선택 — **좁은 쪽이 이긴다**: 적 정의 > 웨이브 컨셉 > 레인 기본.
            u.Move.PathIndex = WaypointRouting.ResolvePathIndex(
                d.WaypointPathIndex, conceptPathIndex,
                lane >= 0 ? snapshot.RouteForSpawn(lane) : -1);

            // **감지 0 = 오늘과 같은 경로.** 부착 자체가 게이트다 — 분기가 아니라 부재로 표현한다.
            if (d.DetectionRange != 0f)
            {
                u.Detection = ctx.World.Parts.RentDetection();
                u.Detection.Range = d.DetectionRange;
            }

            // 적도 방어유닛과 **같은 함수**로 공격을 얻는다(통합 루프가 둘을 구분하지 않는다).
            u.Attack = CombatPhase.BuildAttackState(in d, ctx.Def, ctx.World.Parts);
            return u;
        }

        // 그 칸에서 골로 향하는 방향. 측면 분산이 **진행방향 수직**으로 벌리기 위한 값이다.
        private static float2 HeadingAt(MapRuntime map, int2 cell, byte layers)
        {
            if (map.Snapshot.CellCount == 0) return new float2(1f, 0f);
            var slot = map.Flow.GoalSlot(layers == 0 ? TraversalSlots.DefaultMask : layers);
            var dir = slot.DirAt(cell);
            return math.lengthsq(dir) > 1e-6f ? dir : new float2(1f, 0f);
        }
    }
}
