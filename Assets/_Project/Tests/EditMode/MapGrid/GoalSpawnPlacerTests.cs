using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using Wassup.Data.MapGrid;
using Random = Unity.Mathematics.Random;

namespace Wassup.Tests.EditMode.MapGrid
{
    public class GoalSpawnPlacerTests
    {
        private MapGridGenerationSettings DefaultSettings()
        {
            var s = ScriptableObject.CreateInstance<MapGridGenerationSettings>();
            s.SetForTest();
            return s;
        }

        [Test]
        public void Pick_Deterministic_SameSeedSameResult()
        {
            var s = DefaultSettings();
            var rngA = Random.CreateFromIndex(42);
            var rngB = Random.CreateFromIndex(42);

            var a = GoalSpawnPlacer.Pick(ref rngA, new int2(30, 15), s, Allocator.TempJob);
            var b = GoalSpawnPlacer.Pick(ref rngB, new int2(30, 15), s, Allocator.TempJob);

            try
            {
                Assert.IsTrue(a.IsValid && b.IsValid);
                Assert.AreEqual(a.goal, b.goal);
                Assert.AreEqual(a.activeQuadrantMask, b.activeQuadrantMask);
                Assert.AreEqual(a.spawns.Length, b.spawns.Length);
                for (int i = 0; i < a.spawns.Length; i++)
                    Assert.AreEqual(a.spawns[i], b.spawns[i]);
            }
            finally
            {
                a.Dispose();
                b.Dispose();
                ScriptableObject.DestroyImmediate(s);
            }
        }

        [Test]
        public void Pick_GoalAndSpawnsInDistinctSections()
        {
            var s = DefaultSettings();
            var gridSize = new int2(30, 15);
            int2 layout = GoalSpawnPlacer.GetLayout(gridSize);

            for (uint seed = 1; seed < 200; seed++)
            {
                var rng = Random.CreateFromIndex(seed);
                var r = GoalSpawnPlacer.Pick(ref rng, gridSize, s, Allocator.TempJob);
                try
                {
                    if (!r.IsValid) continue;

                    int goalSection = GetSectionOf(r.goal, layout, gridSize);
                    for (int i = 0; i < r.spawns.Length; i++)
                    {
                        int spSection = GetSectionOf(r.spawns[i], layout, gridSize);
                        Assert.AreNotEqual(goalSection, spSection,
                            $"seed {seed} spawn[{i}] section same as goal");
                        for (int j = i + 1; j < r.spawns.Length; j++)
                        {
                            int spSection2 = GetSectionOf(r.spawns[j], layout, gridSize);
                            Assert.AreNotEqual(spSection, spSection2,
                                $"seed {seed} spawns {i}/{j} same section");
                        }
                    }
                }
                finally { r.Dispose(); }
            }

            ScriptableObject.DestroyImmediate(s);
        }

        [Test]
        public void Pick_SpawnCountWithinRange()
        {
            var s = DefaultSettings();
            for (uint seed = 1; seed < 100; seed++)
            {
                var rng = Random.CreateFromIndex(seed);
                var r = GoalSpawnPlacer.Pick(ref rng, new int2(30, 15), s, Allocator.TempJob);
                try
                {
                    if (!r.IsValid) continue;
                    Assert.GreaterOrEqual(r.spawns.Length, s.MinSpawnCount);
                    Assert.LessOrEqual(r.spawns.Length, s.MaxSpawnCount);
                }
                finally { r.Dispose(); }
            }
            ScriptableObject.DestroyImmediate(s);
        }

        [Test]
        public void Pick_DistanceRulesSatisfied()
        {
            var s = DefaultSettings();
            var gridSize = new int2(30, 15);
            int goalDist = s.EffectiveSpawnToGoalMinManhattan(gridSize);
            int spawnDist = s.SpawnToSpawnMinManhattan;

            for (uint seed = 1; seed < 100; seed++)
            {
                var rng = Random.CreateFromIndex(seed);
                var r = GoalSpawnPlacer.Pick(ref rng, gridSize, s, Allocator.TempJob);
                try
                {
                    if (!r.IsValid) continue;
                    for (int i = 0; i < r.spawns.Length; i++)
                    {
                        Assert.GreaterOrEqual(MapGridIndex.Manhattan(r.spawns[i], r.goal), goalDist,
                            $"seed {seed} spawn[{i}] too close to goal");
                        for (int j = i + 1; j < r.spawns.Length; j++)
                            Assert.GreaterOrEqual(MapGridIndex.Manhattan(r.spawns[i], r.spawns[j]), spawnDist,
                                $"seed {seed} spawns {i}/{j} too close");
                    }
                }
                finally { r.Dispose(); }
            }

            ScriptableObject.DestroyImmediate(s);
        }

