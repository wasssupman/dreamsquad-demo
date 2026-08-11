using System;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using Wassup.Data;
using Wassup.Data.MapGrid;

namespace Wassup.Tests.EditMode.MapGrid
{
    public class WaypointPathAuthoringTests
    {
        [Test]
        public void ToGeneratedMap_FlattensTwoPathsAndPreservesReverseLookup()
        {
            var paths = new[]
            {
                new WaypointPath(new[]
                {
                    new Vector2Int(1, 1),
                    new Vector2Int(2, 1),
                    new Vector2Int(3, 1),
                }),
                new WaypointPath(new[]
                {
                    new Vector2Int(1, 2),
                    new Vector2Int(3, 2),
                }),
            };
            var doc = BuildDocument(paths);

            try
            {
                using var map = MapDocumentBuilder.ToGeneratedMap(doc, Allocator.TempJob);

                Assert.AreEqual(2, map.WaypointPathCount);
                Assert.AreEqual(new int2(0, 3), map.waypointRanges[0]);
                Assert.AreEqual(new int2(3, 2), map.waypointRanges[1]);
                Assert.AreEqual(new int2(1, 1), map.WaypointCellAt(0, 0));
                Assert.AreEqual(new int2(3, 1), map.WaypointCellAt(0, 2));
                Assert.AreEqual(new int2(1, 2), map.WaypointCellAt(1, 0));
                Assert.AreEqual(new int2(3, 2), map.WaypointCellAt(1, 1));
                Assert.Throws<ArgumentOutOfRangeException>(() => map.WaypointCellAt(0, 3),
                    "경로 경계를 넘어 다음 경로 셀을 읽으면 안 된다");
            }
            finally
            {
                ScriptableObject.DestroyImmediate(doc);
            }
        }

        [Test]
        public void ToGeneratedMap_NullOrEmptyPaths_LeavesWaypointArraysUncreatedAndDisposeSafe()
        {
            var doc = BuildDocument(null);
            try
            {
                var nullMap = MapDocumentBuilder.ToGeneratedMap(doc, Allocator.TempJob);
                Assert.AreEqual(0, nullMap.WaypointPathCount);
                Assert.IsFalse(nullMap.waypointCells.IsCreated);
                Assert.IsFalse(nullMap.waypointRanges.IsCreated);
                Assert.DoesNotThrow(() => nullMap.Dispose());
                Assert.DoesNotThrow(() => nullMap.Dispose());

                doc.SetWaypointPaths(Array.Empty<WaypointPath>());
                var emptyMap = MapDocumentBuilder.ToGeneratedMap(doc, Allocator.TempJob);
                Assert.AreEqual(0, emptyMap.WaypointPathCount);
                Assert.IsFalse(emptyMap.waypointCells.IsCreated);
                Assert.IsFalse(emptyMap.waypointRanges.IsCreated);
                Assert.DoesNotThrow(() => emptyMap.Dispose());
            }
            finally
            {
                ScriptableObject.DestroyImmediate(doc);
            }
        }

        [Test]
        public void ValidatePaths_OutOfBoundsIsError_AndGoalOverlapIsWarning()
        {
            var paths = new[]
            {
                new WaypointPath(new[]
                {
                    new Vector2Int(5, 1),
                    new Vector2Int(3, 1),
                }),
            };
            var tiles = FilledTiles(4, 3, MapTileType.Walk);
            var errors = new List<string>();
            var warnings = new List<string>();

            WaypointAuthoringRules.ValidatePaths(
                paths, 4, 3, tiles,
                new[] { new Vector2Int(3, 1) },
                new[] { new Vector2Int(0, 1) },
                errors, warnings);

            Assert.That(errors, Has.Some.Contains("격자 밖"));
            Assert.That(warnings, Has.Some.Contains("골/스폰 셀과 겹친다"));
        }

        [Test]
        public void ValidatePaths_AirOnlyAndConsecutiveDuplicate_AreWarnings()
        {
            var paths = new[]
            {
                new WaypointPath(new[]
                {
                    new Vector2Int(1, 1),
                    new Vector2Int(1, 1),
                }),
            };
            var tiles = FilledTiles(3, 3, MapTileType.Walk);
            tiles[1 * 3 + 1] = MapTileType.Deco;
            var errors = new List<string>();
            var warnings = new List<string>();

            WaypointAuthoringRules.ValidatePaths(
                paths, 3, 3, tiles,
                Array.Empty<Vector2Int>(), Array.Empty<Vector2Int>(),
                errors, warnings);

            Assert.IsEmpty(errors);
            Assert.That(warnings, Has.Some.Contains("Air 경로 전용"));
            Assert.That(warnings, Has.Some.Contains("연속 중복"));
        }

        private static MapDocument BuildDocument(WaypointPath[] paths)
        {
            const int width = 5;
            const int height = 4;
            int count = width * height;
            var doc = ScriptableObject.CreateInstance<MapDocument>();
            doc.SetFrom(
                width, height,
                FilledTiles(width, height, MapTileType.Walk),
                new byte[count], new bool[count], new byte[count],
                new[] { new Vector2Int(width - 1, 1) },
                new[] { new Vector2Int(0, 1) },
                seed: -1, version: 0);
            doc.SetWaypointPaths(paths);
            return doc;
        }

        private static MapTileType[] FilledTiles(int width, int height, MapTileType value)
        {
            var tiles = new MapTileType[width * height];
            for (int i = 0; i < tiles.Length; i++) tiles[i] = value;
            return tiles;
        }
    }
}
