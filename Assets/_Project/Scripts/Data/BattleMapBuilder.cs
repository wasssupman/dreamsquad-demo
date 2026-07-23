using Unity.Collections;
using Unity.Mathematics;

namespace Wassup.Data
{
    // map-pipeline-cleanup unit 3 — legacy 변환기(BuildFromFixture/BuildFromManual) 제거.
    // 남은 역할은 connectivity 실패 시의 라이브 안전망 하나뿐이다.
    public static class BattleMapBuilder
    {
        public static GeneratedMap BuildFallbackLinear(int2 gridSize, int seed = 0, int generatorVersion = 0, int spawnLaneCount = 2)
        {
            int w = math.max(4, gridSize.x);
            int h = math.max(4, gridSize.y);
            int n = w * h;
            spawnLaneCount = math.clamp(spawnLaneCount, 2, math.max(2, (h + 1) / 2));
            int goalY = h / 2;

            var tiles = new NativeArray<MapTileType>(n, Allocator.Persistent);
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                tiles[y * w + x] = MapTileType.Place;
            }

            var laneYs = new NativeArray<int>(spawnLaneCount, Allocator.Temp);
            int minY = h - 1;
            int maxY = 0;
            int span = math.max(1, h - 2);
            for (int i = 0; i < spawnLaneCount; i++)
            {
                int y = 1 + (int)math.round(i * (span / (float)(spawnLaneCount - 1)));
                y = math.clamp(y, 0, h - 1);
                if (i > 0) y = math.max(y, laneYs[i - 1] + 2);
                y = math.min(y, h - 1);
                laneYs[i] = y;
                minY = math.min(minY, y);
                maxY = math.max(maxY, y);
                for (int x = 0; x < w; x++)
                    tiles[y * w + x] = MapTileType.Walk;
            }
            for (int y = minY; y <= maxY; y++)
                tiles[y * w + (w - 1)] = MapTileType.Walk;

            var spawns = new NativeArray<int2>(spawnLaneCount, Allocator.Persistent);
            for (int i = 0; i < spawnLaneCount; i++)
                spawns[i] = new int2(0, laneYs[i]);
            laneYs.Dispose();

            // multi-goal-map 유닛 0: 라이브 안전망이라 goals 를 명시 세팅([goal]).
            var goals = new NativeArray<int2>(1, Allocator.Persistent);
            goals[0] = new int2(w - 1, goalY);

            return new GeneratedMap
            {
                tiles = tiles,
                gridSize = new int2(w, h),
                spawns = spawns,
                goal = new int2(w - 1, goalY),
                goals = goals,
                seed = seed,
                generatorVersion = generatorVersion,
            };
        }
    }
}
