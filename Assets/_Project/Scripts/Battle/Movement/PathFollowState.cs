using Unity.Entities;

namespace Wassup.Battle.Movement
{
    public struct PathFollowState : IComponentData
    {
        public int currentWaypointIndex;
        public float speed;
        public float tileSize; // scale from grid coord to world units
    }
}
