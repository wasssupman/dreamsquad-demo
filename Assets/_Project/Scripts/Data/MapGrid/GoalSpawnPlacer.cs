using Unity.Collections;
using Unity.Mathematics;
using Random = Unity.Mathematics.Random;

namespace Wassup.Data.MapGrid
{
    public static class GoalSpawnPlacer
    {
        // Adaptive 6-section layout: W >= H → 3 cols × 2 rows. W < H → 2 cols × 3 rows.
        public static int2 GetLayout(int2 gridSize) =>
            gridSize.x >= gridSize.y ? new int2(3, 2) : new int2(2, 3);

        public static GoalSpawnResult Pick(
            ref Random rng,
            int2 gridSize,
            MapGridGenerationSettings settings,
            Allocator allocator)
        {
            if (settings == null) return default;

            int2 layout = GetLayout(gridSize);
            int sectionCount = layout.x * layout.y; // = 6

            int goalSection = rng.NextInt(0, sectionCount);
            int2 goalAnchor = GetSectionAnchor(goalSection, layout, gridSize);
            int radius = settings.CornerZoneRadius;

            if (!TryPickInZone(ref rng, goalSection, layout, gridSize, goalAnchor, radius,
                               default, 0, default, 0, out int2 goal))
                return default;

            int spawnCount = rng.NextInt(settings.MinSpawnCount, settings.MaxSpawnCount + 1);
            if (spawnCount < 2 || spawnCount > 4) return default;

            int goalDist = settings.EffectiveSpawnToGoalMinManhattan(gridSize);
            int spawnDist = settings.SpawnToSpawnMinManhattan;

            var picked = new NativeList<int2>(spawnCount, Allocator.TempJob);
            var spawnSections = ShuffleNonGoalSections(ref rng, sectionCount, goalSection);

            try
            {
                int activeMask = 0;
                for (int i = 0; i < spawnCount; i++)
                {
                    int sIdx = spawnSections[i];
                    int2 anchor = GetSectionAnchor(sIdx, layout, gridSize);
                    if (!TryPickInZone(ref rng, sIdx, layout, gridSize, anchor, radius,
                                       goal, goalDist, picked, spawnDist, out int2 cell))
                        return default;
                    picked.Add(cell);
                    activeMask |= (1 << sIdx);
                }

                var spawns = new NativeArray<int2>(picked.Length, allocator);
                for (int i = 0; i < picked.Length; i++) spawns[i] = picked[i];

                return new GoalSpawnResult
                {
                    goal = goal,
                    spawns = spawns,
                    activeQuadrantMask = activeMask,
                    IsValid = true,
                };
            }
            finally
            {
                picked.Dispose();
                spawnSections.Dispose();
            }
        }

        // section 내부 anchor zone 의 셀들을 셔플 후 distance 룰 만족하는 첫 셀.
        // picked 가 default (NativeList.Length=0) 인 goal pick 시엔 distance check skip.
        private static bool TryPickInZone(
            ref Random rng, int sectionIdx, int2 layout, int2 gridSize,
            int2 anchor, int radius,
            int2 goal, int goalDist, NativeList<int2> alreadyPicked, int spawnDist,
            out int2 result)
        {
            GetSectionBounds(sectionIdx, layout, gridSize,
                             out int xmin, out int xmax, out int ymin, out int ymax);

            int zx0 = math.max(xmin, anchor.x - radius + 1);
            int zx1 = math.min(xmax, anchor.x + radius - 1);
            int zy0 = math.max(ymin, anchor.y - radius + 1);
            int zy1 = math.min(ymax, anchor.y + radius - 1);

            int width = math.max(0, zx1 - zx0 + 1);
            int height = math.max(0, zy1 - zy0 + 1);
            int capacity = math.max(1, width * height);

            var cells = new NativeList<int2>(capacity, Allocator.TempJob);
            try
            {
                for (int y = zy0; y <= zy1; y++)
                    for (int x = zx0; x <= zx1; x++)
                        cells.Add(new int2(x, y));

                // Fisher-Yates shuffle
                for (int i = cells.Length - 1; i > 0; i--)
                {
                    int j = rng.NextInt(0, i + 1);
                    var tmp = cells[i]; cells[i] = cells[j]; cells[j] = tmp;
                }

                bool checkGoal = alreadyPicked.IsCreated;
                for (int i = 0; i < cells.Length; i++)
                {
                    var c = cells[i];
                    if (checkGoal)
                    {
                        if (math.all(c == goal)) continue;
                        if (MapGridIndex.Manhattan(c, goal) < goalDist) continue;

                        bool farEnough = true;
                        for (int k = 0; k < alreadyPicked.Length; k++)
                        {
                            if (MapGridIndex.Manhattan(alreadyPicked[k], c) < spawnDist)
                            {
                                farEnough = false;
                                break;
                            }
                        }
                        if (!farEnough) continue;
                    }

                    result = c;
                    return true;
                }
            }
            finally
            {
                cells.Dispose();
            }

            result = default;
            return false;
        }

        public static int2 GetSectionAnchor(int sectionIdx, int2 layout, int2 gridSize)
        {
            int col = sectionIdx % layout.x;
            int row = sectionIdx / layout.x;
            int sectionW = gridSize.x / layout.x;
            int sectionH = gridSize.y / layout.y;

            int sx = col * sectionW + sectionW / 2;
            int sy = row * sectionH + sectionH / 2;

            int ax = (col == 0) ? 0 : (col == layout.x - 1 ? gridSize.x - 1 : sx);
            int ay = (row == 0) ? 0 : (row == layout.y - 1 ? gridSize.y - 1 : sy);

            return new int2(ax, ay);
        }

        public static void GetSectionBounds(
            int sectionIdx, int2 layout, int2 gridSize,
            out int xmin, out int xmax, out int ymin, out int ymax)
        {
            int col = sectionIdx % layout.x;
            int row = sectionIdx / layout.x;
            int sectionW = gridSize.x / layout.x;
            int sectionH = gridSize.y / layout.y;

            xmin = col * sectionW;
            xmax = (col == layout.x - 1) ? gridSize.x - 1 : (col + 1) * sectionW - 1;
            ymin = row * sectionH;
            ymax = (row == layout.y - 1) ? gridSize.y - 1 : (row + 1) * sectionH - 1;
        }

        // {0..sectionCount-1} - {goalSection} 를 Fisher-Yates 부분 셔플로 반환.
        private static NativeList<int> ShuffleNonGoalSections(ref Random rng, int sectionCount, int goalSection)
        {
            var list = new NativeList<int>(sectionCount - 1, Allocator.TempJob);
            for (int i = 0; i < sectionCount; i++) if (i != goalSection) list.Add(i);
            for (int i = list.Length - 1; i > 0; i--)
            {
                int j = rng.NextInt(0, i + 1);
                int tmp = list[i]; list[i] = list[j]; list[j] = tmp;
            }
            return list;
        }
    }
}
