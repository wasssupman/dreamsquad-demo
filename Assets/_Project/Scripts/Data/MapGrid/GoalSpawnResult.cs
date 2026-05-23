using System;
using Unity.Collections;
using Unity.Mathematics;

namespace Wassup.Data.MapGrid
{
    public struct GoalSpawnResult : IDisposable
    {
        public int2 goal;
        public NativeArray<int2> spawns;
        public int activeQuadrantMask;
        public bool IsValid;

        public void Dispose()
        {
            if (spawns.IsCreated) spawns.Dispose();
        }
    }
}
