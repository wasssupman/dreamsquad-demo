// salvaged from Assets/_Project/Scripts/Battle/Effects/FlowFieldBuilder.cs (battle-core-rebuild unit 2)
// 이식 시 바뀐 것:
//   · `NativeArray<T>` → `T[]`, `NativeQueue` → 재사용 링 버퍼(`CellQueue`) — 틱 중 할당 0.
//   · `NativeList<int2> outSources` → `int2[] + out count`(같은 이유).
//   · `[BurstDiscard]` assert → 일반 인자 검사(코어는 Burst 를 모른다).
// 알고리즘·비용·동률 규칙은 한 줄도 바꾸지 않았다 — 여기가 바뀌면 모든 이동이 바뀐다.
using Unity.Mathematics;

namespace Wassup.BattleCore.Map
{
    // 다중 소스 다익스트라 → 방향장. 옥타일 비용 10/14, 코너컷 방지, 라벨 정정법.
    //
    // 적별 A*·NavMesh·Funnel·RVO 를 기각한 이유는 옛 전투의 `enemy-movement-algorithm.md` §6 에
    // 전수 기록돼 있다. 요점 하나: **경로 필드는 판당 한 벌을 모두가 공유한다.**
    public static class FlowFieldBuilder
    {
        // **결정론 계약**: 직교 4 를 앞에, 대각 4 를 뒤에 둔다. 동률이면 직교가 이긴다 —
        // 대각의 실제 이동 거리가 길기 때문이다.
        private const int DirCount = 8;

        private static int2 Dir(int d)
        {
            switch (d)
            {
                case 0: return new int2(1, 0);
                case 1: return new int2(-1, 0);
                case 2: return new int2(0, 1);
                case 3: return new int2(0, -1);
                case 4: return new int2(1, 1);
                case 5: return new int2(1, -1);
                case 6: return new int2(-1, 1);
                default: return new int2(-1, -1);
            }
        }

        // ×10 스케일 정수 비용. 부동소수 dist 는 결정론 위험이 커서 쓰지 않는다.
        // 직교 10 / 대각 14 (≈ 10√2). 단순 BFS 로 8-이웃을 돌리면 dist 가 체비셰프가 되어
        // 대각이 공짜가 되고, 불필요한 대각을 선호하는 왜곡이 생긴다.
        public const int CostOrtho = 10;
        public const int CostDiag = 14;

        private static int Cost(int d) => d < 4 ? CostOrtho : CostDiag;

        // 대각은 인접한 두 직교 이웃이 **둘 다** 통행 가능할 때만 허용한다.
        // 아니면 유닛이 벽 모서리를 관통한다(타일 정렬 벽이라 눈에 잘 띈다).
        private static bool DiagonalAllowed(int2 from, int2 step, byte[] walkMask, int2 gridSize)
        {
            int w = gridSize.x, h = gridSize.y;
            int2 sideA = new int2(from.x + step.x, from.y);
            int2 sideB = new int2(from.x, from.y + step.y);
            if (sideA.x < 0 || sideA.x >= w || sideA.y < 0 || sideA.y >= h) return false;
            if (sideB.x < 0 || sideB.x >= w || sideB.y < 0 || sideB.y >= h) return false;
            return walkMask[sideA.y * w + sideA.x] != 0 && walkMask[sideB.y * w + sideB.x] != 0;
        }

        /// <summary>소스 한 칸. 유효하지 않은 소스(경계 밖/벽)는 「유효 소스 0」과 같은 빈 필드가 된다.</summary>
        public static void Build(byte[] walkMask, int2 gridSize, int2 goal,
                                 float2[] outFlow, int[] outDist, CellQueue queue)
        {
            var one = new int2[1];
            one[0] = goal;
            BuildFromSources(walkMask, gridSize, one, 1, outFlow, outDist, queue);
        }

        // N-소스 다익스트라. 모든 유효 소스가 dist 0 에서 동시에 퍼진다 → 각 칸의 flow 는
        // 최근접 소스를 향한다. 유효 소스 0 개면 전 칸 `int.MaxValue` / zero-flow 다
        // (소비자에게는 그것이 「이 슬롯으로는 못 간다」 신호다).
        public static void BuildFromSources(byte[] walkMask, int2 gridSize,
                                            int2[] sources, int sourceCount,
                                            float2[] outFlow, int[] outDist, CellQueue queue)
        {
            int w = gridSize.x, h = gridSize.y, n = w * h;
            if (walkMask.Length != n || outFlow.Length != n || outDist.Length != n)
                throw new System.ArgumentException(
                    $"FlowFieldBuilder: 배열 길이 불일치 (기대 {n}, walkMask={walkMask.Length}, flow={outFlow.Length}, dist={outDist.Length})");

            for (int i = 0; i < n; i++) outDist[i] = int.MaxValue;
            for (int i = 0; i < n; i++) outFlow[i] = float2.zero;

            // 가중 다익스트라. 비용이 {10, 14} 두 종뿐이라 우선순위 큐 없이 **재삽입 허용
            // 큐**로 충분하다(라벨 정정법). 처리 순서와 무관하게 결과가 같아 결정론이 유지된다.
            queue.Clear();
            for (int s = 0; s < sourceCount; s++)
            {
                int2 src = sources[s];
                if (src.x < 0 || src.x >= w || src.y < 0 || src.y >= h) continue;
                int srcIdx = src.y * w + src.x;
                if (walkMask[srcIdx] == 0) continue;
                if (outDist[srcIdx] == 0) continue;   // 중복 소스 무해
                outDist[srcIdx] = 0;
                queue.Enqueue(srcIdx);
            }

            while (queue.TryDequeue(out int cIdx))
            {
                var c = new int2(cIdx % w, cIdx / w);
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
                    queue.Enqueue(nIdx);
                }
            }

