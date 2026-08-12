using Unity.Entities;
using Unity.Mathematics;

namespace Wassup.Battle.Effects
{
    // 다중 셀 점유 선언 — «나는 이 칸들을 차지한다».
    //
    // 소비자 둘의 관심사가 다르다:
    //   AttackSystem         — 사거리를 **가장 가까운 점유 칸**까지로 잰다(3×3 건물의 옆구리)
    //   ObstacleLifetimeSystem — 점유는 보지 않는다. `BlockingHazard` 를 **함께** 든 것만 막는다
    //
    // 옛 이름은 `BlockingHazardCellsBuffer` 였고, 그 이름이 «점유 = 차단» 을 암시해 본능(건물,
    // 비차단)이 통행까지 봉인하는 결함을 만들었다 — instinct-content unit 1.
    public struct OccupiedCellsBuffer : IBufferElementData
    {
        public int2 cell;
    }
}
