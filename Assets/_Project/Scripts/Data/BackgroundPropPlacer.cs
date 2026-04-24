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
            if (plan == null || plan.gridSize.x <= 0 || plan.gridSize.y <= 0 || theme == null || theme.tileProps == null || theme.tileProps.Length == 0)
                return placements;

            float density = math.clamp(theme.tilePropDensity, 0f, 1f);
            if (density <= 0f)
                return placements;

            int cellCount = plan.gridSize.x * plan.gridSize.y;
            var occupied = new NativeArray<bool>(cellCount, Allocator.Temp);
            try
            {
                uint rngSeed = (uint)math.max(1, seed);
                var rng = new Random(rngSeed);
                int maxCount = theme.maxTilePropCount;
                var recentPropIndices = new Queue<int>();

                while (maxCount <= 0 || placements.Count < maxCount)
                {
                    bool placedThisPass = false;
                    for (int i = 0; i < plan.Regions.Count; i++)
                    {
                        if (maxCount > 0 && placements.Count >= maxCount)
                            return placements;

                        var region = plan.Regions[i];
                        if (region.zoneType != BoardZoneType.Env || region.cellCount <= 0)
                            continue;

                        var candidates = CollectCenteredCandidates(plan, theme, occupied, region, placements, recentPropIndices, density, ref rng);
                        if (candidates.Count == 0)
                            continue;

                        RemoveRecentlyUsedCandidatesWhenAlternativesExist(candidates, recentPropIndices);
                        var candidate = SelectWeightedCandidate(candidates, ref rng);
                        MarkOccupied(occupied, plan.gridSize, candidate.x, candidate.y, candidate.width, candidate.height);
                        placements.Add(new PropPlacement(
                            candidate.propIndex,
                            candidate.x,
                            candidate.y,
                            candidate.width,
                            candidate.height,
                            rng.NextUInt()));
                        TrackRecentProp(recentPropIndices, candidate.propIndex, theme.propRepeatAvoidanceWindow);
                        placedThisPass = true;
                    }

                    if (!placedThisPass)
                        break;
                }
            }
            finally
            {
                // Temp occupancy is owned by this generation call and must always be released.
                if (occupied.IsCreated) occupied.Dispose();
            }

            return placements;
        }

        public static bool CanFit(BoardVisualPlan plan, PropData prop, NativeArray<bool> occupied, int x, int y)
        {
            if (plan == null || prop == null || prop.prefab == null)
                return false;

            int width = math.max(1, prop.footprintX);
            int height = math.max(1, prop.footprintY);

            if (x < 0 || y < 0)
                return false;
            if (x + width > plan.gridSize.x || y + height > plan.gridSize.y)
                return false;

            for (int dy = 0; dy < height; dy++)
            for (int dx = 0; dx < width; dx++)
            {
                int cx = x + dx;
                int cy = y + dy;
                int index = cy * plan.gridSize.x + cx;
                if (occupied.IsCreated && occupied[index])
                    return false;

                if (!IsBackgroundCell(plan.CellAt(new int2(cx, cy))))
                    return false;
            }

            return true;
        }

        public static bool IsBackgroundCell(BoardVisualCell cell)
            => cell.zoneType == BoardZoneType.Env;

        private static List<PlacementCandidate> CollectCenteredCandidates(
            BoardVisualPlan plan,
            MapThemeData theme,
            NativeArray<bool> occupied,
            BoardVisualRegion region,
            IReadOnlyList<PropPlacement> placements,
            Queue<int> recentPropIndices,
            float baseDensity,
            ref Random rng)
        {
            var candidates = new List<PlacementCandidate>();
            var props = theme.tileProps;
            for (int i = 0; i < props.Length; i++)
            {
                if (TryFindBestFit(plan, theme, props[i], occupied, region, out var candidate))
                {
                    candidate.propIndex = i;
                    candidate.weight = CalculateCandidateWeight(plan, theme, props[i], candidate, region, placements, recentPropIndices, baseDensity, ref rng);
                    if (candidate.weight <= 0f)
                        continue;

                    candidates.Add(candidate);
                }
            }

            return candidates;
        }

        private static bool TryFindBestFit(
            BoardVisualPlan plan,
            MapThemeData theme,
            PropData prop,
            NativeArray<bool> occupied,
            BoardVisualRegion region,
            out PlacementCandidate candidate)
        {
            candidate = default;
            if (prop == null || prop.prefab == null)
                return false;

            int width = math.max(1, prop.footprintX);
            int height = math.max(1, prop.footprintY);
            int maxX = region.max.x - width + 1;
            int maxY = region.max.y - height + 1;
            if (maxX < region.min.x || maxY < region.min.y)
                return false;

            float regionCenterX = (region.min.x + region.max.x) * 0.5f;
            float regionCenterY = (region.min.y + region.max.y) * 0.5f;
            bool preferOuter = width * height > math.max(1, theme.pathAdjacentSmallPropMaxArea) &&
                               theme.largePropInnerWeightMultiplier < 1f;
            float bestScore = float.MaxValue;
            bool found = false;

            for (int y = region.min.y; y <= maxY; y++)
            for (int x = region.min.x; x <= maxX; x++)
            {
                if (!CanFit(plan, prop, occupied, x, y))
                    continue;

                float propCenterX = x + (width - 1) * 0.5f;
                float propCenterY = y + (height - 1) * 0.5f;
                float dx = propCenterX - regionCenterX;
                float dy = propCenterY - regionCenterY;
                float score = dx * dx + dy * dy;
                if (preferOuter)
                {
                    int edgeDistance = math.min(
                        math.min(x, y),
                        math.min(plan.gridSize.x - (x + width), plan.gridSize.y - (y + height)));
                    score = edgeDistance * 100f + score * 0.01f;
                }
                if (found && score >= bestScore)
                    continue;

                bestScore = score;
                candidate = new PlacementCandidate
                {
                    x = x,
                    y = y,
                    width = width,
                    height = height,
                    centerX = propCenterX,
                    centerY = propCenterY,
                };
                found = true;
            }

            return found;
        }

        private static float CalculateCandidateWeight(
            BoardVisualPlan plan,
            MapThemeData theme,
            PropData prop,
            PlacementCandidate candidate,
            BoardVisualRegion region,
            IReadOnlyList<PropPlacement> placements,
            Queue<int> recentPropIndices,
            float baseDensity,
            ref Random rng)
        {
            float density = baseDensity;
            if (IsNearSpawnOrGoal(plan, candidate, math.max(0, theme.spawnGoalPropAvoidRadius)))
                density *= math.clamp(theme.spawnGoalPropDensityMultiplier, 0f, 1f);
            if (rng.NextFloat() > density)
                return 0f;

            if (ViolatesMinDistance(prop, candidate, placements))
                return 0f;

            float weight = math.max(0, prop.placementWeight);
            if (weight <= 0f)
                return 0f;

            int area = candidate.width * candidate.height;
            bool largeProp = area > math.max(1, theme.pathAdjacentSmallPropMaxArea);
            if (largeProp && IsAdjacentToWalk(plan, candidate))
                weight *= math.clamp(theme.pathAdjacentLargePropWeightMultiplier, 0f, 1f);

            if (largeProp)
            {
                bool nearOuterEdge = candidate.x <= 0 ||
                                     candidate.y <= 0 ||
                                     candidate.x + candidate.width >= plan.gridSize.x ||
                                     candidate.y + candidate.height >= plan.gridSize.y;
                if (!nearOuterEdge)
                    weight *= math.clamp(theme.largePropInnerWeightMultiplier, 0f, 1f);

                if (region.cellCount < area * 4)
                    weight *= 0.25f;
            }

            if (theme.propRepeatAvoidanceWindow > 0 && recentPropIndices.Contains(candidate.propIndex))
                weight *= 0.05f;

            return weight;
        }

        private static bool ViolatesMinDistance(PropData prop, PlacementCandidate candidate, IReadOnlyList<PropPlacement> placements)
        {
            int minDistance = math.max(0, prop.minDistanceCells);
            if (minDistance <= 0)
                return false;

            for (int i = 0; i < placements.Count; i++)
            {
                var placement = placements[i];
                float centerX = placement.x + (placement.width - 1) * 0.5f;
                float centerY = placement.y + (placement.height - 1) * 0.5f;
                float dx = math.abs(candidate.centerX - centerX);
                float dy = math.abs(candidate.centerY - centerY);
                if (math.max(dx, dy) < minDistance)
                    return true;
            }

            return false;
        }

        private static bool IsNearSpawnOrGoal(BoardVisualPlan plan, PlacementCandidate candidate, int radius)
        {
            if (radius <= 0)
                return false;

            if (DistanceToCell(candidate, plan.goal) <= radius)
                return true;

            for (int i = 0; i < plan.spawns.Length; i++)
            {
                if (DistanceToCell(candidate, plan.spawns[i]) <= radius)
                    return true;
            }

            return false;
        }

        private static float DistanceToCell(PlacementCandidate candidate, int2 cell)
        {
            float dx = math.abs(candidate.centerX - cell.x);
            float dy = math.abs(candidate.centerY - cell.y);
            return math.max(dx, dy);
        }

        private static bool IsAdjacentToWalk(BoardVisualPlan plan, PlacementCandidate candidate)
        {
            int minX = math.max(0, candidate.x - 1);
            int maxX = math.min(plan.gridSize.x - 1, candidate.x + candidate.width);
            int minY = math.max(0, candidate.y - 1);
            int maxY = math.min(plan.gridSize.y - 1, candidate.y + candidate.height);

            for (int y = minY; y <= maxY; y++)
            for (int x = minX; x <= maxX; x++)
            {
                bool insideFootprint = x >= candidate.x &&
                                       x < candidate.x + candidate.width &&
                                       y >= candidate.y &&
                                       y < candidate.y + candidate.height;
                if (insideFootprint)
                    continue;

                if (plan.CellAt(new int2(x, y)).zoneType == BoardZoneType.Walk)
                    return true;
            }

            return false;
        }

        private static void RemoveRecentlyUsedCandidatesWhenAlternativesExist(List<PlacementCandidate> candidates, Queue<int> recentPropIndices)
        {
            if (candidates.Count <= 1 || recentPropIndices.Count == 0)
                return;

            bool hasFreshCandidate = false;
            for (int i = 0; i < candidates.Count; i++)
            {
                if (recentPropIndices.Contains(candidates[i].propIndex))
                    continue;

                hasFreshCandidate = true;
                break;
            }

            if (!hasFreshCandidate)
                return;

            for (int i = candidates.Count - 1; i >= 0; i--)
            {
                if (recentPropIndices.Contains(candidates[i].propIndex))
                    candidates.RemoveAt(i);
            }
        }

        private static PlacementCandidate SelectWeightedCandidate(List<PlacementCandidate> candidates, ref Random rng)
        {
            float totalWeight = 0f;
            for (int i = 0; i < candidates.Count; i++)
                totalWeight += candidates[i].weight;

            if (totalWeight <= 0f)
                return candidates[rng.NextInt(0, candidates.Count)];

            float roll = rng.NextFloat(0f, totalWeight);
            for (int i = 0; i < candidates.Count; i++)
            {
                roll -= candidates[i].weight;
                if (roll <= 0f)
                    return candidates[i];
            }

            return candidates[candidates.Count - 1];
        }

        private static void TrackRecentProp(Queue<int> recentPropIndices, int propIndex, int window)
        {
            if (window <= 0)
                return;

            recentPropIndices.Enqueue(propIndex);
            while (recentPropIndices.Count > window)
                recentPropIndices.Dequeue();
        }

        private static void MarkOccupied(NativeArray<bool> occupied, int2 gridSize, int x, int y, int width, int height)
        {
            for (int dy = 0; dy < height; dy++)
            for (int dx = 0; dx < width; dx++)
            {
                int index = (y + dy) * gridSize.x + (x + dx);
                occupied[index] = true;
            }
        }

        private struct PlacementCandidate
        {
            public int propIndex;
            public int x;
            public int y;
            public int width;
            public int height;
            public float centerX;
            public float centerY;
            public float weight;
        }
    }
}
