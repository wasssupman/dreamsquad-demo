namespace Wassup.Data
{
    public readonly struct BoardVisualCell
    {
        public const byte NoPathProximity = byte.MaxValue;
        public static readonly BoardVisualCell Empty = new(
            MapTileType.Env,
            BoardZoneType.Env,
            -1,
            0,
            0,
            0,
            0,
            BoardShapeType.None,
            0,
            0f,
            NoPathProximity,
            0);

        public readonly MapTileType sourceTileType;
        public readonly BoardZoneType zoneType;
        public readonly int regionId;
        public readonly byte sameZoneMask;
        public readonly byte transitionMask;
        public readonly byte envNeighborMask;
        public readonly byte innerCornerMask;
        public readonly BoardShapeType shapeClass;
        public readonly uint surfaceNoiseHash;
        public readonly float decorBudgetBias;
        // NoPathProximity means the map contains no Walk cells.
        public readonly byte pathProximity;
        public readonly byte borderProximity;
        public bool HasPathProximity => pathProximity != NoPathProximity;

        public BoardVisualCell(
            MapTileType sourceTileType,
            BoardZoneType zoneType,
            int regionId,
            byte sameZoneMask,
            byte transitionMask,
            byte envNeighborMask,
            byte innerCornerMask,
            BoardShapeType shapeClass,
            uint surfaceNoiseHash,
            float decorBudgetBias,
            byte pathProximity,
            byte borderProximity)
        {
            this.sourceTileType = sourceTileType;
            this.zoneType = zoneType;
            this.regionId = regionId;
            this.sameZoneMask = sameZoneMask;
            this.transitionMask = transitionMask;
            this.envNeighborMask = envNeighborMask;
            this.innerCornerMask = innerCornerMask;
            this.shapeClass = shapeClass;
            this.surfaceNoiseHash = surfaceNoiseHash;
            this.decorBudgetBias = decorBudgetBias;
            this.pathProximity = pathProximity;
            this.borderProximity = borderProximity;
        }
    }
}
