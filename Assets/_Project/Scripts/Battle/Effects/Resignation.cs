using Unity.Entities;
using Unity.Mathematics;

namespace Wassup.Battle.Effects
{
    // season-gimmick-clockout unit 1 — 맵 위 사직서 (Effects 소유).
    // 배치 유닛이 사망하면 그 타일에 스폰(ResignationDropSystem, unit 8 재설계). 유닛이 줍지 않는다 —
    // 전역 임계(resignationThreshold 도달) 시에만 소모/destroy(unit 3). 레드불 Pickup
    // (소비 주체=유닛)과 의미가 달라 별개 아키타입. 뷰는 BattleBridge poll-reconcile.
    public struct Resignation : IComponentData
    {
        public int2 cell;
    }
}
