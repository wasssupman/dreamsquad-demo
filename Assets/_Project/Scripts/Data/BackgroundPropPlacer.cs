using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;

namespace Wassup.Data
{
    public static class BackgroundPropPlacer
    {
        public static List<PropPlacement> Generate(BoardVisualPlan plan, MapThemeData theme, int seed)
        {
            var placements = new List<PropPlacement>();
            if (plan == null || theme == null || theme.tileProps == null || theme.tileProps.Length == 0)
                return placements;

            float density = math.saturate(theme.tilePropDensity);
            if (density <= 0f)
                return placements;

            var occupied = new NativeArray<bool>(plan.gridSize.x * plan.gridSize.y, Allocator.Temp);
            try
            {
                var rng = new Random((uint)math.max(1, seed));
                int maxCount = theme.maxTilePropCount;
                int clusterId = 0;
                var recentPropIndices = new Queue<int>();
                var candidatesByRegion = BuildCandidatesByRegion(plan, theme, density, ref rng);

                for (int i = 0; i < plan.DecorAnchors.Count; i++)
                {
                    if (maxCount > 0 && placements.Count >= maxCount)
                        break;

                    var anchor = plan.DecorAnchors[i];
                    if (!TrySelectProp(plan, theme, anchor, density, recentPropIndices, ref rng, out int propIndex))
                        continue;

                    var prop = theme.tileProps[propIndex];
                    var candidates = GetRegionCandidates(candidatesByRegion, anchor.regionId);
                    if (!TryPlaceNearestCandidate(plan, theme, prop, propIndex, anchor.cell, candidates, occupied, placements, ref rng, clusterId))
                    {
                        if (!TryPlace(plan, theme, prop, propIndex, anchor.cell, occupied, placements, ref rng, clusterId))
                            continue;
                    }

                    TrackRecentProp(recentPropIndices, propIndex, theme.propRepeatAvoidanceWindow);

                    if (prop.clusterProbability > 0f && rng.NextFloat() <= prop.clusterProbability)
                    {
                        int count = math.min(math.max(1, prop.clusterCount), math.max(1, plan.Regions[anchor.regionId].cellCount / 8));
                        for (int c = 1; c < count; c++)
                        {
                            if (maxCount > 0 && placements.Count >= maxCount)
                                break;

                            if (!TryPlaceNearestCandidate(plan, theme, prop, propIndex, anchor.cell, candidates, occupied, placements, ref rng, clusterId, math.max(1, prop.clusterRadius)))
                                break;
                        }
                    }

                    clusterId++;
                }
            }
            finally
            {
                if (occupied.IsCreated) occupied.Dispose();
            }

            return placements;
        }

        public static List<int2> GenerateRegionCandidates(BoardVisualPlan plan, BoardVisualRegion region, int minDistance, int maxCount, int maxAttempts, ref Random rng)
        {
            var regionCells = CollectRegionCells(plan, region);
            Shuffle(regionCells, ref rng);

            var candidates = new List<int2>(math.max(1, maxCount));
            if (regionCells.Count == 0 || maxCount <= 0)
                return candidates;

            candidates.Add(region.anchorCell);
            int attempts = math.min(math.max(1, maxAttempts), regionCells.Count);
            int minDistanceSq = math.max(1, minDistance) * math.max(1, minDistance);
            for (int i = 0; i < attempts && candidates.Count < maxCount; i++)
            {
                var cell = regionCells[i];
                bool valid = true;
                for (int c = 0; c < candidates.Count; c++)
                {
                    int2 delta = cell - candidates[c];
                    if (math.dot(delta, delta) >= minDistanceSq)
                        continue;

                    valid = false;
                    break;
                }

                if (valid)
                    candidates.Add(cell);
            }

            candidates.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
            return candidates;
        }

        public static bool CanFit(BoardVisualPlan plan, PropData prop, NativeArray<bool> occupied, int x, int y)
        {
            if (plan == null || prop == null || prop.prefab == null)
                return false;

            int width = math.max(1, prop.footprintX);
            int height = math.max(1, prop.footprintY);
            if (x < 0 || y < 0 || x + width > plan.gridSize.x || y + height > plan.gridSize.y)
                return false;

            for (int dy = 0; dy < height; dy++)
            for (int dx = 0; dx < width; dx++)
            {
                int2 cell = new int2(x + dx, y + dy);
                int index = cell.y * plan.gridSize.x + cell.x;
                if (occupied.IsCreated && occupied[index])
                    return false;
                if (!IsBackgroundCell(plan.CellAt(cell)))
                    return false;
            }

            return true;
        }

        public static bool IsBackgroundCell(BoardVisualCell cell)
            => cell.zoneType == BoardZoneType.Env;

