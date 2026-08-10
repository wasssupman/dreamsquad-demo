using Unity.Entities;
using Unity.Mathematics;

namespace Wassup.Battle.Movement
{
    // summon-patrol-defender unit 1 — 거점 순찰 아군의 이동 제약.
    //
    // Movement 소유(이동을 제약하는 값). writer = BattleBridge(스폰 · 소환사 재배치).
    // Effects(PatrolFieldSystem)와 Movement(MovementSystem)는 RO 로 읽는다.
    //
    // unit 9 — **중심과 집은 다른 칸이다.** 한 필드가 둘을 겸했더니 소환물이 소환사와
    // 같은 칸에 겹쳐 스폰됐다(사용자 지적 2026-08-10: "소환물 스폰은 소환사 주변 타일로").
    //
    //   cell     = 박스 중심 = **소환사 셀**. 배치 프리뷰가 칠하는 그 중심과 같아야 한다
    //              — 셋이 갈려 있던 것을 하나로 접은 것이 unit 9 이므로 여기를 옮기면 안 된다.
    //   homeCell = 대기·복귀 칸 = 소환사 **주변**의 통행 가능한 최근접 칸.
    //
    // 겸직을 풀어야 하는 이유는 두 값의 제약이 다르기 때문이다: 중심은 «플레이어가 손가락을
    // 올린 칸»이라 통행 가능일 필요가 없고, 집은 «순찰병이 실제로 서는 칸»이라 반드시
    // 통행 가능이어야 한다. 선정은 `BattleBridge.TryGetPatrolHomeCell` 단독 소유.
    public struct PatrolAnchor : IComponentData
    {
        public int2 cell;
        public int2 homeCell;
        public int  tileRadius;   // Chebyshev 박스 반경 = 소환사 공격범위(타일)
    }
}
