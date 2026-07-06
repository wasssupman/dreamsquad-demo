using Unity.Mathematics;

namespace Wassup.Battle.Combat
{
    // Shared tile-AOE membership primitive. A candidate cell is "in range" of a
    // center cell when their Chebyshev (chessboard) tile distance is within
    // tileRange — the same rule MeteorResolutionSystem applies inline and the
    // projectile TileAoe payload (unit 4) reuses. Pure + Burst-compatible; callers
    // convert world positions to cells via GridMath.WorldToCell, then loop
    // candidates calling IsInTileRange (no list is materialized here).
    public static class TileAoe
    {
        // Chebyshev tile distance: max(|dx|, |dy|). A diagonal step counts as 1,
        // matching the game's square-range convention (same as GridMath range use).
        public static int TileDistance(int2 a, int2 b)
            => math.max(math.abs(a.x - b.x), math.abs(a.y - b.y));

        public static bool IsInTileRange(int2 candidateCell, int2 centerCell, int tileRange)
            => TileDistance(candidateCell, centerCell) <= tileRange;
    }
}