            // 각 칸에서 8-이웃 중 「그쪽으로 가면 총비용이 가장 줄어드는」 방향.
            // 비용을 빼야 대각의 긴 거리가 반영된다 — dist 만 비교하면 대각이 과하게 선택된다.
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int idx = y * w + x;
                if (outDist[idx] == int.MaxValue) { outFlow[idx] = float2.zero; continue; }
                if (outDist[idx] == 0) { outFlow[idx] = float2.zero; continue; }

                var cell = new int2(x, y);
                // argmin(outDist[n] + Cost(d)). `outDist[idx]` 로 초기화하면 등호라 아무것도
                // 선택되지 않아 전 칸이 zero-flow 가 된다 — MaxValue 로 시작해야 한다.
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
                if (bestScore > outDist[idx]) bestDir = int2.zero;
                outFlow[idx] = math.normalizesafe(new float2(bestDir.x, bestDir.y));
            }
        }

        // 「그 유닛을 공격할 수 있는」 통행 가능 칸을 BFS 소스로 모은다:
        // 체비셰프 ≤ `rangeTiles` 디스크, 자기 칸 제외.
        //
        // ⚠ **「소스 도달 = 발사 가능」은 참이 아니다.** 소스는 칸 체비셰프인데 발사 판정은
        // 월드 원(`AttackReach`)이다. 원이 정사각형의 모서리를 잘라낸 만큼 「도착했는데
        // 사거리 밖」인 칸이 남고, 그 칸은 dist 0 이라 기울기가 없어 **영구 동결**이다.
        // 그 구간은 이동 쪽 접근 보정(`CloseIn`)이 닫는다 — M4·M5.
        //
        // ⚠ **여기서 소스를 원으로 좁히지 말 것.** 사거리 1 이면 칸 전체가 원 안인 소스가
        // 하나도 없어 어그로가 통째로 거부된다.
        //
        // 중복 칸 허용(`BuildFromSources` 가 dist 0 재삽입을 걸러낸다). 반환 = 수집된 소스 수.
        public static int CollectDefenderSources(byte[] walkMask, int2 gridSize,
                                                 int2[] defenderCells, int defenderCount,
                                                 int rangeTiles, int2[] outSources)
        {
            int count = 0;
            int w = gridSize.x, h = gridSize.y;
            for (int i = 0; i < defenderCount; i++)
            {
                int2 c = defenderCells[i];
                for (int dy = -rangeTiles; dy <= rangeTiles; dy++)
                for (int dx = -rangeTiles; dx <= rangeTiles; dx++)
                {
                    if (dx == 0 && dy == 0) continue;   // 자기 칸 — 통상 벽(배치지면)
                    int2 n2 = new int2(c.x + dx, c.y + dy);
                    if (n2.x < 0 || n2.x >= w || n2.y < 0 || n2.y >= h) continue;
                    if (walkMask[n2.y * w + n2.x] == 0) continue;
                    if (count >= outSources.Length) return count;   // 버퍼 상한 — 호출자가 크기를 준다
                    outSources[count++] = n2;
                }
            }
            return count;
        }

        /// <summary>디스크 하나가 낼 수 있는 최대 소스 수. 버퍼 크기 계산의 단일 정의.</summary>
        public static int DiscArea(int rangeTiles)
        {
            int side = 2 * rangeTiles + 1;
            return side * side;
        }
    }

    // 라벨 정정 다익스트라의 재삽입 허용 큐. **판당 한 개를 재사용한다** — 틱 중 할당 0.
    //
    // 링 버퍼인 이유: 재삽입이 있어 총 enqueue 수가 칸 수를 넘을 수 있고, 그때 배열을
    // 앞에서부터 덮어쓰면 아직 안 꺼낸 항목이 사라진다. 가득 차면 두 배로 키운다.
    public sealed class CellQueue
    {
        private int[] _buffer;
        private int _head;
        private int _count;

        public CellQueue(int capacity = 256)
        {
            _buffer = new int[capacity < 4 ? 4 : capacity];
        }

        public int Count => _count;

        public void Clear()
        {
            _head = 0;
            _count = 0;
        }

        public void Enqueue(int value)
        {
            if (_count == _buffer.Length) Grow();
            _buffer[(_head + _count) % _buffer.Length] = value;
            _count++;
        }

        public bool TryDequeue(out int value)
        {
            if (_count == 0) { value = 0; return false; }
            value = _buffer[_head];
            _head = (_head + 1) % _buffer.Length;
            _count--;
            return true;
        }

        private void Grow()
        {
            var next = new int[_buffer.Length * 2];
            for (int i = 0; i < _count; i++) next[i] = _buffer[(_head + i) % _buffer.Length];
            _buffer = next;
            _head = 0;
        }
    }
}
