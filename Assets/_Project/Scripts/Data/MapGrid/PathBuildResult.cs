using System;
using Unity.Collections;

namespace Wassup.Data.MapGrid
{
    public struct PathBuildResult : IDisposable
    {
        public NativeHashSet<int> pathCells;
        public NativeArray<int>   spawnOrder;
        public bool IsValid;

        // IsValid 와 무관하게 항상 안전. 미할당 컨테이너는 IsCreated 체크 후 skip.
        public void Dispose()
        {
            if (pathCells.IsCreated) pathCells.Dispose();
            if (spawnOrder.IsCreated) spawnOrder.Dispose();
        }
    }
}
