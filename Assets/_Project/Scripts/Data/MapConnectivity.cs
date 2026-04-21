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
            if (!InBounds(map.goal, map.gridSize)) return false;
            if (map.spawns.Length < 2) return false;

            int n = map.gridSize.x * map.gridSize.y;
            if (map.tiles.Length != n) return false;
            if (map.TileAt(map.goal) != MapTileType.Walk) return false;

            var reachable = new NativeArray<byte>(n, Allocator.Temp);
            var queue = new NativeQueue<int2>(Allocator.Temp);
            try
            {
                int goalIndex = map.CellIndex(map.goal);
                reachable[goalIndex] = 1;
                queue.Enqueue(map.goal);

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
