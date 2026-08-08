using Unity.Entities;

namespace Wassup.Battle.Effects
{
    // aggro-tile-chase unit 1 — 어그로된 적의 chase dist field (gridSize.x*y 길이).
    // 목적지(가디언 사거리 내 walk 셀) 집합이 dist 0, 하강은 FlowRecovery.RecoveryDir.
    // writer 는 둘 다 Effects — AggroStateSystem(획득 시 1회 계산 부착, 해제 시 제거)과
    // FlowFieldRebuildSystem(장애물 변경 시 Aggroed 와 함께 제거).
    // continuous-agent-movement unit 5(D1-b)로 "맵 정적" 전제가 깨졌다 — 장애물이 맵을
    // 동적으로 만들어, 획득 시 한 번 구운 필드가 낡으면 재빌드가 무효화해야 한다.
    // MovementSystem 은 RO 소비.
    [InternalBufferCapacity(0)]
    public struct AggroChaseCell : IBufferElementData
    {
        public int dist;
    }
}
