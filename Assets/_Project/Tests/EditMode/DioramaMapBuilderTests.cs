using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using Wassup.Data;
using Wassup.Data.MapGrid;

namespace Wassup.Tests.EditMode
{
    // map-diorama-stage unit 1 — 스캔(plain) → GeneratedMap 조립의 순수 코어 검증.
    // 씬/컴포넌트 없이 StageScan 픽스처로만 검증한다 — 스캔 자체는 얇은 변환(MapStageScanner)이라
    // 라이브 경로 검증은 unit 2 의 PlayMode 스모크가 맡는다.
    public class DioramaMapBuilderTests
    {
        // ---- 양자화 (MapStageMath — 기즈모·빌더 공유 산식) ----

        [Test]
        public void LocalToCell_FloorsOnBoundary_AndNegative()
        {
            Vector3 origin = Vector3.zero;
            // 셀 경계선 위(정확히 1.0)는 다음 셀 — floor 규칙.
            Assert.AreEqual(new Vector2Int(1, 0), MapStageMath.LocalToCell(new Vector3(1f, 0f, 0.5f), origin, 1f));
            // 경계 직전은 이전 셀.
            Assert.AreEqual(new Vector2Int(0, 0), MapStageMath.LocalToCell(new Vector3(0.999f, 0f, 0.5f), origin, 1f));
            // 음수 로컬 좌표는 -1 셀 (0 이 아니라) — floor 가 truncate 와 갈리는 지점.
            Assert.AreEqual(new Vector2Int(-1, -1), MapStageMath.LocalToCell(new Vector3(-0.5f, 0f, -0.5f), origin, 1f));
            // 원점 오프셋 반영.
            Assert.AreEqual(new Vector2Int(0, 0), MapStageMath.LocalToCell(new Vector3(2.5f, 0f, 3.5f), new Vector3(2f, 0f, 3f), 1f));
        }

        [Test]
        public void FootprintCells_ClampsSizeToMinimumOne()
        {
            RectInt cells = MapStageMath.FootprintCells(new Vector2Int(3, 4), Vector2Int.zero, Vector2Int.zero);
            Assert.AreEqual(new RectInt(3, 4, 1, 1), cells);
        }

        // ---- 조립: tiles 합성 / placeMask ----

        static StageScan MinimalScan(int w = 8, int h = 6)
        {
            var scan = new StageScan { playAreaCells = new Vector2Int(w, h) };
            scan.spawns.Add(new StageSpawnPoint { cell = new Vector2Int(0, 0), laneIndex = 0, routeIndex = -1 });
            scan.spawns.Add(new StageSpawnPoint { cell = new Vector2Int(0, h - 1), laneIndex = 1, routeIndex = -1 });
            scan.goals.Add(new Vector2Int(w - 1, h / 2));
            return scan;
        }

        [Test]
        public void Assemble_OpenIsWalk_BlockedIsDeco_MaskMirrors()
        {
            var scan = MinimalScan();
            scan.blockedRects.Add(new RectInt(3, 2, 2, 2));
            var map = DioramaMapBuilder.Assemble(scan, Allocator.Persistent);
            try
            {
                Assert.AreEqual(MapTileType.Walk, map.TileAt(new int2(0, 0)));
                Assert.AreEqual(MapTileType.Deco, map.TileAt(new int2(3, 2)));
                Assert.AreEqual(MapTileType.Deco, map.TileAt(new int2(4, 3)));
                Assert.AreEqual(DioramaMapBuilder.OpenCellLayers, map.LayersAt(new int2(0, 0)));
                Assert.AreEqual(0, map.LayersAt(new int2(3, 2)));
            }
            finally { map.Dispose(); }
        }

        [Test]
        public void Assemble_BlockedRectOrder_DoesNotChangeOutput()
        {
            var a = MinimalScan();
            a.blockedRects.Add(new RectInt(1, 1, 2, 1));
            a.blockedRects.Add(new RectInt(5, 3, 1, 2));
            var b = MinimalScan();
            b.blockedRects.Add(new RectInt(5, 3, 1, 2));
            b.blockedRects.Add(new RectInt(1, 1, 2, 1));

            var mapA = DioramaMapBuilder.Assemble(a, Allocator.Persistent);
            var mapB = DioramaMapBuilder.Assemble(b, Allocator.Persistent);
            try
            {
                for (int i = 0; i < mapA.tiles.Length; i++)
                {
                    Assert.AreEqual(mapA.tiles[i], mapB.tiles[i]);
                    Assert.AreEqual(mapA.placeMask[i], mapB.placeMask[i]);
                }
            }
            finally { mapA.Dispose(); mapB.Dispose(); }
        }

