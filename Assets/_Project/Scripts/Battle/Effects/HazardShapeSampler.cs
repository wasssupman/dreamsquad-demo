using System.Collections.Generic;
using Unity.Mathematics;

namespace Wassup.Battle.Effects
{
    public static class HazardShapeSampler
    {
        public static List<int2> Sample(HazardShape shape, int2 origin, int radius)
        {
            switch (shape)
            {
                case HazardShape.SingleCell:
                    return new List<int2>(1) { origin };
                case HazardShape.Square3x3:
                    return Enumerate(origin, 1);
                case HazardShape.RadiusSquare:
                    return Enumerate(origin, math.max(1, radius));
                default:
                    return new List<int2>(0);
            }
        }

        private static List<int2> Enumerate(int2 origin, int radius)
        {
            int side = 2 * radius + 1;
            var cells = new List<int2>(side * side);
            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                    cells.Add(new int2(origin.x + dx, origin.y + dy));
            }
            return cells;
        }
    }
}
