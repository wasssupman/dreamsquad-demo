using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace Wassup.Data
{
    public sealed class BoardVisualPlan
    {
        private readonly BoardVisualCell[] cells;
        private readonly BoardVisualRegion[] regions;
        private readonly BoardDecorAnchor[] decorAnchors;

        public readonly int seed;
        public readonly int2 gridSize;
        public readonly int2 goal;
        public readonly int2[] spawns;
        public IReadOnlyList<BoardVisualCell> Cells => cells;
        public IReadOnlyList<BoardVisualRegion> Regions => regions;
        public IReadOnlyList<BoardDecorAnchor> DecorAnchors => decorAnchors;

        public BoardVisualPlan(int seed, int2 gridSize, BoardVisualCell[] cells, BoardVisualRegion[] regions, BoardDecorAnchor[] decorAnchors)
            : this(seed, gridSize, int2.zero, null, cells, regions, decorAnchors)
        {
        }

        public BoardVisualPlan(
            int seed,
            int2 gridSize,
            int2 goal,
            int2[] spawns,
            BoardVisualCell[] cells,
            BoardVisualRegion[] regions,
            BoardDecorAnchor[] decorAnchors)
        {
            this.seed = seed;
            this.gridSize = gridSize;
            this.goal = goal;
            this.spawns = spawns ?? Array.Empty<int2>();
            this.cells = cells ?? Array.Empty<BoardVisualCell>();
            this.regions = regions ?? Array.Empty<BoardVisualRegion>();
            this.decorAnchors = decorAnchors ?? Array.Empty<BoardDecorAnchor>();
        }

        public bool ContainsCell(int2 cell)
            => cell.x >= 0 && cell.y >= 0 && cell.x < gridSize.x && cell.y < gridSize.y;

        public int CellIndex(int2 cell) => cell.y * gridSize.x + cell.x;

        public BoardVisualCell CellAt(int2 cell)
            => ContainsCell(cell) ? cells[CellIndex(cell)] : BoardVisualCell.Empty;
    }
}
