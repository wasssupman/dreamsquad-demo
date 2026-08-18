using System.Collections.Generic;
using System.Text;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using Wassup.Data.MapGrid;

namespace Wassup.Data
{
    // map-diorama-stage unit 1 — 스테이지 스캔 결과(plain) → GeneratedMap 조립 순수 코어.
    // 씬/컴포넌트를 모른다 — 스캔은 MapStageScanner(Core)가 하고 여기는 plain 값만 받는다(제약 10).
    // Validate = 형식 오류 목록(에디터 린트가 소비, 예외 아님) · Assemble = 조립(오류 시
    // MapGenerationFailedException — 브리지 hard-fail 경로와 동형, README 계약 9).

    public struct StageSpawnPoint
    {
        public Vector2Int cell;
        public int laneIndex;   // 웨이브 결정론 정본 (README 계약 5) — 0부터 연속
        public int routeIndex;  // -1 = 골 직행(열린 마당 기본값)
    }

    public struct StageRoutePoint
    {
        public int routeIndex;  // 0부터 연속
        public int order;       // 같은 routeIndex 안에서 유일
        public Vector2Int cell;
    }

    // 셀 좌표는 전부 양자화 완료(playArea 셀 공간) 상태로 들어온다.
    public sealed class StageScan
    {
        public Vector2Int playAreaCells = Vector2Int.one;
        public float previewTileSize = 1f;   // 저작 기즈모 표시값 — 런타임과 다르면 형식 오류(기즈모가 거짓말)
        public float runtimeTileSize = 1f;   // 양자화 정본 (BattleBridge.tileSize)
        public readonly List<RectInt> blockedRects = new List<RectInt>();
        public readonly List<RectInt> placementBlockRects = new List<RectInt>();
        public readonly List<StageSpawnPoint> spawns = new List<StageSpawnPoint>();
        public readonly List<Vector2Int> goals = new List<Vector2Int>();
        public readonly List<StageRoutePoint> routePoints = new List<StageRoutePoint>();
    }

    public static class DioramaMapBuilder
    {
        // README 계약 3 — 열린 셀 기본 층. 전선/금지는 PlacementBlockZone 차감으로 저작한다.
        public const byte OpenCellLayers =
            (byte)(PlacementLayer.Ground | PlacementLayer.Path | PlacementLayer.Air);

        // 형식 오류 전수 목록. 순서는 결정적(스캔 목록 순회 순서)이다.
        public static List<string> Validate(StageScan scan)
        {
            var errors = new List<string>();
            Vector2Int area = scan.playAreaCells;

            if (area.x < 1 || area.y < 1)
                errors.Add($"playAreaCells {area} 가 1×1 미만이다.");
            if (Mathf.Abs(scan.previewTileSize - scan.runtimeTileSize) > 1e-4f)
                errors.Add($"previewTileSize({scan.previewTileSize})와 런타임 tileSize({scan.runtimeTileSize})가 다르다 — 기즈모가 거짓 셀을 보여준다.");

            bool[] blocked = BuildBlocked(scan);

            // 스폰 — laneIndex 는 0..N-1 연속 (README 계약 5). 하한 2 = 멀티레인 계약
            // (MapConnectivity 도 스폰 1개 맵을 거부한다 — 기존 저작 규칙 승계).
            if (scan.spawns.Count < 2)
                errors.Add($"SpawnMarker 가 {scan.spawns.Count}개다 — 최소 2개(멀티레인 계약) 필요.");
            var seenLanes = new HashSet<int>();
            foreach (var s in scan.spawns)
            {
                if (!seenLanes.Add(s.laneIndex))
                    errors.Add($"laneIndex {s.laneIndex} 중복.");
                if (s.laneIndex < 0 || s.laneIndex >= scan.spawns.Count)
                    errors.Add($"laneIndex {s.laneIndex} 가 0..{scan.spawns.Count - 1} 범위 밖 — 공백 없이 연속이어야 한다.");
                CheckCell(errors, area, blocked, s.cell, $"스폰(lane {s.laneIndex})");
            }

            if (scan.goals.Count == 0)
                errors.Add("GoalMarker 가 0개다 — 최소 1개 필요.");
            foreach (var g in scan.goals)
                CheckCell(errors, area, blocked, g, "골");

            // 루트 — routeIndex 0..P-1 연속, (routeIndex, order) 유일
            int routeCount = 0;
            var seenOrders = new HashSet<(int, int)>();
            foreach (var p in scan.routePoints)
            {
                if (p.routeIndex < 0) errors.Add($"routeIndex {p.routeIndex} 음수.");
                else routeCount = Mathf.Max(routeCount, p.routeIndex + 1);
                if (!seenOrders.Add((p.routeIndex, p.order)))
                    errors.Add($"루트 R{p.routeIndex}.{p.order} 중복.");
                CheckCell(errors, area, blocked, p.cell, $"루트 R{p.routeIndex}.{p.order}");
            }
            if (routeCount > 0)
            {
                var routesPresent = new bool[routeCount];
                foreach (var p in scan.routePoints)
                    if (p.routeIndex >= 0 && p.routeIndex < routeCount) routesPresent[p.routeIndex] = true;
                for (int r = 0; r < routeCount; r++)
                    if (!routesPresent[r]) errors.Add($"routeIndex 가 0부터 연속이 아니다 — R{r} 이 비었다.");
            }
            foreach (var s in scan.spawns)
                if (s.routeIndex >= routeCount && s.routeIndex >= 0)
                    errors.Add($"스폰(lane {s.laneIndex})의 routeIndex {s.routeIndex} 가 존재하지 않는 루트다.");

            return errors;
        }

