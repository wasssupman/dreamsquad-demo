using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Wassup.Battle.Effects
{
    // Phase 9 — Effects 맥락이 소유하는 정적 flow field.
    // Allocator.Persistent 로 할당, 판 종료 / OnDestroy 에서 dispose.
    // Movement 맥락이 읽기 전용으로 consume.
    public struct FlowFieldSingleton : IComponentData
    {
        public NativeArray<float2> flow;        // [width * height], 각 cell 의 단위 방향. goal = zero.
        public NativeArray<int>    dist;        // BFS cost from goal. Unreachable = int.MaxValue.
        public int2                gridSize;    // (Width, Height)
        public int2                goalCell;
        public float               tileSize;
        public int                 version;     // 디버그 / Phase 10 event-driven rebuild 마커

        public bool IsCreated => flow.IsCreated && dist.IsCreated;

        public void Dispose()
        {
            if (flow.IsCreated) flow.Dispose();
            if (dist.IsCreated) dist.Dispose();
        }
    }
}
