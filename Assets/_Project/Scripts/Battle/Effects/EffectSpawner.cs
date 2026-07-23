using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Wassup.Battle.Movement;
using Wassup.Battle.Units;
using Wassup.Data;

namespace Wassup.Battle.Effects
{
    // Central choke point for adding/updating Effects-context components from
    // outside the Effects systems (typically BattleBridge.CastSkill*). Keeping
    // all writes behind this helper makes it straightforward to audit that only
    // Effects code mutates CcEffect and other Effects-context components.
    //
    // Apply semantics: if the entity already carries the effect, the longer
    // remaining time wins and the newly supplied multiplier replaces the old
    // one. This matches Phase 2's non-stackable assumption — re-casting just
    // refreshes/extends the effect.
    public static class EffectSpawner
    {
        // Adds or merges a CcEffect into the target's DynamicBuffer<CcEffect>.
        // 병합 정책은 CcEffectMerge 단일 소스(CcApplySystem 과 공유) — tickInterval/tickTimer
        // 포함 동일 규칙. 여기 Impulse/Sleep 은 tickInterval=0 이라 이산 tick 필드는 무영향.
        public static void ApplyCc(EntityManager em, Entity target, CcEffect effect)
        {
            if (!em.Exists(target))
                return;

            var buffer = em.GetBuffer<CcEffect>(target);
            CcEffectMerge.Apply(ref buffer, effect);
        }

        // Phase 8 §17 — Tornado: carrier entity with area data. MovementSystem
        // queries live TornadoField entities each frame and applies pull to any
        // attacker inside the radius (continuous, not snapshot). Re-cast spawns
        // an independent field; multiple fields can coexist.
        public static Entity SpawnTornadoField(EntityManager em, float3 centerWorld, int tileRange, float pullSpeed, float duration)
        {
            var e = em.CreateEntity();
            em.AddComponentData(e, new TornadoField
            {
                centerWorld = centerWorld,
                tileRange = tileRange,
                pullSpeed = pullSpeed,
                remaining = duration,
            });
            return e;
        }

        // Phase 7 — Portal: carrier entity with the two endpoints. Re-cast spawns a
        // separate link (player-decided overlap) rather than merging.
        // Phase 9: exitWaypointIndex parameter dropped. After teleport, next-frame
        // flow field lookup supplies the exit direction.
        public static Entity SpawnPortal(EntityManager em, float3 entryWorld, float3 exitWorld, float entryRadius, float duration)
        {
            var e = em.CreateEntity();
            em.AddComponentData(e, new PortalLink
            {
                entryWorld = entryWorld,
                exitWorld = exitWorld,
                entryRadius = entryRadius,
                remaining = duration,
            });
            return e;
        }

        public static Entity SpawnObstacle(EntityManager em, Unity.Mathematics.int2 cell, float3 worldPos, float lifetime)
        {
            var e = em.CreateEntity();
            em.AddComponentData(e, new Obstacle { cell = cell, worldPosition = worldPos, remainingLife = lifetime });
            return e;
        }

        public static Entity SpawnHazard(EntityManager em, HazardSO so, int2 originCell)
        {
            if (so == null) return Entity.Null;

            var e = em.CreateEntity();
            em.AddComponentData(e, new Hazard
            {
                remainingLife = so.lifetime,
            });

            var cellsBuffer = em.AddBuffer<HazardCellsBuffer>(e);
            var cells = HazardShapeSampler.Sample(so.shape, originCell, so.radius);
            for (int i = 0; i < cells.Count; i++)
                cellsBuffer.Add(new HazardCellsBuffer { cell = cells[i] });

            var effectsBuffer = em.AddBuffer<HazardEffectsBuffer>(e);
            if (so.effects != null)
            {
                for (int i = 0; i < so.effects.Length; i++)
                    effectsBuffer.Add(new HazardEffectsBuffer { effect = so.effects[i] });
            }

            return e;
        }

