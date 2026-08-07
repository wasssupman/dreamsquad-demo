using Unity.Collections;
using Unity.Mathematics;
using Random = Unity.Mathematics.Random;

namespace Wassup.Data
{
    // map-pipeline-cleanup unit 3 — 절차 생성 전용 Place() 는 생성기와 함께 제거.
    // DesignateDeco 는 authored-Deco 를 칠하지 않은 문서맵의 배치칸 커빙에 살아있다.
    public static class ObstaclePlacer
    {
        // placement-mask unit 1 — "마스크가 파생값(tiles==Place)과 상이한 셀이 있나" = 마스크 브러시로
        // 저작된 수동 배치판인가. 참이면 BattleBridge 가 시드 커빙을 skip 한다(authored-Deco 규칙과 동형).
        // bool 비교라 마스크 비정규값(≠0/1)에도 안전. 마스크 미생성이면 저작 의도 없음.
        public static bool HasAuthoredMaskIntent(NativeArray<MapTileType> tiles, NativeArray<byte> placeMask)
        {
            if (!placeMask.IsCreated) return false;
            for (int i = 0; i < tiles.Length; i++)
                if ((placeMask[i] != 0) != (tiles[i] == MapTileType.Place)) return true;
            return false;
        }

        // Place(=buildable) 를 솔리드 블롭으로 남기고 나머지를 Deco 로 변환. keepFraction = 남길 Place 비율 [0,1].
        // 시드 결정적. BattleBridge 의 맵 빌드(데코 미지정 문서맵)가 소비한다.
        public static void DesignateDeco(
            ref Random rng,
            NativeArray<MapTileType> tiles,
            int2 gridSize,
            float keepFraction)
        {
            int n = gridSize.x * gridSize.y;
            int placeCount = 0;
            for (int i = 0; i < n; i++)
                if (tiles[i] == MapTileType.Place) placeCount++;

            int keepTarget = (int)math.ceil(placeCount * keepFraction);
            if (keepTarget >= placeCount) return;

            // Place(=dirt) 를 클러스터(덩어리)로 남기고 나머지는 Deco(=grass) 로 변환한다.
            // 예전엔 무작위 셀을 Deco 로 바꿔 Place 가 swiss-cheese 로 파편화 → dirt 외곽이
            // 뾰족/노치투성이였다. 시드에서 BFS 로 블롭을 키워 dirt 를 솔리드 덩어리로 만든다.
            var keep = new NativeArray<bool>(n, Allocator.Temp);
            var queue = new NativeList<int>(Allocator.Temp);
            try
            {
                int kept = 0;
                int guard = 0;
                while (kept < keepTarget && guard++ < n * 4)
                {
                    int seed = FindUnkeptPlace(ref rng, tiles, keep, gridSize, n);
                    if (seed < 0) break;

                    // 작고 다양한 패치(2~6칸). 시드 간격(아래)과 함께 잘게 흩어진 자연스러운 분포.
                    int blobTarget = math.min(rng.NextInt(2, 7), keepTarget - kept);
                    queue.Clear();
                    queue.Add(seed);
                    keep[seed] = true; kept++;
                    int blob = 1;
                    int head = 0;
                    while (head < queue.Length && blob < blobTarget && kept < keepTarget)
                    {
                        int cur = queue[head++];
                        int cx = cur % gridSize.x;
                        int cy = cur / gridSize.x;
                        TryKeep(tiles, keep, queue, gridSize, cx + 1, cy, blobTarget, keepTarget, ref kept, ref blob);
                        TryKeep(tiles, keep, queue, gridSize, cx - 1, cy, blobTarget, keepTarget, ref kept, ref blob);
                        TryKeep(tiles, keep, queue, gridSize, cx, cy + 1, blobTarget, keepTarget, ref kept, ref blob);
                        TryKeep(tiles, keep, queue, gridSize, cx, cy - 1, blobTarget, keepTarget, ref kept, ref blob);
                    }
                }

                for (int i = 0; i < n; i++)
                    if (tiles[i] == MapTileType.Place && !keep[i])
                        tiles[i] = MapTileType.Deco;

                // 병합/볼록화 smoothing 은 하지 않는다 — 작은 패치들을 그대로 두어 잘게 흩어진
                // 자연스러운 분포를 유지한다. 오목/대각선은 부드러운 inner/cross 타일이 곡선 렌더.
            }
            finally
            {
                if (keep.IsCreated) keep.Dispose();
                if (queue.IsCreated) queue.Dispose();
            }
        }

        private static int FindUnkeptPlace(ref Random rng, NativeArray<MapTileType> tiles, NativeArray<bool> keep, int2 gridSize, int n)
        {
            // 1차: 기존 dirt 와 한 칸 이상 떨어진 시드 (패치끼리 붙지 않게 → 흩어진 분포)
            for (int t = 0; t < 96; t++)
            {
                int c = rng.NextInt(0, n);
                if (tiles[c] == MapTileType.Place && !keep[c] && !HasKeptNeighbor8(keep, gridSize, c)) return c;
            }
            // 2차: 간격 둔 시드가 없으면 아무 unkept Place
            for (int i = 0; i < n; i++)
                if (tiles[i] == MapTileType.Place && !keep[i]) return i;
            return -1;
        }

        private static bool HasKeptNeighbor8(NativeArray<bool> keep, int2 gridSize, int idx)
        {
            int x = idx % gridSize.x, y = idx / gridSize.x;
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0) continue;
                int nx = x + dx, ny = y + dy;
                if (nx < 0 || nx >= gridSize.x || ny < 0 || ny >= gridSize.y) continue;
                if (keep[ny * gridSize.x + nx]) return true;
            }
            return false;
        }

        private static void TryKeep(
            NativeArray<MapTileType> tiles, NativeArray<bool> keep, NativeList<int> queue,
            int2 gridSize, int x, int y, int blobTarget, int keepTarget, ref int kept, ref int blob)
        {
            if (kept >= keepTarget || blob >= blobTarget) return;
            if (x < 0 || x >= gridSize.x || y < 0 || y >= gridSize.y) return;
            int idx = y * gridSize.x + x;
            if (tiles[idx] != MapTileType.Place || keep[idx]) return;
            keep[idx] = true; kept++; blob++;
            queue.Add(idx);
        }
    }
}
