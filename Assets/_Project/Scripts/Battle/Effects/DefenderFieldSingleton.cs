using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Wassup.Battle.Effects
{
    // boss-defender-field unit 1 — 방어유닛-지향 flow field. Effects 소유.
    // 유일 writer = DefenderFieldSystem (매 프레임 in-place 재빌드). Movement 는 RO 소비.
    // goal field(FlowFieldSingleton)와 같은 그리드/원점. SimFieldInstaller 가 생성/teardown.
    // continuous-agent-movement unit 1 — walkMask 는 FlowFieldSingleton 이 단독 소유한다.
    // 여기서 사본을 들면 double dispose 위험 + 벽 정의가 두 곳이 된다. 소스 수집·BFS 순회에
    // 쓸 마스크는 DefenderFieldSystem 이 goal field 에서 읽어 온다.
    public struct DefenderFieldSingleton : IComponentData
    {
        public NativeArray<float2> flow;     // 최근접 방어유닛-이웃 소스로 향하는 단위 방향
        public NativeArray<int>    dist;     // 소스까지 BFS cost. 방어유닛 0/도달불가 = int.MaxValue
        public int2                gridSize;
        public float               tileSize;
        public float3              origin;

        public bool IsCreated => flow.IsCreated && dist.IsCreated;

        public void Dispose()
        {
            if (flow.IsCreated) flow.Dispose();
            if (dist.IsCreated) dist.Dispose();
        }
    }
}
