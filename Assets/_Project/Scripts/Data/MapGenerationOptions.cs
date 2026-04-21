using System;
using Unity.Mathematics;

namespace Wassup.Data
{
    [Serializable]
    public struct MapGenerationOptions
    {
        public MapPathShape pathShape;
        public int2 gridSize;
        public MapObstacleDensity obstacleDensity;
        public int spawnLaneCount;

        public static MapGenerationOptions Default => new()
        {
            pathShape = MapPathShape.Straight,
            gridSize = new int2(20, 10),
            obstacleDensity = MapObstacleDensity.Low,
            spawnLaneCount = 2,
        };

        public float MinPlaceableRatio => obstacleDensity switch
        {
            MapObstacleDensity.High => 0.25f,
            MapObstacleDensity.Medium => 0.4f,
            _ => 0.6f,
        };

        public MapGenerationOptions Normalized()
        {
            int width = gridSize.x > 0 ? gridSize.x : 20;
            int height = gridSize.y > 0 ? gridSize.y : 10;
            return new MapGenerationOptions
            {
                pathShape = pathShape,
                gridSize = new int2(math.max(4, width), math.max(4, height)),
                obstacleDensity = obstacleDensity,
                spawnLaneCount = math.clamp(spawnLaneCount <= 0 ? 2 : spawnLaneCount, 2, math.max(2, (height + 1) / 2)),
            };
        }
    }
}