        private static List<int2>[] BuildCandidatesByRegion(BoardVisualPlan plan, MapThemeData theme, float density, ref Random rng)
        {
            var result = new List<int2>[plan.Regions.Count];
            for (int i = 0; i < plan.Regions.Count; i++)
            {
                var region = plan.Regions[i];
                if (region.zoneType != BoardZoneType.Env)
                {
                    result[i] = new List<int2>();
                    continue;
                }

                int minDistance = math.max(1, theme.propPoissonMinDistance);
                int areaCap = math.max(1, (int)math.ceil(region.cellCount * density));
                int densityCap = math.max(1, region.cellCount / math.max(1, theme.propPoissonDensityPerRegionArea));
                int maxCount = math.min(areaCap, densityCap);
                result[i] = GenerateRegionCandidates(plan, region, minDistance, maxCount, theme.propPoissonMaxAttemptsPerRegion, ref rng);
            }

            return result;
        }

        private static List<int2> GetRegionCandidates(List<int2>[] candidatesByRegion, int regionId)
            => regionId >= 0 && regionId < candidatesByRegion.Length ? candidatesByRegion[regionId] : null;

        private static bool TrySelectProp(
            BoardVisualPlan plan,
            MapThemeData theme,
            BoardDecorAnchor anchor,
            float density,
            Queue<int> recentPropIndices,
            ref Random rng,
            out int propIndex)
        {
            propIndex = -1;
            var cell = plan.CellAt(anchor.cell);
            float total = 0f;
            var weights = new float[theme.tileProps.Length];
            for (int i = 0; i < theme.tileProps.Length; i++)
            {
                var prop = theme.tileProps[i];
                if (!IsEligible(plan, prop, anchor, cell))
                    continue;

                float spawnGoalMultiplier = IsNearSpawnOrGoal(plan, anchor.cell, math.max(0, theme.spawnGoalPropAvoidRadius))
                    ? math.saturate(theme.spawnGoalPropDensityMultiplier)
                    : 1f;
                float weight = math.max(0, prop.placementWeight) * cell.decorBudgetBias * density * spawnGoalMultiplier;
                int area = math.max(1, prop.footprintX) * math.max(1, prop.footprintY);
                if (area > math.max(1, theme.pathAdjacentSmallPropMaxArea) && cell.pathProximity <= 1)
                    weight *= math.saturate(theme.pathAdjacentLargePropWeightMultiplier);
                if (theme.propRepeatAvoidanceWindow > 0 && theme.tileProps.Length > 1 && recentPropIndices.Contains(i))
                    weight = 0f;
                weights[i] = weight;
                total += weight;
            }

            if (total <= 0f || rng.NextFloat() > math.saturate(total))
                return false;

            float roll = rng.NextFloat(0f, total);
            for (int i = 0; i < weights.Length; i++)
            {
                roll -= weights[i];
                if (roll > 0f)
                    continue;

                propIndex = i;
                return true;
            }

            return false;
        }

        private static bool TryPlaceNearestCandidate(
            BoardVisualPlan plan,
            MapThemeData theme,
            PropData prop,
            int propIndex,
            int2 anchorCell,
            List<int2> candidates,
            NativeArray<bool> occupied,
            List<PropPlacement> placements,
            ref Random rng,
            int clusterId,
            int maxDistance = int.MaxValue)
        {
            if (candidates == null || candidates.Count == 0)
                return false;

            int bestIndex = -1;
            int bestScore = int.MaxValue;
            for (int i = 0; i < candidates.Count; i++)
            {
                int2 delta = candidates[i] - anchorCell;
                int distance = math.cmax(math.abs(delta));
                if (distance > maxDistance)
                    continue;

                int score = math.dot(delta, delta);
                if (score >= bestScore)
                    continue;

                if (!CanPlaceAtCandidate(plan, theme, prop, candidates[i], occupied, placements))
                    continue;

                bestIndex = i;
                bestScore = score;
            }

            if (bestIndex < 0)
                return false;

            var cell = candidates[bestIndex];
            candidates.RemoveAt(bestIndex);
            return TryPlace(plan, theme, prop, propIndex, cell, occupied, placements, ref rng, clusterId);
        }

        private static bool CanPlaceAtCandidate(BoardVisualPlan plan, MapThemeData theme, PropData prop, int2 cell, NativeArray<bool> occupied, IReadOnlyList<PropPlacement> placements)
        {
            int width = math.max(1, prop.footprintX);
            int height = math.max(1, prop.footprintY);
            int x = cell.x - width / 2;
            int y = cell.y - height / 2;
            return CanFit(plan, prop, occupied, x, y)
                && !ViolatesMinDistance(prop, x, y, placements)
                && !ViolatesSameCategory(theme, prop, x, y, placements);
        }

