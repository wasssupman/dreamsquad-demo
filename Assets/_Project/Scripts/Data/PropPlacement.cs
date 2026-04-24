namespace Wassup.Data
{
    public readonly struct PropPlacement
    {
        public readonly int propIndex;
        public readonly int x;
        public readonly int y;
        public readonly int width;
        public readonly int height;
        public readonly uint variantSeed;
        public readonly float rotationYaw;
        public readonly float scale;
        public readonly int clusterId;

        public PropPlacement(int propIndex, int x, int y, int width, int height, uint variantSeed)
            : this(propIndex, x, y, width, height, variantSeed, 0f, 1f, -1)
        {
        }

        public PropPlacement(int propIndex, int x, int y, int width, int height, uint variantSeed, float rotationYaw, float scale, int clusterId)
        {
            this.propIndex = propIndex;
            this.x = x;
            this.y = y;
            this.width = width;
            this.height = height;
            this.variantSeed = variantSeed;
            this.rotationYaw = rotationYaw;
            this.scale = scale;
            this.clusterId = clusterId;
        }
    }
}