        [Test]
        public void Assemble_BlockZone_ClosesPlacement_KeepsTraversal()
        {
            var scan = MinimalScan();
            scan.placementBlockRects.Add(new RectInt(2, 2, 3, 2));
            var map = DioramaMapBuilder.Assemble(scan, Allocator.Persistent);
            try
            {
                var cell = new int2(3, 3);
                // 배치는 전 층 닫힘 (critic C-2 — BlockZone 이 전선 저작의 후계)
                Assert.IsFalse(map.PlaceableAt(cell, PlacementLayer.Ground));
                Assert.IsFalse(map.PlaceableAt(cell, PlacementLayer.Path));
                // 통행은 불변 — Walk 그대로 (walkMask = tiles==Walk 파생식이 이 셀을 계속 연다)
                Assert.AreEqual(MapTileType.Walk, map.TileAt(cell));
            }
            finally { map.Dispose(); }
        }

        [Test]
        public void Assemble_FootprintPartialOverlap_ClipsToPlayArea()
        {
            var scan = MinimalScan(8, 6);
            // playArea 오른쪽 경계에 걸친 3×1 — 안쪽 (7,2) 만 차단, 밖은 무시 (경계 걸침 규칙)
            scan.blockedRects.Add(new RectInt(7, 2, 3, 1));
            var map = DioramaMapBuilder.Assemble(scan, Allocator.Persistent);
            try
            {
                Assert.AreEqual(MapTileType.Deco, map.TileAt(new int2(7, 2)));
                Assert.AreEqual(MapTileType.Walk, map.TileAt(new int2(6, 2)));
            }
            finally { map.Dispose(); }
        }

        // ---- 조립: 결정론 정렬 ----

        [Test]
        public void Assemble_SpawnsSortByLane_GoalsSortLexicographic()
        {
            var scan = new StageScan { playAreaCells = new Vector2Int(8, 6) };
            // 저작(씬) 순서를 일부러 역순으로
            scan.spawns.Add(new StageSpawnPoint { cell = new Vector2Int(0, 5), laneIndex = 1, routeIndex = -1 });
            scan.spawns.Add(new StageSpawnPoint { cell = new Vector2Int(0, 0), laneIndex = 0, routeIndex = -1 });
            scan.goals.Add(new Vector2Int(7, 4));
            scan.goals.Add(new Vector2Int(7, 1));

            var map = DioramaMapBuilder.Assemble(scan, Allocator.Persistent);
            try
            {
                Assert.AreEqual(new int2(0, 0), map.spawns[0]);   // lane 0 이 먼저
                Assert.AreEqual(new int2(0, 5), map.spawns[1]);
                Assert.AreEqual(new int2(7, 1), map.goals[0]);    // (y,x) 사전순
                Assert.AreEqual(new int2(7, 1), map.goal);        // goal = goals[0] (critic M-2)
            }
            finally { map.Dispose(); }
        }

        // ---- 조립: 루트 flatten ----

        [Test]
        public void Assemble_RouteFlatten_RoundTripsAndMapsLanes()
        {
            var scan = MinimalScan();
            var s0 = scan.spawns[0]; s0.routeIndex = 1; scan.spawns[0] = s0;
            // order 역순 저작 — 정렬돼 들어가야 한다
            scan.routePoints.Add(new StageRoutePoint { routeIndex = 0, order = 1, cell = new Vector2Int(2, 1) });
            scan.routePoints.Add(new StageRoutePoint { routeIndex = 0, order = 0, cell = new Vector2Int(1, 1) });
            scan.routePoints.Add(new StageRoutePoint { routeIndex = 1, order = 0, cell = new Vector2Int(4, 4) });

            var map = DioramaMapBuilder.Assemble(scan, Allocator.Persistent);
            try
            {
                Assert.AreEqual(2, map.WaypointPathCount);
                Assert.AreEqual(new int2(1, 1), map.WaypointCellAt(0, 0));
                Assert.AreEqual(new int2(2, 1), map.WaypointCellAt(0, 1));
                Assert.AreEqual(new int2(4, 4), map.WaypointCellAt(1, 0));
                Assert.AreEqual(1, map.RouteForSpawn(0));     // lane 0 → route 1
                Assert.AreEqual(-1, map.RouteForSpawn(1));    // lane 1 → 직행
            }
            finally { map.Dispose(); }
        }

