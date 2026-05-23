using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Wassup.Data.MapGrid
{
    public static class MapDocumentBuilder
    {
        public static GeneratedMap ToGeneratedMap(MapDocument doc, Allocator allocator)
        {
            int w = doc.Width;
            int h = doc.Height;
            int n = w * h;

            var tiles = new NativeArray<MapTileType>(n, allocator);
            var mergeDegree = new NativeArray<byte>(n, allocator);
            var chokepoint = new NativeArray<byte>(n, allocator);
            var propLayerId = new NativeArray<byte>(n, allocator);

            for (int i = 0; i < n; i++)
            {
                tiles[i] = doc.Tiles[i];
                mergeDegree[i] = doc.MergeDegree[i];
                chokepoint[i] = (byte)(doc.Chokepoint[i] ? 1 : 0);
                propLayerId[i] = doc.PropLayerId[i];
            }

            var spawns = new NativeArray<int2>(doc.Spawns.Count, allocator);
            for (int i = 0; i < spawns.Length; i++)
                spawns[i] = new int2(doc.Spawns[i].x, doc.Spawns[i].y);

            return new GeneratedMap
            {
                tiles = tiles,
                mergeDegree = mergeDegree,
                chokepoint = chokepoint,
                propLayerId = propLayerId,
                gridSize = new int2(w, h),
                spawns = spawns,
                goal = new int2(doc.Goal.x, doc.Goal.y),
                seed = doc.AuthoringSeed,
                generatorVersion = doc.GeneratorVersion,
            };
        }

        public static void WriteToDocument(MapDocument doc, in GeneratedMap map)
        {
            int n = map.gridSize.x * map.gridSize.y;
            var tiles = new MapTileType[n];
            var mergeDegree = new byte[n];
            var chokepoint = new bool[n];
            var propLayerId = new byte[n];

            for (int i = 0; i < n; i++)
            {
                tiles[i] = map.tiles[i];
                mergeDegree[i] = map.mergeDegree[i];
                chokepoint[i] = map.chokepoint[i] != 0;
                propLayerId[i] = map.propLayerId[i];
            }

            var spawns = new Vector2Int[map.spawns.Length];
            for (int i = 0; i < spawns.Length; i++)
                spawns[i] = new Vector2Int(map.spawns[i].x, map.spawns[i].y);

            doc.SetFrom(
                map.gridSize.x, map.gridSize.y,
                tiles, mergeDegree, chokepoint, propLayerId,
                new Vector2Int(map.goal.x, map.goal.y),
                spawns,
                map.seed, map.generatorVersion);
        }
    }
}
