using Unity.Mathematics;

namespace Wassup.Data.MapGrid
{
    public static class MapGridIndex
    {
        public static int CellIndex(int2 cell, int2 gridSize) => cell.y * gridSize.x + cell.x;

        public static int CellIndex(int x, int y, int2 gridSize) => y * gridSize.x + x;

        public static int2 IndexToCell(int index, int2 gridSize) =>
            new int2(index % gridSize.x, index / gridSize.x);

        public static bool InBounds(int2 cell, int2 gridSize) =>
            cell.x >= 0 && cell.x < gridSize.x && cell.y >= 0 && cell.y < gridSize.y;

        public static int Manhattan(int2 a, int2 b) =>
            math.abs(a.x - b.x) + math.abs(a.y - b.y);
    }
}
