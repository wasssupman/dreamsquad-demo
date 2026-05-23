using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using Wassup.Data.MapGrid;
using Random = Unity.Mathematics.Random;

namespace Wassup.Tests.EditMode.MapGrid
{
    public class IncrementalPathBuilderTests
    {
        private MapGridGenerationSettings DefaultSettings()
        {
            var s = ScriptableObject.CreateInstance<MapGridGenerationSettings>();
            s.SetForTest();
            return s;
        }

        private NativeArray<int2> MakeSpawns(params int2[] cells)
        {
            var arr = new NativeArray<int2>(cells.Length, Allocator.TempJob);
            for (int i = 0; i < cells.Length; i++) arr[i] = cells[i];
            return arr;
        }

        [Test]
        public void Build_TwoSpawn_ProducesValidTreeFromPlacer()
        {
            var s = DefaultSettings();
            s.SetForTest(minSpawn: 2, maxSpawn: 2);

            var rng = Random.CreateFromIndex(7);
            var gs = GoalSpawnPlacer.Pick(ref rng, new int2(30, 15), s, Allocator.TempJob);
            Assert.IsTrue(gs.IsValid, "placer failed unexpectedly");

            try
            {
                var result = IncrementalPathBuilder.Build(ref rng, new int2(30, 15), gs.goal, gs.spawns, s, Allocator.TempJob);
                try
                {
                    Assert.IsTrue(result.IsValid, "builder should produce valid path");
                    Assert.Greater(result.pathCells.Count, 2);
                }
                finally { result.Dispose(); }
            }
            finally
            {
                gs.Dispose();
                ScriptableObject.DestroyImmediate(s);
            }
        }

        [Test]
        public void Build_NoTwoByTwoBlock_AcrossSeeds()
        {
            var s = DefaultSettings();
            var gridSize = new int2(30, 15);

            for (uint seed = 1; seed < 50; seed++)
            {
                var rng = Random.CreateFromIndex(seed);
                var gs = GoalSpawnPlacer.Pick(ref rng, gridSize, s, Allocator.TempJob);
                if (!gs.IsValid) { gs.Dispose(); continue; }

                var result = IncrementalPathBuilder.Build(ref rng, gridSize, gs.goal, gs.spawns, s, Allocator.TempJob);
                try
                {
                    if (!result.IsValid) continue;
                    AssertNoTwoByTwoBlock(result.pathCells, gridSize, seed);
                }
                finally
                {
                    result.Dispose();
                    gs.Dispose();
                }
            }

            ScriptableObject.DestroyImmediate(s);
        }

        [Test]
        public void Build_Deterministic_SameInputSamePath()
        {
            var s = DefaultSettings();
            var gridSize = new int2(30, 15);

            // 두 번 같은 시퀀스로 placer + builder 실행
            int[] hashesA = RunHashes(s, gridSize, 5);
            int[] hashesB = RunHashes(s, gridSize, 5);

            CollectionAssert.AreEqual(hashesA, hashesB);
            ScriptableObject.DestroyImmediate(s);
        }

        private int[] RunHashes(MapGridGenerationSettings s, int2 gridSize, int count)
        {
            var hashes = new int[count];
            for (int seed = 0; seed < count; seed++)
            {
                var rng = Random.CreateFromIndex((uint)(seed + 1));
                var gs = GoalSpawnPlacer.Pick(ref rng, gridSize, s, Allocator.TempJob);
                if (!gs.IsValid) { hashes[seed] = 0; gs.Dispose(); continue; }

                var result = IncrementalPathBuilder.Build(ref rng, gridSize, gs.goal, gs.spawns, s, Allocator.TempJob);
                try
                {
                    if (!result.IsValid) { hashes[seed] = -1; continue; }
                    var enumerator = result.pathCells.GetEnumerator();
                    // 결정성 비교를 위해 commutative 합산 (XOR) 사용 — hash 가 셔플 순서에 영향받지 않아야 함
                    int sum = 0;
                    while (enumerator.MoveNext())
                        sum ^= unchecked(enumerator.Current * (int)2654435761u);
                    hashes[seed] = sum;
                }
                finally
                {
                    result.Dispose();
                    gs.Dispose();
                }
            }
            return hashes;
        }

