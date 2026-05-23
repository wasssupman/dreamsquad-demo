using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using Wassup.Data;
using Wassup.Data.MapGrid;

namespace Wassup.Tests.EditMode.MapGrid
{
    public class MapGridBattleAdapterTests
    {
        [Test]
        public void Build_NullSettings_Throws()
        {
            Assert.Throws<System.InvalidOperationException>(
                () => MapGridBattleAdapter.Build(0, null, null));
        }

        [Test]
        public void Build_WithSettings_NoDocument_NoOverride_UsesSeedBasedPreset()
        {
            var s = ScriptableObject.CreateInstance<MapGridGenerationSettings>();
            s.SetForTest();
            using var map = MapGridBattleAdapter.Build(0, s, null);
            Assert.IsTrue(map.IsCreated);
            Assert.AreEqual(0, map.seed);
            ScriptableObject.DestroyImmediate(s);
        }

        [Test]
        public void Build_WithGridSizeOverride_UsesExactSize()
        {
            var s = ScriptableObject.CreateInstance<MapGridGenerationSettings>();
            s.SetForTest();
            using var map = MapGridBattleAdapter.Build(0, s, null, new int2(25, 12));
            Assert.IsTrue(map.IsCreated);
            Assert.AreEqual(25, map.gridSize.x);
            Assert.AreEqual(12, map.gridSize.y);
            ScriptableObject.DestroyImmediate(s);
        }

        [Test]
        public void Build_GridSizeBelowMin_IsClampedToMin()
        {
            var s = ScriptableObject.CreateInstance<MapGridGenerationSettings>();
            s.SetForTest();
            using var map = MapGridBattleAdapter.Build(0, s, null, new int2(3, 3));
            Assert.IsTrue(map.IsCreated);
            Assert.AreEqual(MapGridBattleAdapter.MinGridDimension, map.gridSize.x);
            Assert.AreEqual(MapGridBattleAdapter.MinGridDimension, map.gridSize.y);
            ScriptableObject.DestroyImmediate(s);
        }

        [Test]
        public void Build_WithDocument_UsesCacheNotGenerator()
        {
            var s = ScriptableObject.CreateInstance<MapGridGenerationSettings>();
            s.SetForTest(minBranchCells: 1000); // impossible — generator 가 호출되면 throw 발생할 settings

            // 손수 작성한 작은 doc
            int w = 6, h = 4, n = w * h;
            var tiles = new MapTileType[n];
            for (int i = 0; i < n; i++) tiles[i] = MapTileType.Place;
            for (int x = 0; x < w; x++) tiles[2 * w + x] = MapTileType.Walk;

            var doc = ScriptableObject.CreateInstance<MapDocument>();
            doc.SetFrom(w, h, tiles,
                new byte[n], new bool[n], new byte[n],
                new Vector2Int(w - 1, 2), new[] { new Vector2Int(0, 2) },
                seed: -1, version: 0);

            using var map = MapGridBattleAdapter.Build(0, s, doc);
            Assert.IsTrue(map.IsCreated);
            Assert.AreEqual(w, map.gridSize.x);
            Assert.AreEqual(h, map.gridSize.y);
            Assert.AreEqual(-1, map.seed); // doc 의 authoringSeed

            ScriptableObject.DestroyImmediate(doc);
            ScriptableObject.DestroyImmediate(s);
        }
    }
}
