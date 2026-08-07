using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using Wassup.Data;
using Wassup.Data.MapGrid;

namespace Wassup.Tests.EditMode.MapGrid
{
    public class MapDocumentRoundTripTests
    {
        private MapDocument BuildSampleDocument()
        {
            const int w = 6;
            const int h = 4;
            int n = w * h;

            var tiles = new MapTileType[n];
            for (int i = 0; i < n; i++) tiles[i] = MapTileType.Place;
            // 작은 직선 path: (0,2) -> (5,2)
            for (int x = 0; x < w; x++) tiles[2 * w + x] = MapTileType.Walk;

            var mergeDegree = new byte[n];
            for (int x = 1; x < w - 1; x++) mergeDegree[2 * w + x] = 2;
            mergeDegree[2 * w + 0] = 1;
            mergeDegree[2 * w + (w - 1)] = 1;

            var chokepoint = new bool[n];
            var propLayerId = new byte[n];

            var doc = ScriptableObject.CreateInstance<MapDocument>();
            doc.SetFrom(
                w, h,
                tiles, mergeDegree, chokepoint, propLayerId,
                new[] { new Vector2Int(w - 1, 2) },
                new[] { new Vector2Int(0, 2) },
                seed: 42,
                version: 1);
            return doc;
        }

        [Test]
        public void RoundTrip_TilesAndMeta_Identity()
        {
            var source = BuildSampleDocument();
            using var map = MapDocumentBuilder.ToGeneratedMap(source, Allocator.TempJob);

            var roundTripped = ScriptableObject.CreateInstance<MapDocument>();
            MapDocumentBuilder.WriteToDocument(roundTripped, in map);

            Assert.AreEqual(source.Width, roundTripped.Width);
            Assert.AreEqual(source.Height, roundTripped.Height);
            Assert.AreEqual(source.Goal, roundTripped.Goal);
            Assert.AreEqual(source.Goals.Count, roundTripped.Goals.Count);
            for (int i = 0; i < source.Goals.Count; i++)
                Assert.AreEqual(source.Goals[i], roundTripped.Goals[i], $"goals[{i}]");
            Assert.AreEqual(source.AuthoringSeed, roundTripped.AuthoringSeed);
            Assert.AreEqual(source.GeneratorVersion, roundTripped.GeneratorVersion);

            Assert.AreEqual(source.Spawns.Count, roundTripped.Spawns.Count);
            for (int i = 0; i < source.Spawns.Count; i++)
                Assert.AreEqual(source.Spawns[i], roundTripped.Spawns[i]);

            Assert.AreEqual(source.Tiles.Count, roundTripped.Tiles.Count);
            for (int i = 0; i < source.Tiles.Count; i++)
            {
                Assert.AreEqual(source.Tiles[i], roundTripped.Tiles[i], $"tiles[{i}]");
                Assert.AreEqual(source.MergeDegree[i], roundTripped.MergeDegree[i], $"mergeDegree[{i}]");
                Assert.AreEqual(source.Chokepoint[i], roundTripped.Chokepoint[i], $"chokepoint[{i}]");
                Assert.AreEqual(source.PropLayerId[i], roundTripped.PropLayerId[i], $"propLayerId[{i}]");
            }

            ScriptableObject.DestroyImmediate(source);
            ScriptableObject.DestroyImmediate(roundTripped);
        }

        [Test]
        public void Dispose_IsIdempotent()
        {
            var doc = BuildSampleDocument();
            var map = MapDocumentBuilder.ToGeneratedMap(doc, Allocator.TempJob);
            map.Dispose();
            // 두 번 호출해도 예외 없음 — IsCreated 가드 확인.
            Assert.DoesNotThrow(() => map.Dispose());

            ScriptableObject.DestroyImmediate(doc);
        }

        [Test]
        public void IsCreated_DefaultStruct_ReturnsFalse()
        {
            GeneratedMap empty = default;
            Assert.IsFalse(empty.IsCreated);
        }

        [Test]
        public void ToGeneratedMap_ProducesAllMetaArrays()
        {
            var doc = BuildSampleDocument();
            using var map = MapDocumentBuilder.ToGeneratedMap(doc, Allocator.TempJob);

            Assert.IsTrue(map.tiles.IsCreated);
            Assert.IsTrue(map.mergeDegree.IsCreated);
            Assert.IsTrue(map.chokepoint.IsCreated);
            Assert.IsTrue(map.propLayerId.IsCreated);
            Assert.IsTrue(map.spawns.IsCreated);
            Assert.IsTrue(map.goalMaxStability.IsCreated);
            Assert.AreEqual(map.goals.Length, map.goalMaxStability.Length, "goalMaxStability 는 goals 와 같은 길이");

            int n = map.gridSize.x * map.gridSize.y;
            Assert.AreEqual(n, map.tiles.Length);
            Assert.AreEqual(n, map.mergeDegree.Length);
            Assert.AreEqual(n, map.chokepoint.Length);
            Assert.AreEqual(n, map.propLayerId.Length);

            ScriptableObject.DestroyImmediate(doc);
        }