        public static GeneratedMap Assemble(StageScan scan, Allocator allocator)
        {
            var errors = Validate(scan);
            if (errors.Count > 0)
            {
                var sb = new StringBuilder("[DioramaMapBuilder] 스테이지 형식 오류:");
                foreach (var e in errors) sb.Append("\n- ").Append(e);
                throw new MapGenerationFailedException(sb.ToString());
            }

            int w = scan.playAreaCells.x;
            int h = scan.playAreaCells.y;
            int n = w * h;
            bool[] blocked = BuildBlocked(scan);

            // tiles 합성 (README 계약 2): 열림 = Walk, 차단 = Deco — 기존 파생식 무변경의 근거.
            var tiles = new NativeArray<MapTileType>(n, allocator);
            var placeMask = new NativeArray<byte>(n, allocator);
            for (int i = 0; i < n; i++)
            {
                tiles[i] = blocked[i] ? MapTileType.Deco : MapTileType.Walk;
                placeMask[i] = blocked[i] ? (byte)0 : OpenCellLayers;
            }
            foreach (var rect in scan.placementBlockRects)
            {
                RectInt clipped = ClipToArea(rect, scan.playAreaCells);
                for (int y = clipped.yMin; y < clipped.yMax; y++)
                for (int x = clipped.xMin; x < clipped.xMax; x++)
                    placeMask[y * w + x] = 0;
            }

            // 스폰 = laneIndex 오름차순 (씬 계층 순서 비의존 — README 계약 5)
            var sortedSpawns = new List<StageSpawnPoint>(scan.spawns);
            sortedSpawns.Sort((a, b) => a.laneIndex.CompareTo(b.laneIndex));
            var spawns = new NativeArray<int2>(sortedSpawns.Count, allocator);
            for (int i = 0; i < sortedSpawns.Count; i++)
                spawns[i] = new int2(sortedSpawns[i].cell.x, sortedSpawns[i].cell.y);

            // 골 = 셀 사전순(y, x) — 저작 순서 비의존. goal = goals[0] (critic M-2)
            var sortedGoals = new List<Vector2Int>(scan.goals);
            sortedGoals.Sort((a, b) => a.y != b.y ? a.y.CompareTo(b.y) : a.x.CompareTo(b.x));
            var goals = new NativeArray<int2>(sortedGoals.Count, allocator);
            for (int i = 0; i < sortedGoals.Count; i++)
                goals[i] = new int2(sortedGoals[i].x, sortedGoals[i].y);

            // 루트 flatten — MapDocumentBuilder 침략 모드 경로와 같은 형식(ranges = (start, count)).
            // 루트 없음 = 미생성(기존 폴백 모양). routeIndex 연속성은 Validate 가 보장.
            NativeArray<int2> waypointCells = default;
            NativeArray<int2> waypointRanges = default;
            int routeCount = 0;
            foreach (var p in scan.routePoints) routeCount = Mathf.Max(routeCount, p.routeIndex + 1);
            if (routeCount > 0)
            {
                var byRoute = new List<StageRoutePoint>[routeCount];
                for (int r = 0; r < routeCount; r++) byRoute[r] = new List<StageRoutePoint>();
                foreach (var p in scan.routePoints) byRoute[p.routeIndex].Add(p);
                for (int r = 0; r < routeCount; r++) byRoute[r].Sort((a, b) => a.order.CompareTo(b.order));

                int total = scan.routePoints.Count;
                waypointCells = new NativeArray<int2>(total, allocator);
                waypointRanges = new NativeArray<int2>(routeCount, allocator);
                int written = 0;
                for (int r = 0; r < routeCount; r++)
                {
                    waypointRanges[r] = new int2(written, byRoute[r].Count);
                    foreach (var p in byRoute[r])
                        waypointCells[written++] = new int2(p.cell.x, p.cell.y);
                }
            }

            // 레인별 기본 경로 — 전 레인 -1(직행)이면 미생성(기본 모양 유지, unit 1 규칙)
            NativeArray<int> spawnRoutes = default;
            bool anyRoute = false;
            foreach (var s in sortedSpawns) anyRoute |= s.routeIndex >= 0;
            if (anyRoute)
            {
                spawnRoutes = new NativeArray<int>(sortedSpawns.Count, allocator);
                for (int i = 0; i < sortedSpawns.Count; i++)
                    spawnRoutes[i] = sortedSpawns[i].routeIndex;
            }

            return new GeneratedMap
            {
                tiles = tiles,
                placeMask = placeMask,
                gridSize = new int2(w, h),
                spawns = spawns,
                goal = goals[0],
                goals = goals,
                waypointCells = waypointCells,
                waypointRanges = waypointRanges,
                spawnRoutes = spawnRoutes,
                // 거점은 이 브랜치에서 비가용(README 계약 11) — MapDocumentBuilder 처럼 빈 생성으로 통일.
                structures = new NativeArray<StructurePlacement>(0, allocator),
                seed = -1,              // 수동 저작 관례 (MapDocument authoringSeed=-1 승계)
                generatorVersion = 0,
            };
        }