        [Test]
        public void Assemble_NoRoutes_LeavesWaypointArraysUncreated()
        {
            var map = DioramaMapBuilder.Assemble(MinimalScan(), Allocator.Persistent);
            try
            {
                Assert.IsFalse(map.waypointCells.IsCreated);   // 기존 폴백 모양 유지
                Assert.IsFalse(map.waypointRanges.IsCreated);
                Assert.IsFalse(map.spawnRoutes.IsCreated);
                Assert.AreEqual(-1, map.RouteForSpawn(0));
            }
            finally { map.Dispose(); }
        }

        // ---- 조립: 필드 전수 대조 (필드 추가 회귀 방지) ----

        [Test]
        public void Assemble_FieldAudit_NothingLeftDefault()
        {
            var map = DioramaMapBuilder.Assemble(MinimalScan(8, 6), Allocator.Persistent);
            try
            {
                Assert.IsTrue(map.IsCreated);
                Assert.IsTrue(map.placeMask.IsCreated);        // 빌더 산출물 불변식 (placement-mask unit 0)
                Assert.IsTrue(map.goals.IsCreated);
                Assert.IsTrue(map.structures.IsCreated);       // MapDocumentBuilder 와 같은 빈-생성 모양
                Assert.AreEqual(0, map.structures.Length);     // 거점 비가용 (README 계약 11)
                Assert.AreEqual(new int2(8, 6), map.gridSize);
                Assert.AreEqual(-1, map.seed);                 // 수동 저작 관례
                Assert.AreEqual(0, map.generatorVersion);
            }
            finally { map.Dispose(); }
        }

        // ---- Validate: 형식 오류 ----

        [Test]
        public void Validate_LaneDuplicate_AndGap_AreErrors()
        {
            var scan = MinimalScan();
            var s1 = scan.spawns[1]; s1.laneIndex = 0; scan.spawns[1] = s1;   // 중복 0, 0
            Assert.IsNotEmpty(DioramaMapBuilder.Validate(scan));

            var gap = MinimalScan();
            var g1 = gap.spawns[1]; g1.laneIndex = 2; gap.spawns[1] = g1;     // 0, 2 — 공백
            Assert.IsNotEmpty(DioramaMapBuilder.Validate(gap));
        }

        [Test]
        public void Validate_SpawnOrGoalOnBlockedOrOutside_AreErrors()
        {
            var onBlocked = MinimalScan();
            onBlocked.blockedRects.Add(new RectInt(0, 0, 1, 1));              // 스폰 0 위
            Assert.IsNotEmpty(DioramaMapBuilder.Validate(onBlocked));

            var outside = MinimalScan();
            outside.goals[0] = new Vector2Int(99, 0);                          // playArea 밖
            Assert.IsNotEmpty(DioramaMapBuilder.Validate(outside));
        }

        [Test]
        public void Validate_TileSizeMismatch_And_MissingRoute_AreErrors()
        {
            var mismatch = MinimalScan();
            mismatch.previewTileSize = 2f;                                     // 런타임 1 과 상이
            Assert.IsNotEmpty(DioramaMapBuilder.Validate(mismatch));

            var missingRoute = MinimalScan();
            var s0 = missingRoute.spawns[0]; s0.routeIndex = 3; missingRoute.spawns[0] = s0;   // 존재하지 않는 루트
            Assert.IsNotEmpty(DioramaMapBuilder.Validate(missingRoute));
        }

        [Test]
        public void Assemble_InvalidScan_ThrowsMapGenerationFailed()
        {
            var scan = new StageScan { playAreaCells = new Vector2Int(4, 4) };   // 스폰/골 없음
            Assert.Throws<MapGenerationFailedException>(
                () => DioramaMapBuilder.Assemble(scan, Allocator.Persistent));
        }

        // ---- 연결성 (기존 MapConnectivity 재사용 — 양성/음성 대조군) ----

        [Test]
        public void Assemble_OpenYard_PassesConnectivity()
        {
            var scan = MinimalScan();
            scan.blockedRects.Add(new RectInt(3, 1, 1, 3));   // 부분 장애물 — 돌아가면 도달
            var map = DioramaMapBuilder.Assemble(scan, Allocator.Persistent);
            try { Assert.IsTrue(MapConnectivity.AllSpawnsReachGoal(map)); }
            finally { map.Dispose(); }
        }