        [Test]
        public void MultiGoal_ToGeneratedMap_And_RoundTrip_Preserved()
        {
            const int w = 6, h = 4; int n = w * h;
            var tiles = new MapTileType[n];
            for (int i = 0; i < n; i++) tiles[i] = MapTileType.Place;
            for (int x = 0; x < w; x++) tiles[2 * w + x] = MapTileType.Walk;

            var goals = new[] { new Vector2Int(5, 2), new Vector2Int(3, 2) };
            var doc = ScriptableObject.CreateInstance<MapDocument>();
            doc.SetFrom(w, h, tiles, new byte[n], new bool[n], new byte[n],
                goals, new[] { new Vector2Int(0, 2) }, seed: 7, version: 0);

            Assert.AreEqual(2, doc.Goals.Count);
            Assert.AreEqual(new Vector2Int(5, 2), doc.Goal, "primary = goals[0]");

            using var map = MapDocumentBuilder.ToGeneratedMap(doc, Allocator.TempJob);
            Assert.IsTrue(map.goals.IsCreated);
            Assert.AreEqual(2, map.goals.Length);
            Assert.AreEqual(new int2(5, 2), map.goals[0]);
            Assert.AreEqual(new int2(3, 2), map.goals[1]);
            Assert.AreEqual(map.goals[0], map.goal, "GeneratedMap.goal = goals[0]");

            var roundTripped = ScriptableObject.CreateInstance<MapDocument>();
            MapDocumentBuilder.WriteToDocument(roundTripped, in map);
            Assert.AreEqual(2, roundTripped.Goals.Count);
            Assert.AreEqual(goals[0], roundTripped.Goals[0]);
            Assert.AreEqual(goals[1], roundTripped.Goals[1]);

            ScriptableObject.DestroyImmediate(doc);
            ScriptableObject.DestroyImmediate(roundTripped);
        }

        // ── goal-stability unit 0 — per-goal 최대 안정도 M ──────────────────────

        private static MapDocument BuildTwoGoalDocument(float[] stability)
        {
            const int w = 6, h = 4; int n = w * h;
            var tiles = new MapTileType[n];
            for (int i = 0; i < n; i++) tiles[i] = MapTileType.Place;
            for (int x = 0; x < w; x++) tiles[2 * w + x] = MapTileType.Walk;

            var doc = ScriptableObject.CreateInstance<MapDocument>();
            doc.SetFrom(w, h, tiles, new byte[n], new bool[n], new byte[n],
                new[] { new Vector2Int(5, 2), new Vector2Int(3, 2) },
                new[] { new Vector2Int(0, 2) },
                seed: 7, version: 0,
                goalStabilityArr: stability);
            return doc;
        }

        [Test]
        public void GoalStability_RoundTrip_Preserved()
        {
            var doc = BuildTwoGoalDocument(new[] { 30f, 0f });
            using var map = MapDocumentBuilder.ToGeneratedMap(doc, Allocator.TempJob);

            Assert.IsTrue(map.goalMaxStability.IsCreated);
            Assert.AreEqual(2, map.goalMaxStability.Length);
            Assert.AreEqual(30f, map.goalMaxStability[0]);
            Assert.AreEqual(0f, map.goalMaxStability[1]);

            var roundTripped = ScriptableObject.CreateInstance<MapDocument>();
            MapDocumentBuilder.WriteToDocument(roundTripped, in map);
            Assert.AreEqual(2, roundTripped.GoalMaxStability.Count);
            Assert.AreEqual(30f, roundTripped.GoalMaxStability[0]);
            Assert.AreEqual(0f, roundTripped.GoalMaxStability[1]);

            ScriptableObject.DestroyImmediate(doc);
            ScriptableObject.DestroyImmediate(roundTripped);
        }

        [Test]
        public void GoalStability_Absent_FallsBackToZero()
        {
            // 레거시/미authored: SetFrom 에 안정도 미전달 → 전 골 0, 배열은 goals 길이로 생성.
            var doc = BuildSampleDocument();
            using var map = MapDocumentBuilder.ToGeneratedMap(doc, Allocator.TempJob);

            Assert.IsTrue(map.goalMaxStability.IsCreated);
            Assert.AreEqual(map.goals.Length, map.goalMaxStability.Length);
            for (int i = 0; i < map.goalMaxStability.Length; i++)
                Assert.AreEqual(0f, map.goalMaxStability[i], $"goalMaxStability[{i}]");

            ScriptableObject.DestroyImmediate(doc);
        }