        static void CheckCell(List<string> errors, Vector2Int area, bool[] blocked, Vector2Int cell, string what)
        {
            if (cell.x < 0 || cell.y < 0 || cell.x >= area.x || cell.y >= area.y)
            {
                errors.Add($"{what} 셀 {cell} 이 playArea {area} 밖이다.");
                return;
            }
            if (blocked[cell.y * area.x + cell.x])
                errors.Add($"{what} 셀 {cell} 이 차단 셀 위다.");
        }

        // 경계 걸침 규칙(unit 1): footprint 가 playArea 밖으로 삐져나가면 안쪽 셀만 차단한다 —
        // 앵커 기준으로 통째 버리면 «보이는 벽을 적이 통과»한다. 완전히 밖인 rect 는 빈 clip.
        static bool[] BuildBlocked(StageScan scan)
        {
            int w = scan.playAreaCells.x;
            var blocked = new bool[w * scan.playAreaCells.y];
            foreach (var rect in scan.blockedRects)
            {
                RectInt clipped = ClipToArea(rect, scan.playAreaCells);
                for (int y = clipped.yMin; y < clipped.yMax; y++)
                for (int x = clipped.xMin; x < clipped.xMax; x++)
                    blocked[y * w + x] = true;
            }
            return blocked;
        }

        static RectInt ClipToArea(RectInt rect, Vector2Int area)
        {
            int xMin = Mathf.Max(rect.xMin, 0);
            int yMin = Mathf.Max(rect.yMin, 0);
            int xMax = Mathf.Min(rect.xMax, area.x);
            int yMax = Mathf.Min(rect.yMax, area.y);
            return new RectInt(xMin, yMin, Mathf.Max(0, xMax - xMin), Mathf.Max(0, yMax - yMin));
        }
    }
}
