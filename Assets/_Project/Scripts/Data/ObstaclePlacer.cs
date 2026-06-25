using Unity.Collections;
using Unity.Mathematics;
using Random = Unity.Mathematics.Random;

namespace Wassup.Data
{
    public static class ObstaclePlacer
    {
        public static void Place(
            ref Random rng,
            NativeArray<MapTileType> tiles,
            int2 gridSize,
            MapThemeData theme,
            float minPlaceableRatio = -1f)
        {
            if (theme == null || theme.obstaclePrefabs == null || theme.obstaclePrefabs.Length == 0) return;

            float ratio = minPlaceableRatio >= 0f ? minPlaceableRatio : theme.minPlaceableRatio;
            // dirt coverage 를 살짝 줄인다(0.85×). 박스화 채움을 제거했으므로 최종 dirt 는
            // 대략 이 시드량 + 약간의 오목 smoothing 수준.
            DesignateDeco(ref rng, tiles, gridSize, math.clamp(ratio, 0.2f, 0.8f) * 0.85f);
        }

        // Place(=buildable) 를 솔리드 블롭으로 남기고 나머지를 Deco 로 변환. keepFraction = 남길 Place 비율 [0,1].
        // 시드 결정적. ProceduralMapGenerator(obstacle)·MapGrid(decorative deco) 양쪽이 공유한다.
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
