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
        // boss-jjangssen unit 3 — 보스 면역 부여 거절 지점 2곳 중 하나(다른 하나는 CcApplySystem).
        public static void ApplyCc(EntityManager em, Entity target, CcEffect effect)
        {
            if (!em.Exists(target))
                return;
            if (em.HasComponent<StructureTag>(target))
                return;
            if (!em.HasBuffer<CcEffect>(target))
                return;
            if (em.HasComponent<Wassup.Battle.Combat.BossTag>(target)
                && CcActionLock.IsBossImmune(effect.kind))
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

        // active-ally-zone unit 0 — 아군 버프 모디파이어의 재발행 지속시간(초).
        // **밸런스 값이 아니라 프레임워크 지연 상한**이다: 장판을 벗어나거나 장판이 사라진 뒤
        // 버프가 풀리기까지의 최대 지연이 이 값이다(ModifierStatsAggregateSystem 의 clamp 상수와
        // 같은 성격).
        //
        // ⚠ 기준은 "한 프레임 시간" 이 아니라 **Unity 가 허용하는 최대 프레임 델타**다
        // (ProjectSettings Maximum Allowed Timestep = 0.3333). 이보다 작으면 히칭 프레임 한 번에
        // StatModifierTickSystem(UpdateAfter ModifierApplySystem)이 방금 갱신한 값을 넘어서 깎아
        // 슬롯을 제거하고, 그 프레임만 장판 안 전원이 base 스탯으로 돌아간다 — 조용하고 자가치유
        // 돼서 버그로 안 보이고 "버프가 일정하지 않다" 로만 느껴진다. 배율이 1을 넘길 여지까지 두고 0.5.
        // public 인 이유: PlayMode 테스트가 대기시간을 이 값에서 계산한다(상수 복제 금지).
        public const float AllyBuffApplySec = 0.5f;

        // active-ally-zone unit 0 — 아군 버프 장판. TornadoField 와 같은 캐리어 엔티티이고,
        // 멤버십 갱신은 AllyBuffFieldSystem 이 매 프레임 한다(스냅샷 아님). 재캐스트는 독립
        // 장판을 만든다 — 겹쳐도 merge 키가 같아 모디파이어는 한 슬롯으로 접힌다(refresh).
        public static Entity SpawnAllyBuffField(EntityManager em, int2 centerCell, int tileRange,
            StatKind stat, float magnitude, float duration)
        {
            var e = em.CreateEntity();
            em.AddComponentData(e, new AllyBuffField
            {
                centerCell = centerCell,
                tileRange  = tileRange,
                stat       = stat,
                magnitude  = magnitude,
                remaining  = duration,
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

        public static Entity SpawnHazard(
            EntityManager em, HazardSO so, int2 originCell, byte targetTraversalLayers = 0)
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
                {
                    var effect = so.effects[i];
                    effect.targetTraversalLayers = targetTraversalLayers;
                    effectsBuffer.Add(new HazardEffectsBuffer { effect = effect });
                }
            }

            return e;
        }

        // bomb-barrel-on-place unit 0 — explodeDataIndex 는 **브리지가 풀어 넘긴다**
        // (SO→투사체 index 해석은 레지스트리를 가진 브리지 몫). -1 = 미배선.
        public static Entity SpawnBlockingHazard(EntityManager em, BlockingHazardSO so, int2 originCell,
            int hazardSoIndex, int explodeDataIndex = -1)
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
                // unit 7 — 길막 설치물에 시한은 없다. 부서져야만 사라진다.
                remainingLife = float.PositiveInfinity,
            });
            if (so.explodeDamage > 0f && explodeDataIndex < 0)
                Debug.LogWarning($"[BlockingHazard] '{so.name}' has explodeDamage {so.explodeDamage} " +
                                 "but no explodeProjectile wired — the blast would borrow projectile 0's visual.");
            em.AddComponentData(entity, new BlockingHazard
            {
                hazardSoIndex = hazardSoIndex,
                maxHp = so.maxHp,
                explodeDamage = so.explodeDamage,
                explodeTileRange = so.explodeTileRange,
                explodeTargetCap = so.explodeTargetCap,
                explodeDataIndex = explodeDataIndex,
            });
            var buffer = em.AddBuffer<OccupiedCellsBuffer>(entity);
            for (int i = 0; i < cells.Count; i++)
                buffer.Add(new OccupiedCellsBuffer { cell = cells[i] });

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