        private static bool TryPlace(
            BoardVisualPlan plan,
            MapThemeData theme,
            PropData prop,
            int propIndex,
            int2 cell,
            NativeArray<bool> occupied,
            List<PropPlacement> placements,
            ref Random rng,
            int clusterId)
        {
            int width = math.max(1, prop.footprintX);
            int height = math.max(1, prop.footprintY);
            int x = cell.x - width / 2;
            int y = cell.y - height / 2;
            if (!CanFit(plan, prop, occupied, x, y)
                || ViolatesMinDistance(prop, x, y, placements)
                || ViolatesSameCategory(theme, prop, x, y, placements))
                return false;

            for (int dy = 0; dy < height; dy++)
            for (int dx = 0; dx < width; dx++)
                occupied[(y + dy) * plan.gridSize.x + x + dx] = true;

            float yaw = prop.billboardMode == PropBillboardMode.FullCamera ? 0f : rng.NextFloat(-prop.rotationJitterDegrees, prop.rotationJitterDegrees);
            float scale = 1f + rng.NextFloat(-prop.scaleJitter, prop.scaleJitter);
            placements.Add(new PropPlacement(propIndex, x, y, width, height, rng.NextUInt(), yaw, scale, clusterId));
            return true;
        }

        private static bool IsEligible(BoardVisualPlan plan, PropData prop, BoardDecorAnchor anchor, BoardVisualCell cell)
        {
            if (prop == null || prop.prefab == null || prop.placementWeight <= 0)
                return false;

            if (anchor.regionId < 0 || anchor.regionId >= plan.Regions.Count)
                return false;

            if (plan.Regions[anchor.regionId].cellCount < prop.preferredRegionSizeMin)
                return false;

            if (prop.preferredAnchorTypes != null && prop.preferredAnchorTypes.Length > 0)
            {
                bool matches = false;
                for (int i = 0; i < prop.preferredAnchorTypes.Length; i++)
                    matches |= prop.preferredAnchorTypes[i] == anchor.anchorType;
                if (!matches)
                    return false;
            }

            int path = cell.pathProximity == BoardVisualCell.NoPathProximity ? 255 : cell.pathProximity;
            return path >= prop.pathProximityRange.x &&
                   path <= prop.pathProximityRange.y &&
                   cell.borderProximity >= prop.borderProximityRange.x &&
                   cell.borderProximity <= prop.borderProximityRange.y;
        }

        private static bool ViolatesMinDistance(PropData prop, int x, int y, IReadOnlyList<PropPlacement> placements)
        {
            int minDistance = math.max(0, prop.minDistanceCells);
            if (minDistance <= 0)
                return false;

            for (int i = 0; i < placements.Count; i++)
            {
                float dx = math.abs(x - placements[i].x);
                float dy = math.abs(y - placements[i].y);
                if (math.max(dx, dy) < minDistance)
                    return true;
            }

            return false;
        }

        // 같은 category 프랍이 Chebyshev 거리 sameCategoryMinDistanceCells 안에 있으면 차단.
        // 꽃처럼 연속 배치가 어색한 타입을 흩어놓는 룰. category 비었거나 0 이면 무효 → 그 외엔 랜덤 허용.
        // flower_y/p/w 처럼 변종이 여러 PropData 라도 category 만 같으면 한 그룹으로 본다.
        private static bool ViolatesSameCategory(MapThemeData theme, PropData prop, int x, int y, IReadOnlyList<PropPlacement> placements)
        {
            int minDistance = math.max(0, prop.sameCategoryMinDistanceCells);
            if (minDistance <= 0 || string.IsNullOrEmpty(prop.category) || theme == null || theme.tileProps == null)
                return false;

            for (int i = 0; i < placements.Count; i++)
            {
                int idx = placements[i].propIndex;
                if (idx < 0 || idx >= theme.tileProps.Length)
                    continue;
                var other = theme.tileProps[idx];
                if (other == null || other.category != prop.category)
                    continue;

                float dx = math.abs(x - placements[i].x);
                float dy = math.abs(y - placements[i].y);
                if (math.max(dx, dy) < minDistance)
                    return true;
            }

            return false;
        }

        private static bool IsNearSpawnOrGoal(BoardVisualPlan plan, int2 cell, int radius)
        {
            if (radius <= 0)
                return false;

            if (math.cmax(math.abs(cell - plan.goal)) <= radius)
                return true;

            for (int i = 0; i < plan.spawns.Length; i++)
            {
                if (math.cmax(math.abs(cell - plan.spawns[i])) <= radius)
                    return true;
            }

            return false;
        }

        private static List<int2> CollectRegionCells(BoardVisualPlan plan, BoardVisualRegion region)
        {
            var cells = new List<int2>(region.cellCount);
            for (int y = region.min.y; y <= region.max.y; y++)
            for (int x = region.min.x; x <= region.max.x; x++)
            {
                var cell = new int2(x, y);
                if (plan.CellAt(cell).regionId == region.id)
                    cells.Add(cell);
            }

            return cells;
        }

        private static void Shuffle(List<int2> cells, ref Random rng)
        {
            for (int i = cells.Count - 1; i > 0; i--)
            {
                int j = rng.NextInt(0, i + 1);
                var tmp = cells[i];
                cells[i] = cells[j];
                cells[j] = tmp;
            }
        }

        private static void TrackRecentProp(Queue<int> recentPropIndices, int propIndex, int window)
        {
            if (window <= 0)
                return;

            recentPropIndices.Enqueue(propIndex);
            while (recentPropIndices.Count > window)
                recentPropIndices.Dequeue();
        }
    }
}
