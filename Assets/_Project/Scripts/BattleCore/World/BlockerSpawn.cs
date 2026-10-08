using Unity.Mathematics;
using Somnia.Battle.Skills;
using Somnia.Battle.BattleCore.Map;

namespace Somnia.Battle.BattleCore
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
                                    int defIndex, int2 cell, int tick, float explodeDamage, out Reject reason)
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
            var u = world.Spawn(UnitKind.BlockingHazard, Faction.BlockingHazard, defIndex,
                                pos, bd.BodyRadius, bd.MaxHealth, deploying: false, tick: tick);
            if (u != null) u.BlockerExplodeDamage = explodeDamage;
            return u;
        }

        /// <summary>
        /// unit 7d — **부서진 길막이 터진다**(사망 seam 핸들러). 옛 `BarrelExplosionSystem`: 이번 틱 피해로 부서진 설치물마다
        /// 그 칸 중심에 즉발 광역 한 발 — 해결은 폭탄맨 평타와 **같은 길**(`TileAoe`)이다. 폭발 저작(`ExplodeDamage`)이 0 이면
        /// 안 터진다(기존 길막 무회귀). 피해는 설치물이 든다(`Unit.BlockerExplodeDamage` — 세운 탄의 피해 · U10). 문은 「부서짐」 하나다 — 시한 만료는 은퇴했다(6b).
        /// ⚠ **자리에 떨어지는 것**(몸 0 · 칸 반폭) · 적 전용(옛 `targetFaction = Enemy` 명시) · 통행 층 무필터(옛 기본 0).
        /// ⚠ 처치 귀속 = **그 설치물**. 옛 처치 점수는 킬러를 안 봤다 — 출처 없이 쏘면 새 코어에서 배럴로 잡은 적이
        /// 점수·각성을 안 준다(귀속된 죽음에만 처치 사건이 난다).
        /// </summary>
        public static void ExplodeBroken(TickContext ctx)
        {
            var def = ctx.Def;
            if (def.BlockingHazards == null || def.BlockingHazards.Length == 0) return;
            var units = ctx.World.Units;
            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i];
                if (u.Kind != UnitKind.BlockingHazard || !u.Dead || u.DeathTick != ctx.Tick) continue;
                if (u.DefIndex < 0 || u.DefIndex >= def.BlockingHazards.Length) continue;
                ref var bd = ref def.BlockingHazards[u.DefIndex];
                if (u.BlockerExplodeDamage <= 0f) continue;
                if (bd.ExplodeProjectileDefIndex < 0 || bd.ExplodeProjectileDefIndex >= def.Projectiles.Length)
                {
                    ctx.Warn($"[Blocker] '{bd.Id}' 폭발 피해가 저작됐는데 폭발 탄 줄이 없다 — 안 터진다.");
                    continue;
                }
                var map = ctx.Map;
                float3 impact = map != null && map.Snapshot.CellCount > 0 ? map.CenterOf(map.CellOf(u.Position)) : u.Position;
                var req = Combat.Projectile.ProjectileRequest.Empty;
                req.DefIndex = bd.ExplodeProjectileDefIndex;
                req.Movement = Combat.Projectile.MovementKind.SkyFall;
                req.Payload = Combat.Projectile.PayloadKind.TileAoe;
                req.Owner = u.Id;
                req.OwnerFaction = Faction.DefenderUnit;   // 플레이어가 놓은 설치물 — 적을 때린다
                req.TargetMask = (int)Faction.EnemyUnit;
                req.TargetLayers = 0;
                req.Origin = impact;
                req.Impact = impact;
                req.Damage = u.BlockerExplodeDamage;
                req.ImpactTileRange = bd.ExplodeTileRange;
                req.AoeTargetCap = bd.ExplodeTargetCap;
                req.OriginBodyRadius = 0f;   // 자리형
                req.FlightTime = 0f;         // 부서지는 순간이 폭발이다(예고를 주면 두 사건으로 갈린다)
                ctx.World.ProjectileRequests.Add(req);
            }
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