        [Test]
        public void Build_GoalAndSpawnsHaveDegreeOne()
        {
            var s = DefaultSettings();
            var gridSize = new int2(30, 15);

            int validRuns = 0;
            for (uint seed = 1; seed < 30; seed++)
            {
                var rng = Random.CreateFromIndex(seed);
                var gs = GoalSpawnPlacer.Pick(ref rng, gridSize, s, Allocator.TempJob);
                if (!gs.IsValid) { gs.Dispose(); continue; }

                var result = IncrementalPathBuilder.Build(ref rng, gridSize, gs.goal, gs.spawns, s, Allocator.TempJob);
                try
                {
                    if (!result.IsValid) continue;
                    validRuns++;
                    Assert.AreEqual(1, DegreeOf(gs.goal, gridSize, result.pathCells), $"goal seed {seed}");
                    for (int i = 0; i < gs.spawns.Length; i++)
                        Assert.AreEqual(1, DegreeOf(gs.spawns[i], gridSize, result.pathCells),
                            $"spawn[{i}] seed {seed}");
                }
                finally
                {
                    result.Dispose();
                    gs.Dispose();
                }
            }

            Assert.Greater(validRuns, 10, "최소 10개 유효 케이스 필요");
            ScriptableObject.DestroyImmediate(s);
        }

        [Test]
        public void Build_FailsGracefullyWhenBoxed()
        {
            var s = DefaultSettings();
            // 명시적으로 빡빡한 설정: minBranchCellCount 매우 크게 → 라우터가 실패 의도
            s.SetForTest(minBranchCells: 1000, maxRouteAttemptsValue: 5);

            var spawns = MakeSpawns(new int2(0, 0));
            var rng = Random.CreateFromIndex(1);
            var result = IncrementalPathBuilder.Build(ref rng, new int2(30, 15), new int2(15, 7), spawns, s, Allocator.TempJob);

            try
            {
                Assert.IsTrue(result.pathCells.IsCreated, "result must be disposable even on failure");
                // 단일 spawn 이고 firstRoute=true 라 라우터는 보통 성공할 가능성도 있음.
                // 핵심은 Dispose 가 leak 없이 동작하는지.
            }
            finally
            {
                result.Dispose();
                spawns.Dispose();
                ScriptableObject.DestroyImmediate(s);
            }
        }

        private static void AssertNoTwoByTwoBlock(NativeHashSet<int> path, int2 gridSize, uint seed)
        {
            for (int y = 0; y < gridSize.y - 1; y++)
                for (int x = 0; x < gridSize.x - 1; x++)
                {
                    int a = MapGridIndex.CellIndex(new int2(x, y), gridSize);
                    int b = MapGridIndex.CellIndex(new int2(x + 1, y), gridSize);
                    int c = MapGridIndex.CellIndex(new int2(x, y + 1), gridSize);
                    int d = MapGridIndex.CellIndex(new int2(x + 1, y + 1), gridSize);
                    Assert.IsFalse(
                        path.Contains(a) && path.Contains(b) && path.Contains(c) && path.Contains(d),
                        $"seed {seed} has 2x2 block at ({x},{y})");
                }
        }

        private static int DegreeOf(int2 cell, int2 gridSize, NativeHashSet<int> path)
        {
            int deg = 0;
            if (cell.x + 1 < gridSize.x && path.Contains(MapGridIndex.CellIndex(new int2(cell.x + 1, cell.y), gridSize))) deg++;
            if (cell.x - 1 >= 0          && path.Contains(MapGridIndex.CellIndex(new int2(cell.x - 1, cell.y), gridSize))) deg++;
            if (cell.y + 1 < gridSize.y && path.Contains(MapGridIndex.CellIndex(new int2(cell.x, cell.y + 1), gridSize))) deg++;
            if (cell.y - 1 >= 0          && path.Contains(MapGridIndex.CellIndex(new int2(cell.x, cell.y - 1), gridSize))) deg++;
            return deg;
        }
    }
}
