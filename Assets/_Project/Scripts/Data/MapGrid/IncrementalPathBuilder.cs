using Unity.Collections;
using Unity.Mathematics;
using Random = Unity.Mathematics.Random;

namespace Wassup.Data.MapGrid
{
    public static class IncrementalPathBuilder
    {
        public static PathBuildResult Build(
            ref Random rng,
            int2 gridSize,
            int2 goal,
            NativeArray<int2> spawns,
            MapGridGenerationSettings settings,
            Allocator allocator)
        {
            int capacity = gridSize.x * gridSize.y / 2;
            var path = new NativeHashSet<int>(capacity, allocator);
            var order = new NativeArray<int>(spawns.Length, allocator);

            path.Add(MapGridIndex.CellIndex(goal, gridSize));

            // spawnHashSet for O(1) "is spawn" lookup
            var spawnSet = new NativeHashSet<int>(spawns.Length, Allocator.TempJob);
            try
            {
                for (int i = 0; i < spawns.Length; i++)
                    spawnSet.Add(MapGridIndex.CellIndex(spawns[i], gridSize));

                for (int i = 0; i < spawns.Length; i++)
                {
                    bool firstRoute = (i == 0);
                    int2 spawn = spawns[i];
                    int spawnIdx = MapGridIndex.CellIndex(spawn, gridSize);

                    if (path.Contains(spawnIdx))
                    {
                        // spawn 이 기존 path 안에 이미 들어와 있으면 trivially 성공
                        order[i] = i;
                        continue;
                    }

                    bool ok = TryFindRoute(
                        ref rng, spawn, firstRoute, gridSize, goal, spawnSet,
                        path, settings);

                    if (!ok)
                    {
                        return new PathBuildResult
                        {
                            pathCells = path,
                            spawnOrder = order,
                            IsValid = false,
                        };
                    }

                    order[i] = i;
                }

                return new PathBuildResult
                {
                    pathCells = path,
                    spawnOrder = order,
                    IsValid = true,
                };
            }
            finally
            {
                spawnSet.Dispose();
            }
        }

        private static bool TryFindRoute(
            ref Random rng,
            int2 start,
            bool firstRoute,
            int2 gridSize,
            int2 goal,
            NativeHashSet<int> spawnSet,
            NativeHashSet<int> path,
            MapGridGenerationSettings settings)
        {
            int startIdx = MapGridIndex.CellIndex(start, gridSize);

            var attachCandidates = BuildAttachCandidates(firstRoute, goal, gridSize, spawnSet, path, ref rng);
            if (attachCandidates.Length == 0)
            {
                attachCandidates.Dispose();
                return false;
            }

            var waypoints = new NativeList<int2>(8, Allocator.TempJob);
            var cells = new NativeList<int2>(64, Allocator.TempJob);

            try
            {
                int routeAttempts = settings.MaxRouteAttempts;
                int midSamples = settings.RouteCandidateMidpointSamples;

                int minTurnsForRoute = math.max(1, settings.EffectiveMinBranchTurnCount(gridSize));
                for (int attempt = 0; attempt < routeAttempts; attempt++)
                {
                    int2 attach = attachCandidates[rng.NextInt(0, attachCandidates.Length)];

                    // 높은 turn 형태부터 시도. validator 의 effective minTurns 를 충족시키기 위해.
                    if (minTurnsForRoute >= 5)
                    {
                        for (int s = 0; s < midSamples; s++)
                        {
                            int mx1 = rng.NextInt(1, gridSize.x - 1);
                            int my1 = rng.NextInt(1, gridSize.y - 1);
                            int mx2 = rng.NextInt(1, gridSize.x - 1);
                            int my2 = rng.NextInt(1, gridSize.y - 1);
                            if (TryMultiTurnShape5(start, attach, startIdx, gridSize, mx1, my1, mx2, my2, path, spawnSet, waypoints, cells))
                                return true;
                        }
                    }

                    if (minTurnsForRoute >= 4)
                    {
                        for (int s = 0; s < midSamples; s++)
                        {
                            int mx1 = rng.NextInt(1, gridSize.x - 1);
                            int my = rng.NextInt(1, gridSize.y - 1);
                            int mx2 = rng.NextInt(1, gridSize.x - 1);
                            if (TryMultiTurnShape4(start, attach, startIdx, gridSize, mx1, my, mx2, path, spawnSet, waypoints, cells))
                                return true;
                        }
                    }

                    if (minTurnsForRoute <= 3)
                    {
                        // Z(shape 4/5 = 3 turns), U(shape 2/3 = 2 turns), L(shape 0/1 = 1 turn) 순서.
                        for (int s = 0; s < midSamples; s++)
                        {
                            int mx = rng.NextInt(1, gridSize.x - 1);
                            int my = rng.NextInt(1, gridSize.y - 1);

                            for (int shape = 4; shape < PathRouter.ShapeCount; shape++)
                            {
                                if (TryShape(shape, start, attach, startIdx, gridSize,
                                              mx, my, path, spawnSet, waypoints, cells))
                                    return true;
                            }
                        }
                    }

                    if (minTurnsForRoute <= 2)
                    {
                        for (int s = 0; s < midSamples; s++)
                        {
                            int mx = rng.NextInt(1, gridSize.x - 1);
                            int my = rng.NextInt(1, gridSize.y - 1);

                            for (int shape = 2; shape < 4; shape++)
                            {
                                if (TryShape(shape, start, attach, startIdx, gridSize,
                                              mx, my, path, spawnSet, waypoints, cells))
                                    return true;
                            }
                        }
                    }

                    if (minTurnsForRoute <= 1)
                    {
                        for (int shape = 0; shape < 2; shape++)
                        {
                            if (TryShape(shape, start, attach, startIdx, gridSize,
                                          0, 0, path, spawnSet, waypoints, cells))
                                return true;
                        }
                    }
                }

                return false;
            }
            finally
            {
                attachCandidates.Dispose();
                waypoints.Dispose();
                cells.Dispose();
            }
        }

