using Unity.Collections;
using Unity.Mathematics;

namespace Wassup.Battle.Movement
{
    // boss-defender-field unit 2 rev — zero-flow 셀 복구 방향 계산(순수, ecs-review M3).
    // 임펄스 등으로 flow 가 zero 인 셀에 밀려났을 때 4-이웃 중 dist 가 더 작은 최소
    // 이웃 방향을 고른다. MovementSystem 이 goal field / defender field 어느 쪽 dist
    // 로도 호출한다(사냥 분기에서 dist 배열이 바뀌는 지점이라 회귀 테스트 대상).
    public static class FlowRecovery
    {
        // 반환 zero = 더 나은 이웃 없음(고립 셀) — 호출자는 정지를 선택한다.
        public static float2 RecoveryDir(int2 cell, in NativeArray<int> dist, int2 gridSize)
        {
            int idx = GridMath.CellIndex(cell, gridSize);
            float2 dir = float2.zero;
            int best = dist[idx];
            int2 nb;
            int d;
            nb = cell + new int2( 1, 0); if (nb.x < gridSize.x) { d = dist[GridMath.CellIndex(nb, gridSize)]; if (d < best) { best = d; dir = new float2( 1, 0); } }
            nb = cell + new int2(-1, 0); if (nb.x >= 0)         { d = dist[GridMath.CellIndex(nb, gridSize)]; if (d < best) { best = d; dir = new float2(-1, 0); } }
            nb = cell + new int2( 0, 1); if (nb.y < gridSize.y) { d = dist[GridMath.CellIndex(nb, gridSize)]; if (d < best) { best = d; dir = new float2( 0, 1); } }
            nb = cell + new int2( 0,-1); if (nb.y >= 0)         { d = dist[GridMath.CellIndex(nb, gridSize)]; if (d < best) { best = d; dir = new float2( 0,-1); } }
            return dir;
        }
    }
}