        public static Entity SpawnBlockingHazard(EntityManager em, BlockingHazardSO so, int2 originCell, int hazardSoIndex)
        {
            if (so == null) return Entity.Null;

            if (!CanSpawnBlockingHazard(em, so, originCell, out string reason))
            {
                Debug.LogWarning($"[BlockingHazard] spawn rejected at {originCell}: {reason}");
                return Entity.Null;
            }

            using var ffQuery = em.CreateEntityQuery(ComponentType.ReadOnly<FlowFieldSingleton>());
            ffQuery.TryGetSingleton<FlowFieldSingleton>(out var ff);
            var cells = HazardShapeSampler.Sample(so.shape, originCell, radius: 1);
            int2 centerCell = ComputeCenterCell(cells);
            float3 worldPos = GridMath.CellToWorldCenter(centerCell, ff.tileSize, origin: ff.origin);

            var entity = em.CreateEntity();
            em.AddComponentData(entity, new Obstacle
            {
                cell = centerCell,
                worldPosition = worldPos,
                remainingLife = float.PositiveInfinity,
            });
            em.AddComponentData(entity, new BlockingHazard
            {
                hazardSoIndex = hazardSoIndex,
                maxHp = so.maxHp,
            });
            var buffer = em.AddBuffer<BlockingHazardCellsBuffer>(entity);
            for (int i = 0; i < cells.Count; i++)
                buffer.Add(new BlockingHazardCellsBuffer { cell = cells[i] });

            em.AddComponentData(entity, new Health { value = so.maxHp, max = so.maxHp });
            em.AddBuffer<IncomingDamage>(entity);
            em.AddComponentData(entity, new FactionTag { value = Faction.BlockingHazard });
            em.AddComponentData(entity, LocalTransform.FromPosition(worldPos));
            return entity;
        }

        public static bool CanSpawnBlockingHazard(EntityManager em, BlockingHazardSO so, int2 originCell, out string reason)
        {
            if (so == null)
            {
                reason = "BlockingHazardSO is null";
                return false;
            }

            using var ffQuery = em.CreateEntityQuery(ComponentType.ReadOnly<FlowFieldSingleton>());
            if (!ffQuery.TryGetSingleton<FlowFieldSingleton>(out var ff))
            {
                reason = "FlowFieldSingleton missing";
                return false;
            }

            var cells = HazardShapeSampler.Sample(so.shape, originCell, radius: 1);
            if (cells == null || cells.Count == 0)
            {
                reason = "shape sampled no cells";
                return false;
            }

            return ValidateCellsForBlockingHazard(em, cells, ff, out reason);
        }

        private static bool ValidateCellsForBlockingHazard(
            EntityManager em,
            List<int2> cells,
            FlowFieldSingleton ff,
            out string reason)
        {
            for (int i = 0; i < cells.Count; i++)
            {
                int2 cell = cells[i];
                if (cell.x < 0 || cell.x >= ff.gridSize.x || cell.y < 0 || cell.y >= ff.gridSize.y)
                {
                    reason = $"cell {cell} is outside grid {ff.gridSize}";
                    return false;
                }

                // multi-goal-map — 차단 해저드가 어느 골이든 덮지 못하게(IsGoalCell = goals 멤버십/goalCell 폴백).
                if (ff.IsGoalCell(cell))
                {
                    reason = $"cell {cell} overlaps goal cell";
                    return false;
                }
            }

            using (var obstacleQuery = em.CreateEntityQuery(ComponentType.ReadOnly<ObstacleSingleton>()))
            {
                if (obstacleQuery.TryGetSingleton<ObstacleSingleton>(out var obstacleSingleton)
                    && obstacleSingleton.blockedCells.IsCreated)
                {
                    for (int i = 0; i < cells.Count; i++)
                    {
                        if (!obstacleSingleton.blockedCells.Contains(cells[i])) continue;
                        reason = $"cell {cells[i]} overlaps existing blocked cell";
                        return false;
                    }
                }
            }

            using (var defenderQuery = em.CreateEntityQuery(ComponentType.ReadOnly<DefenderTile>()))
            {
                var defenderTiles = defenderQuery.ToComponentDataArray<DefenderTile>(Unity.Collections.Allocator.Temp);
                try
                {
                    for (int i = 0; i < cells.Count; i++)
                    {
                        for (int j = 0; j < defenderTiles.Length; j++)
                        {
                            if (!cells[i].Equals(defenderTiles[j].cell)) continue;
                            reason = $"cell {cells[i]} overlaps defender tile";
                            return false;
                        }
                    }
                }
                finally
                {
                    defenderTiles.Dispose();
                }
            }

            reason = string.Empty;
            return true;
        }

        private static int2 ComputeCenterCell(List<int2> cells)
        {
            int sx = 0;
            int sy = 0;
            for (int i = 0; i < cells.Count; i++)
            {
                sx += cells[i].x;
                sy += cells[i].y;
            }

            return new int2(sx / cells.Count, sy / cells.Count);
        }

    }
}
