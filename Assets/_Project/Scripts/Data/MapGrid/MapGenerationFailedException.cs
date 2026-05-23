using System;
using Unity.Mathematics;

namespace Wassup.Data.MapGrid
{
    public sealed class MapGenerationFailedException : Exception
    {
        public int Seed { get; }
        public int2 GridSize { get; }
        public int Attempts { get; }

        public MapGenerationFailedException(int seed, int2 gridSize, int attempts)
            : base($"Map generation failed after {attempts} attempts (seed={seed}, grid={gridSize.x}x{gridSize.y})")
        {
            Seed = seed;
            GridSize = gridSize;
            Attempts = attempts;
        }
    }
}
