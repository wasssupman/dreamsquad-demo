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

                for (int i = 0; i < plan.DecorAnchors.Count; i++)
                {
                    if (maxCount > 0 && placements.Count >= maxCount)
                        break;

                    var anchor = plan.DecorAnchors[i];
                    if (!TrySelectProp(plan, theme, anchor, density, recentPropIndices, ref rng, out int propIndex))
                        continue;

                    var prop = theme.tileProps[propIndex];
                    if (!TryPlace(plan, prop, propIndex, anchor.cell, occupied, placements, ref rng, clusterId))
                        continue;
                    TrackRecentProp(recentPropIndices, propIndex, theme.propRepeatAvoidanceWindow);

                    if (prop.clusterProbability > 0f && rng.NextFloat() <= prop.clusterProbability)
                    {
                        int count = math.max(1, prop.clusterCount);
                        int radius = math.max(1, prop.clusterRadius);
                        for (int c = 1; c < count; c++)
                        {
                            if (maxCount > 0 && placements.Count >= maxCount)
                                break;

                            var offset = new int2(rng.NextInt(-radius, radius + 1), rng.NextInt(-radius, radius + 1));
                            if (math.abs(offset.x) + math.abs(offset.y) > radius)
                                continue;

                            TryPlace(plan, prop, propIndex, anchor.cell + offset, occupied, placements, ref rng, clusterId);
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

        private static bool TryPlace(
            BoardVisualPlan plan,
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
            if (!CanFit(plan, prop, occupied, x, y) || ViolatesMinDistance(prop, x, y, placements))
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
