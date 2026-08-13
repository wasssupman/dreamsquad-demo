using System;
using System.Collections.Generic;
using UnityEngine;

namespace Wassup.Data.MapGrid
{
    [Serializable]
    public sealed class WaypointPath
    {
        [SerializeField] private Vector2Int[] cells;

        public IReadOnlyList<Vector2Int> Cells => cells;

        public WaypointPath(Vector2Int[] cells)
        {
            this.cells = cells;
        }
    }

    // waypoint-routing unit 0 — MapDocument.OnValidate 와 unit 5 페인터가 공유하는
    // 아키텍처 중립 저작 검증. plain 값만 받아 에러/경고를 분리해 결정한다.
    public static class WaypointAuthoringRules
    {
        private const byte GroundTraversalLayers =
            (byte)(PlacementLayer.Ground | PlacementLayer.Path);

        public static void ValidatePaths(
            IReadOnlyList<WaypointPath> paths,
            int width,
            int height,
            IReadOnlyList<MapTileType> tiles,
            IReadOnlyList<Vector2Int> goals,
            IReadOnlyList<Vector2Int> spawns,
            List<string> errors,
            List<string> warnings)
        {
            if (paths == null) return;

            for (int pathIndex = 0; pathIndex < paths.Count; pathIndex++)
            {
                var cells = paths[pathIndex]?.Cells;
                if (cells == null) continue;

                for (int cellIndex = 0; cellIndex < cells.Count; cellIndex++)
                {
                    Vector2Int cell = cells[cellIndex];
                    bool inBounds = cell.x >= 0 && cell.x < width
                        && cell.y >= 0 && cell.y < height;

                    if (!inBounds)
                    {
                        errors.Add($"경로 {pathIndex} 지점 {cellIndex} {cell} 이 격자 밖 ({width}×{height})");
                    }
                    else if (tiles != null && tiles.Count == width * height
                        && (PlacementLayers.Derive(tiles[cell.y * width + cell.x])
                            & GroundTraversalLayers) == 0)
                    {
                        warnings.Add($"경로 {pathIndex} 지점 {cellIndex} {cell}: 지상 층이 닫힌 칸 — Air 경로 전용");
                    }

                    if (Contains(goals, cell) || Contains(spawns, cell))
                        warnings.Add($"경로 {pathIndex} 지점 {cellIndex} {cell} 이 골/스폰 셀과 겹친다");

                    if (cellIndex > 0 && cells[cellIndex - 1] == cell)
                        warnings.Add($"경로 {pathIndex} 지점 {cellIndex} {cell} 이 직전 지점과 연속 중복이다");
                    // waypoint-routing unit 9 — 도달 판정이 체비셰프 1(자기 칸 + 8이웃)이라
                    // **인접한 두 지점은 뒤엣것이 자동으로 통과된다.** 저작자에겐 지점이 둘로
                    // 보이는데 실제로는 하나만 작동하므로 저작 시점에 드러낸다(계약 9).
                    else if (cellIndex > 0
                             && Chebyshev(cells[cellIndex - 1], cell) <= 1)
                        warnings.Add($"경로 {pathIndex} 지점 {cellIndex} {cell} 이 직전 지점과 인접이다 "
                            + "— 도달 판정이 1칸이라 이 지점은 자동 통과된다(2칸 이상 띄울 것)");
                }
            }
        }

        private static bool Contains(IReadOnlyList<Vector2Int> cells, Vector2Int candidate)
        {
            if (cells == null) return false;
            for (int i = 0; i < cells.Count; i++)
                if (cells[i] == candidate) return true;
            return false;
        }

        // waypoint-routing unit 8 — 스폰(레인)별 기본 경로 배정 저작 검증.
        // spawnRoutes 는 spawns 와 같은 순서의 병렬 배열, -1(또는 음수) = 최단거리 폴백.
        public static void ValidateSpawnRoutes(
            IReadOnlyList<int> spawnRoutes,
            IReadOnlyList<WaypointPath> paths,
            IReadOnlyList<Vector2Int> spawns,
            List<string> errors,
            List<string> warnings)
        {
            if (spawnRoutes == null || spawnRoutes.Count == 0) return;

            int spawnCount = spawns?.Count ?? 0;
            int pathCount = paths?.Count ?? 0;
            int laneCount = Math.Min(spawnRoutes.Count, spawnCount);

            for (int i = spawnCount; i < spawnRoutes.Count; i++)
                warnings.Add($"spawnRoutes 항목 {i} 이 스폰 개수({spawnCount})를 넘는다 — 어느 레인에도 안 붙는다");

            for (int lane = 0; lane < laneCount; lane++)
            {
                int routeIndex = spawnRoutes[lane];
                if (routeIndex < 0) continue; // 최단거리 폴백 — 정상

                if (routeIndex >= pathCount)
                {
                    errors.Add($"레인 {lane} 의 기본 경로 인덱스 {routeIndex} 가 경로 배열 밖 (경로 {pathCount}개)");
                    continue;
                }

                for (int other = lane + 1; other < laneCount; other++)
                {
                    if (spawnRoutes[other] == routeIndex)
                        warnings.Add($"레인 {lane} 과 레인 {other} 이 같은 기본 경로 {routeIndex} 를 가리킨다 — 합류 저작일 수 있음");
                }

                var cells = paths[routeIndex]?.Cells;
                if (cells == null || cells.Count == 0) continue;

                Vector2Int firstCell = cells[0];
                Vector2Int ownSpawn = spawns[lane];
                int ownDist = Chebyshev(firstCell, ownSpawn);

                int closestOther = -1;
                int closestDist = int.MaxValue;
                for (int other = 0; other < spawnCount; other++)
                {
                    if (other == lane) continue;
                    int dist = Chebyshev(firstCell, spawns[other]);
                    if (dist < ownDist && dist < closestDist)
                    {
                        closestDist = dist;
                        closestOther = other;
                    }
                }

                if (closestOther >= 0)
                {
                    warnings.Add($"레인 {lane} 의 기본 경로 {routeIndex} 의 첫 지점 {firstCell} 이 레인 {closestOther} 스폰에 더 가깝다 — 가로지르기");
                }
            }
        }

        private static int Chebyshev(Vector2Int a, Vector2Int b)
            => Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));
    }
}
