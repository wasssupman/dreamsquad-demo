using NUnit.Framework;
using Unity.Collections;
using UnityEngine;
using Wassup.Data;
using Wassup.Data.MapGrid;

namespace Wassup.Tests.EditMode.MapGrid
{
    // map-pipeline-cleanup unit 4 — 절차 폴백 제거 후 단순화된 adapter 계약 2가지:
    // usable 문서 → ToGeneratedMap 과 동등 / unusable → hard-fail(MapGenerationFailedException).
    public class MapGridBattleAdapterTests
    {
        private static MapDocument BuildUsableDocument()
        {
            const int w = 6;
            const int h = 4;
            int n = w * h;

            var tiles = new MapTileType[n];
            for (int i = 0; i < n; i++) tiles[i] = MapTileType.Place;
            for (int x = 0; x < w; x++) tiles[2 * w + x] = MapTileType.Walk;

            var doc = ScriptableObject.CreateInstance<MapDocument>();
            doc.SetFrom(
                w, h,
                tiles, new byte[n], new bool[n], new byte[n],
                new[] { new Vector2Int(w - 1, 2) },
                new[] { new Vector2Int(0, 2), new Vector2Int(1, 2) },
                seed: 77,
                version: 3);
            return doc;
        }

        [Test]
        public void Build_UsableDocument_EquivalentToToGeneratedMap()
        {
            var doc = BuildUsableDocument();
            using var built = MapGridBattleAdapter.Build(doc);
            using var direct = MapDocumentBuilder.ToGeneratedMap(doc, Allocator.TempJob);

            Assert.IsTrue(built.IsCreated);
            Assert.AreEqual(direct.gridSize, built.gridSize);
            Assert.AreEqual(direct.seed, built.seed);
            Assert.AreEqual(direct.generatorVersion, built.generatorVersion);
            Assert.AreEqual(direct.goal, built.goal);
            Assert.AreEqual(direct.spawns.Length, built.spawns.Length);
            for (int i = 0; i < direct.tiles.Length; i++)
                Assert.AreEqual(direct.tiles[i], built.tiles[i], $"tiles[{i}]");

            ScriptableObject.DestroyImmediate(doc);
        }

        [Test]
        public void Build_UnusableDocument_Throws()
        {
            // null / 빈 문서 모두 hard-fail — 조용한 절차 폴백은 은퇴했다.
            Assert.Throws<MapGenerationFailedException>(() => MapGridBattleAdapter.Build(null));

            var empty = ScriptableObject.CreateInstance<MapDocument>();
            Assert.Throws<MapGenerationFailedException>(() => MapGridBattleAdapter.Build(empty));
            ScriptableObject.DestroyImmediate(empty);
        }
    }
}
