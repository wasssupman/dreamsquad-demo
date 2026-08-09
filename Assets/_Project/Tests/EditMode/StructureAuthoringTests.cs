using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using Wassup.Battle.Units;
using Wassup.Data;
using Wassup.Data.MapGrid;

namespace Wassup.Tests.EditMode
{
    // battle-structures unit 3 — 거점 저작. 진영 파생 · 모드 파생 · 저작 규칙 · 왕복.
    //
    // 규칙이 런타임 순수 함수에 있는 이유가 이 파일이다 — 페인터에 인라인하면 에디터
    // 어셈블리에 갇혀 검증할 수 없고, 두 벌로 갈리면 «툴은 통과인데 런타임이 거부» 가 난다.
    public class StructureAuthoringTests
    {
        // ── 진영 파생 ───────────────────────────────────────────────────────────
        // 저작은 편 × 종류 두 축만 만진다. 거점이 아닌 비트는 이 함수에서 나올 수 없다.

        [Test]
        public void DeriveFaction_CoversAllSideKindCombos()
        {
            Assert.AreEqual(Faction.DefenderCore,
                StructurePlacements.DeriveFaction(StructureSide.Defender, StructureKind.Core));
            Assert.AreEqual(Faction.DefenderInstinct,
                StructurePlacements.DeriveFaction(StructureSide.Defender, StructureKind.Instinct));
            Assert.AreEqual(Faction.EnemyCore,
                StructurePlacements.DeriveFaction(StructureSide.Enemy, StructureKind.Core));
            Assert.AreEqual(Faction.EnemyInstinct,
                StructurePlacements.DeriveFaction(StructureSide.Enemy, StructureKind.Instinct));
            Assert.AreEqual(Faction.NeutralCore,
                StructurePlacements.DeriveFaction(StructureSide.Neutral, StructureKind.Core));
            Assert.AreEqual(Faction.NeutralInstinct,
                StructurePlacements.DeriveFaction(StructureSide.Neutral, StructureKind.Instinct));
        }

        [Test]
        public void DeriveFaction_NeverProducesNonStructureBits()
        {
            foreach (StructureSide side in new[] { StructureSide.Defender, StructureSide.Enemy, StructureSide.Neutral })
                foreach (StructureKind kind in new[] { StructureKind.Core, StructureKind.Instinct })
                {
                    int bits = (int)StructurePlacements.DeriveFaction(side, kind);
                    Assert.AreNotEqual(0, bits & Factions.AnyStructure, $"{side}/{kind} 은 거점 비트여야 한다");
                    Assert.AreEqual(0, bits & Factions.AnyUnit, $"{side}/{kind} 에 유닛 비트가 섞였다");
                    Assert.AreEqual(0, bits & (int)Faction.BlockingHazard);
                }
        }

        [Test]
        public void FootprintOf_DerivesFromKindBit()
        {
            Assert.AreEqual(StructurePlacements.CoreFootprint,
                StructurePlacements.FootprintOf(Faction.DefenderCore), "마음 1×1");
            Assert.AreEqual(StructurePlacements.InstinctFootprint,
                StructurePlacements.FootprintOf(Faction.EnemyInstinct), "본능 3×3");
            Assert.IsTrue(StructurePlacements.IsCore(Faction.EnemyCore));
            Assert.IsTrue(StructurePlacements.IsInstinct(Faction.DefenderInstinct));
        }

        // ── 모드 파생 + 저작 규칙 (README §모드 판정 표) ────────────────────────

        [Test]
        public void DeriveMode_FromEnemyCoreCount()
        {
            Assert.AreEqual(MapMode.Invasion, StructureAuthoringRules.DeriveMode(0));
            Assert.AreEqual(MapMode.Siege, StructureAuthoringRules.DeriveMode(1));
            Assert.AreEqual(MapMode.Invalid, StructureAuthoringRules.DeriveMode(2));
        }

