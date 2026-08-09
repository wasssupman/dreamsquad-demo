using Unity.Collections;
using Unity.Mathematics;

namespace Wassup.Data
{
    public static class MapConnectivity
    {
        private static readonly int2[] Dirs =
        {
            new int2(1, 0), new int2(-1, 0), new int2(0, 1), new int2(0, -1),
        };

        public static bool AllSpawnsReachGoal(GeneratedMap map)
        {
            if (!map.IsCreated) return false;
            if (map.gridSize.x <= 0 || map.gridSize.y <= 0) return false;
            // battle-structures unit 3 — 하한을 2 → 1 로 내렸다. 공성 맵은 적 마음 1개가
            // 스폰 지점이므로 스폰이 1개다(모드 파생, unit 6) — 하한 2 면 통과할 수 없었다.
            // 침략 맵은 실측상 전부 2개 이상이라 영향 없다.
            if (map.spawns.Length < 1) return false;

            int n = map.gridSize.x * map.gridSize.y;
            if (map.tiles.Length != n) return false;

            // multi-goal-map — goals 전체를 BFS 시드(각 spawn 이 아무 골이든 도달하면 통과 →
            // 분리 복도 각자 골 지원). goals 미설정 생산자(레거시/픽스처)는 primary [goal] 폴백.
            bool hasGoals = map.goals.IsCreated && map.goals.Length > 0;
            int goalCount = hasGoals ? map.goals.Length : 1;

            var reachable = new NativeArray<byte>(n, Allocator.Temp);
            var queue = new NativeQueue<int2>(Allocator.Temp);
            try
            {
                for (int g = 0; g < goalCount; g++)
                {
                    int2 goal = hasGoals ? map.goals[g] : map.goal;
                    if (!InBounds(goal, map.gridSize)) return false;
                    if (map.TileAt(goal) != MapTileType.Walk) return false;
                    int gi = map.CellIndex(goal);
                    if (reachable[gi] == 0)
                    {
                        reachable[gi] = 1;
                        queue.Enqueue(goal);
                    }
                }

                while (queue.TryDequeue(out var cell))
                {
                    for (int i = 0; i < Dirs.Length; i++)
                    {
                        int2 next = cell + Dirs[i];
                        if (!InBounds(next, map.gridSize)) continue;
                        int idx = map.CellIndex(next);
                        if (reachable[idx] != 0) continue;
                        if (map.tiles[idx] != MapTileType.Walk) continue;
                        reachable[idx] = 1;
                        queue.Enqueue(next);
                    }
                }

                for (int i = 0; i < map.spawns.Length; i++)
                {
                    int2 spawn = map.spawns[i];
                    if (!InBounds(spawn, map.gridSize)) return false;
                    if (map.TileAt(spawn) != MapTileType.Walk) return false;
                    if (reachable[map.CellIndex(spawn)] == 0) return false;
                }

                return true;
            }
            finally
            {
                if (queue.IsCreated) queue.Dispose();
                if (reachable.IsCreated) reachable.Dispose();
            }
        }

        public static bool InBounds(int2 cell, int2 gridSize)
        {
            return cell.x >= 0 && cell.x < gridSize.x && cell.y >= 0 && cell.y < gridSize.y;
        }
    }
}
