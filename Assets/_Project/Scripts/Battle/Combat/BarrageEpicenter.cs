using Unity.Collections;
using Unity.Mathematics;

namespace Wassup.Battle.Combat
{
    // nightmare-catcher unit 2 — deterministic epicenter pick for AreaBarrage:
    // index-based round-robin over the stable (row-major ascending) ordering of
    // the candidate cells, NOT seeded RNG (계약 7 — 시뮬 구조적 결정론). Pure
    // math over plain arrays (제약 10), EditMode-pinned; the caller snapshots
    // the living-defender cells and owns fireCount (increment only on an actual
    // fire, so a 0-candidate no-op never drifts the rotation phase).
    public static class BarrageEpicenter
    {
        // Returns the index (into `cells`) of the (fireCount % N)-th candidate in
        // row-major cell order, -1 when empty. Snapshot order does not matter —
        // the row-major key makes the rotation identical across chunk layouts.
        // Duplicate cells (impossible for tile-locked defenders, defensive only)
        // tie-break on the lower snapshot index.
        public static int Select(in NativeArray<int2> cells, int fireCount, int2 gridSize)
        {
            int n = cells.Length;
            if (n <= 0) return -1;
            int k = (fireCount % n + n) % n;
            for (int i = 0; i < n; i++)
            {
                long keyI = (long)cells[i].y * gridSize.x + cells[i].x;
                int rank = 0;
                for (int j = 0; j < n; j++)
                {
                    long keyJ = (long)cells[j].y * gridSize.x + cells[j].x;
                    if (keyJ < keyI || (keyJ == keyI && j < i)) rank++;
                }
                if (rank == k) return i;
            }
            return -1; // unreachable: ranks are a permutation of 0..n-1
        }
    }
}
