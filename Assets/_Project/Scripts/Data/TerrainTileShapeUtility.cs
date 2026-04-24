using Unity.Mathematics;

namespace Wassup.Data
{
    public static class TerrainTileShapeUtility
    {
        public static TerrainTileShape GetShapeForMask(int mask)
        {
            mask &= 15;

            bool n = (mask & 1) != 0;
            bool e = (mask & 2) != 0;
            bool s = (mask & 4) != 0;
            bool w = (mask & 8) != 0;
            int count = (n ? 1 : 0) + (e ? 1 : 0) + (s ? 1 : 0) + (w ? 1 : 0);

            if (count == 0) return TerrainTileShape.Single;
            if (count == 4) return TerrainTileShape.Cross;

            if (count == 1)
            {
                if (n) return TerrainTileShape.EndN;
                if (e) return TerrainTileShape.EndE;
                if (s) return TerrainTileShape.EndS;
                return TerrainTileShape.EndW;
            }

            if (count == 2)
            {
                if (n && s) return TerrainTileShape.StraightNS;
                if (e && w) return TerrainTileShape.StraightEW;
                if (n && e) return TerrainTileShape.CornerNE;
                if (n && w) return TerrainTileShape.CornerNW;
                if (s && e) return TerrainTileShape.CornerSE;
                return TerrainTileShape.CornerSW;
            }

            if (!n) return TerrainTileShape.TJunctionS;
            if (!e) return TerrainTileShape.TJunctionW;
            if (!s) return TerrainTileShape.TJunctionN;
            return TerrainTileShape.TJunctionE;
        }

        public static TerrainTileShape GetWalkShape(GeneratedMap map, int x, int y)
        {
            if (!map.IsCreated || map.TileAt(new int2(x, y)) != MapTileType.Walk)
                return TerrainTileShape.None;

            return GetShapeForMask(GetCardinalNeighborMask(map, x, y, MapTileType.Walk));
        }

        public static int GetCardinalNeighborMask(GeneratedMap map, int x, int y, MapTileType targetType)
        {
            if (!map.IsCreated)
                return 0;

            int mask = 0;
            if (HasType(map, x, y + 1, targetType)) mask |= 1;
            if (HasType(map, x + 1, y, targetType)) mask |= 2;
            if (HasType(map, x, y - 1, targetType)) mask |= 4;
            if (HasType(map, x - 1, y, targetType)) mask |= 8;
            return mask;
        }

        public static int GetBackgroundNeighborMask(GeneratedMap map, int x, int y)
        {
            if (!map.IsCreated)
                return 0;

            int mask = 0;
            if (IsBackground(map, x, y + 1)) mask |= 1;
            if (IsBackground(map, x + 1, y)) mask |= 2;
            if (IsBackground(map, x, y - 1)) mask |= 4;
            if (IsBackground(map, x - 1, y)) mask |= 8;
            return mask;
        }

        private static bool IsWalk(GeneratedMap map, int x, int y)
        {
            return HasType(map, x, y, MapTileType.Walk);
        }

        private static bool IsBackground(GeneratedMap map, int x, int y)
        {
            if (x < 0 || y < 0 || x >= map.gridSize.x || y >= map.gridSize.y)
                return false;

            var type = map.TileAt(new int2(x, y));
            return type == MapTileType.Env || type == MapTileType.Deco;
        }

        private static bool HasType(GeneratedMap map, int x, int y, MapTileType type)
        {
            if (x < 0 || y < 0 || x >= map.gridSize.x || y >= map.gridSize.y)
                return false;

            return map.TileAt(new int2(x, y)) == type;
        }
    }
}
