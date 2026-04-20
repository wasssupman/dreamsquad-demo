using Unity.Entities;

namespace Wassup.Battle.Movement
{
    public struct PathFollowState : IComponentData
    {
        public float speed;
        // Phase 9: currentWaypointIndex 제거 — flow field 가 대체
        // Phase 9: tileSize 제거 — FlowFieldSingleton.tileSize 가 단일 소스
    }
}
