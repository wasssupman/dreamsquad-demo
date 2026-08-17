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

        // ── 거점 규칙 (투트랙 리뷰 M-a — 런타임 이관으로 검증 가능해진 규칙들) ──
        // 이 규칙들이 페인터에 인라인돼 있던 동안은 테스트가 볼 수 없었고, 인스펙터로
        // 페인터를 우회해 (Defender, Core) 를 찍으면 «골이 두 벌» 이 재발할 수 있었다.

        private static StructureData MakeData(StructureKind kind)
        {
            var d = ScriptableObject.CreateInstance<StructureData>();
            d.kind = kind;
            return d;
        }

        [Test]
        public void ValidateStructures_DefenderCore_IsForbidden()
        {
            var core = MakeData(StructureKind.Core);
            try
            {
                var errs = new List<string>();
                StructureAuthoringRules.ValidateStructures(new[]
                {
                    new StructureEntry { cell = new Vector2Int(3, 3), side = StructureSide.Defender, data = core },
                }, 8, 8, errs);
                Assert.AreEqual(1, errs.Count,
                    "방어 마음의 정본은 goals[] — structures 로 찍으면 골이 두 벌이 된다");
            }
            finally { Object.DestroyImmediate(core); }
        }

        [Test]
        public void ValidateStructures_InstinctFootprint_OutOfBounds_IsError()
        {
            var instinct = MakeData(StructureKind.Instinct);
            try
            {
                var errs = new List<string>();
                // 3×3 본능을 (0,0) 에 — 반경 1 이 격자 밖으로 나간다.
                StructureAuthoringRules.ValidateStructures(new[]
                {
                    new StructureEntry { cell = new Vector2Int(0, 0), side = StructureSide.Enemy, data = instinct },
                }, 8, 8, errs);
                Assert.AreEqual(1, errs.Count);
            }
            finally { Object.DestroyImmediate(instinct); }
        }

        [Test]
        public void ValidateStructures_OverlappingFootprints_IsError()
        {
            var instinct = MakeData(StructureKind.Instinct);
            try
            {
                var errs = new List<string>();
                // 3×3 두 개가 한 칸 겹치게 (중심 거리 2).
                StructureAuthoringRules.ValidateStructures(new[]
                {
                    new StructureEntry { cell = new Vector2Int(2, 2), side = StructureSide.Enemy, data = instinct },
                    new StructureEntry { cell = new Vector2Int(4, 2), side = StructureSide.Enemy, data = instinct },
                }, 10, 10, errs);
                Assert.Greater(errs.Count, 0, "footprint 겹침은 에러");
            }
            finally { Object.DestroyImmediate(instinct); }
        }

        [Test]
        public void ValidateStructures_ValidLayout_NoErrors()
        {
            var core = MakeData(StructureKind.Core);
            var instinct = MakeData(StructureKind.Instinct);
            // 방어 본능은 편에 맞는 마스크여야 한다 — SO 기본값(DefenderUnit, 적 본능용)을
            // 그대로 물리면 리뷰 M-8 의 아군 사격 검증에 걸린다(그게 그 검증의 존재 이유다).
            instinct.targetFactions = Faction.EnemyUnit;
            try
            {
                var errs = new List<string>();
                StructureAuthoringRules.ValidateStructures(new[]
                {
                    new StructureEntry { cell = new Vector2Int(1, 1), side = StructureSide.Enemy, data = core },
                    new StructureEntry { cell = new Vector2Int(5, 5), side = StructureSide.Defender, data = instinct },
                }, 10, 10, errs);
                Assert.IsEmpty(errs, "적 마음 1×1 + 방어 본능 3×3(적 유닛 마스크), 겹침 없음 — 유효 저작");
            }
            finally
            {
                Object.DestroyImmediate(core);
                Object.DestroyImmediate(instinct);
            }
        }

        // 리뷰 M-8 — 아군 사격 저작 함정 검출선.
        [Test]
        public void ValidateStructures_DefenderInstinct_TargetingDefenders_IsError()
        {
            var instinct = MakeData(StructureKind.Instinct);   // 기본 SO = DefenderUnit 마스크(적 본능용)
            try
            {
                var errs = new List<string>();
                StructureAuthoringRules.ValidateStructures(new[]
                {
                    new StructureEntry { cell = new Vector2Int(5, 5), side = StructureSide.Defender, data = instinct },
                }, 10, 10, errs);
                Assert.AreEqual(1, errs.Count,
                    "SO 는 진영을 모르고 진영은 배치가 정한다 — 이 틈의 아군 사격 조합을 저작에서 잡는다");
            }
            finally { Object.DestroyImmediate(instinct); }
        }

        [Test]
        public void CountEnemyCores_CountsOnlyEnemyCores()
        {
            var core = MakeData(StructureKind.Core);
            var instinct = MakeData(StructureKind.Instinct);
            try
            {
                Assert.AreEqual(0, StructureAuthoringRules.CountEnemyCores(null));
                Assert.AreEqual(1, StructureAuthoringRules.CountEnemyCores(new[]
                {
                    new StructureEntry { cell = default, side = StructureSide.Enemy, data = core },
                    new StructureEntry { cell = default, side = StructureSide.Enemy, data = instinct },   // 본능은 미계수
                    new StructureEntry { cell = default, side = StructureSide.Defender, data = core },    // 방어는 미계수
                    new StructureEntry { cell = default, side = StructureSide.Enemy, data = null },       // 빈 데이터 미계수
                }));
            }
            finally
            {
                Object.DestroyImmediate(core);
                Object.DestroyImmediate(instinct);
            }
        }

        // ── 문서 왕복 ───────────────────────────────────────────────────────────

        private static MapDocument BuildDocument(StructureEntry[] structures)
        {
            const int w = 8, h = 6; int n = w * h;
            var tiles = new MapTileType[n];
            for (int i = 0; i < n; i++) tiles[i] = MapTileType.Place;
            for (int x = 0; x < w; x++) tiles[3 * w + x] = MapTileType.Walk;

            var doc = ScriptableObject.CreateInstance<MapDocument>();
            doc.SetFrom(w, h, tiles,
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

        // ── unit 6 — 공성 모드 파생 (적 마음 → spawns[]) ────────────────────────

        private static MapDocument BuildDocumentWithSpawns(Vector2Int[] spawns, StructureEntry[] structures)
        {
            const int w = 8, h = 6; int n = w * h;
            var tiles = new MapTileType[n];
            for (int i = 0; i < n; i++) tiles[i] = MapTileType.Place;
            // siege-lane-spawn unit 0 — 파생 스폰이 마음의 하단/상단 셀이라 복도가 3줄(y=2..4)
            // 이어야 스폰 클러스터가 Walk 다. 구 픽스처(한 줄 y=3)는 마음 셀 스폰 시절의 것.
            for (int x = 0; x < w; x++)
                for (int y = 2; y <= 4; y++) tiles[y * w + x] = MapTileType.Walk;

            var doc = ScriptableObject.CreateInstance<MapDocument>();
            doc.SetFrom(w, h, tiles,
                new[] { new Vector2Int(7, 3) },
                spawns,
                seed: 7, version: 0);
            if (structures != null) doc.SetStructures(structures);
            return doc;
        }

        [Test]
        public void SiegeDoc_EnemyCoreFlanks_BecomeTheSpawns_AndPassConnectivity()
        {
            var core = ScriptableObject.CreateInstance<StructureData>();
            core.kind = StructureKind.Core;
            // 공성 문서: spawns 미저작(빈 배열) + 적 마음 1 (Walk 복도 y=2..4 가운데).
            var doc = BuildDocumentWithSpawns(new Vector2Int[0], new[]
            {
                new StructureEntry { cell = new Vector2Int(0, 3), side = StructureSide.Enemy, data = core },
            });
            try
            {
                using var map = MapDocumentBuilder.ToGeneratedMap(doc, Allocator.TempJob);
                // siege-lane-spawn unit 0 — 파생 스폰 = 마음의 하단·상단 2셀. 순서가 곧 레인
                // 번호라(하단 = lane 0) 여기서 pin 한다 — 뒤집히면 레인별 spawnRoutes 가 서로 바뀐다.
                Assert.AreEqual(2, map.spawns.Length, "적 마음 1 = 파생 스폰 2 (하단·상단)");
                Assert.AreEqual(new int2(0, 2), map.spawns[0], "하단(y−1) 먼저 = lane 0");
                Assert.AreEqual(new int2(0, 4), map.spawns[1], "상단(y+1) = lane 1");
                Assert.IsTrue(MapConnectivity.AllSpawnsReachGoal(map),
                    "파생 스폰 둘 다에서 골까지 가야 한다");
            }
            finally { Object.DestroyImmediate(doc); Object.DestroyImmediate(core); }
        }

        [Test]
        public void SiegeDoc_AuthoredSpawnsAreOverridden_ByDerivation()
        {
            var core = ScriptableObject.CreateInstance<StructureData>();
            core.kind = StructureKind.Core;
            // «공성 + spawns 저작» 은 검증 에러지만 — 뚫고 와도 파생이 덮는다.
            var doc = BuildDocumentWithSpawns(
                new[] { new Vector2Int(1, 3), new Vector2Int(2, 3) },
                new[] { new StructureEntry { cell = new Vector2Int(0, 3), side = StructureSide.Enemy, data = core } });
            try
            {
                using var map = MapDocumentBuilder.ToGeneratedMap(doc, Allocator.TempJob);
                Assert.AreEqual(2, map.spawns.Length, "표현 불가능해야 할 상태를 런타임에서도 화해시킨다");
                Assert.AreEqual(new int2(0, 2), map.spawns[0]);
                Assert.AreEqual(new int2(0, 4), map.spawns[1]);
            }
            finally { Object.DestroyImmediate(doc); Object.DestroyImmediate(core); }
        }

        [Test]
        public void InvasionDoc_NoEnemyCore_KeepsAuthoredSpawns()
        {
            var instinct = ScriptableObject.CreateInstance<StructureData>();
            instinct.kind = StructureKind.Instinct;
            // 침략 + 방어 본능 — 적 마음이 없으면 파생은 손대지 않는다(현행 9장 무회귀 축).
            var doc = BuildDocumentWithSpawns(
                new[] { new Vector2Int(0, 3), new Vector2Int(1, 3) },
                new[] { new StructureEntry { cell = new Vector2Int(4, 3), side = StructureSide.Defender, data = instinct } });
            try
            {
                using var map = MapDocumentBuilder.ToGeneratedMap(doc, Allocator.TempJob);
                Assert.AreEqual(2, map.spawns.Length, "적 마음 0 → 저작 spawns 그대로");
                Assert.AreEqual(new int2(0, 3), map.spawns[0]);
            }
            finally { Object.DestroyImmediate(doc); Object.DestroyImmediate(instinct); }
        }

        [Test]
        public void ValidateStructures_EnemyCoreOnNonWalk_IsError()
        {
            var core = MakeData(StructureKind.Core);
            try
            {
                const int w = 8, h = 6;
                var tiles = new MapTileType[w * h];
                for (int i = 0; i < tiles.Length; i++) tiles[i] = MapTileType.Place;   // 전부 비-Walk

                var errs = new List<string>();
                StructureAuthoringRules.ValidateStructures(new[]
                {
                    new StructureEntry { cell = new Vector2Int(2, 2), side = StructureSide.Enemy, data = core },
                }, w, h, errs, tiles);
                // siege-lane-spawn unit 0 — 스폰 클러스터 = 마음·하단·상단 3셀. 셋 다 비-Walk 라
                // 셀별 에러 3건 — 연결성 런타임 hard-fail 을 저작에서 잡는다.
                Assert.AreEqual(3, errs.Count,
                    "스폰 클러스터(마음·하단·상단) 3셀이 전부 비-Walk 면 셀별로 잡힌다");

                errs.Clear();
                tiles[1 * w + 2] = MapTileType.Walk;   // 하단
                tiles[2 * w + 2] = MapTileType.Walk;   // 마음
                tiles[3 * w + 2] = MapTileType.Walk;   // 상단
                StructureAuthoringRules.ValidateStructures(new[]
                {
                    new StructureEntry { cell = new Vector2Int(2, 2), side = StructureSide.Enemy, data = core },
                }, w, h, errs, tiles);
                Assert.IsEmpty(errs, "스폰 클러스터 3셀이 Walk 면 통과");
            }
            finally { Object.DestroyImmediate(core); }
        }

        // ── 연결성 하한 ─────────────────────────────────────────────────────────

        // instinct-content unit 1 — 연결성 BFS 가 벽으로 세는 것은 **타일뿐**이다. 거점은
        // 마음이든 본능이든 통행을 막지 않으므로, 복도를 «봉인» 하는 거점이란 게 없다.
        //
        // 옛 리뷰 H-2 는 정반대(본능은 벽)를 단정했다. 그 계약(12)이 폐기되면서 이 단정도
        // 뒤집힌다 — 지금 이 검사가 false 를 돌려주면 저작 가능한 정상 맵이 툴에서 거절된다.
        [Test]
        public void AllSpawnsReachGoal_StructuresNeverOccludeCorridor()
        {
            int w = 7, h = 3, n = w * h;
            var tiles = new NativeArray<MapTileType>(n, Allocator.Persistent);
            var spawns = new NativeArray<int2>(1, Allocator.Persistent);
            var goals = new NativeArray<int2>(1, Allocator.Persistent);
            var structures = new NativeArray<StructurePlacement>(1, Allocator.Persistent);
            try
            {
                for (int i = 0; i < n; i++) tiles[i] = MapTileType.Walk;   // 3줄 전면 Walk
                spawns[0] = new int2(0, 1);
                goals[0] = new int2(6, 1);
                // 3×3 본능이 (3,1) — 높이 3 복도를 footprint 로 정확히 덮는 최악 배치.
                structures[0] = new StructurePlacement
                {
                    cell = new int2(3, 1),
                    faction = Wassup.Battle.Units.Faction.EnemyInstinct,
                };

                var map = new GeneratedMap
                {
                    tiles = tiles, spawns = spawns, goals = goals, structures = structures,
                    gridSize = new int2(w, h), goal = goals[0],
                };
                Assert.IsTrue(MapConnectivity.AllSpawnsReachGoal(map),
                    "본능은 벽이 아니라 건물이다 — 복도를 덮어도 적은 그 위를 지나간다");

                // 마음도 같다 — 거점 종류는 통행에 아무 영향이 없다.
                structures[0] = new StructurePlacement
                {
                    cell = new int2(3, 1),
                    faction = Wassup.Battle.Units.Faction.EnemyCore,
                };
                Assert.IsTrue(MapConnectivity.AllSpawnsReachGoal(map), "마음도 통행을 막지 않는다");
            }
            finally
            {
                if (tiles.IsCreated) tiles.Dispose();
                if (spawns.IsCreated) spawns.Dispose();
                if (goals.IsCreated) goals.Dispose();
                if (structures.IsCreated) structures.Dispose();
            }
        }

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
