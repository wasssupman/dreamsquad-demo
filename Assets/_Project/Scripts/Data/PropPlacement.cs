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

        public PropPlacement(int propIndex, int x, int y, int width, int height, uint variantSeed)
        {
            this.propIndex = propIndex;
            this.x = x;
            this.y = y;
            this.width = width;
            this.height = height;
            this.variantSeed = variantSeed;
        }
    }
}