        [Test]
        public void Assemble_WalledGoal_FailsConnectivity()
        {
            var scan = MinimalScan(8, 6);
            // 골 (7,3) 을 차단 링으로 봉쇄 — 골 셀 자체는 열림(형식 오류 아님), 연결성만 실패
            scan.blockedRects.Add(new RectInt(6, 0, 1, 6));
            var map = DioramaMapBuilder.Assemble(scan, Allocator.Persistent);
            try { Assert.IsFalse(MapConnectivity.AllSpawnsReachGoal(map)); }
            finally { map.Dispose(); }
        }

        // ---- unit 9: 보너스 포탈 (규칙 소유자 BonusSpawnAuthoringRules 를 스테이지 경로에서 재사용) ----

        static bool HasBonusError(StageScan scan)
            => DioramaMapBuilder.Validate(scan).Exists(e => e.Contains("bonusSpawn"));

        [Test]
        public void BonusSpawns_None_IsValid_AndAssemblesEmptyArray()
        {
            var scan = MinimalScan();
            Assert.IsFalse(HasBonusError(scan));
            var map = DioramaMapBuilder.Assemble(scan, Allocator.Persistent);
            try
            {
                // MapDocumentBuilder 와 동형 — 0개도 생성해 두고 소비 측은 Length>0 으로 미저작을 읽는다.
                Assert.IsTrue(map.bonusSpawns.IsCreated);
                Assert.AreEqual(0, map.bonusSpawns.Length);
            }
            finally { map.Dispose(); }
        }

        [Test]
        public void BonusSpawns_OneOrThree_Rejected()
        {
            var one = MinimalScan();
            one.bonusSpawns.Add(new Vector2Int(3, 1));
            Assert.IsTrue(HasBonusError(one), "1개");

            var three = MinimalScan();
            three.bonusSpawns.Add(new Vector2Int(3, 1));
            three.bonusSpawns.Add(new Vector2Int(4, 1));
            three.bonusSpawns.Add(new Vector2Int(5, 1));
            Assert.IsTrue(HasBonusError(three), "3개");
        }

        [Test]
        public void BonusSpawns_DuplicateCell_Rejected()
        {
            var scan = MinimalScan();
            scan.bonusSpawns.Add(new Vector2Int(3, 1));
            scan.bonusSpawns.Add(new Vector2Int(3, 1));
            Assert.IsTrue(HasBonusError(scan));
        }

        [Test]
        public void BonusSpawns_OnBlockedCell_Rejected()
        {
            var scan = MinimalScan();
            scan.blockedRects.Add(new RectInt(3, 1, 1, 1));
            scan.bonusSpawns.Add(new Vector2Int(3, 1));   // 차단 위
            scan.bonusSpawns.Add(new Vector2Int(5, 1));
            Assert.IsTrue(HasBonusError(scan));
        }

        [Test]
        public void BonusSpawns_IsolatedCell_Rejected()
        {
            var scan = MinimalScan();
            // (3,3) 의 8이웃 전부 차단 — 4/8 연결 어느 판정에서도 골에 못 닿는 격리 칸.
            scan.blockedRects.Add(new RectInt(2, 2, 3, 1));
            scan.blockedRects.Add(new RectInt(2, 4, 3, 1));
            scan.blockedRects.Add(new RectInt(2, 3, 1, 1));
            scan.blockedRects.Add(new RectInt(4, 3, 1, 1));
            scan.bonusSpawns.Add(new Vector2Int(3, 3));
            scan.bonusSpawns.Add(new Vector2Int(6, 1));
            Assert.IsTrue(HasBonusError(scan));
        }

        [Test]
        public void BonusSpawns_Two_AssembledInRowMajorOrder()
        {
            var scan = MinimalScan();
            scan.bonusSpawns.Add(new Vector2Int(2, 4));   // 저작 순서 역순으로 넣어도
            scan.bonusSpawns.Add(new Vector2Int(5, 1));
            Assert.IsFalse(HasBonusError(scan));
            var map = DioramaMapBuilder.Assemble(scan, Allocator.Persistent);
            try
            {
                Assert.AreEqual(2, map.bonusSpawns.Length);
                Assert.AreEqual(new int2(5, 1), map.bonusSpawns[0]);   // (y, x) 사전순 — 골과 같은 규약
                Assert.AreEqual(new int2(2, 4), map.bonusSpawns[1]);
            }
            finally { map.Dispose(); }
        }
    }
}
