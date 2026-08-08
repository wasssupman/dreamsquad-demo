using Unity.Entities;

namespace Wassup.Battle.Effects
{
    // aggro-targeting Unit 0 — Effects-owned. Sticky link from an aggroed enemy
    // to the guardian holding it. writer/clearer 는 둘 다 Effects 맥락 —
    // AggroStateSystem(히트 게이트 통과 시 획득 / 가디언 소멸·해제 시 제거)과
    // FlowFieldRebuildSystem(장애물 변경 시 무효화). 후자가 떼는 이유: 낡은 chase field 가
    // 가리키는 경로가 무효가 되므로 Marching 으로 되돌려 다음 히트에 재획득시킨다.
    // MovementSystem and AttackSystem read it cross-context (read-only), mirroring
    // the TornadoField→MovementSystem precedent.
    public struct Aggroed : IComponentData
    {
        public Entity guardian;  // the guardian that aggroed this enemy (first-come, sticky)
    }
}
