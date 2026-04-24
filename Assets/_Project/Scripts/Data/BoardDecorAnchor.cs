using Unity.Mathematics;

namespace Wassup.Data
{
    public readonly struct BoardDecorAnchor
    {
        public readonly BoardDecorAnchorType anchorType;
        public readonly int2 cell;
        public readonly int regionId;

        public BoardDecorAnchor(BoardDecorAnchorType anchorType, int2 cell, int regionId)
        {
            this.anchorType = anchorType;
            this.cell = cell;
            this.regionId = regionId;
        }
    }
}