        [Test]
        public void ValidateMode_Invasion_RequiresSpawnsAndGoals()
        {
            var errs = new List<string>();
            StructureAuthoringRules.ValidateMode(0, defenderGoalCount: 2, spawnCount: 2, errs);
            Assert.IsEmpty(errs, "침략: 스폰 2 · 골 2 는 현행 맵 형태다");

            errs.Clear();
            StructureAuthoringRules.ValidateMode(0, defenderGoalCount: 1, spawnCount: 1, errs);
            Assert.IsEmpty(errs, "침략 스폰 하한은 1 — 런타임 MapConnectivity 하한과 같다");

            errs.Clear();
            StructureAuthoringRules.ValidateMode(0, defenderGoalCount: 0, spawnCount: 0, errs);
            Assert.AreEqual(2, errs.Count, "스폰 0 · 골 0 은 둘 다 에러");
        }

        [Test]
        public void ValidateMode_Siege_ForbidsMultiGoalAndAuthoredSpawns()
        {
            var errs = new List<string>();
            StructureAuthoringRules.ValidateMode(1, defenderGoalCount: 1, spawnCount: 0, errs);
            Assert.IsEmpty(errs, "공성: 방어 마음 1 · 스폰 미저작(파생이 채운다)");

            errs.Clear();
            StructureAuthoringRules.ValidateMode(1, defenderGoalCount: 2, spawnCount: 0, errs);
            Assert.AreEqual(1, errs.Count, "공성 멀티골 금지");

            errs.Clear();
            StructureAuthoringRules.ValidateMode(1, defenderGoalCount: 1, spawnCount: 2, errs);
            Assert.AreEqual(1, errs.Count, "공성은 spawns 저작 금지");
        }

        [Test]
        public void ValidateMode_TwoEnemyCores_IsError()
        {
            var errs = new List<string>();
            StructureAuthoringRules.ValidateMode(2, defenderGoalCount: 1, spawnCount: 0, errs);
            Assert.AreEqual(1, errs.Count, "적 마음 2+ 는 그 자체가 에러");
        }

        // ── 문서 왕복 ───────────────────────────────────────────────────────────

        private static MapDocument BuildDocument(StructureEntry[] structures)
        {
            const int w = 8, h = 6; int n = w * h;
            var tiles = new MapTileType[n];
            for (int i = 0; i < n; i++) tiles[i] = MapTileType.Place;
            for (int x = 0; x < w; x++) tiles[3 * w + x] = MapTileType.Walk;

            var doc = ScriptableObject.CreateInstance<MapDocument>();
            doc.SetFrom(w, h, tiles, new byte[n], new bool[n], new byte[n],
                new[] { new Vector2Int(7, 3) },
                new[] { new Vector2Int(0, 3), new Vector2Int(1, 3) },
                seed: 7, version: 0);
            if (structures != null) doc.SetStructures(structures);
            return doc;
        }

        [Test]
        public void NoStructures_ProjectsEmpty_CurrentMapsUnchanged()
        {
            var doc = BuildDocument(null);
            try
            {
                using var map = MapDocumentBuilder.ToGeneratedMap(doc, Allocator.TempJob);
                Assert.IsTrue(map.structures.IsCreated, "미저작도 배열은 생성된다(길이 0)");
                Assert.AreEqual(0, map.structures.Length, "거점 없는 맵 = 행동 변화 0");
            }
            finally { Object.DestroyImmediate(doc); }
        }

        [Test]
        public void Structures_ProjectToCellAndDerivedFaction()
        {
            var core = ScriptableObject.CreateInstance<StructureData>();
            core.kind = StructureKind.Core;
            var instinct = ScriptableObject.CreateInstance<StructureData>();
            instinct.kind = StructureKind.Instinct;

            var doc = BuildDocument(new[]
            {
                new StructureEntry { cell = new Vector2Int(1, 1), side = StructureSide.Enemy, data = core },
                new StructureEntry { cell = new Vector2Int(4, 3), side = StructureSide.Defender, data = instinct },
            });
            try
            {
                using var map = MapDocumentBuilder.ToGeneratedMap(doc, Allocator.TempJob);
                Assert.AreEqual(2, map.structures.Length);
                Assert.AreEqual(new int2(1, 1), map.structures[0].cell);
                Assert.AreEqual(Faction.EnemyCore, map.structures[0].faction, "적 + 마음 → EnemyCore");
                Assert.AreEqual(new int2(4, 3), map.structures[1].cell);
                Assert.AreEqual(Faction.DefenderInstinct, map.structures[1].faction);
            }
            finally
            {
                Object.DestroyImmediate(doc);
                Object.DestroyImmediate(core);
                Object.DestroyImmediate(instinct);
            }
        }

