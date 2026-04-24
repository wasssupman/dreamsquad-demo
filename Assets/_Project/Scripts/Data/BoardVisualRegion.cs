using Unity.Mathematics;

namespace Wassup.Data
{
    public readonly struct BoardVisualRegion
    {
        public readonly int id;
        public readonly BoardZoneType zoneType;
        public readonly int cellCount;
        public readonly int2 min;
        public readonly int2 max;
        public readonly int2 anchorCell;

        public BoardVisualRegion(int id, BoardZoneType zoneType, int cellCount, int2 min, int2 max, int2 anchorCell)
        {
            this.id = id;
            this.zoneType = zoneType;
            this.cellCount = cellCount;
            this.min = min;
            this.max = max;
            this.anchorCell = anchorCell;
        }
    }
}
