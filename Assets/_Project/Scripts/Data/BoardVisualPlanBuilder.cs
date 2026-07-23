using System.Collections.Generic;
using Unity.Mathematics;

namespace Wassup.Data
{
    public static class BoardVisualPlanBuilder
    {
        private static readonly int2[] CardinalDirections =
        {
            new int2(0, 1),
            new int2(1, 0),
            new int2(0, -1),
            new int2(-1, 0),
        };

        public static BoardVisualPlan Build(in GeneratedMap map, int visualSeed)
        {
            if (!map.IsCreated)
                return new BoardVisualPlan(visualSeed, int2.zero, null, null, null);

            int width = map.gridSize.x;
            int height = map.gridSize.y;
            int cellCount = width * height;
            if (width <= 0 || height <= 0 || cellCount <= 0)
                return new BoardVisualPlan(visualSeed, map.gridSize, null, null, null);

            var zones = new BoardZoneType[cellCount];
            var regionIds = new int[cellCount];
            var pathDistances = new byte[cellCount];
            var borderDistances = new byte[cellCount];
            var cells = new BoardVisualCell[cellCount];
            var regions = new List<BoardVisualRegion>(8);
            var decorAnchors = new List<BoardDecorAnchor>(16);
            var spawns = new int2[map.spawns.Length];
            for (int i = 0; i < spawns.Length; i++)
                spawns[i] = map.spawns[i];

            for (int i = 0; i < cellCount; i++)
                regionIds[i] = -1;

            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int index = y * width + x;
                zones[index] = ToBoardZoneType(map.tiles[index]);
            }

            ComputePathProximity(map.gridSize, zones, pathDistances);
            ComputeBorderProximity(map.gridSize, borderDistances);
            BuildRegions(map.gridSize, zones, pathDistances, borderDistances, regionIds, regions, decorAnchors);

            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int index = y * width + x;
                BoardZoneType zone = zones[index];
                byte sameZoneMask = GetNeighborMask(map.gridSize, zones, x, y, zone);
                byte transitionMask = GetTransitionMask(map.gridSize, zones, x, y, zone);
                byte envNeighborMask = (byte)(GetNeighborMask(map.gridSize, zones, x, y, BoardZoneType.Env) & 15);
                var shapeClass = BoardShapeUtility.GetShapeForMask(sameZoneMask);
                byte innerCornerMask = BoardShapeUtility.GetInnerCornerMask(sameZoneMask);
                byte pathProximity = pathDistances[index];
                byte borderProximity = borderDistances[index];
                uint surfaceNoiseHash = ComputeSurfaceNoiseHash(visualSeed, x, y);
                float decorBudgetBias = ComputeDecorBudgetBias(pathProximity, borderProximity);

                cells[index] = new BoardVisualCell(
                    map.tiles[index],
                    zone,
                    regionIds[index],
                    sameZoneMask,
                    transitionMask,
                    envNeighborMask,
                    innerCornerMask,
                    shapeClass,
                    surfaceNoiseHash,
                    decorBudgetBias,
                    pathProximity,
                    borderProximity);
            }

