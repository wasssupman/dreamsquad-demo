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
        // static 접근은 Burst 미보장.
        //
        // continuous-agent-movement unit 4 — 4방향 → 8방향.
        // **결정론 계약(구 "+x, -x, +y, -y" 를 대체)**: 직교 4 를 앞에, 대각 4 를 뒤에 둔다.
        // 동률이면 직교가 이긴다 — 대각의 실제 이동 거리가 길기 때문이다.
        private const int DirCount = 8;

        private static int2 Dir(int d)
        {
            switch (d)
            {
                case 0:  return new int2( 1,  0);
                case 1:  return new int2(-1,  0);
                case 2:  return new int2( 0,  1);
                case 3:  return new int2( 0, -1);
                case 4:  return new int2( 1,  1);
                case 5:  return new int2( 1, -1);
                case 6:  return new int2(-1,  1);
                default: return new int2(-1, -1);
            }
        }

        // ×10 스케일 정수 비용. 부동소수 dist 는 결정론 위험이 커서 쓰지 않는다.
        // 직교 10 / 대각 14 (≈ 10√2 = 14.14). 단순 BFS 로 8-이웃을 돌리면 dist 가 체비셰프가
        // 되어 대각이 공짜가 되고, 불필요한 대각을 선호하는 반대 방향 왜곡이 생긴다.
        public const int CostOrtho = 10;
        public const int CostDiag  = 14;

        private static int Cost(int d) => d < 4 ? CostOrtho : CostDiag;

        // 대각은 인접한 두 직교 이웃이 **둘 다** 통행 가능할 때만 허용한다.
        // 아니면 유닛이 벽 모서리를 관통한다(타일 정렬 벽이라 눈에 잘 띈다).
        private static bool DiagonalAllowed(int2 from, int2 step, NativeArray<byte> walkMask, int2 gridSize)
        {
            int w = gridSize.x, h = gridSize.y;
            int2 sideA = new int2(from.x + step.x, from.y);
            int2 sideB = new int2(from.x, from.y + step.y);
            if (sideA.x < 0 || sideA.x >= w || sideA.y < 0 || sideA.y >= h) return false;
            if (sideB.x < 0 || sideB.x >= w || sideB.y < 0 || sideB.y >= h) return false;
            return walkMask[sideA.y * w + sideA.x] != 0 && walkMask[sideB.y * w + sideB.x] != 0;
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

            // continuous-agent-movement unit 4 — 가중 다익스트라. 비용이 {10, 14} 두 종뿐이라
            // 우선순위 큐 없이 **재삽입 허용 큐**로 충분하다(라벨 정정법). 맵이 180셀 규모라
            // 재삽입 비용이 무시되고, 처리 순서와 무관하게 결과가 같아 결정론이 유지된다.
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
                    for (int d = 0; d < DirCount; d++)
                    {
                        int2 step = Dir(d);
                        int2 n2 = c + step;
                        if (n2.x < 0 || n2.x >= w || n2.y < 0 || n2.y >= h) continue;
                        int nIdx = n2.y * w + n2.x;
                        if (walkMask[nIdx] == 0) continue;
                        if (d >= 4 && !DiagonalAllowed(c, step, walkMask, gridSize)) continue;
                        int nd = cDist + Cost(d);
                        if (outDist[nIdx] <= nd) continue;
                        outDist[nIdx] = nd;
                        queue.Enqueue(n2);
                    }
                }
            }
            finally { queue.Dispose(); }

            // Fill flow: 각 cell 에서 8-neighbor 중 "그쪽으로 가면 총비용이 가장 줄어드는" 방향.
            // 비용을 빼야 대각의 긴 거리가 반영된다 — dist 만 비교하면 대각이 과하게 선택된다.
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int idx = y * w + x;
                if (outDist[idx] == int.MaxValue) { outFlow[idx] = float2.zero; continue; }
                if (outDist[idx] == 0)             { outFlow[idx] = float2.zero; continue; }

                var cell = new int2(x, y);
                // argmin(outDist[n] + Cost(d)). 다익스트라 최적성에 의해 최소값은 outDist[idx]
                // 와 같고, 그 이웃이 곧 최적 선행자다. `outDist[idx]` 로 초기화하면 등호라
                // 아무것도 선택되지 않아 전 셀이 zero-flow 가 된다 — MaxValue 로 시작해야 한다.
                int bestScore = int.MaxValue;
                int2 bestDir = int2.zero;
                for (int d = 0; d < DirCount; d++)
                {
                    int2 step = Dir(d);
                    int2 n2 = cell + step;
                    if (n2.x < 0 || n2.x >= w || n2.y < 0 || n2.y >= h) continue;
                    int nIdx = n2.y * w + n2.x;
                    if (outDist[nIdx] == int.MaxValue) continue;
                    if (d >= 4 && !DiagonalAllowed(cell, step, walkMask, gridSize)) continue;
                    int score = outDist[nIdx] + Cost(d);
                    if (score >= bestScore) continue;   // 동률이면 앞선 방향(직교 우선) 유지
                    bestScore = score;
                    bestDir = step;
                }
                // 자기보다 나은 이웃이 없으면(소스 인접 예외 상황) 정지 신호를 유지한다.
                if (bestScore > outDist[idx]) bestDir = int2.zero;
                // 대각은 정규화해 저장한다 — "필드는 단위 벡터" 라는 기존 계약 유지.
                outFlow[idx] = math.normalizesafe(new float2(bestDir.x, bestDir.y));
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
        // ⚠ **「소스 도달 = Engaging 전이 보장」은 stale 이다**(distance-based-range unit 4a):
        // FSM 사거리 판정은 월드 원으로 바뀌었고 여기는 셀 Chebyshev 로 남았다(결정 4).
        // 원이 정사각형 모서리를 잘라낸 만큼 「도착했는데 사거리 밖」인 칸이 남고, 그 칸은
        // dist 0 이라 기울기가 없어 자기 이동도 0 = **영구 동결**이다.
        //
        // ⚠⚠ **이 함수의 소비자는 둘이고, 한쪽만 닫혀 있다.**
        //   · 어그로 추격(`AggroChaseMath.BuildChaseField`) — **닫힘**(unit 4c,
        //     `MovementSystem.arrivedAtFiringCell`).
        //   · 보스/사냥꾼(`DefenderFieldSystem`) — **열려 있다.** 보정이 `ai == Chasing` +
        //     `Aggroed` 보유를 요구하는데 보스는 `AggroStateSystem:168` 로 **어그로 면역**이라
        //     둘 다 영원히 거짓이다. 사냥 분기(`MovementSystem` 의 `recovDir` zero → `continue`)
        //     는 종전대로 그 자리에 선다.
        // 사냥 레인을 닫으려면 Movement 가 「어느 방어유닛 쪽인가」를 알아야 하는데
        // 사냥에는 `Aggroed` 같은 링크가 없다 — 새 배관이 필요하다. spec 후속 후보.
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
