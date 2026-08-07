using Unity.Collections;
using Unity.Mathematics;

namespace Wassup.Battle.Effects
{
    // Phase 9 — goal 에서 시작하는 4-neighbor BFS 로 dist + flow 계산.
    // 순수 함수. EditMode 테스트로 결정론 검증.
    public static class FlowFieldBuilder
    {
        // boss-defender-field unit 0 — 기존 static readonly int2[] 를 switch 로 교체.
        // Burst ISystem(DefenderFieldSystem)에서 호출 가능해야 하는데 managed 배열
        // static 접근은 Burst 미보장. 순서(+x, -x, +y, -y)는 flow 타이브레이크 결정론 계약.
        private static int2 Dir(int d)
        {
            switch (d)
            {
                case 0:  return new int2(1, 0);
                case 1:  return new int2(-1, 0);
                case 2:  return new int2(0, 1);
                default: return new int2(0, -1);
            }
        }

        public static void Build(
            NativeArray<byte>   walkMask, // 1 = walkable, 0 = blocked
            int2                gridSize,
            int2                goal,
            NativeArray<float2> outFlow,
            NativeArray<int>    outDist)
        {
            // boss-defender-field unit 0 — 단일 goal 은 1-소스 특수형. 유효하지 않은
            // goal(경계 밖/벽)은 "유효 소스 0" 규칙으로 동일하게 빈 필드가 된다.
            var sources = new NativeArray<int2>(1, Allocator.Temp);
            try
            {
                sources[0] = goal;
                BuildFromSources(walkMask, gridSize, sources, outFlow, outDist);
            }
            finally { sources.Dispose(); }
        }

        // boss-defender-field unit 0 — N-소스 BFS. 모든 유효 소스(경계 내 + walkable)가
        // dist 0 에서 동시에 퍼진다 → 각 cell 의 flow 는 최근접 소스를 향한다.
        // 유효 소스 0 개면 전 셀 int.MaxValue / zero-flow (소비자의 goal-fallback 신호).
        public static void BuildFromSources(
            NativeArray<byte>   walkMask,
            int2                gridSize,
            NativeArray<int2>   sources,
            NativeArray<float2> outFlow,
            NativeArray<int>    outDist)
        {
            int w = gridSize.x, h = gridSize.y, n = w * h;

            AssertLengths(n, walkMask.Length, outFlow.Length, outDist.Length);

            for (int i = 0; i < n; i++) outDist[i] = int.MaxValue;
            for (int i = 0; i < n; i++) outFlow[i] = float2.zero;

            var queue = new NativeQueue<int2>(Allocator.Temp);
            try
            {
                for (int s = 0; s < sources.Length; s++)
                {
                    int2 src = sources[s];
                    if (src.x < 0 || src.x >= w || src.y < 0 || src.y >= h) continue;
                    int srcIdx = src.y * w + src.x;
                    if (walkMask[srcIdx] == 0) continue;
                    if (outDist[srcIdx] == 0) continue; // 중복 소스 무해
                    outDist[srcIdx] = 0;
                    queue.Enqueue(src);
                }

                while (queue.TryDequeue(out var c))
                {
                    int cIdx = c.y * w + c.x;
                    int cDist = outDist[cIdx];
                    for (int d = 0; d < 4; d++)
                    {
                        int2 n2 = c + Dir(d);
                        if (n2.x < 0 || n2.x >= w || n2.y < 0 || n2.y >= h) continue;
                        int nIdx = n2.y * w + n2.x;
                        if (walkMask[nIdx] == 0) continue;
                        if (outDist[nIdx] <= cDist + 1) continue;
                        outDist[nIdx] = cDist + 1;
                        queue.Enqueue(n2);
                    }
                }
            }
            finally { queue.Dispose(); }

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
                    int2 n2 = new int2(x, y) + Dir(d);
                    if (n2.x < 0 || n2.x >= w || n2.y < 0 || n2.y >= h) continue;
                    int nIdx = n2.y * w + n2.x;
                    if (outDist[nIdx] >= bestDist) continue;
                    bestDist = outDist[nIdx];
                    bestDir = Dir(d);
                }
                outFlow[idx] = new float2(bestDir.x, bestDir.y);
            }
        }

        // boss-defender-field unit 1 — Burst 호출 경로에서 managed string 포맷 제거.
        // 관리 코드 호출자에선 기존 assert 그대로, Burst 컴파일 시 호출 자체가 사라진다.
        [Unity.Burst.BurstDiscard]
        private static void AssertLengths(int n, int walkLen, int flowLen, int distLen)
        {
            UnityEngine.Debug.Assert(walkLen == n && distLen == n && flowLen == n,
                $"FlowFieldBuilder: array length mismatch (expected {n}, got walkMask={walkLen}, outFlow={flowLen}, outDist={distLen})");
        }

        // boss-defender-field unit 0/5 — 방어유닛을 "공격 가능한" walkable 셀을 BFS 소스로
        // 수집한다: Chebyshev 거리 ≤ rangeTiles(보스 공격 사거리) 디스크, 자기 셀 제외.
        // FSM 사거리 판정(HasFireTarget)과 같은 메트릭이라 소스 도달 = Engaging 전이 보장.
        // (unit 5 rev — 초기 4-이웃 규칙은 레인 비인접 배치를 전부 놓쳐 goal 마칭 결함.)
        // 중복 셀 허용(BuildFromSources 가 dist 0 재삽입을 걸러냄). 반환값 = 수집된 소스 수.
        public static int CollectDefenderSources(
            NativeArray<byte> walkMask,
            int2              gridSize,
            NativeArray<int2> defenderCells,
            int               rangeTiles,
            NativeList<int2>  outSources)
        {
            outSources.Clear();
            int w = gridSize.x, h = gridSize.y;
            for (int i = 0; i < defenderCells.Length; i++)
            {
                int2 c = defenderCells[i];
                for (int dy = -rangeTiles; dy <= rangeTiles; dy++)
                for (int dx = -rangeTiles; dx <= rangeTiles; dx++)
                {
                    if (dx == 0 && dy == 0) continue; // 방어유닛 자신의 셀 — 통상 벽(비-walkable). placement-mask B-1 로 Walk 셀 위 배치여도 인접 소스만으로 필드가 선다.
                    int2 n2 = new int2(c.x + dx, c.y + dy);
                    if (n2.x < 0 || n2.x >= w || n2.y < 0 || n2.y >= h) continue;
                    if (walkMask[n2.y * w + n2.x] == 0) continue;
                    outSources.Add(n2);
                }
            }
            return outSources.Length;
        }
    }
}