        private static bool TryShape(
            int shape, int2 start, int2 attach, int startIdx, int2 gridSize,
            int mx, int my,
            NativeHashSet<int> path, NativeHashSet<int> spawnSet,
            NativeList<int2> waypoints, NativeList<int2> cells)
        {
            if (!PathRouter.TryBuildShape(shape, start, attach, gridSize, mx, my, waypoints))
                return false;
            if (!PathRouter.TryExpandToCells(waypoints, cells))
                return false;
            if (!IsValidRoute(cells, start, attach, startIdx, gridSize, path, spawnSet))
                return false;
            CommitRoute(cells, path, gridSize);
            return true;
        }

        private static bool TryMultiTurnShape4(
            int2 start, int2 attach, int startIdx, int2 gridSize,
            int mx1, int my, int mx2,
            NativeHashSet<int> path, NativeHashSet<int> spawnSet,
            NativeList<int2> waypoints, NativeList<int2> cells)
        {
            if (!PathRouter.TryBuild4Turn(start, attach, gridSize, mx1, my, mx2, waypoints)) return false;
            if (!PathRouter.TryExpandToCells(waypoints, cells)) return false;
            if (!IsValidRoute(cells, start, attach, startIdx, gridSize, path, spawnSet)) return false;
            CommitRoute(cells, path, gridSize);
            return true;
        }

        private static bool TryMultiTurnShape5(
            int2 start, int2 attach, int startIdx, int2 gridSize,
            int mx1, int my1, int mx2, int my2,
            NativeHashSet<int> path, NativeHashSet<int> spawnSet,
            NativeList<int2> waypoints, NativeList<int2> cells)
        {
            if (!PathRouter.TryBuild5Turn(start, attach, gridSize, mx1, my1, mx2, my2, waypoints)) return false;
            if (!PathRouter.TryExpandToCells(waypoints, cells)) return false;
            if (!IsValidRoute(cells, start, attach, startIdx, gridSize, path, spawnSet)) return false;
            CommitRoute(cells, path, gridSize);
            return true;
        }

        private static NativeList<int2> BuildAttachCandidates(
            bool firstRoute, int2 goal, int2 gridSize,
            NativeHashSet<int> spawnSet, NativeHashSet<int> path, ref Random rng)
        {
            var list = new NativeList<int2>(16, Allocator.TempJob);
            if (firstRoute)
            {
                list.Add(goal);
                return list;
            }

            int goalIdx = MapGridIndex.CellIndex(goal, gridSize);

            var enumerator = path.GetEnumerator();
            while (enumerator.MoveNext())
            {
                int idx = enumerator.Current;
                if (idx == goalIdx) continue;
                if (spawnSet.Contains(idx)) continue;

                int2 c = MapGridIndex.IndexToCell(idx, gridSize);
                if (DegreeInPath(c, gridSize, path) <= 2)
                    list.Add(c);
            }

            // Fisher-Yates shuffle
            for (int i = list.Length - 1; i > 0; i--)
            {
                int j = rng.NextInt(0, i + 1);
                var tmp = list[i]; list[i] = list[j]; list[j] = tmp;
            }

            return list;
        }