        [Test]
        public void Pick_SmallGrid10x20_StillProducesValid()
        {
            var s = DefaultSettings();
            int success = 0;
            for (uint seed = 1; seed < 100; seed++)
            {
                var rng = Random.CreateFromIndex(seed);
                var r = GoalSpawnPlacer.Pick(ref rng, new int2(10, 20), s, Allocator.TempJob);
                try { if (r.IsValid) success++; }
                finally { r.Dispose(); }
            }
            Assert.GreaterOrEqual(success, 85, $"Tall10x20 success ≥85 expected, got {success}");
            ScriptableObject.DestroyImmediate(s);
        }

        [Test]
        public void Pick_GoalEdgeOnly_GoalLandsOnMapEdge()
        {
            var s = ScriptableObject.CreateInstance<MapGridGenerationSettings>();
            s.SetForTest();
            s.SetGoalEdgeOnly(true);

            var gridSize = new int2(30, 15);
            int success = 0;
            for (uint seed = 1; seed < 100; seed++)
            {
                var rng = Random.CreateFromIndex(seed);
                var r = GoalSpawnPlacer.Pick(ref rng, gridSize, s, Allocator.TempJob);
                try
                {
                    if (!r.IsValid) continue;
                    success++;
                    Assert.IsTrue(GoalSpawnPlacer.IsOnMapEdge(r.goal, gridSize),
                        $"seed {seed} goal=({r.goal.x},{r.goal.y}) not on map edge");
                }
                finally { r.Dispose(); }
            }
            Assert.Greater(success, 50, "edge-only mode should still succeed for majority of seeds");
            ScriptableObject.DestroyImmediate(s);
        }

        [Test]
        public void GetLayout_AspectRatio()
        {
            Assert.AreEqual(new int2(3, 2), GoalSpawnPlacer.GetLayout(new int2(30, 15)));
            Assert.AreEqual(new int2(3, 2), GoalSpawnPlacer.GetLayout(new int2(20, 20)));
            Assert.AreEqual(new int2(2, 3), GoalSpawnPlacer.GetLayout(new int2(10, 20)));
        }

        [Test]
        public void GetSectionAnchor_CornerSections_AreMapCorners()
        {
            var gridSize = new int2(30, 15);
            var layout = new int2(3, 2);
            Assert.AreEqual(new int2(0, 0), GoalSpawnPlacer.GetSectionAnchor(0, layout, gridSize)); // TL
            Assert.AreEqual(new int2(29, 0), GoalSpawnPlacer.GetSectionAnchor(2, layout, gridSize)); // TR
            Assert.AreEqual(new int2(0, 14), GoalSpawnPlacer.GetSectionAnchor(3, layout, gridSize)); // BL
            Assert.AreEqual(new int2(29, 14), GoalSpawnPlacer.GetSectionAnchor(5, layout, gridSize)); // BR
        }

        [Test]
        public void GetSectionAnchor_EdgeSections_AreEdgeMidpoints()
        {
            var gridSize = new int2(30, 15);
            var layout = new int2(3, 2);
            // S1: top-middle. col=1, row=0. sx = 1*10 + 5 = 15. ay=0.
            Assert.AreEqual(new int2(15, 0), GoalSpawnPlacer.GetSectionAnchor(1, layout, gridSize));
            // S4: bottom-middle. ay=14.
            Assert.AreEqual(new int2(15, 14), GoalSpawnPlacer.GetSectionAnchor(4, layout, gridSize));
        }

        private static int GetSectionOf(int2 cell, int2 layout, int2 gridSize)
        {
            int sectionW = gridSize.x / layout.x;
            int sectionH = gridSize.y / layout.y;
            int col = math.min(layout.x - 1, cell.x / sectionW);
            int row = math.min(layout.y - 1, cell.y / sectionH);
            return row * layout.x + col;
        }
    }
}