            // multi-goal-map — goals 전체를 plan 에 실어 배경프랍 클리어런스가 모든 골을 회피(폴백 [goal]).
            int2[] goals = (map.goals.IsCreated && map.goals.Length > 0) ? map.goals.ToArray() : new[] { map.goal };
            return new BoardVisualPlan(visualSeed, map.gridSize, map.goal, spawns, cells, regions.ToArray(), decorAnchors.ToArray(), goals);
        }

        private static void ComputePathProximity(int2 gridSize, BoardZoneType[] zones, byte[] distances)
        {
            int width = gridSize.x;
            int height = gridSize.y;
            var queue = new Queue<int2>(width * height);

            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int index = y * width + x;
                if (zones[index] == BoardZoneType.Walk)
                {
                    distances[index] = 0;
                    queue.Enqueue(new int2(x, y));
                }
                else
                {
                    distances[index] = BoardVisualCell.NoPathProximity;
                }
            }

            while (queue.Count > 0)
            {
                int2 current = queue.Dequeue();
                int currentIndex = current.y * width + current.x;
                byte baseDistance = distances[currentIndex];

                for (int i = 0; i < CardinalDirections.Length; i++)
                {
                    int2 next = current + CardinalDirections[i];
                    if (!IsInside(gridSize, next))
                        continue;

                    int nextIndex = next.y * width + next.x;
                    byte nextDistance = (byte)math.min(byte.MaxValue, baseDistance + 1);
                    if (nextDistance >= distances[nextIndex])
                        continue;

                    distances[nextIndex] = nextDistance;
                    queue.Enqueue(next);
                }
            }
        }

        private static void ComputeBorderProximity(int2 gridSize, byte[] distances)
        {
            int width = gridSize.x;
            int height = gridSize.y;
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int borderDistance = math.min(math.min(x, width - 1 - x), math.min(y, height - 1 - y));
                distances[y * width + x] = (byte)math.max(0, borderDistance);
            }
        }

        private static void BuildRegions(
            int2 gridSize,
            BoardZoneType[] zones,
            byte[] pathDistances,
            byte[] borderDistances,
            int[] regionIds,
            List<BoardVisualRegion> regions,
            List<BoardDecorAnchor> decorAnchors)
        {
            int width = gridSize.x;
            int height = gridSize.y;
            var queue = new Queue<int2>(32);
            var members = new List<int2>(32);

            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int startIndex = y * width + x;
                if (regionIds[startIndex] >= 0)
                    continue;

                BoardZoneType zone = zones[startIndex];
                int regionId = regions.Count;
                regionIds[startIndex] = regionId;
                queue.Enqueue(new int2(x, y));
                members.Clear();
                members.Add(new int2(x, y));

                int2 min = new int2(x, y);
                int2 max = new int2(x, y);

                while (queue.Count > 0)
                {
                    int2 current = queue.Dequeue();
                    for (int i = 0; i < CardinalDirections.Length; i++)
                    {
                        int2 next = current + CardinalDirections[i];
                        if (!IsInside(gridSize, next))
                            continue;

                        int nextIndex = next.y * width + next.x;
                        if (regionIds[nextIndex] >= 0 || zones[nextIndex] != zone)
                            continue;

                        regionIds[nextIndex] = regionId;
                        queue.Enqueue(next);
                        members.Add(next);
                        min = math.min(min, next);
                        max = math.max(max, next);
                    }
                }

                int2 anchorCell = ChooseAnchorCell(members);
                regions.Add(new BoardVisualRegion(regionId, zone, members.Count, min, max, anchorCell));
                AddDecorAnchors(gridSize, regionIds, pathDistances, borderDistances, zone, regionId, members, anchorCell, decorAnchors);
            }
        }

        private static int2 ChooseAnchorCell(List<int2> members)
        {
            int2 min = members[0];
            int2 max = members[0];
            for (int i = 1; i < members.Count; i++)
            {
                min = math.min(min, members[i]);
                max = math.max(max, members[i]);
            }

            float2 desired = new float2(min.x + max.x, min.y + max.y) * 0.5f;
            int bestScore = int.MaxValue;
            int2 bestCell = members[0];

            for (int i = 0; i < members.Count; i++)
            {
                int score =
                    (int)math.round(math.abs(members[i].x - desired.x) * 100f) +
                    (int)math.round(math.abs(members[i].y - desired.y) * 100f);
                if (score >= bestScore)
                    continue;

                bestScore = score;
                bestCell = members[i];
            }

            return bestCell;
        }

        private static void AddDecorAnchors(
            int2 gridSize,
            int[] regionIds,
            byte[] pathDistances,
            byte[] borderDistances,
            BoardZoneType zone,
            int regionId,
            List<int2> members,
            int2 anchorCell,
            List<BoardDecorAnchor> decorAnchors)
        {
            if (zone != BoardZoneType.Env)
                return;

            members.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
            decorAnchors.Add(new BoardDecorAnchor(BoardDecorAnchorType.RegionCenter, anchorCell, regionId));
            var categorized = new bool[members.Count];

            for (int i = 0; i < members.Count; i++)
            {
                int2 cell = members[i];
                int edgeCount = 0;
                if (!IsRegionMember(gridSize, regionIds, regionId, cell + new int2(0, 1))) edgeCount++;
                if (!IsRegionMember(gridSize, regionIds, regionId, cell + new int2(1, 0))) edgeCount++;
                if (!IsRegionMember(gridSize, regionIds, regionId, cell + new int2(0, -1))) edgeCount++;
                if (!IsRegionMember(gridSize, regionIds, regionId, cell + new int2(-1, 0))) edgeCount++;

                if (edgeCount <= 0 || cell.Equals(anchorCell))
                    continue;

                decorAnchors.Add(new BoardDecorAnchor(BoardDecorAnchorType.RegionEdge, cell, regionId));
                categorized[i] = true;

                int index = cell.y * gridSize.x + cell.x;
                if (borderDistances[index] <= 1)
                    decorAnchors.Add(new BoardDecorAnchor(BoardDecorAnchorType.OuterBorder, cell, regionId));
            }

            for (int i = 0; i < members.Count; i++)
            {
                int2 cell = members[i];
                int index = cell.y * gridSize.x + cell.x;
                if (pathDistances[index] != 1)
                    continue;

                decorAnchors.Add(new BoardDecorAnchor(BoardDecorAnchorType.NearWalkButSafe, cell, regionId));
                categorized[i] = true;
            }

            int fillerStride = 4;
            int fillerPhase = 0;
            for (int i = 0; i < members.Count; i++)
            {
                if (categorized[i] || members[i].Equals(anchorCell))
                    continue;

                if (fillerPhase++ % fillerStride != 0)
                    continue;

                decorAnchors.Add(new BoardDecorAnchor(BoardDecorAnchorType.Filler, members[i], regionId));
            }
        }

        private static byte GetNeighborMask(int2 gridSize, BoardZoneType[] zones, int x, int y, BoardZoneType targetZone)
        {
            int width = gridSize.x;
            int height = gridSize.y;
            byte mask = 0;

            if (y + 1 < height && zones[(y + 1) * width + x] == targetZone) mask |= 1;
            if (x + 1 < width && zones[y * width + (x + 1)] == targetZone) mask |= 2;
            if (y - 1 >= 0 && zones[(y - 1) * width + x] == targetZone) mask |= 4;
            if (x - 1 >= 0 && zones[y * width + (x - 1)] == targetZone) mask |= 8;
            if (x + 1 < width && y + 1 < height && zones[(y + 1) * width + (x + 1)] == targetZone) mask |= 16;
            if (x + 1 < width && y - 1 >= 0 && zones[(y - 1) * width + (x + 1)] == targetZone) mask |= 32;
            if (x - 1 >= 0 && y - 1 >= 0 && zones[(y - 1) * width + (x - 1)] == targetZone) mask |= 64;
            if (x - 1 >= 0 && y + 1 < height && zones[(y + 1) * width + (x - 1)] == targetZone) mask |= 128;

            return mask;
        }

        private static uint ComputeSurfaceNoiseHash(int visualSeed, int x, int y)
        {
            unchecked
            {
                uint h = (uint)visualSeed;
                h ^= (uint)(x * 374761393);
                h ^= (uint)(y * 668265263);
                h = (h ^ (h >> 13)) * 1274126177u;
                return h;
            }
        }

        private static float ComputeDecorBudgetBias(byte pathProximity, byte borderProximity)
        {
            float pathPenalty = pathProximity == BoardVisualCell.NoPathProximity ? 0f : pathProximity / 8f;
            float borderPenalty = borderProximity == 0 ? 0.3f : 0f;
            return math.saturate(1f - pathPenalty - borderPenalty);
        }

        private static byte GetTransitionMask(int2 gridSize, BoardZoneType[] zones, int x, int y, BoardZoneType zone)
        {
            int width = gridSize.x;
            int height = gridSize.y;
            byte mask = 0;

            if (y + 1 < height && zones[(y + 1) * width + x] != zone) mask |= 1;
            if (x + 1 < width && zones[y * width + (x + 1)] != zone) mask |= 2;
            if (y - 1 >= 0 && zones[(y - 1) * width + x] != zone) mask |= 4;
            if (x - 1 >= 0 && zones[y * width + (x - 1)] != zone) mask |= 8;

            return mask;
        }

        private static bool IsRegionMember(int2 gridSize, int[] regionIds, int regionId, int2 cell)
        {
            if (!IsInside(gridSize, cell))
                return false;

            return regionIds[cell.y * gridSize.x + cell.x] == regionId;
        }

        private static bool IsInside(int2 gridSize, int2 cell)
        {
            return cell.x >= 0 && cell.y >= 0 && cell.x < gridSize.x && cell.y < gridSize.y;
        }

        private static BoardZoneType ToBoardZoneType(MapTileType tileType)
        {
            return tileType switch
            {
                MapTileType.Walk => BoardZoneType.Walk,
                MapTileType.Place => BoardZoneType.Place,
                _ => BoardZoneType.Env,
            };
        }
    }
}