        [Test]
        public void GoalStability_LengthMismatch_FallsBackToZero()
        {
            // 골 2개 vs 안정도 1개 — 부분 채택 없이 전 골 0 (README 폴백 계약).
            var doc = BuildTwoGoalDocument(new[] { 30f });
            using var map = MapDocumentBuilder.ToGeneratedMap(doc, Allocator.TempJob);

            Assert.AreEqual(2, map.goalMaxStability.Length);
            Assert.AreEqual(0f, map.goalMaxStability[0]);
            Assert.AreEqual(0f, map.goalMaxStability[1]);

            ScriptableObject.DestroyImmediate(doc);
        }

        [Test]
        public void GoalStability_Negative_ClampedToZero()
        {
            var doc = BuildTwoGoalDocument(new[] { -5f, 12f });
            using var map = MapDocumentBuilder.ToGeneratedMap(doc, Allocator.TempJob);

            Assert.AreEqual(0f, map.goalMaxStability[0], "음수는 0 clamp");
            Assert.AreEqual(12f, map.goalMaxStability[1]);

            ScriptableObject.DestroyImmediate(doc);
        }

        // ── placement-mask unit 0 — placeMask 왕복·파생 폴백 ────────────────────

        private static MapDocument BuildMaskedDocument(byte[] mask)
        {
            const int w = 6, h = 4; int n = w * h;
            var tiles = new MapTileType[n];
            for (int i = 0; i < n; i++) tiles[i] = MapTileType.Place;
            for (int x = 0; x < w; x++) tiles[2 * w + x] = MapTileType.Walk;

            var doc = ScriptableObject.CreateInstance<MapDocument>();
            doc.SetFrom(w, h, tiles, new byte[n], new bool[n], new byte[n],
                new[] { new Vector2Int(w - 1, 2) }, new[] { new Vector2Int(0, 2) },
                seed: 42, version: 1,
                placeMaskArr: mask);
            return doc;
        }

        [Test]
        public void PlaceMask_RoundTrip_Preserved()
        {
            const int n = 24;
            var mask = new byte[n];
            mask[2 * 6 + 3] = 1;   // Walk 셀 (3,2) 에 배치 허용 — 타일 종류와 직교 확인.
            var doc = BuildMaskedDocument(mask);
            using var map = MapDocumentBuilder.ToGeneratedMap(doc, Allocator.TempJob);

            Assert.IsTrue(map.placeMask.IsCreated);
            Assert.AreEqual(1, map.placeMask[2 * 6 + 3], "Walk 셀 mask=1 보존");
            Assert.AreEqual(0, map.placeMask[0], "Place 셀 mask=0 보존 (파생 아님)");

            var roundTripped = ScriptableObject.CreateInstance<MapDocument>();
            MapDocumentBuilder.WriteToDocument(roundTripped, in map);
            Assert.AreEqual(n, roundTripped.PlaceMask.Count);
            for (int i = 0; i < n; i++)
                Assert.AreEqual(mask[i], roundTripped.PlaceMask[i], $"placeMask[{i}]");

            ScriptableObject.DestroyImmediate(doc);
            ScriptableObject.DestroyImmediate(roundTripped);
        }

        [Test]
        public void PlaceMask_Absent_DerivesFromPlaceTiles()
        {
            var doc = BuildSampleDocument();   // placeMask 미전달
            using var map = MapDocumentBuilder.ToGeneratedMap(doc, Allocator.TempJob);

            Assert.IsTrue(map.placeMask.IsCreated, "빌더 산출물 불변식: 항상 생성");
            int n = map.gridSize.x * map.gridSize.y;
            for (int i = 0; i < n; i++)
                Assert.AreEqual(map.tiles[i] == MapTileType.Place ? 1 : 0, map.placeMask[i], $"placeMask[{i}] 파생");

            ScriptableObject.DestroyImmediate(doc);
        }

        [Test]
        public void PlaceMask_LengthMismatch_DerivesFromPlaceTiles()
        {
            var doc = BuildMaskedDocument(new byte[] { 1, 1, 1 });   // 3 != 24
            using var map = MapDocumentBuilder.ToGeneratedMap(doc, Allocator.TempJob);

            int n = map.gridSize.x * map.gridSize.y;
            for (int i = 0; i < n; i++)
                Assert.AreEqual(map.tiles[i] == MapTileType.Place ? 1 : 0, map.placeMask[i], $"placeMask[{i}] 파생 폴백");

            ScriptableObject.DestroyImmediate(doc);
        }

        [Test]
        public void PlaceMask_NonBinaryValue_NormalizedToOne()
        {
            const int n = 24;
            var mask = new byte[n];
            mask[0] = 2;   // 비정규값 — intent 비교 오염 방지 정규화 확인.
            var doc = BuildMaskedDocument(mask);
            using var map = MapDocumentBuilder.ToGeneratedMap(doc, Allocator.TempJob);

            Assert.AreEqual(1, map.placeMask[0], "2 → 1 정규화");

            ScriptableObject.DestroyImmediate(doc);
        }
    }
}