        [Test]
        public void Structures_NullData_IsSkippedInProjection()
        {
            var core = ScriptableObject.CreateInstance<StructureData>();
            core.kind = StructureKind.Core;
            var doc = BuildDocument(new[]
            {
                new StructureEntry { cell = new Vector2Int(1, 1), side = StructureSide.Enemy, data = null },
                new StructureEntry { cell = new Vector2Int(2, 1), side = StructureSide.Enemy, data = core },
            });
            try
            {
                using var map = MapDocumentBuilder.ToGeneratedMap(doc, Allocator.TempJob);
                Assert.AreEqual(1, map.structures.Length, "data 빈 엔트리는 투영에서 빠진다");
                Assert.AreEqual(new int2(2, 1), map.structures[0].cell);
            }
            finally { Object.DestroyImmediate(doc); Object.DestroyImmediate(core); }
        }

        // GeneratedMap 은 관리 참조를 왕복시킬 수 없다 — 저작 엔트리는 별도 인자로 흐른다.
        [Test]
        public void WriteToDocument_PassesStructureEntriesThrough()
        {
            var core = ScriptableObject.CreateInstance<StructureData>();
            core.kind = StructureKind.Core;
            var entries = new[]
            {
                new StructureEntry { cell = new Vector2Int(2, 1), side = StructureSide.Enemy, data = core },
            };
            var source = BuildDocument(entries);
            var target = ScriptableObject.CreateInstance<MapDocument>();
            try
            {
                using var map = MapDocumentBuilder.ToGeneratedMap(source, Allocator.TempJob);
                MapDocumentBuilder.WriteToDocument(target, in map, entries);

                Assert.AreEqual(1, target.Structures.Count);
                Assert.AreEqual(new Vector2Int(2, 1), target.Structures[0].cell);
                Assert.AreEqual(StructureSide.Enemy, target.Structures[0].side);
                Assert.AreSame(core, target.Structures[0].data, "SO 참조가 보존된다");
            }
            finally
            {
                Object.DestroyImmediate(source);
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(core);
            }
        }

        [Test]
        public void WriteToDocument_WithoutStructures_LeavesAuthoringUntouched()
        {
            var core = ScriptableObject.CreateInstance<StructureData>();
            core.kind = StructureKind.Core;
            var target = BuildDocument(new[]
            {
                new StructureEntry { cell = new Vector2Int(2, 1), side = StructureSide.Enemy, data = core },
            });
            try
            {
                using var map = MapDocumentBuilder.ToGeneratedMap(target, Allocator.TempJob);
                MapDocumentBuilder.WriteToDocument(target, in map);   // structures 미전달
                Assert.AreEqual(1, target.Structures.Count,
                    "null = 거점 저작을 건드리지 않는다(기존 호출자 무회귀)");
            }
            finally { Object.DestroyImmediate(target); Object.DestroyImmediate(core); }
        }

        // ── 연결성 하한 ─────────────────────────────────────────────────────────

        [Test]
        public void AllSpawnsReachGoal_AcceptsSingleSpawn()
        {
            int w = 5, h = 3, n = w * h;
            var tiles = new NativeArray<MapTileType>(n, Allocator.Persistent);
            var spawns = new NativeArray<int2>(1, Allocator.Persistent);
            var goals = new NativeArray<int2>(1, Allocator.Persistent);
            try
            {
                for (int i = 0; i < n; i++) tiles[i] = MapTileType.Place;
                for (int x = 0; x < w; x++) tiles[1 * w + x] = MapTileType.Walk;
                spawns[0] = new int2(0, 1);
                goals[0] = new int2(4, 1);

                var map = new GeneratedMap
                {
                    tiles = tiles, spawns = spawns, goals = goals,
                    gridSize = new int2(w, h), goal = goals[0],
                };
                Assert.IsTrue(MapConnectivity.AllSpawnsReachGoal(map),
                    "공성 맵은 적 마음 1개 = 스폰 1개다 — 하한 2 면 통과할 수 없었다");
            }
            finally
            {
                if (tiles.IsCreated) tiles.Dispose();
                if (spawns.IsCreated) spawns.Dispose();
                if (goals.IsCreated) goals.Dispose();
            }
        }
    }
}