        private static bool IsValidRoute(
            NativeList<int2> cells, int2 start, int2 attach, int startIdx, int2 gridSize,
            NativeHashSet<int> path, NativeHashSet<int> spawnSet)
        {
            if (cells.Length < 2) return false;
            if (!math.all(cells[0] == start)) return false;
            if (!math.all(cells[cells.Length - 1] == attach)) return false;

            int attachIdx = MapGridIndex.CellIndex(attach, gridSize);
            if (!path.Contains(attachIdx)) return false;

            // 새 셀들을 hashset 으로 별도 관리 (path 와 분리). 2×2 check 시 local 윈도우만 검사.
            var newCells = new NativeHashSet<int>(cells.Length, Allocator.TempJob);
            try
            {
                int lastNew = cells.Length - 2;
                for (int i = 0; i <= lastNew; i++)
                {
                    var c = cells[i];
                    int idx = MapGridIndex.CellIndex(c, gridSize);
                    if (!newCells.Add(idx)) return false;
                    if (idx != startIdx && spawnSet.Contains(idx)) return false;
                    if (path.Contains(idx)) return false;
                }

                // path 와의 접촉은 attach 점(마지막 new) 외엔 금지
                for (int i = 0; i <= lastNew; i++)
                {
                    var c = cells[i];
                    if (TouchesPathOutsideAttach(c, gridSize, path, attachIdx, isLastNew: i == lastNew))
                        return false;
                }

                // 2×2 block: 각 new 셀이 4 방향 코너 중 어디서 2×2 정사각형을 닫을 수 있는지만 검사
                for (int i = 0; i <= lastNew; i++)
                {
                    var c = cells[i];
                    if (FormsTwoByTwoBlock(c, gridSize, path, newCells, attachIdx))
                        return false;
                }
            }
            finally
            {
                newCells.Dispose();
            }

            return true;
        }

        private static bool TouchesPathOutsideAttach(
            int2 cell, int2 gridSize, NativeHashSet<int> path, int attachIdx, bool isLastNew)
        {
            for (int d = 0; d < 4; d++)
            {
                int2 n = NeighborOf(cell, d);
                if (!MapGridIndex.InBounds(n, gridSize)) continue;
                int nIdx = MapGridIndex.CellIndex(n, gridSize);
                if (!path.Contains(nIdx)) continue;

                if (isLastNew && nIdx == attachIdx) continue;
                return true;
            }
            return false;
        }

        // cell 을 포함한 2×2 정사각형이 path + newCells 안에 4 셀 모두 들어있는지 검사. attach 는 path 의 일부.
        private static bool FormsTwoByTwoBlock(
            int2 cell, int2 gridSize,
            NativeHashSet<int> path, NativeHashSet<int> newCells, int attachIdx)
        {
            // 4 가능 정사각형 (cell 을 4 코너 중 하나로):
            //   (cell, +x, +y, +x+y)
            //   (cell, -x, +y, -x+y)
            //   (cell, +x, -y, +x-y)
            //   (cell, -x, -y, -x-y)
            for (int q = 0; q < 4; q++)
            {
                int dx = (q == 0 || q == 2) ? 1 : -1;
                int dy = (q == 0 || q == 1) ? 1 : -1;

                int2 a = new int2(cell.x + dx, cell.y);
                int2 b = new int2(cell.x,      cell.y + dy);
                int2 c = new int2(cell.x + dx, cell.y + dy);

                if (!MapGridIndex.InBounds(a, gridSize)) continue;
                if (!MapGridIndex.InBounds(b, gridSize)) continue;
                if (!MapGridIndex.InBounds(c, gridSize)) continue;

                int ai = MapGridIndex.CellIndex(a, gridSize);
                int bi = MapGridIndex.CellIndex(b, gridSize);
                int ci = MapGridIndex.CellIndex(c, gridSize);

                bool aIn = path.Contains(ai) || newCells.Contains(ai);
                bool bIn = path.Contains(bi) || newCells.Contains(bi);
                bool cIn = path.Contains(ci) || newCells.Contains(ci);

                if (aIn && bIn && cIn) return true;
            }
            return false;
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

        private static int DegreeInPath(int2 cell, int2 gridSize, NativeHashSet<int> path)
        {
            int deg = 0;
            if (cell.x + 1 < gridSize.x && path.Contains(MapGridIndex.CellIndex(new int2(cell.x + 1, cell.y), gridSize))) deg++;
            if (cell.x - 1 >= 0          && path.Contains(MapGridIndex.CellIndex(new int2(cell.x - 1, cell.y), gridSize))) deg++;
            if (cell.y + 1 < gridSize.y && path.Contains(MapGridIndex.CellIndex(new int2(cell.x, cell.y + 1), gridSize))) deg++;
            if (cell.y - 1 >= 0          && path.Contains(MapGridIndex.CellIndex(new int2(cell.x, cell.y - 1), gridSize))) deg++;
            return deg;
        }

        private static void CommitRoute(NativeList<int2> cells, NativeHashSet<int> path, int2 gridSize)
        {
            // 마지막 셀 = attach 는 이미 path 에 있음
            for (int i = 0; i < cells.Length - 1; i++)
                path.Add(MapGridIndex.CellIndex(cells[i], gridSize));
        }
    }
}
