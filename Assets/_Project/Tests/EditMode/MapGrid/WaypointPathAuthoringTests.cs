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

        [Test]
        public void ValidateSpawnRoutes_NullOrEmpty_ProducesNoDiagnostics()
        {
            var errors = new List<string>();
            var warnings = new List<string>();
            var paths = new[] { new WaypointPath(new[] { new Vector2Int(1, 1) }) };
            var spawns = new[] { new Vector2Int(0, 0), new Vector2Int(0, 3) };

            WaypointAuthoringRules.ValidateSpawnRoutes(null, paths, spawns, errors, warnings);
            WaypointAuthoringRules.ValidateSpawnRoutes(Array.Empty<int>(), paths, spawns, errors, warnings);

            Assert.IsEmpty(errors, "미저작 spawnRoutes 는 기존 맵 11장의 전 레인 최단거리 폴백이라 에러가 없어야 한다");
            Assert.IsEmpty(warnings, "미저작 spawnRoutes 는 경고도 없어야 한다");
        }

        [Test]
        public void ValidateSpawnRoutes_OutOfBoundsIndex_IsError()
        {
            var errors = new List<string>();
            var warnings = new List<string>();
            var paths = new[] { new WaypointPath(new[] { new Vector2Int(1, 1) }) };
            var spawns = new[] { new Vector2Int(0, 0) };

            WaypointAuthoringRules.ValidateSpawnRoutes(
                new[] { 1 }, paths, spawns, errors, warnings);

            Assert.That(errors, Has.Some.Contains("경로 배열 밖"),
                "범위 밖 인덱스는 조용한 골 직행 폴백이 아니라 에러여야 한다");
        }

        [Test]
        public void ValidateSpawnRoutes_NegativeIndex_IsSilent()
        {
            var errors = new List<string>();
            var warnings = new List<string>();
            var paths = new[] { new WaypointPath(new[] { new Vector2Int(1, 1) }) };
            var spawns = new[] { new Vector2Int(0, 0), new Vector2Int(0, 3) };

            WaypointAuthoringRules.ValidateSpawnRoutes(
                new[] { -1, -5 }, paths, spawns, errors, warnings);

            Assert.IsEmpty(errors, "음수는 최단거리 폴백 — 에러가 아니다");
            Assert.IsEmpty(warnings, "음수는 경고도 아니다");
        }

        [Test]
        public void ValidateSpawnRoutes_TwoLanesShareRoute_IsSingleWarning()
        {
            var errors = new List<string>();
            var warnings = new List<string>();
            var paths = new[] { new WaypointPath(new[] { new Vector2Int(1, 1) }) };
            var spawns = new[] { new Vector2Int(0, 0), new Vector2Int(0, 3) };

            WaypointAuthoringRules.ValidateSpawnRoutes(
                new[] { 0, 0 }, paths, spawns, errors, warnings);

            Assert.IsEmpty(errors);
            Assert.AreEqual(1, CountContaining(warnings, "같은 기본 경로"),
                "두 레인이 같은 경로를 가리키면 합류 저작 가능성 경고 1개만 나와야 한다");
        }

        [Test]
        public void ValidateSpawnRoutes_FirstCellCloserToOtherSpawn_IsCrossingWarning()
        {
            var errors = new List<string>();
            var warnings = new List<string>();
            // 경로 0 의 첫 지점 (0,3) — 레인 0 스폰(0,0)까지 거리 3, 레인 1 스폰(0,3)까지 거리 0.
            var paths = new[] { new WaypointPath(new[] { new Vector2Int(0, 3), new Vector2Int(2, 3) }) };
            var spawns = new[] { new Vector2Int(0, 0), new Vector2Int(0, 3) };

            WaypointAuthoringRules.ValidateSpawnRoutes(
                new[] { 0, -1 }, paths, spawns, errors, warnings);

            Assert.IsEmpty(errors);
            Assert.That(warnings, Has.Some.Contains("가로지르기"),
                "레인 0 이 고른 경로의 첫 지점이 레인 1 스폰에 더 가까우면 가로지르기 경고가 나와야 한다");
        }

        [Test]
        public void ValidateSpawnRoutes_OwnSpawnClosest_NoCrossingWarning()
        {
            var errors = new List<string>();
            var warnings = new List<string>();
            // 경로 0 의 첫 지점 (0,0) — 레인 0 스폰(0,0)까지 거리 0, 레인 1 스폰(0,3)까지 거리 3.
            var paths = new[] { new WaypointPath(new[] { new Vector2Int(0, 0), new Vector2Int(2, 0) }) };
            var spawns = new[] { new Vector2Int(0, 0), new Vector2Int(0, 3) };

            WaypointAuthoringRules.ValidateSpawnRoutes(
                new[] { 0, -1 }, paths, spawns, errors, warnings);

            Assert.IsEmpty(errors);
            Assert.IsEmpty(warnings, "자기 스폰이 가장 가까우면 가로지르기 경고가 없어야 한다");
        }

        [Test]
        public void ValidateSpawnRoutes_TieDistance_NoCrossingWarning()
        {
            var errors = new List<string>();
            var warnings = new List<string>();
            // 경로 0 의 첫 지점 (1,0) — 레인 0 스폰(0,0)까지 거리 1, 레인 1 스폰(2,0)까지 거리 1. 동률.
            var paths = new[] { new WaypointPath(new[] { new Vector2Int(1, 0) }) };
            var spawns = new[] { new Vector2Int(0, 0), new Vector2Int(2, 0) };

            WaypointAuthoringRules.ValidateSpawnRoutes(
                new[] { 0, -1 }, paths, spawns, errors, warnings);

            Assert.IsEmpty(errors);
            Assert.IsEmpty(warnings, "동률은 가로지르기 경고 대상이 아니다 — 엄격히 더 가까울 때만");
        }

        [Test]
        public void ValidateSpawnRoutes_LongerThanSpawns_IsWarning()
        {
            var errors = new List<string>();
            var warnings = new List<string>();
            var paths = new[] { new WaypointPath(new[] { new Vector2Int(1, 1) }) };
            var spawns = new[] { new Vector2Int(0, 0) };

            WaypointAuthoringRules.ValidateSpawnRoutes(
                new[] { -1, 0 }, paths, spawns, errors, warnings);

            Assert.IsEmpty(errors);
            Assert.That(warnings, Has.Some.Contains("스폰 개수"),
                "spawnRoutes 가 spawns 보다 길면 남는 항목이 어느 레인에도 안 붙는다는 경고가 나와야 한다");
        }

        [Test]
        public void ValidateSpawnRoutes_EmptyOrNullPathCells_SkipsCrossingCheckWithoutThrowing()
        {
            var errors = new List<string>();
            var warnings = new List<string>();
            var paths = new WaypointPath[] { new WaypointPath(Array.Empty<Vector2Int>()), null };
            var spawns = new[] { new Vector2Int(0, 0), new Vector2Int(0, 3) };

            Assert.DoesNotThrow(() => WaypointAuthoringRules.ValidateSpawnRoutes(
                new[] { 0, 1 }, paths, spawns, errors, warnings));

            Assert.IsEmpty(errors);
        }

        private static int CountContaining(List<string> messages, string substring)
        {
            int count = 0;
            foreach (var m in messages)
                if (m.Contains(substring)) count++;
            return count;
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
