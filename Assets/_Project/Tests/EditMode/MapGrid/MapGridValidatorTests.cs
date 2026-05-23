using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using Wassup.Data.MapGrid;

namespace Wassup.Tests.EditMode.MapGrid
{
    public class MapGridValidatorTests
    {
        private MapGridGenerationSettings MakeSettings(int minCells = 4, int minTurns = 1)
        {
            var s = ScriptableObject.CreateInstance<MapGridGenerationSettings>();
            s.SetForTest(minBranchCells: minCells, minBranchTurns: minTurns);
            return s;
        }

        // 직선 path 만들기 helper: (sx, y) -> (ex, y)
        private PathBuildResult BuildHorizontalPath(int sx, int ex, int y, int2 gridSize)
        {
            var path = new NativeHashSet<int>(32, Allocator.TempJob);
            int from = math.min(sx, ex), to = math.max(sx, ex);
            for (int x = from; x <= to; x++)
                path.Add(MapGridIndex.CellIndex(new int2(x, y), gridSize));

            return new PathBuildResult
            {
                pathCells = path,
                spawnOrder = new NativeArray<int>(1, Allocator.TempJob),
                IsValid = true,
            };
        }

        // L 자 path: (sx, y0) -> (mx, y0) -> (mx, y1)
        private PathBuildResult BuildLPath(int sx, int y0, int mx, int y1, int2 gridSize)
        {
            var path = new NativeHashSet<int>(32, Allocator.TempJob);
            int from = math.min(sx, mx), to = math.max(sx, mx);
            for (int x = from; x <= to; x++)
                path.Add(MapGridIndex.CellIndex(new int2(x, y0), gridSize));
            int fy = math.min(y0, y1), ty = math.max(y0, y1);
            for (int y = fy; y <= ty; y++)
                path.Add(MapGridIndex.CellIndex(new int2(mx, y), gridSize));

            return new PathBuildResult
            {
                pathCells = path,
                spawnOrder = new NativeArray<int>(1, Allocator.TempJob),
                IsValid = true,
            };
        }

        // Z 자 path with 3 turns: spawn=(sx, y0) → (mx1, y0) → (mx1, y1) → (mx2, y1) → (mx2, y2)
        private PathBuildResult BuildZPath(int sx, int y0, int mx1, int y1, int mx2, int y2, int2 gridSize)
        {
            var path = new NativeHashSet<int>(64, Allocator.TempJob);
            int hFrom = math.min(sx, mx1), hTo = math.max(sx, mx1);
            for (int x = hFrom; x <= hTo; x++) path.Add(MapGridIndex.CellIndex(new int2(x, y0), gridSize));
            int v1From = math.min(y0, y1), v1To = math.max(y0, y1);
            for (int y = v1From; y <= v1To; y++) path.Add(MapGridIndex.CellIndex(new int2(mx1, y), gridSize));
            int h2From = math.min(mx1, mx2), h2To = math.max(mx1, mx2);
            for (int x = h2From; x <= h2To; x++) path.Add(MapGridIndex.CellIndex(new int2(x, y1), gridSize));
            int v2From = math.min(y1, y2), v2To = math.max(y1, y2);
            for (int y = v2From; y <= v2To; y++) path.Add(MapGridIndex.CellIndex(new int2(mx2, y), gridSize));

            return new PathBuildResult
            {
                pathCells = path,
                spawnOrder = new NativeArray<int>(1, Allocator.TempJob),
                IsValid = true,
            };
        }

        [Test]
        public void Validate_StraightLine_FailsBranchTooFewTurns()
        {
            var s = MakeSettings(minCells: 4, minTurns: 1);
            var gridSize = new int2(20, 10);
            var build = BuildHorizontalPath(0, 9, 5, gridSize);
            try
            {
                var spawns = new NativeArray<int2>(1, Allocator.TempJob);
                spawns[0] = new int2(0, 5);
                int2 goal = new int2(9, 5);

                var r = MapGridValidator.Validate(build, gridSize, goal, spawns, s);
                Assert.AreEqual(MapGridValidator.FailReason.BranchTooFewTurns, r);

                spawns.Dispose();
            }
            finally
            {
                build.Dispose();
                ScriptableObject.DestroyImmediate(s);
            }
        }

        [Test]
        public void Validate_LongZPath_PassesOk()
        {
            var s = MakeSettings(minCells: 4, minTurns: 1);
            var gridSize = new int2(20, 10);
            // Z path: (0,5) → (5,5) → (5,2) → (15,2) → (15,7). 3 turns, ~18 cells.
            var build = BuildZPath(0, 5, 5, 2, 15, 7, gridSize);
            try
            {
                var spawns = new NativeArray<int2>(1, Allocator.TempJob);
                spawns[0] = new int2(0, 5);
                int2 goal = new int2(15, 7);

                var r = MapGridValidator.Validate(build, gridSize, goal, spawns, s);
                Assert.AreEqual(MapGridValidator.FailReason.Ok, r);

                spawns.Dispose();
            }
            finally
            {
                build.Dispose();
                ScriptableObject.DestroyImmediate(s);
            }
        }

        [Test]
        public void EffectiveMinBranchTurnCount_ScalesWithGridSize_CappedAt4()
        {
            var s = MakeSettings(minCells: 4, minTurns: 3);
            // formula = min(4, max(SO, min(W,H)/4))
            Assert.AreEqual(3, s.EffectiveMinBranchTurnCount(new int2(30, 15))); // min(4, max(3, 3)) = 3
            Assert.AreEqual(4, s.EffectiveMinBranchTurnCount(new int2(20, 20))); // min(4, max(3, 5)) = 4 (cap)
            Assert.AreEqual(3, s.EffectiveMinBranchTurnCount(new int2(10, 20))); // min(4, max(3, 2)) = 3
            Assert.AreEqual(3, s.EffectiveMinBranchTurnCount(new int2(20, 10))); // min(4, max(3, 2)) = 3
            ScriptableObject.DestroyImmediate(s);
        }

        [Test]
        public void Validate_TooShortPath_FailsBranchTooShort()
        {
            var s = MakeSettings(minCells: 20, minTurns: 0);
            var gridSize = new int2(20, 10);
            var build = BuildLPath(0, 5, 4, 3, gridSize);
            try
            {
                var spawns = new NativeArray<int2>(1, Allocator.TempJob);
                spawns[0] = new int2(0, 5);
                int2 goal = new int2(4, 3);

                var r = MapGridValidator.Validate(build, gridSize, goal, spawns, s);
                Assert.AreEqual(MapGridValidator.FailReason.BranchTooShort, r);

                spawns.Dispose();
            }
            finally
            {
                build.Dispose();
                ScriptableObject.DestroyImmediate(s);
            }
        }

        [Test]
        public void MeasureBranch_LPath_CountsCellsAndTurns()
        {
            var gridSize = new int2(20, 10);
            var build = BuildLPath(0, 5, 9, 2, gridSize);
            try
            {
                bool ok = MapGridValidator.MeasureBranch(
                    new int2(0, 5), new int2(9, 2), gridSize, build.pathCells,
                    out int cells, out int turns);
                Assert.IsTrue(ok);
                // 수평 (0,5)..(9,5) = 10 + 수직 (9,2)..(9,5) = 4. junction (9,5) 1개 중복 → 13
                Assert.AreEqual(13, cells);
                Assert.AreEqual(1, turns); // L 자 1 꺾임
            }
            finally
            {
                build.Dispose();
            }
        }
    }
}
