using Unity.Entities;
using Unity.Mathematics;

namespace Wassup.Battle.Movement
{
    // summon-patrol-defender unit 1 — 거점 순찰 아군의 이동 제약.
    //
    // Movement 소유(이동을 제약하는 값). writer = BattleBridge(스폰 · 소환사 재배치).
    // Effects(PatrolFieldSystem)와 Movement(MovementSystem)는 RO 로 읽는다.
    //
    // cell 은 **walk 타일**이다 — 소환사 셀이 아니다. 방어유닛 셀은 통상 walkable 이
    // 아니라서(placement-mask B-1 로 Walk 셀 위 배치도 가능하지만, 그 경우가 아니면)
    // 소환사 셀을 그대로 거점으로 쓰면 순찰병이 절대 설 수 없는 칸을 향해 영원히
    // 전진한다. Bridge 가 TryGetNearestWalkCell 로 스냅한 값을 넣는다(README 계약 4)
    // — 소환사가 Walk 셀 위면 스냅이 자기 셀을 즉시 반환하므로 두 경우 모두 안전.
    public struct PatrolAnchor : IComponentData
    {
        public int2 cell;
        public int  tileRadius;   // Chebyshev 박스 반경
    }
}
