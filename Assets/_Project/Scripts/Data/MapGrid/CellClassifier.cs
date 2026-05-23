using Unity.Collections;
using Unity.Mathematics;

namespace Wassup.Data.MapGrid
{
    public static class CellClassifier
    {
        public static GeneratedMap Bake(
            int seed,
            int2 gridSize,
            int generatorVersion,
            in PathBuildResult build,
            int2 goal,
            NativeArray<int2> spawns,
            Allocator allocator)
        {
            int n = gridSize.x * gridSize.y;
            var tiles       = new NativeArray<MapTileType>(n, allocator);
            var mergeDegree = new NativeArray<byte>(n, allocator);
            var chokepoint  = new NativeArray<byte>(n, allocator);
            var propLayerId = new NativeArray<byte>(n, allocator);

            for (int i = 0; i < n; i++) tiles[i] = MapTileType.Place;

            var pathEnum = build.pathCells.GetEnumerator();
            while (pathEnum.MoveNext())
            {
                int idx = pathEnum.Current;
                tiles[idx] = MapTileType.Walk;

                int2 c = MapGridIndex.IndexToCell(idx, gridSize);
                byte deg = CountPathNeighbors(c, gridSize, build.pathCells);
                mergeDegree[idx] = deg;
                chokepoint[idx]  = (byte)(deg >= 3 ? 1 : 0);
            }

            var outSpawns = new NativeArray<int2>(spawns.Length, allocator);
            for (int i = 0; i < spawns.Length; i++) outSpawns[i] = spawns[i];

            return new GeneratedMap
            {
                tiles = tiles,
                mergeDegree = mergeDegree,
                chokepoint = chokepoint,
                propLayerId = propLayerId,
                gridSize = gridSize,
                spawns = outSpawns,
                goal = goal,
                seed = seed,
                generatorVersion = generatorVersion,
            };
        }

        private static byte CountPathNeighbors(int2 c, int2 gridSize, NativeHashSet<int> path)
        {
            byte deg = 0;
            if (c.x + 1 < gridSize.x && path.Contains(MapGridIndex.CellIndex(new int2(c.x + 1, c.y), gridSize))) deg++;
            if (c.x - 1 >= 0          && path.Contains(MapGridIndex.CellIndex(new int2(c.x - 1, c.y), gridSize))) deg++;
            if (c.y + 1 < gridSize.y && path.Contains(MapGridIndex.CellIndex(new int2(c.x, c.y + 1), gridSize))) deg++;
            if (c.y - 1 >= 0          && path.Contains(MapGridIndex.CellIndex(new int2(c.x, c.y - 1), gridSize))) deg++;
            return deg;
        }
    }
}
