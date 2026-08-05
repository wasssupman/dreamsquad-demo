using System.Collections.Generic;

namespace Wassup.Sim.Effects
{
    /// <summary>
    /// battle-sim-extraction unit 18-E/2 — 소스에서 퍼지는 4-이웃 BFS 로 `dist` + `flow` 계산.
    /// 구 `FlowFieldBuilder` 이식. 순수 함수. 오라클: `FlowFieldBuilderTests`.
    /// </summary>
    public static class FlowFieldBuilder
    {
        /// <summary>
        /// ⚠ **순서 `(+x, -x, +y, -y)` 가 flow 타이브레이크의 결정론 계약이다.**
        /// 같은 dist 를 가진 이웃이 여러 개일 때 어느 방향을 고르는지가 이 순서로 정해진다 —
        /// 재배열하면 경로가 달라지고 그건 exact parity 축이다.
        /// (구 sim 이 배열 대신 switch 를 쓴 이유는 Burst 였고, 신 sim 에선 배열도 되지만
        ///  **순서를 눈에 보이게** 두는 값이 있어 모양을 유지한다.)
        /// </summary>
        private static SimInt2 Dir(int d)
        {
            switch (d)
            {
                case 0: return new SimInt2(1, 0);
                case 1: return new SimInt2(-1, 0);
                case 2: return new SimInt2(0, 1);
                default: return new SimInt2(0, -1);
            }
        }

        /// <summary>
        /// 단일 goal = 1-소스 특수형. 유효하지 않은 goal(경계 밖/벽)은 "유효 소스 0" 규칙으로
        /// 빈 필드가 된다 — 소비자의 goal-fallback 신호와 같은 모양이다.
        /// </summary>
        public static void Build(byte[] walkMask, SimInt2 gridSize, SimInt2 goal,
                                 SimVec2[] outFlow, int[] outDist)
            => BuildFromSources(walkMask, gridSize, new[] { goal }, 1, outFlow, outDist);

        /// <summary>
        /// N-소스 BFS. 모든 **유효** 소스(경계 내 + walkable)가 `dist 0` 에서 동시에 퍼져
        /// 각 셀의 flow 가 최근접 소스를 향한다. 유효 소스 0 개면 전 셀 `int.MaxValue` +
        /// zero-flow — 그것이 소비자의 goal-fallback 신호다.
        ///
        /// `sourceCount` 는 `sources` 의 **앞쪽 유효 길이**다(구 `NativeList` 의 Length 대응 —
        /// 호출자가 여유 있게 잡은 버퍼를 재사용할 수 있게).
        /// </summary>
        public static void BuildFromSources(byte[] walkMask, SimInt2 gridSize,
                                            SimInt2[] sources, int sourceCount,
                                            SimVec2[] outFlow, int[] outDist)
        {
            int w = gridSize.x, h = gridSize.y, n = w * h;
            AssertLengths(n, walkMask.Length, outFlow.Length, outDist.Length);

            for (int i = 0; i < n; i++) outDist[i] = int.MaxValue;
            for (int i = 0; i < n; i++) outFlow[i] = SimVec2.Zero;

            var queue = new Queue<SimInt2>();
            for (int s = 0; s < sourceCount; s++)
            {
                SimInt2 src = sources[s];
                if (src.x < 0 || src.x >= w || src.y < 0 || src.y >= h) continue;
                int srcIdx = src.y * w + src.x;
                if (walkMask[srcIdx] == 0) continue;
                if (outDist[srcIdx] == 0) continue;   // 중복 소스 무해
                outDist[srcIdx] = 0;
                queue.Enqueue(src);
            }

            while (queue.Count > 0)
            {
                SimInt2 c = queue.Dequeue();
                int cIdx = c.y * w + c.x;
                int cDist = outDist[cIdx];
                for (int d = 0; d < 4; d++)
                {
                    SimInt2 nb = c + Dir(d);
                    if (nb.x < 0 || nb.x >= w || nb.y < 0 || nb.y >= h) continue;
                    int nIdx = nb.y * w + nb.x;
                    if (walkMask[nIdx] == 0) continue;
                    if (outDist[nIdx] <= cDist + 1) continue;
                    outDist[nIdx] = cDist + 1;
                    queue.Enqueue(nb);
                }
            }

            // flow 채우기: 각 셀에서 4-이웃 중 dist 최소 방향의 단위 벡터.
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int idx = y * w + x;
                if (outDist[idx] == int.MaxValue) { outFlow[idx] = SimVec2.Zero; continue; }
                if (outDist[idx] == 0) { outFlow[idx] = SimVec2.Zero; continue; }

                int bestDist = outDist[idx];
                SimInt2 bestDir = default;
                for (int d = 0; d < 4; d++)
                {
                    SimInt2 nb = new SimInt2(x, y) + Dir(d);
                    if (nb.x < 0 || nb.x >= w || nb.y < 0 || nb.y >= h) continue;
                    int nIdx = nb.y * w + nb.x;
                    if (outDist[nIdx] >= bestDist) continue;   // **strict** — 첫 최소가 이긴다
                    bestDist = outDist[nIdx];
                    bestDir = Dir(d);
                }
                outFlow[idx] = new SimVec2(bestDir.x, bestDir.y);
            }
        }

        /// 구 sim 의 `[BurstDiscard]` assert 대응. 배선 실수를 조용히 통과시키지 않는다.
        private static void AssertLengths(int n, int walkLen, int flowLen, int distLen)
        {
            if (walkLen != n || distLen != n || flowLen != n)
                throw new System.ArgumentException(
                    $"FlowFieldBuilder: 배열 길이 불일치(기대 {n}, walkMask={walkLen}, " +
                    $"outFlow={flowLen}, outDist={distLen}).");
        }

        /// <summary>
        /// 방어유닛의 "공격 가능한" walkable 셀을 BFS 소스로 수집한다 —
        /// **체비셰프 ≤ `rangeTiles` 디스크, 자기 셀 제외**(그 칸은 Place = 벽이다).
        /// FSM 사거리 판정과 같은 메트릭이라 소스 도달 = Engaging 전이가 보장된다.
        ///
        /// 중복 셀을 허용한다(<see cref="BuildFromSources"/> 가 `dist 0` 재삽입을 걸러낸다).
        /// 반환값 = 수집된 소스 수 = `outSources` 의 유효 길이.
        /// </summary>
        public static int CollectDefenderSources(byte[] walkMask, SimInt2 gridSize,
                                                 SimInt2[] defenderCells, int defenderCount,
                                                 int rangeTiles, List<SimInt2> outSources)
        {
            outSources.Clear();
            int w = gridSize.x, h = gridSize.y;
            for (int i = 0; i < defenderCount; i++)
            {
                SimInt2 c = defenderCells[i];
                for (int dy = -rangeTiles; dy <= rangeTiles; dy++)
                for (int dx = -rangeTiles; dx <= rangeTiles; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    var nb = new SimInt2(c.x + dx, c.y + dy);
                    if (nb.x < 0 || nb.x >= w || nb.y < 0 || nb.y >= h) continue;
                    if (walkMask[nb.y * w + nb.x] == 0) continue;
                    outSources.Add(nb);
                }
            }
            return outSources.Count;
        }
    }
}
