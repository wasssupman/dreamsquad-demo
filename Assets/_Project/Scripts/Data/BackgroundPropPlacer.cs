using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;

namespace Wassup.Data
{
    public static class BackgroundPropPlacer
    {
        public static List<PropPlacement> Generate(GeneratedMap map, MapThemeData theme, int seed)
        {
            var placements = new List<PropPlacement>();
            if (!map.IsCreated || theme == null || theme.tileProps == null || theme.tileProps.Length == 0)
                return placements;

            float density = math.clamp(theme.tilePropDensity, 0f, 1f);
            if (density <= 0f)
                return placements;

            int cellCount = map.gridSize.x * map.gridSize.y;
            var occupied = new NativeArray<bool>(cellCount, Allocator.Temp);
            var visited = new NativeArray<bool>(cellCount, Allocator.Temp);
            try
            {
                uint rngSeed = (uint)math.max(1, seed);
                var rng = new Random(rngSeed);
                int maxCount = theme.maxTilePropCount;

                while (maxCount <= 0 || placements.Count < maxCount)
                {
                    for (int i = 0; i < visited.Length; i++)
                        visited[i] = false;

                    bool placedThisPass = false;
                    for (int y = 0; y < map.gridSize.y; y++)
                    for (int x = 0; x < map.gridSize.x; x++)
                    {
                        if (maxCount > 0 && placements.Count >= maxCount)
                            return placements;

                        int index = y * map.gridSize.x + x;
                        if (visited[index] || occupied[index] || !IsBackgroundTile(map.TileAt(new int2(x, y))))
                            continue;

                        var region = FloodFillRegion(map, occupied, visited, x, y);
                        if (region.cellCount <= 0)
                            continue;

                        if (rng.NextFloat() > density)
                            continue;

                        var candidates = CollectCenteredCandidates(map, theme.tileProps, occupied, region);
                        if (candidates.Count == 0)
                            continue;

                        var candidate = candidates[rng.NextInt(0, candidates.Count)];
                        MarkOccupied(occupied, map.gridSize, candidate.x, candidate.y, candidate.width, candidate.height);
                        placements.Add(new PropPlacement(
                            candidate.propIndex,
                            candidate.x,
                            candidate.y,
                            candidate.width,
                            candidate.height,
                            rng.NextUInt()));
                        placedThisPass = true;
                    }

                    if (!placedThisPass)
                        break;
                }
            }
            finally
            {
                if (occupied.IsCreated) occupied.Dispose();
                if (visited.IsCreated) visited.Dispose();
            }

            return placements;
        }

        public static bool CanFit(GeneratedMap map, PropData prop, NativeArray<bool> occupied, int x, int y)
        {
            if (!map.IsCreated || prop == null || prop.prefab == null)
                return false;

            int width = math.max(1, prop.footprintX);
            int height = math.max(1, prop.footprintY);

            if (x < 0 || y < 0)
                return false;
            if (x + width > map.gridSize.x || y + height > map.gridSize.y)
                return false;

            for (int dy = 0; dy < height; dy++)
            for (int dx = 0; dx < width; dx++)
            {
                int cx = x + dx;
                int cy = y + dy;
                int index = cy * map.gridSize.x + cx;
                if (occupied.IsCreated && occupied[index])
                    return false;

                if (!IsBackgroundTile(map.TileAt(new int2(cx, cy))))
                    return false;
            }

            return true;
        }

        public static bool IsBackgroundTile(MapTileType tile)
        {
            return tile == MapTileType.Deco || tile == MapTileType.Env;
        }

        private static List<PlacementCandidate> CollectCenteredCandidates(
            GeneratedMap map,
            PropData[] props,
            NativeArray<bool> occupied,
            AvailableRegion region)
        {
            var candidates = new List<PlacementCandidate>();
            for (int i = 0; i < props.Length; i++)
            {
                if (TryFindCenteredFit(map, props[i], occupied, region, out var candidate))
                {
                    candidate.propIndex = i;
                    candidates.Add(candidate);
                }
            }

            return candidates;
        }

        private static bool TryFindCenteredFit(
            GeneratedMap map,
            PropData prop,
            NativeArray<bool> occupied,
            AvailableRegion region,
            out PlacementCandidate candidate)
        {
            candidate = default;
            if (prop == null || prop.prefab == null)
                return false;

            int width = math.max(1, prop.footprintX);
            int height = math.max(1, prop.footprintY);
            int maxX = region.maxX - width + 1;
            int maxY = region.maxY - height + 1;
            if (maxX < region.minX || maxY < region.minY)
                return false;

            float regionCenterX = (region.minX + region.maxX) * 0.5f;
            float regionCenterY = (region.minY + region.maxY) * 0.5f;
            float bestScore = float.MaxValue;
            bool found = false;

            for (int y = region.minY; y <= maxY; y++)
            for (int x = region.minX; x <= maxX; x++)
            {
                if (!CanFit(map, prop, occupied, x, y))
                    continue;

                float propCenterX = x + (width - 1) * 0.5f;
                float propCenterY = y + (height - 1) * 0.5f;
                float dx = propCenterX - regionCenterX;
                float dy = propCenterY - regionCenterY;
                float score = dx * dx + dy * dy;
                if (found && score >= bestScore)
                    continue;

                bestScore = score;
                candidate = new PlacementCandidate
                {
                    x = x,
                    y = y,
                    width = width,
                    height = height,
                };
                found = true;
            }

            return found;
        }

        private static AvailableRegion FloodFillRegion(
            GeneratedMap map,
            NativeArray<bool> occupied,
            NativeArray<bool> visited,
            int startX,
            int startY)
        {
            var region = new AvailableRegion
            {
                minX = startX,
                maxX = startX,
                minY = startY,
                maxY = startY,
            };

            var queue = new Queue<int2>();
            queue.Enqueue(new int2(startX, startY));
            visited[startY * map.gridSize.x + startX] = true;

            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                region.cellCount++;
                region.minX = math.min(region.minX, cell.x);
                region.maxX = math.max(region.maxX, cell.x);
                region.minY = math.min(region.minY, cell.y);
                region.maxY = math.max(region.maxY, cell.y);

                TryEnqueue(map, occupied, visited, queue, cell.x + 1, cell.y);
                TryEnqueue(map, occupied, visited, queue, cell.x - 1, cell.y);
                TryEnqueue(map, occupied, visited, queue, cell.x, cell.y + 1);
                TryEnqueue(map, occupied, visited, queue, cell.x, cell.y - 1);
            }

            return region;
        }

        private static void TryEnqueue(
            GeneratedMap map,
            NativeArray<bool> occupied,
            NativeArray<bool> visited,
            Queue<int2> queue,
            int x,
            int y)
        {
            if (x < 0 || y < 0 || x >= map.gridSize.x || y >= map.gridSize.y)
                return;

            int index = y * map.gridSize.x + x;
            if (visited[index] || occupied[index] || !IsBackgroundTile(map.TileAt(new int2(x, y))))
                return;

            visited[index] = true;
            queue.Enqueue(new int2(x, y));
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

        private struct AvailableRegion
        {
            public int minX;
            public int maxX;
            public int minY;
            public int maxY;
            public int cellCount;
        }

        private struct PlacementCandidate
        {
            public int propIndex;
            public int x;
            public int y;
            public int width;
            public int height;
        }
    }
}
