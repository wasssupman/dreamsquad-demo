using Unity.Collections;
using Unity.Mathematics;

namespace Wassup.Battle.Effects
{
    // Phase 9 — goal 에서 시작하는 4-neighbor BFS 로 dist + flow 계산.
    // 순수 함수. EditMode 테스트로 결정론 검증.
    public static class FlowFieldBuilder
    {
        private static readonly int2[] Dirs = {
            new int2(1, 0), new int2(-1, 0), new int2(0, 1), new int2(0, -1),
        };

        public static void Build(
            NativeArray<byte>   walkMask, // 1 = walkable, 0 = blocked
            int2                gridSize,
            int2                goal,
            NativeArray<float2> outFlow,
            NativeArray<int>    outDist)
        {
            int w = gridSize.x, h = gridSize.y, n = w * h;

            for (int i = 0; i < n; i++) outDist[i] = int.MaxValue;
            for (int i = 0; i < n; i++) outFlow[i] = float2.zero;

            if (goal.x < 0 || goal.x >= w || goal.y < 0 || goal.y >= h) return;
            int goalIdx = goal.y * w + goal.x;
            if (walkMask[goalIdx] == 0) return;

            outDist[goalIdx] = 0;

            var queue = new NativeQueue<int2>(Allocator.Temp);
            queue.Enqueue(goal);

            while (queue.TryDequeue(out var c))
            {
                int cIdx = c.y * w + c.x;
                int cDist = outDist[cIdx];
                for (int d = 0; d < 4; d++)
                {
                    int2 n2 = c + Dirs[d];
                    if (n2.x < 0 || n2.x >= w || n2.y < 0 || n2.y >= h) continue;
                    int nIdx = n2.y * w + n2.x;
                    if (walkMask[nIdx] == 0) continue;
                    if (outDist[nIdx] <= cDist + 1) continue;
                    outDist[nIdx] = cDist + 1;
                    queue.Enqueue(n2);
                }
            }
            queue.Dispose();

            // Fill flow: 각 cell 에서 4-neighbor 중 dist 최소 방향 unit vector.
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int idx = y * w + x;
                if (outDist[idx] == int.MaxValue) { outFlow[idx] = float2.zero; continue; }
                if (outDist[idx] == 0)             { outFlow[idx] = float2.zero; continue; }

                int bestDist = outDist[idx];
                int2 bestDir = int2.zero;
                for (int d = 0; d < 4; d++)
                {
                    int2 n2 = new int2(x, y) + Dirs[d];
                    if (n2.x < 0 || n2.x >= w || n2.y < 0 || n2.y >= h) continue;
                    int nIdx = n2.y * w + n2.x;
                    if (outDist[nIdx] >= bestDist) continue;
                    bestDist = outDist[nIdx];
                    bestDir = Dirs[d];
                }
                outFlow[idx] = new float2(bestDir.x, bestDir.y);
            }
        }
    }
}
