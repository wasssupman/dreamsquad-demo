using Unity.Entities;

namespace Wassup.Battle.Effects
{
    // aggro-tile-chase unit 1 — 어그로된 적의 chase dist field (gridSize.x*y 길이).
    // 목적지(가디언 사거리 내 walk 셀) 집합이 dist 0, 하강은 FlowRecovery.RecoveryDir.
    // AggroStateSystem(Effects)이 유일 writer — 획득 시 1회 계산 부착(가디언·맵 정적),
    // 해제 시 제거. MovementSystem 은 RO 소비.
    [InternalBufferCapacity(0)]
    public struct AggroChaseCell : IBufferElementData
    {
        public int dist;
    }
}
