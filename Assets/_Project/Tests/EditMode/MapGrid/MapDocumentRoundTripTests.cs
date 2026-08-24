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

            var doc = ScriptableObject.CreateInstance<MapDocument>();
            doc.SetFrom(
                w, h,
                tiles,
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
                Assert.AreEqual(source.Tiles[i], roundTripped.Tiles[i], $"tiles[{i}]");

            ScriptableObject.DestroyImmediate(source);
            ScriptableObject.DestroyImmediate(roundTripped);
        }

        // bonus-wave-pull unit 1 — 보너스 포탈 칸 왕복. 이 축은 SetFrom 이 아니라 전용
        // setter 를 거치므로(「전달 안 하면 지워짐」을 암묵 규칙으로 만들지 않기 위해)
        // WriteToDocument 에도 명시 인자로 넘겨야 반영된다.
        [Test]
        public void RoundTrip_BonusSpawns_Identity()
        {
            var source = BuildSampleDocument();
            var authored = new[] { new Vector2Int(2, 2), new Vector2Int(4, 2) };
            source.SetBonusSpawns(authored);

            using var map = MapDocumentBuilder.ToGeneratedMap(source, Allocator.TempJob);
            Assert.IsTrue(map.bonusSpawns.IsCreated);
            Assert.AreEqual(2, map.bonusSpawns.Length);
            Assert.AreEqual(new int2(2, 2), map.bonusSpawns[0]);
            Assert.AreEqual(new int2(4, 2), map.bonusSpawns[1]);

            var roundTripped = ScriptableObject.CreateInstance<MapDocument>();
            MapDocumentBuilder.WriteToDocument(
                roundTripped, in map, bonusSpawns: authored);
            Assert.AreEqual(2, roundTripped.BonusSpawns.Count);
            for (int i = 0; i < authored.Length; i++)
                Assert.AreEqual(authored[i], roundTripped.BonusSpawns[i], $"bonusSpawns[{i}]");

            ScriptableObject.DestroyImmediate(source);
            ScriptableObject.DestroyImmediate(roundTripped);
        }

        // 미저작(기존 16장)이 길이 0 으로 투영되고 Dispose 가 안전해야 한다 — goals 를
        // IsCreated 불변식에서 뺀 것과 같은 보호(안 채우는 생산자가 뒤집히지 않게).
        [Test]
        public void BonusSpawns_미저작이면_길이_0()
        {
            var doc = BuildSampleDocument();
            using var map = MapDocumentBuilder.ToGeneratedMap(doc, Allocator.TempJob);
            Assert.IsTrue(map.bonusSpawns.IsCreated);
            Assert.AreEqual(0, map.bonusSpawns.Length);
            Assert.IsTrue(map.IsCreated, "bonusSpawns 는 IsCreated 불변식에 들지 않는다");
            ScriptableObject.DestroyImmediate(doc);
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
        public void ToGeneratedMap_ProducesCellArrays()
        {
            var doc = BuildSampleDocument();
            using var map = MapDocumentBuilder.ToGeneratedMap(doc, Allocator.TempJob);

            Assert.IsTrue(map.tiles.IsCreated);
            Assert.IsTrue(map.spawns.IsCreated);
            // 빌더 산출물 불변식: IsCreated ⇒ placeMask 생성됨 (placement-mask unit 0).
            Assert.IsTrue(map.placeMask.IsCreated);

            int n = map.gridSize.x * map.gridSize.y;
            Assert.AreEqual(n, map.tiles.Length);
            Assert.AreEqual(n, map.placeMask.Length);

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
            doc.SetFrom(w, h, tiles,
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

        // battle-structures unit 0 — goal-stability 의 per-goal 안정도(M) 저작 축 테스트 4개를
        // 제거했다. 그 축(MapDocument.goalMaxStability → GeneratedMap → 페인터 컬럼)을 읽는
        // 런타임 경로가 사라져 저작해도 아무 일이 없는 필드였고, 축 자체를 걷어냈다.
        // 거점 체력 저작은 unit 3 StructureData 가 맡는다.

        // ── placement-mask unit 0 — placeMask 왕복·파생 폴백 ────────────────────

        private static MapDocument BuildMaskedDocument(byte[] mask)
        {
            const int w = 6, h = 4; int n = w * h;
            var tiles = new MapTileType[n];
            for (int i = 0; i < n; i++) tiles[i] = MapTileType.Place;
            for (int x = 0; x < w; x++) tiles[2 * w + x] = MapTileType.Walk;

            var doc = ScriptableObject.CreateInstance<MapDocument>();
            doc.SetFrom(w, h, tiles,
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
            mask[2 * 6 + 3] = (byte)PlacementLayer.Ground;   // Walk 셀 (3,2) 에 Ground 층 개방 — 타일 종류와 직교 확인.
            var doc = BuildMaskedDocument(mask);
            using var map = MapDocumentBuilder.ToGeneratedMap(doc, Allocator.TempJob);

            Assert.IsTrue(map.placeMask.IsCreated);
            Assert.AreEqual((byte)PlacementLayer.Ground, map.placeMask[2 * 6 + 3], "Walk 셀 Ground 비트 보존");
            Assert.AreEqual(0, map.placeMask[0], "Place 셀 닫힘 보존 (파생 아님)");

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
            // 기대치를 Derive 호출로 쓰면 "Derive == Derive" tautology 가 된다(구현이 그 함수를 쓴다).
            // 픽스처(6×4, y=2 행만 Walk)의 기대 층을 **명시**해 파생 매핑을 이 테스트가 직접 고정한다.
            int w = map.gridSize.x, h = map.gridSize.y;
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                byte expected = y == 2
                    ? (byte)(PlacementLayer.Path | PlacementLayer.Air)
                    : (byte)(PlacementLayer.Ground | PlacementLayer.Air);
                Assert.AreEqual(expected, map.placeMask[y * w + x],
                    $"placeMask[{x},{y}] — 복도행=경로|Air, 나머지=지면|Air");
            }

            ScriptableObject.DestroyImmediate(doc);
        }

        [Test]
        public void PlaceMask_LengthMismatch_DerivesFromPlaceTiles()
        {
            var doc = BuildMaskedDocument(new byte[] { 1, 1, 1 });   // 3 != 24
            using var map = MapDocumentBuilder.ToGeneratedMap(doc, Allocator.TempJob);

            int n = map.gridSize.x * map.gridSize.y;
            for (int i = 0; i < n; i++)
                Assert.AreEqual(PlacementLayers.Derive(map.tiles[i]), map.placeMask[i], $"placeMask[{i}] 파생 폴백");

            ScriptableObject.DestroyImmediate(doc);
        }

        [Test]
        public void PlaceMask_LengthZero_DerivesFromPlaceTiles()
        {
            // 기존 asset 6종의 실제 로드 모양 — Unity 는 신규 배열 필드를 length-0 으로 로드한다.
            var doc = BuildMaskedDocument(new byte[0]);
            using var map = MapDocumentBuilder.ToGeneratedMap(doc, Allocator.TempJob);

            int n = map.gridSize.x * map.gridSize.y;
            for (int i = 0; i < n; i++)
                Assert.AreEqual(PlacementLayers.Derive(map.tiles[i]), map.placeMask[i], $"placeMask[{i}] 파생 폴백");

            ScriptableObject.DestroyImmediate(doc);
        }

        [Test]
        public void WriteToDocument_UncreatedMask_ExportsDerived()
        {
            // 직접 구성 map(마스크 미생성) 내보내기 — WriteToDocument 의 파생 분기.
            const int w = 4, h = 2; int n = w * h;
            var tiles = new NativeArray<MapTileType>(n, Allocator.TempJob);
            var spawns = new NativeArray<int2>(1, Allocator.TempJob);
            for (int i = 0; i < n; i++) tiles[i] = MapTileType.Place;
            tiles[1] = MapTileType.Walk;
            var map = new GeneratedMap
            {
                tiles = tiles,
                spawns = spawns,
                gridSize = new int2(w, h),
            };
            var doc = ScriptableObject.CreateInstance<MapDocument>();
            try
            {
                MapDocumentBuilder.WriteToDocument(doc, in map);
                Assert.AreEqual(n, doc.PlaceMask.Count);
                for (int i = 0; i < n; i++)
                    Assert.AreEqual(PlacementLayers.Derive(map.tiles[i]), doc.PlaceMask[i], $"placeMask[{i}] 파생 내보내기");
            }
            finally
            {
                map.Dispose();
                ScriptableObject.DestroyImmediate(doc);
            }
        }

        [Test]
        public void PlaceMask_UndefinedBits_SanitizedAway()
        {
            const int n = 24;
            var mask = new byte[n];
            mask[0] = (byte)(PlacementLayer.Ground | PlacementLayer.Path);   // 정의된 2층 동시 개방
            mask[1] = 0x80;   // 미정의 비트만 — 어떤 층도 열지 않은 것과 같아야 한다.
            var doc = BuildMaskedDocument(mask);
            using var map = MapDocumentBuilder.ToGeneratedMap(doc, Allocator.TempJob);

            Assert.AreEqual((byte)(PlacementLayer.Ground | PlacementLayer.Path), map.placeMask[0], "2층 동시 개방 보존");
            Assert.AreEqual(0, map.placeMask[1], "미정의 비트는 Sanitize 로 제거");

            ScriptableObject.DestroyImmediate(doc);
        }
    }
}
