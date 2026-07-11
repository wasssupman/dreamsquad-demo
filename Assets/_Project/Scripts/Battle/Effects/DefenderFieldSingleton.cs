using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Wassup.Battle.Effects
{
    // boss-defender-field unit 1 — 방어유닛-지향 flow field. Effects 소유.
    // 유일 writer = DefenderFieldSystem (매 프레임 in-place 재빌드). Movement 는 RO 소비.
    // goal field(FlowFieldSingleton)와 같은 그리드/원점. BattleBridge 가 생성/teardown.
    // walkMask 는 goal field 가 저장하지 않는 값이라 여기서 보유(소스 수집 + BFS 순회용).
    public struct DefenderFieldSingleton : IComponentData
    {
        public NativeArray<byte>   walkMask; // 1 = walkable, 0 = blocked
        public NativeArray<float2> flow;     // 최근접 방어유닛-이웃 소스로 향하는 단위 방향
        public NativeArray<int>    dist;     // 소스까지 BFS cost. 방어유닛 0/도달불가 = int.MaxValue
        public int2                gridSize;
        public float               tileSize;
        public float3              origin;

        public bool IsCreated => walkMask.IsCreated && flow.IsCreated && dist.IsCreated;

        public void Dispose()
        {
            if (walkMask.IsCreated) walkMask.Dispose();
            if (flow.IsCreated) flow.Dispose();
            if (dist.IsCreated) dist.Dispose();
        }
    }
}
