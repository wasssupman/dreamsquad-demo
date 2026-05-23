using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using Wassup.Data;
using Wassup.Data.MapGrid;

namespace Wassup.Tests.EditMode.MapGrid
{
    public class MapGridIntegrationTests
    {
        private MapGridGenerationSettings DefaultSettings()
        {
            var s = ScriptableObject.CreateInstance<MapGridGenerationSettings>();
            s.SetForTest();
            return s;
        }

        [Test]
        public void Integration_DefaultSettings_Wide30x15_Seed42()
        {
            var s = DefaultSettings();
            using var map = MapGridGenerator.Generate(42, new int2(30, 15), s, Allocator.TempJob);

            Assert.IsTrue(map.IsCreated);
            int n = 30 * 15;
            Assert.AreEqual(n, map.tiles.Length);
            Assert.AreEqual(n, map.mergeDegree.Length);
            Assert.AreEqual(n, map.chokepoint.Length);
            Assert.AreEqual(n, map.propLayerId.Length);

            int goalIdx = map.goal.y * 30 + map.goal.x;
            Assert.AreEqual(MapTileType.Walk, map.tiles[goalIdx]);
            Assert.AreEqual((byte)1, map.mergeDegree[goalIdx]);

            for (int i = 0; i < map.spawns.Length; i++)
            {
                int sIdx = map.spawns[i].y * 30 + map.spawns[i].x;
                Assert.AreEqual(MapTileType.Walk, map.tiles[sIdx], $"spawn[{i}]");
                Assert.AreEqual((byte)1, map.mergeDegree[sIdx], $"spawn[{i}] degree");
            }

            ScriptableObject.DestroyImmediate(s);
        }

        [Test]
        public void Integration_Tall10x20_Seed0_Succeeds()
        {
            var s = DefaultSettings();
            using var map = MapGridGenerator.Generate(0, new int2(10, 20), s, Allocator.TempJob);
            Assert.IsTrue(map.IsCreated);
            ScriptableObject.DestroyImmediate(s);
        }

        [Test]
        public void Integration_Square20x20_Seed0_Succeeds()
        {
            var s = DefaultSettings();
            using var map = MapGridGenerator.Generate(0, new int2(20, 20), s, Allocator.TempJob);
            Assert.IsTrue(map.IsCreated);
            ScriptableObject.DestroyImmediate(s);
        }

        [Test]
        public void Integration_RoundTrip_GeneratedMap_To_MapDocument_To_GeneratedMap()
        {
            var s = DefaultSettings();
            using var first = MapGridGenerator.Generate(7, new int2(30, 15), s, Allocator.TempJob);

            var doc = ScriptableObject.CreateInstance<MapDocument>();
            MapDocumentBuilder.WriteToDocument(doc, in first);

            using var second = MapDocumentBuilder.ToGeneratedMap(doc, Allocator.TempJob);

            Assert.AreEqual(first.gridSize, second.gridSize);
            Assert.AreEqual(first.goal, second.goal);
            Assert.AreEqual(first.spawns.Length, second.spawns.Length);
            for (int i = 0; i < first.tiles.Length; i++)
            {
                Assert.AreEqual(first.tiles[i], second.tiles[i], $"tiles[{i}]");
                Assert.AreEqual(first.mergeDegree[i], second.mergeDegree[i], $"merge[{i}]");
                Assert.AreEqual(first.chokepoint[i], second.chokepoint[i], $"choke[{i}]");
            }

            ScriptableObject.DestroyImmediate(doc);
            ScriptableObject.DestroyImmediate(s);
        }
    }
}
