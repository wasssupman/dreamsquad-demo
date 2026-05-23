using Unity.Collections;
using Unity.Mathematics;

namespace Wassup.Data.MapGrid
{
    public static class MapGridValidator
    {
        public enum FailReason : byte
        {
            Ok = 0,
            Disconnected = 1,
            GoalDegreeNotOne = 2,
            SpawnDegreeNotOne = 3,
            HasTwoByTwoBlock = 4,
            BranchTooShort = 5,
            BranchTooFewTurns = 6,
        }

        public static FailReason Validate(
            in PathBuildResult build,
            int2 gridSize,
            int2 goal,
            NativeArray<int2> spawns,
            MapGridGenerationSettings settings)
        {
            if (!build.IsValid || !build.pathCells.IsCreated) return FailReason.Disconnected;

            var path = build.pathCells;
            int goalIdx = MapGridIndex.CellIndex(goal, gridSize);

            // 1. connectivity from goal
            int totalReached = BfsCount(goal, gridSize, path);
            if (totalReached != path.Count) return FailReason.Disconnected;

            // 2. degree checks
            if (DegreeOf(goal, gridSize, path) != 1) return FailReason.GoalDegreeNotOne;
            for (int i = 0; i < spawns.Length; i++)
                if (DegreeOf(spawns[i], gridSize, path) != 1) return FailReason.SpawnDegreeNotOne;

            // 3. 2x2 block
            if (HasTwoByTwoBlock(path, gridSize)) return FailReason.HasTwoByTwoBlock;

            // 4. branch length / turns — 길이를 먼저 검사
            int minCells = settings.EffectiveMinBranchCellCount(gridSize);
            int minTurns = settings.EffectiveMinBranchTurnCount(gridSize);

            for (int i = 0; i < spawns.Length; i++)
            {
                if (!MeasureBranch(spawns[i], goal, gridSize, path, out int cellCount, out int turnCount))
                    return FailReason.Disconnected;

                if (cellCount < minCells) return FailReason.BranchTooShort;
                if (turnCount < minTurns) return FailReason.BranchTooFewTurns;
            }

            return FailReason.Ok;
        }

        // BFS spawn → goal. 셀 수 = cellCount (포함 양 끝), turn count = 90° 꺾임 횟수.
        public static bool MeasureBranch(
            int2 spawn, int2 goal, int2 gridSize, NativeHashSet<int> path,
            out int cellCount, out int turnCount)
        {
            cellCount = 0;
            turnCount = 0;

            int n = gridSize.x * gridSize.y;
            var prev = new NativeArray<int>(n, Allocator.TempJob);
            try
            {
                for (int i = 0; i < n; i++) prev[i] = -1;

                var queue = new NativeQueue<int>(Allocator.TempJob);
                try
                {
                    int spawnIdx = MapGridIndex.CellIndex(spawn, gridSize);
                    int goalIdx = MapGridIndex.CellIndex(goal, gridSize);
                    prev[spawnIdx] = spawnIdx;
                    queue.Enqueue(spawnIdx);

                    bool reached = false;
                    while (queue.TryDequeue(out int cur))
                    {
                        if (cur == goalIdx) { reached = true; break; }
                        int2 c = MapGridIndex.IndexToCell(cur, gridSize);
                        for (int d = 0; d < 4; d++)
                        {
                            int2 nb = NeighborOf(c, d);
                            if (!MapGridIndex.InBounds(nb, gridSize)) continue;
                            int nbIdx = MapGridIndex.CellIndex(nb, gridSize);
                            if (!path.Contains(nbIdx)) continue;
                            if (prev[nbIdx] != -1) continue;
                            prev[nbIdx] = cur;
                            queue.Enqueue(nbIdx);
                        }
                    }

                    if (!reached) return false;

                    // 역추적 + cell 수 + 꺾임 횟수
                    int idx = goalIdx;
                    int prevIdx = -1;
                    int2 lastDir = new int2(0, 0);
                    bool hasLastDir = false;
                    int cells = 0;
                    int turns = 0;

                    while (true)
                    {
                        cells++;
                        if (prevIdx != -1)
                        {
                            int2 cur2 = MapGridIndex.IndexToCell(idx, gridSize);
                            int2 prv2 = MapGridIndex.IndexToCell(prevIdx, gridSize);
                            int2 dir = new int2(math.sign(cur2.x - prv2.x), math.sign(cur2.y - prv2.y));
                            if (hasLastDir && !math.all(dir == lastDir)) turns++;
                            lastDir = dir;
                            hasLastDir = true;
                        }

                        if (idx == spawnIdx) break;
                        prevIdx = idx;
                        idx = prev[idx];
                    }

                    cellCount = cells;
                    turnCount = turns;
                    return true;
                }
                finally
                {
                    queue.Dispose();
                }
            }
            finally
            {
                prev.Dispose();
            }
        }

        private static int BfsCount(int2 start, int2 gridSize, NativeHashSet<int> path)
        {
            int n = gridSize.x * gridSize.y;
            var visited = new NativeArray<bool>(n, Allocator.TempJob);
            var queue = new NativeQueue<int>(Allocator.TempJob);
            try
            {
                int startIdx = MapGridIndex.CellIndex(start, gridSize);
                if (!path.Contains(startIdx)) return 0;
                visited[startIdx] = true;
                queue.Enqueue(startIdx);
                int count = 0;
                while (queue.TryDequeue(out int cur))
                {
                    count++;
                    int2 c = MapGridIndex.IndexToCell(cur, gridSize);
                    for (int d = 0; d < 4; d++)
                    {
                        int2 nb = NeighborOf(c, d);
                        if (!MapGridIndex.InBounds(nb, gridSize)) continue;
                        int nbIdx = MapGridIndex.CellIndex(nb, gridSize);
                        if (!path.Contains(nbIdx)) continue;
                        if (visited[nbIdx]) continue;
                        visited[nbIdx] = true;
                        queue.Enqueue(nbIdx);
                    }
                }
                return count;
            }
            finally
            {
                visited.Dispose();
                queue.Dispose();
            }
        }

        private static bool HasTwoByTwoBlock(NativeHashSet<int> path, int2 gridSize)
        {
            for (int y = 0; y < gridSize.y - 1; y++)
                for (int x = 0; x < gridSize.x - 1; x++)
                {
                    int a = MapGridIndex.CellIndex(new int2(x, y), gridSize);
                    int b = MapGridIndex.CellIndex(new int2(x + 1, y), gridSize);
                    int c = MapGridIndex.CellIndex(new int2(x, y + 1), gridSize);
                    int d = MapGridIndex.CellIndex(new int2(x + 1, y + 1), gridSize);
                    if (path.Contains(a) && path.Contains(b) && path.Contains(c) && path.Contains(d)) return true;
                }
            return false;
        }

        private static int DegreeOf(int2 cell, int2 gridSize, NativeHashSet<int> path)
        {
            int deg = 0;
            for (int d = 0; d < 4; d++)
            {
                int2 nb = NeighborOf(cell, d);
                if (!MapGridIndex.InBounds(nb, gridSize)) continue;
                if (path.Contains(MapGridIndex.CellIndex(nb, gridSize))) deg++;
            }
            return deg;
        }

        private static int2 NeighborOf(int2 cell, int dir)
        {
            switch (dir)
            {
                case 0: return new int2(cell.x + 1, cell.y);
                case 1: return new int2(cell.x - 1, cell.y);
                case 2: return new int2(cell.x, cell.y + 1);
                default: return new int2(cell.x, cell.y - 1);
            }
        }
    }
}
