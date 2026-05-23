using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using Wassup.Data;
using Wassup.Data.MapGrid;

namespace Wassup.Tests.EditMode.MapGrid
{
    public class MapGridGeneratorTests
    {
        private MapGridGenerationSettings DefaultSettings()
        {
            var s = ScriptableObject.CreateInstance<MapGridGenerationSettings>();
            s.SetForTest();
            return s;
        }

        [Test]
        public void Generate_DefaultSettings_Wide30x15_Succeeds()
        {
            var s = DefaultSettings();
            using var map = MapGridGenerator.Generate(0, new int2(30, 15), s, Allocator.TempJob);
            Assert.IsTrue(map.IsCreated);
            Assert.AreEqual(30 * 15, map.tiles.Length);
            ScriptableObject.DestroyImmediate(s);
        }

        [Test]
        public void Generate_Deterministic()
        {
            var s = DefaultSettings();
            using var a = MapGridGenerator.Generate(42, new int2(30, 15), s, Allocator.TempJob, out int attemptsA);
            using var b = MapGridGenerator.Generate(42, new int2(30, 15), s, Allocator.TempJob, out int attemptsB);

            Assert.AreEqual(attemptsA, attemptsB);
            Assert.AreEqual(a.goal, b.goal);
            Assert.AreEqual(a.spawns.Length, b.spawns.Length);
            for (int i = 0; i < a.spawns.Length; i++) Assert.AreEqual(a.spawns[i], b.spawns[i]);
            for (int i = 0; i < a.tiles.Length; i++) Assert.AreEqual(a.tiles[i], b.tiles[i], $"tile[{i}]");

            ScriptableObject.DestroyImmediate(s);
        }

        [Test]
        public void Generate_ThrowsOnImpossibleSettings()
        {
            var s = ScriptableObject.CreateInstance<MapGridGenerationSettings>();
            s.SetForTest(minBranchCells: 1000, maxMapAttemptsValue: 5);

            Assert.Throws<MapGenerationFailedException>(
                () =>
                {
                    var m = MapGridGenerator.Generate(0, new int2(30, 15), s, Allocator.TempJob);
                    if (m.IsCreated) m.Dispose();
                });

            ScriptableObject.DestroyImmediate(s);
        }

        [Test]
        public void Generate_AllPresets_HighSuccessRate()
        {
            var s = DefaultSettings();
            int totalSuccess = 0;
            var presets = new[] { MapGridPreset.Wide30x15, MapGridPreset.Square20x20, MapGridPreset.Tall10x20 };
            foreach (var p in presets)
            {
                int2 size = MapGridGenerationSettings.PresetToGridSize(p);
                int success = 0;
                for (int seed = 0; seed < 50; seed++)
                {
                    GeneratedMap m = default;
                    try
                    {
                        m = MapGridGenerator.Generate(seed, size, s, Allocator.TempJob);
                        success++;
                    }
                    catch (MapGenerationFailedException) { }
                    finally { if (m.IsCreated) m.Dispose(); }
                }
                totalSuccess += success;
                Assert.GreaterOrEqual(success, 45, $"{p}: ≥45/50 expected, got {success}");
            }

            ScriptableObject.DestroyImmediate(s);
        }

        [Test]
        public void HashSeed_NoCollisionsAcrossAttempts()
        {
            var seen = new System.Collections.Generic.HashSet<uint>();
            int total = 0;
            for (int seed = 0; seed < 100; seed++)
                for (int attempt = 0; attempt < 600; attempt++)
                {
                    uint h = MapGridGenerator.HashSeed(seed, attempt, 1);
                    seen.Add(h);
                    total++;
                }
            float uniqueRatio = seen.Count / (float)total;
            Assert.GreaterOrEqual(uniqueRatio, 0.999f, $"hash unique ratio {uniqueRatio}");
        }
    }
}
