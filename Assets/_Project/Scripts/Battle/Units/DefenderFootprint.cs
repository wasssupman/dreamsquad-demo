using Unity.Mathematics;
using Unity.Entities;

namespace Wassup.Battle.Units
{
    // distance-based-range unit 10 — 방어유닛의 **저장 좌표**. 앵커 + 크기 둘뿐이고
    // 기하 중심·발밑·점유 rect 는 전부 `Wassup.Data.FootprintMath` 가 파생한다.
    //
    // ⚠ **구 `DefenderTile { int2 cell }`(= 대표 셀)의 후신이다.** 이름을 바꾼 것이 핵심이다 —
    // `cell` 이라는 이름이 남아 있는 한 다음 사람이 그것을 「유닛의 위치」로 오해하고,
    // 짝수 변 footprint 에서 사거리를 반 칸 옮긴다(`distance-based-range` 사용자 확정 결정 1).
    //
    // ⚠ **`anchor` 를 「유닛이 서 있는 칸」으로 읽지 말 것.** 앵커는 점유 rect 의 min 코너,
    // 즉 **정체성 키**다. 시각·사거리는 기하 중심, 격자 질의는 rect 로 가라.
    public struct DefenderFootprint : IComponentData
    {
        public int2 anchor;
        public int2 size;
    }
}
