using System;
using Unity.Mathematics;

namespace Wassup.Data
{
    [Serializable]
    public struct ManualMapInput
    {
        public int2 gridSize;
        public int2[] walkCells;
        public int2[] placeCells;
        public int2[] spawns;
        public int2 goal;
        public int2[] envCells;
        public int2[] decoCells;
    }
}
