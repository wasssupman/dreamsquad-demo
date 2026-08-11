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
    }
}
