// salvaged from Assets/_Project/Scripts/Battle/Movement/FlowRecovery.cs (battle-core-rebuild unit 2)
// 이식 시 바뀐 것: `NativeArray<int>` → `int[]`.
using Unity.Mathematics;
using Wassup.BattleCore.Map;

namespace Wassup.BattleCore.Move
{
    // 방향이 없는 칸(흐름 0)에 밀려났을 때의 복구 방향.
    //
    // 4-이웃 중 dist 가 더 작은 최소 이웃 쪽을 고른다. 골 필드·사냥판·추격판 어느 dist 로도
    // 불린다 — 그래서 필드가 아니라 **배열**을 받는다.
    //
    // ⚠ cardinal 만 쓴다. 대각 이웃은 대각 코너 슬립(미수리)에 걸리고, 현행 이동이 cardinal
    // 인 것은 의도다.
    public static class FlowRecovery
    {
        // 반환 zero = 더 나은 이웃 없음(고립 칸) — 호출자는 정지를 선택한다.
        // 순서는 (+x, −x, +y, −y) 고정 — 동률에서 결정론을 주는 것이 이 순서다.
        public static float2 RecoveryDir(int2 cell, int[] dist, int2 gridSize)
        {
            int idx = GridMath.CellIndex(cell, gridSize);
            float2 dir = float2.zero;
            int best = dist[idx];
            int2 nb;
            int d;
            nb = cell + new int2(1, 0); if (nb.x < gridSize.x) { d = dist[GridMath.CellIndex(nb, gridSize)]; if (d < best) { best = d; dir = new float2(1, 0); } }
            nb = cell + new int2(-1, 0); if (nb.x >= 0) { d = dist[GridMath.CellIndex(nb, gridSize)]; if (d < best) { best = d; dir = new float2(-1, 0); } }
            nb = cell + new int2(0, 1); if (nb.y < gridSize.y) { d = dist[GridMath.CellIndex(nb, gridSize)]; if (d < best) { best = d; dir = new float2(0, 1); } }
            nb = cell + new int2(0, -1); if (nb.y >= 0) { d = dist[GridMath.CellIndex(nb, gridSize)]; if (d < best) { best = d; dir = new float2(0, -1); } }
            return dir;
        }
    }
}
